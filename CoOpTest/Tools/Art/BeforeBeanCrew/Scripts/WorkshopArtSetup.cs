using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CoopPrototype.Editor
{
    /// <summary>One-time art migration. Output consists of ordinary FBX, prefab, material and animation assets.</summary>
    public static class WorkshopArtSetup
    {
        const string Root = "Assets/CoopPrototype";
        const string Art = Root + "/Art";
        static GameObject Model(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Models/" + name + ".fbx");

        [MenuItem("Coop Prototype/Art/Apply Workshop Art Upgrade")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before applying art.");
            Directory.CreateDirectory(Art + "/Materials");
            Directory.CreateDirectory(Art + "/Animations");
            AssetDatabase.Refresh();
            foreach (var path in Directory.GetFiles(Art + "/Models", "*.fbx"))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                importer.animationType = path.Contains("WorkshopWorker") ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
                importer.importAnimation = false;
                // Blender 5 FBX unit metadata is read by this editor as millimetres.
                // Convert the source metre measurements at import, never scale the network/physics root.
                importer.globalScale = 1000;
                if (path.Contains("WorkshopWorker")) importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
            }
            UpgradePlayer();
            UpgradePickup("Crate", "CargoCrate");
            UpgradePickup("Battery", "WorkshopBattery");
            UpgradePickup("Tool", "Spanner");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != PrototypeBuilder.ScenePath)
                EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);
            SceneVisual("Generator body", "PortableGenerator", new Vector3(0, -.6f, 0));
            SceneVisual("Shelf", "WorkshopBench", new Vector3(0, -.5f, 0));
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[Coop Art] Character, Animator, props and scene saved.");
        }

        static void RemapMaterials(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
                {
                    var path = Art + "/Materials/" + source.name + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material != null) return material;
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = source.name };
                    material.color = source.HasProperty("_Color") ? source.GetColor("_Color") : source.color;
                    material.SetFloat("_Smoothness", .22f);
                    if (source.name.Contains("steel")) material.SetFloat("_Metallic", .5f);
                    AssetDatabase.CreateAsset(material, path);
                    return material;
                }).ToArray();
            }
        }
        static GameObject AddVisual(GameObject root, string model, Vector3 position)
        {
            var existing = root.transform.Find("Art Visual");
            if (existing != null) return existing.gameObject; // Preserve subsequent developer edits.
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(Model(model));
            visual.name = "Art Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = position;
            // Original physics roots are scaled; author the visual in metres without changing their IDs or colliders.
            var scale = root.transform.lossyScale;
            visual.transform.localScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
            RemapMaterials(visual);
            var renderer = root.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = false;
            return visual;
        }
        static void UpgradePickup(string prefabName, string modelName)
        {
            string path = Root + "/Prefabs/" + prefabName + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                AddVisual(root, modelName, Vector3.zero);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static void SceneVisual(string name, string modelName, Vector3 offsetMetres)
        {
            var root = GameObject.Find(name);
            if (root == null) throw new InvalidOperationException("Missing scene object: " + name);
            var scale = root.transform.lossyScale;
            AddVisual(root, modelName, new Vector3(offsetMetres.x / scale.x, offsetMetres.y / scale.y, offsetMetres.z / scale.z));
        }
        static void UpgradePlayer()
        {
            string path = Root + "/Prefabs/NetworkPlayer.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var capsule = root.transform.Find("Capsule visual");
                if (capsule != null) capsule.gameObject.SetActive(false);
                var visual = AddVisual(root, "WorkshopWorker", new Vector3(0, -1, 0));
                var animator = visual.GetComponent<Animator>();
                if (animator == null) animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = MakeController(visual);
                animator.applyRootMotion = false;
                if (root.GetComponent<PlayerThrow>() == null) root.AddComponent<PlayerThrow>();
                var animation = root.GetComponent<PlayerAnimation>();
                if (animation == null) animation = root.AddComponent<PlayerAnimation>();
                animation.animator = animator;
                var motor = root.GetComponent<NetworkPlayerMotor>();
                motor.thirdPerson = true;
                motor.holdPoint.localPosition = new Vector3(0, .08f, .67f);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static AnimatorController MakeController(GameObject visual)
        {
            string path = Art + "/Animations/WorkshopWorker.controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null) return existing;
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Carrying", AnimatorControllerParameterType.Bool);
            var machine = controller.layers[0].stateMachine;
            AnimatorState State(string name, bool walk, bool carry, bool isThrow, Vector3 position)
            {
                var state = machine.AddState(name, position);
                state.motion = MakeClip(visual, name, walk, carry, isThrow);
                return state;
            }
            var idle = State("Idle", false, false, false, new Vector3(200, 0));
            var walk = State("Walk", true, false, false, new Vector3(450, 0));
            var carry = State("Carry", false, true, false, new Vector3(200, 180));
            var carryWalk = State("Carry Walk", true, true, false, new Vector3(450, 180));
            var throwing = State("Throw", false, true, true, new Vector3(750, 90));
            machine.defaultState = idle;
            var states = new[] { idle, walk, carry, carryWalk };
            foreach (var from in states)
                foreach (var to in states)
                {
                    if (from == to) continue;
                    var t = from.AddTransition(to); t.hasExitTime = false; t.duration = .12f;
                    bool held = to == carry || to == carryWalk;
                    bool moving = to == walk || to == carryWalk;
                    t.AddCondition(held ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "Carrying");
                    t.AddCondition(moving ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, .08f, "Speed");
                }
            foreach (var to in states)
            {
                var t = throwing.AddTransition(to); t.hasExitTime = true; t.exitTime = .95f; t.duration = .1f;
                t.AddCondition(to == carry || to == carryWalk ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "Carrying");
                t.AddCondition(to == walk || to == carryWalk ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, .08f, "Speed");
            }
            return controller;
        }
        [MenuItem("Coop Prototype/Art/Recreate Authored Animation Clips")]
        public static void RefreshClips()
        {
            var visual = Object.Instantiate(Model("WorkshopWorker"));
            try
            {
                MakeClip(visual, "Idle", false, false, false);
                MakeClip(visual, "Walk", true, false, false);
                MakeClip(visual, "Carry", false, true, false);
                MakeClip(visual, "Carry Walk", true, true, false);
                MakeClip(visual, "Throw", false, true, true);
            }
            finally { Object.DestroyImmediate(visual); }
        }
        static AnimationClip MakeClip(GameObject root, string name, bool walk, bool carry, bool throwing)
        {
            var clip = new AnimationClip { name = name, frameRate = 30 };
            float length = throwing ? .7f : walk ? .8f : 2;
            var bones = root.GetComponentsInChildren<Transform>().Where(t => t.name == "Hips" || t.name == "Spine" || t.name == "Head" || t.name.StartsWith("UpperArm") || t.name.StartsWith("Forearm") || t.name.StartsWith("Thigh") || t.name.StartsWith("Shin") || t.name.StartsWith("Foot")).ToArray();
            foreach (var bone in bones)
            {
                var curves = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
                string path = AnimationUtility.CalculateTransformPath(bone, root.transform);
                int frames = Mathf.RoundToInt(length * 30);
                for (int i = 0; i <= frames; i++)
                {
                    float t = i / (float)frames;
                    float wave = Mathf.Sin(t * Mathf.PI * 2);
                    float side = bone.name.EndsWith("L") ? 1 : -1;
                    float angle = 0;
                    if (bone.name.StartsWith("Thigh")) angle = walk ? wave * 26 * side : 0;
                    if (bone.name.StartsWith("Shin")) angle = walk ? Mathf.Max(0, -wave * side) * 35 : 0;
                    if (bone.name.StartsWith("Foot")) angle = walk ? -Mathf.Max(0, -wave * side) * 12 : 0;
                    if (bone.name.StartsWith("UpperArm")) angle = carry ? -65 : walk ? -wave * 26 * side : 3 * wave;
                    if (bone.name.StartsWith("Forearm")) angle = carry ? -35 : -12;
                    if (bone.name == "Head") angle = wave * (walk ? 3 : 1.5f);
                    if (throwing)
                    {
                        // Anticipation, release at .25 seconds, then follow-through. Gameplay uses PlayerThrow's server timer.
                        float arm = t < .22f ? Mathf.Lerp(-65, -125, t / .22f) : t < .36f ? Mathf.Lerp(-125, -45, (t-.22f)/.14f) : Mathf.Lerp(-45, 0, (t-.36f)/.64f);
                        if (bone.name.StartsWith("UpperArm")) angle = arm;
                        if (bone.name.StartsWith("Forearm")) angle = t < .36f ? -40 : Mathf.Lerp(-40, -12, (t-.36f)/.64f);
                        if (bone.name == "Spine") angle = t < .25f ? -8 * Mathf.Sin(t/.25f*Mathf.PI*.5f) : 10 * Mathf.Sin((t-.25f)/.75f*Mathf.PI);
                    }
                    // Rotate about the character's X axis regardless of FBX bone orientation.
                    Vector3 axis = bone.parent.InverseTransformDirection(root.transform.right);
                    var q = Quaternion.AngleAxis(angle, axis) * bone.localRotation;
                    curves[0].AddKey(t*length,q.x); curves[1].AddKey(t*length,q.y); curves[2].AddKey(t*length,q.z); curves[3].AddKey(t*length,q.w);
                }
                for (int i=0;i<4;i++) clip.SetCurve(path,typeof(Transform),"m_LocalRotation."+"xyzw"[i],curves[i]);
                if (bone.name == "Hips")
                {
                    var up = bone.parent.InverseTransformVector(root.transform.up);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        var curve = new AnimationCurve();
                        for (int i=0;i<=frames;i++)
                        {
                            float t = i/(float)frames;
                            float bounce = walk ? .025f*(1-Mathf.Cos(t*Mathf.PI*4)) : .008f*Mathf.Sin(t*Mathf.PI*2);
                            curve.AddKey(t*length,bone.localPosition[axis] + up[axis] * bounce);
                        }
                        clip.SetCurve(path,typeof(Transform),"m_LocalPosition."+"xyz"[axis],curve);
                    }
                }
            }
            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = !throwing; AnimationUtility.SetAnimationClipSettings(clip, settings);
            var assetPath = Art + "/Animations/" + name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (existing != null) { EditorUtility.CopySerialized(clip, existing); Object.DestroyImmediate(clip); EditorUtility.SetDirty(existing); return existing; }
            AssetDatabase.CreateAsset(clip, assetPath);
            return clip;
        }

        public static void RenderPreview(string pose = "")
        {
            var preview = EditorSceneManager.NewPreviewScene();
            var character = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/NetworkPlayer.prefab"), preview);
            character.transform.position = Vector3.up;
            if (!string.IsNullOrEmpty(pose))
                AssetDatabase.LoadAssetAtPath<AnimationClip>(Art + "/Animations/" + pose + ".anim").SampleAnimation(character.transform.Find("Art Visual").gameObject, .2f);
            File.WriteAllText("Temp/WorkshopArtReport.txt", string.Join("\n", Model("WorkshopWorker").GetComponentsInChildren<Transform>(true).Select(t => t.name + " assetPos=" + t.localPosition.ToString("F5") + " rot=" + t.localRotation)) + "\nINSTANCE\n" + string.Join("\n", character.GetComponentsInChildren<Transform>(true).Select(t => t.name + " active=" + t.gameObject.activeInHierarchy + " pos=" + t.position + " scale=" + t.lossyScale)) + "\n" + string.Join("\n", character.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.name + " mesh=" + r.sharedMesh.bounds + " enabled=" + r.enabled + " bounds=" + r.bounds)));
            var camObj = new GameObject("Preview camera"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camObj, preview);
            var camera = camObj.AddComponent<Camera>(); camera.scene = preview; camera.backgroundColor = new Color(.10f,.14f,.16f); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = new Vector3(3,2.1f,4.5f); camera.transform.LookAt(new Vector3(0,1.0f,0)); camera.fieldOfView = 28;
            var lightObj = new GameObject("Preview key"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObj, preview);
            var light = lightObj.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 2; light.transform.rotation = Quaternion.Euler(35,-130,0);
            var rt = new RenderTexture(900,900,24); camera.targetTexture=rt; camera.Render();
            var previous = RenderTexture.active; RenderTexture.active=rt;
            var image = new Texture2D(900,900,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,900,900),0,0); image.Apply();
            Directory.CreateDirectory("TestResults/Art"); File.WriteAllBytes("TestResults/Art/Character" + pose + ".png",image.EncodeToPNG());
            RenderTexture.active=previous; camera.targetTexture=null; Object.DestroyImmediate(rt); Object.DestroyImmediate(image); EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}
