using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace CoopPrototype.Editor
{
    /// <summary>Applies the Bean Crew art pass (characters, animation, props) through normal Unity assets.
    /// Re-running is safe: existing materials, controller and visuals are reused, older visuals are only deactivated.</summary>
    public static class BeanCrewSetup
    {
        const string Root = "Assets/CoopPrototype";
        public const string Art = Root + "/Art/BeanCrew";
        public const string CharacterModel = Art + "/Models/BeanCrew.fbx";
        const string PropModels = Art + "/Models/Props";
        const string Materials = Art + "/Materials";
        const string Palette = Materials + "/BeanPalette.json";
        const string ControllerPath = Art + "/Animations/BeanCrew.controller";
        const string Legacy = Root + "/Art/Legacy";
        public const string VisualName = "Bean Visual";
        const string LegacyVisualName = "Legacy Worker Visual";
        const float CrateHeightMetres = .56f; // measured in Blender; used to calibrate FBX unit scale
        static readonly (string name, bool loop)[] Clips =
            { ("Idle", true), ("Walk", true), ("Carry", true), ("Carry Walk", true), ("Pick Up", false), ("Throw", false), ("Wave", false) };

        [Serializable] class PaletteEntry { public string name; public float[] rgb; public float roughness; public float metallic; public float[] emission; }
        [Serializable] class PaletteFile { public PaletteEntry[] materials; }

        [MenuItem("Coop Prototype/Art/Apply Bean Crew (characters + props)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before applying art.");
            AssetDatabase.Refresh();
            PreserveLegacyWorker();
            float yaw = ConfigureImports();
            var controller = BuildController();
            UpgradePlayer(controller, yaw);
            SetupFirstPerson();
            // Props share the character's export frame: their front (Blender -Y) faces the player's approach (-Z).
            UpgradePickup("Crate", "BeanCrate", yaw);
            UpgradePickup("Battery", "PowerCell", yaw);
            UpgradePickup("Tool", "Wrench", yaw);
            UpgradeScene(yaw);
            AssetDatabase.SaveAssets();
            Debug.Log("[Coop Art] Bean Crew applied: characters, controller, props and scene saved.");
        }

        public static bool IsApplied()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/NetworkPlayer.prefab");
            return prefab != null && prefab.transform.Find(VisualName) != null;
        }

        // ------------------------------------------------------------------ legacy worker
        static void PreserveLegacyWorker()
        {
            string path = Legacy + "/WorkshopWorker_Legacy.prefab";
            if (File.Exists(path)) return;
            EnsureFolder(Legacy);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Art/Models/WorkshopWorker.fbx");
            if (model == null) { Debug.LogWarning("[Coop Art] Legacy worker model not found; nothing to preserve."); return; }
            var player = PrefabUtility.LoadPrefabContents(Root + "/Prefabs/NetworkPlayer.prefab");
            try
            {
                var existing = player.transform.Find("Art Visual") ?? player.transform.Find(LegacyVisualName);
                var copy = (GameObject)PrefabUtility.InstantiatePrefab(model);
                copy.name = "WorkshopWorker (legacy)";
                if (existing != null)
                {
                    // Carry over the URP materials used on the player.
                    var source = existing.GetComponentsInChildren<Renderer>(true).ToDictionary(r => r.name, r => r.sharedMaterials);
                    foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
                        if (source.TryGetValue(r.name, out var mats)) r.sharedMaterials = mats;
                }
                var animator = Get<Animator>(copy);
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Root + "/Art/Animations/WorkshopWorker.controller");
                animator.applyRootMotion = false;
                PrefabUtility.SaveAsPrefabAsset(copy, path);
                Object.DestroyImmediate(copy);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            Debug.Log("[Coop Art] Legacy worker preserved at " + path);
        }

        // ------------------------------------------------------------------ import + materials
        static IEnumerable<string> ModelPaths() =>
            Directory.GetFiles(Art + "/Models", "*.fbx", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/'));

        /// <summary>Configures importers, calibrates FBX units and remaps materials. Returns the yaw that makes the character face +Z.</summary>
        static float ConfigureImports()
        {
            foreach (var path in ModelPaths())
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                bool character = path == CharacterModel;
                importer.animationType = character ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
                if (character) importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = character;
                importer.importBlendShapes = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.useFileScale = true;
                // Props are placed with explicit rotations, so bake Blender's Z-up conversion into the mesh (identity root).
                importer.bakeAxisConversion = !character;
                importer.SaveAndReimport();
            }
            // Calibrate unit scale once from the crate (exporters and Unity versions disagree about FBX units).
            var cratePath = PropModels + "/BeanCrate.fbx";
            var crateImporter = (ModelImporter)AssetImporter.GetAtPath(cratePath);
            float height = MeasureHeight(AssetDatabase.LoadAssetAtPath<GameObject>(cratePath));
            float scale = crateImporter.globalScale;
            if (height > 0)
            {
                float factor = CrateHeightMetres / height;
                if (Mathf.Abs(factor - 1) > .05f) scale = crateImporter.globalScale * Mathf.Pow(10, Mathf.Round(Mathf.Log10(factor)));
            }
            var palette = LoadPalette();
            foreach (var path in ModelPaths())
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.globalScale = scale;
                foreach (var source in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), MaterialFor(source.name, palette));
                if (path == CharacterModel) ConfigureClips(importer);
                importer.SaveAndReimport();
            }
            // Blender's front is -Y; find where it ended up so the character faces the motor's +Z.
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModel);
            var head = FindDeep(rig.transform, "Head"); var eyes = FindDeep(rig.transform, "Eyes");
            float yaw = eyes != null && head != null && (eyes.position.z - head.position.z) < 0 ? 180 : 0;
            Debug.Log($"[Coop Art] FBX scale {scale} (crate measured {height:F3} m before), character yaw {yaw}");
            return yaw;
        }

        static float MeasureHeight(GameObject asset)
        {
            if (asset == null) return 0;
            var instance = Object.Instantiate(asset);
            try
            {
                var renderers = instance.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) return 0;
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                return bounds.size.y;
            }
            finally { Object.DestroyImmediate(instance); }
        }

        static void ConfigureClips(ModelImporter importer)
        {
            var defaults = importer.defaultClipAnimations;
            var clips = new List<ModelImporterClipAnimation>();
            foreach (var (name, loop) in Clips)
            {
                var source = defaults.FirstOrDefault(c => c.takeName.Split('|').Last() == name);
                if (source == null) { Debug.LogWarning("[Coop Art] Missing animation take: " + name); continue; }
                source.name = name;
                source.loopTime = loop;
                source.loopPose = false;
                source.lockRootRotation = source.lockRootHeightY = source.lockRootPositionXZ = false;
                clips.Add(source);
            }
            importer.clipAnimations = clips.ToArray();
        }

        static Dictionary<string, PaletteEntry> LoadPalette()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(Palette);
            if (text == null) return new Dictionary<string, PaletteEntry>();
            return JsonUtility.FromJson<PaletteFile>(text.text).materials.ToDictionary(m => m.name);
        }

        static Material MaterialFor(string name, Dictionary<string, PaletteEntry> palette)
        {
            EnsureFolder(Materials);
            string path = Materials + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material; // keep developer edits
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            if (palette.TryGetValue(name, out var entry))
            {
                material.color = new Color(entry.rgb[0] / 255f, entry.rgb[1] / 255f, entry.rgb[2] / 255f);
                material.SetFloat("_Smoothness", Mathf.Clamp01(1 - entry.roughness) * .8f);
                material.SetFloat("_Metallic", entry.metallic);
                if (entry.emission != null && entry.emission.Length == 3)
                {
                    material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    material.SetColor("_EmissionColor", new Color(entry.emission[0] / 255f, entry.emission[1] / 255f, entry.emission[2] / 255f) * 2.2f);
                }
            }
            else material.color = Color.gray;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Material SurfaceMaterial(string name, Color color, float smoothness)
        {
            string path = Materials + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ------------------------------------------------------------------ animator
        static AnimatorController BuildController()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null) return existing; // preserve developer edits
            EnsureFolder(Path.GetDirectoryName(ControllerPath));
            var clips = AssetDatabase.LoadAllAssetsAtPath(CharacterModel).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Carrying", AnimatorControllerParameterType.Bool);
            controller.AddParameter(new AnimatorControllerParameter { name = "MoveRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
            var machine = controller.layers[0].stateMachine;
            AnimatorState State(string name, Vector3 position, bool rate = false)
            {
                var state = machine.AddState(name, position);
                if (clips.TryGetValue(name, out var clip)) state.motion = clip;
                else Debug.LogWarning("[Coop Art] Clip missing for state " + name);
                if (rate) { state.speedParameter = "MoveRate"; state.speedParameterActive = true; }
                return state;
            }
            var idle = State("Idle", new Vector3(250, 0));
            var walk = State("Walk", new Vector3(520, 0), true);
            var carry = State("Carry", new Vector3(250, 200));
            var carryWalk = State("Carry Walk", new Vector3(520, 200), true);
            var pickUp = State("Pick Up", new Vector3(20, 300));
            var throwing = State("Throw", new Vector3(800, 100));
            State("Wave", new Vector3(20, -120));
            machine.defaultState = idle;
            var locomotion = new[] { idle, walk, carry, carryWalk };
            void To(AnimatorState from, AnimatorState to, bool exit, float duration)
            {
                var t = from.AddTransition(to);
                t.hasExitTime = exit; t.exitTime = .92f; t.duration = duration;
                bool held = to == carry || to == carryWalk;
                bool moving = to == walk || to == carryWalk;
                t.AddCondition(held ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "Carrying");
                t.AddCondition(moving ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, .08f, "Speed");
            }
            foreach (var from in locomotion)
                foreach (var to in locomotion)
                    if (from != to) To(from, to, false, .15f);
            foreach (var to in locomotion)
            {
                To(throwing, to, true, .12f);
                To(pickUp, to, true, .1f);
                To(machine.states.First(s => s.state.name == "Wave").state, to, true, .15f);
            }
            AssetDatabase.SaveAssets();
            return controller;
        }

        // ------------------------------------------------------------------ player
        static void UpgradePlayer(AnimatorController controller, float yaw)
        {
            string path = Root + "/Prefabs/NetworkPlayer.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var capsule = root.transform.Find("Capsule visual");
                if (capsule != null) capsule.gameObject.SetActive(false);
                var old = root.transform.Find("Art Visual");
                if (old != null) { old.name = LegacyVisualName; old.gameObject.SetActive(false); }
                var visual = root.transform.Find(VisualName)?.gameObject;
                if (visual == null)
                {
                    visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModel));
                    visual.name = VisualName;
                    visual.transform.SetParent(root.transform, false);
                    visual.transform.localPosition = new Vector3(0, -1, 0);
                    visual.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                }
                var animator = Get<Animator>(visual);
                if (animator.runtimeAnimatorController == null) animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var appearance = Get<CharacterAppearance>(root);
                var renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                appearance.variants = appearance.variantNames
                    .Select(n => (Renderer)renderers.FirstOrDefault(r => r.name == "Crew_" + n)).Where(r => r != null).ToArray();
                foreach (var r in renderers) r.updateWhenOffscreen = true; // big throw poses leave the bind-pose bounds
                appearance.Apply(0);
                var animation = Get<PlayerAnimation>(root);
                animation.animator = animator;
                var motor = root.GetComponent<NetworkPlayerMotor>();
                motor.holdPoint.localPosition = new Vector3(0, .02f, .62f);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ------------------------------------------------------------------ first person
        public const string ArmsModel = Art + "/Models/FirstPersonArms.fbx";
        public const int ViewModelLayer = 8;
        const string ViewModelLayerName = "FirstPersonView";

        static void EnsureLayer()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layer = tagManager.FindProperty("layers").GetArrayElementAtIndex(ViewModelLayer);
            if (layer.stringValue == ViewModelLayerName) return;
            if (!string.IsNullOrEmpty(layer.stringValue)) throw new InvalidOperationException($"Layer {ViewModelLayer} is already used by '{layer.stringValue}'. Change BeanCrewSetup.ViewModelLayer.");
            layer.stringValue = ViewModelLayerName;
            tagManager.ApplyModifiedProperties();
        }

        /// <summary>Owner view model: outfit arms + overlay camera. Created once; later Inspector edits are kept.</summary>
        static void SetupFirstPerson()
        {
            EnsureLayer();
            string path = Root + "/Prefabs/NetworkPlayer.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var motor = root.GetComponent<NetworkPlayerMotor>();
                var camera = motor.playerCamera;
                camera.cullingMask &= ~(1 << ViewModelLayer);
                camera.nearClipPlane = .05f;
                var cameraData = Get<UniversalAdditionalCameraData>(camera.gameObject);
                var overlayTransform = camera.transform.Find("View Model Camera");
                if (overlayTransform == null)
                {
                    var go = new GameObject("View Model Camera");
                    go.transform.SetParent(camera.transform, false);
                    var overlay = go.AddComponent<Camera>();
                    overlay.clearFlags = CameraClearFlags.Depth;
                    overlay.cullingMask = 1 << ViewModelLayer;
                    overlay.fieldOfView = camera.fieldOfView;
                    overlay.nearClipPlane = .01f; overlay.farClipPlane = 5;
                    var overlayData = go.AddComponent<UniversalAdditionalCameraData>();
                    overlayData.renderType = CameraRenderType.Overlay;
                    overlayData.renderShadows = false;
                    overlayTransform = go.transform;
                }
                var overlayCamera = overlayTransform.GetComponent<Camera>();
                if (!cameraData.cameraStack.Contains(overlayCamera)) cameraData.cameraStack.Add(overlayCamera);

                if (camera.transform.Find("First Person Arms") == null)
                {
                    var armsGo = new GameObject("First Person Arms");
                    armsGo.transform.SetParent(camera.transform, false);
                    var arms = armsGo.AddComponent<FirstPersonArms>();
                    arms.motor = motor;
                    arms.interactor = root.GetComponent<PlayerInteractor>();
                    arms.throwing = root.GetComponent<PlayerThrow>();
                    arms.playerAnimation = root.GetComponent<PlayerAnimation>();
                    arms.appearance = root.GetComponent<CharacterAppearance>();
                    arms.viewModelLayer = ViewModelLayer;
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(ArmsModel);
                    Renderer[] Build(string side, out Transform pivot)
                    {
                        pivot = new GameObject("Arm " + side).transform;
                        pivot.SetParent(armsGo.transform, false);
                        var list = new List<Renderer>();
                        foreach (var outfit in arms.appearance.variantNames)
                        {
                            var source = FindDeep(model.transform, $"FP_{outfit}_{side}");
                            if (source == null) { Debug.LogWarning($"[Coop Art] Missing arm FP_{outfit}_{side}"); list.Add(null); continue; }
                            var piece = new GameObject($"{outfit} {side}");
                            piece.transform.SetParent(pivot, false);
                            piece.transform.localPosition = source.localPosition;
                            piece.transform.localRotation = source.localRotation;
                            piece.transform.localScale = source.localScale;
                            piece.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
                            var renderer = piece.AddComponent<MeshRenderer>();
                            renderer.sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
                            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                            renderer.receiveShadows = false;
                            list.Add(renderer);
                        }
                        return list.ToArray();
                    }
                    arms.leftVariants = Build("L", out arms.leftArm);
                    arms.rightVariants = Build("R", out arms.rightArm);
                    foreach (var t in armsGo.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = ViewModelLayer;
                    arms.leftArm.gameObject.SetActive(false); arms.rightArm.gameObject.SetActive(false);
                    motor.thirdPerson = false; // first person by default (V toggles in play)
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ------------------------------------------------------------------ props
        static GameObject Prop(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(PropModels + "/" + name + ".fbx");

        /// <summary>Adds (once) a metre-scaled visual under a possibly scaled physics root and hides older visuals.</summary>
        static GameObject Dress(GameObject root, string model, float yaw, Vector3 offsetMetres = default, string childName = VisualName)
        {
            var existing = root.transform.Find(childName);
            if (existing != null) return existing.gameObject;
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(Prop(model));
            visual.name = childName;
            visual.transform.SetParent(root.transform, false);
            var s = root.transform.lossyScale;
            visual.transform.localScale = new Vector3(1 / s.x, 1 / s.y, 1 / s.z);
            visual.transform.localPosition = new Vector3(offsetMetres.x / s.x, offsetMetres.y / s.y, offsetMetres.z / s.z);
            visual.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            if (childName == VisualName)
            {
                var oldArt = root.transform.Find("Art Visual");
                if (oldArt != null) oldArt.gameObject.SetActive(false);
                var renderer = root.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.enabled = false;
            }
            return visual;
        }

        static void UpgradePickup(string prefabName, string model, float yaw)
        {
            string path = Root + "/Prefabs/" + prefabName + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try { Dress(root, model, yaw); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static GameObject SceneObject(string name)
        {
            var found = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == name && t.parent == null);
            if (found == null) Debug.LogWarning("[Coop Art] Scene object not found: " + name);
            return found != null ? found.gameObject : null;
        }

        static void UpgradeScene(float yaw)
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != PrototypeBuilder.ScenePath)
            {
                if (scene.isDirty) throw new InvalidOperationException("Save or discard changes in " + scene.path + " first, then open CoopWorkshop.unity.");
                scene = EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);
            }
            void DressNamed(string name, string model, Vector3 offset = default) { var go = SceneObject(name); if (go != null) Dress(go, model, yaw, offset); }
            DressNamed("Generator body", "Generator");
            DressNamed("Shelf", "Workbench");
            DressNamed("Shelf Slot 1", "SlotTray"); DressNamed("Shelf Slot 2", "SlotTray"); DressNamed("Shelf Slot 3", "SlotTray");
            DressNamed("Generator Battery Socket", "BatteryDock");
            DressNamed("Door", "WorkshopDoor");
            DressNamed("Door Button", "BigButton");

            // Pendant lamps: the glow child is switched by the generator's StatePresentation.
            var presentations = Object.FindObjectsByType<StatePresentation>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < 3; i++)
            {
                var lamp = SceneObject("Generator lamp " + i);
                if (lamp == null) continue;
                Dress(lamp, "PendantLamp", yaw, new Vector3(0, .25f, 0));
                bool created = lamp.transform.Find("Bulb Glow") == null;
                var glow = Dress(lamp, "PendantLampGlow", yaw, new Vector3(0, .25f, 0), "Bulb Glow");
                var light = lamp.GetComponent<Light>();
                foreach (var presentation in presentations)
                {
                    if (light == null || presentation.lights == null || !presentation.lights.Contains(light)) continue;
                    if (presentation.activeObjects == null || !presentation.activeObjects.Contains(glow))
                        presentation.activeObjects = (presentation.activeObjects ?? new GameObject[0]).Append(glow).ToArray();
                    if (created) glow.SetActive(light.enabled);
                    EditorUtility.SetDirty(presentation);
                }
            }

            // Warmer floor and walls.
            var floor = SceneObject("Floor");
            if (floor != null) floor.GetComponent<Renderer>().sharedMaterial = SurfaceMaterial("Workshop Floor", new Color(.46f, .5f, .52f), .15f);
            foreach (var wall in new[] { "Back wall", "Front wall", "Left wall", "Right wall" })
            {
                var go = SceneObject(wall);
                if (go != null) go.GetComponent<Renderer>().sharedMaterial = SurfaceMaterial("Workshop Wall", new Color(.93f, .86f, .72f), .1f);
            }

            // Static set dressing with simple colliders (not networked).
            var existingDecor = SceneObject("Workshop Decor");
            if (existingDecor != null)
                foreach (Transform piece in existingDecor.transform)
                {
                    piece.localRotation = Quaternion.Euler(0, piece.localEulerAngles.y, 0);
                    var box = piece.GetComponent<BoxCollider>();
                    if (box != null) FitCollider(piece.gameObject, box);
                }
            if (existingDecor == null)
            {
                var decor = new GameObject("Workshop Decor");
                void Place(string model, Vector3 position, float turn, bool collide = true)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(Prop(model));
                    go.transform.SetParent(decor.transform, false);
                    go.transform.localPosition = position;
                    go.transform.localRotation = Quaternion.Euler(0, yaw + turn, 0);
                    go.isStatic = true;
                    if (!collide) return;
                    FitCollider(go, go.AddComponent<BoxCollider>());
                }
                Place("PalletStack", new Vector3(-8.9f, 0, 8.6f), 20);
                Place("PalletStack", new Vector3(8.7f, 0, -8.6f), -15);
                Place("PalletStack", new Vector3(-9.1f, 0, -2f), 90);
                Place("OilDrum", new Vector3(9.2f, 0, 8.9f), 0);
                Place("OilDrum", new Vector3(8.4f, 0, 9.3f), 40);
                Place("OilDrum", new Vector3(9.35f, 0, 7.95f), 80);
                Place("TrafficCone", new Vector3(1.7f, 0, 6.3f), 0);
                Place("TrafficCone", new Vector3(-1.2f, 0, 8.1f), 30);
                Place("TrafficCone", new Vector3(6.5f, 0, -1.5f), 10);
                Place("Pegboard", new Vector3(-4f, 1.7f, 9.8f), 0, false);
                Place("Pegboard", new Vector3(10.8f, 1.7f, 3.6f), 90, false);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void FitCollider(GameObject go, BoxCollider box)
        {
            var filters = go.GetComponentsInChildren<MeshFilter>();
            if (filters.Length == 0) return;
            var toLocal = go.transform.worldToLocalMatrix;
            Bounds? total = null;
            foreach (var f in filters)
            {
                var m = toLocal * f.transform.localToWorldMatrix; var b = f.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var p = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (total == null) total = new Bounds(p, Vector3.zero); else { var t = total.Value; t.Encapsulate(p); total = t; }
                }
            }
            box.center = total.Value.center; box.size = total.Value.size;
        }

        /// <summary>One-off fix for scenes dressed by the first version of this tool, which turned every prop 180 degrees.</summary>
        public static int TurnPropsAround()
        {
            int turned = 0;
            void Turn(Transform t) { t.localRotation = Quaternion.Euler(0, 180, 0) * t.localRotation; turned++; }
            foreach (var name in new[] { "Crate", "Battery", "Tool" })
            {
                string path = Root + "/Prefabs/" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try { var v = root.transform.Find(VisualName); if (v != null) Turn(v); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var scene = SceneManager.GetActiveScene();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.parent == null) continue;
                // Pickups live in prefabs (handled above); only scene-authored visuals are turned here.
                if (t.name == VisualName && t.GetComponentInParent<PickupItem>(true) != null)
                {
                    PrefabUtility.RevertObjectOverride(t, InteractionMode.AutomatedAction);
                    continue;
                }
                if (t.name == VisualName && t.GetComponentInParent<PlayerAnimation>() == null && PrefabUtility.GetPrefabInstanceHandle(t.gameObject) != null && t.parent.name != "Workshop Decor") Turn(t);
                else if (t.name == "Bulb Glow") Turn(t);
                else if (t.parent.name == "Workshop Decor" && t.parent.parent == null) Turn(t);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            return turned;
        }

        static T Get<T>(GameObject go) where T : Component => go.TryGetComponent(out T c) ? c : go.AddComponent<T>();

        public static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = FindDeep(c, name); if (f != null) return f; }
            return null;
        }

        // ------------------------------------------------------------------ previews
        /// <summary>Renders the player prefab (outfit index, clip, normalised time) to TestResults/Art.</summary>
        public static string RenderCharacter(int variant, string clipName, float time, string file, float cameraYaw = 35)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var character = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/NetworkPlayer.prefab"), preview);
                character.transform.position = Vector3.up;
                character.GetComponent<CharacterAppearance>().Apply(variant);
                var visual = character.transform.Find(VisualName).gameObject;
                var clip = AssetDatabase.LoadAllAssetsAtPath(CharacterModel).OfType<AnimationClip>().FirstOrDefault(c => c.name == clipName);
                if (clip != null) clip.SampleAnimation(visual, time * clip.length);
                var holdPoint = character.GetComponent<NetworkPlayerMotor>().holdPoint;
                if (clipName.StartsWith("Carry"))
                {
                    var crate = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Crate.prefab"), preview);
                    crate.transform.SetPositionAndRotation(holdPoint.position, holdPoint.rotation);
                }
                var dir = Quaternion.Euler(12, 180 + cameraYaw, 0) * Vector3.forward;
                return Capture(preview, new Vector3(0, 1f, 0) - dir * 5.2f, new Vector3(0, .95f, 0), 26, file, 700, 800);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        public static string Capture(Scene scene, Vector3 position, Vector3 target, float fov, string file, int width, int height)
        {
            var camObj = new GameObject("Preview camera");
            var lightObj = new GameObject("Preview key");
            if (scene.IsValid() && EditorSceneManager.IsPreviewScene(scene))
            {
                SceneManager.MoveGameObjectToScene(camObj, scene);
                SceneManager.MoveGameObjectToScene(lightObj, scene);
            }
            try
            {
                var camera = camObj.AddComponent<Camera>();
                if (EditorSceneManager.IsPreviewScene(scene)) { camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.55f, .62f, .7f); }
                camera.transform.position = position; camera.transform.LookAt(target); camera.fieldOfView = fov;
                if (EditorSceneManager.IsPreviewScene(scene))
                {
                    var light = lightObj.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.6f; light.transform.rotation = Quaternion.Euler(40, -140, 0);
                }
                var rt = new RenderTexture(width, height, 24) { antiAliasing = 4 };
                camera.targetTexture = rt; camera.Render();
                var previous = RenderTexture.active; RenderTexture.active = rt;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                Directory.CreateDirectory("TestResults/Art");
                string output = "TestResults/Art/" + file + ".png";
                File.WriteAllBytes(output, image.EncodeToPNG());
                RenderTexture.active = previous; camera.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(image);
                return output;
            }
            finally { Object.DestroyImmediate(camObj); Object.DestroyImmediate(lightObj); }
        }

        /// <summary>Renders the open scene from a temporary camera (edit mode).</summary>
        public static string CaptureScene(Vector3 position, Vector3 target, string file) =>
            Capture(SceneManager.GetActiveScene(), position, target, 50, file, 1280, 720);
    }

    /// <summary>Editor-only mailbox for automated art checks: write Temp/BeanCrewRequest.json, read Temp/BeanCrewResponse.txt.
    /// Inactive without the request file and excluded from builds.</summary>
    [InitializeOnLoad]
    static class BeanCrewMailbox
    {
        [Serializable] class Request { public string id; public string command; public string target; public int variant; public float time; public Vector3 position; public Vector3 look; public float yaw = 35; }
        static double next;
        static BeanCrewMailbox() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            next = EditorApplication.timeSinceStartup + .25;
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp"));
            string path = Path.Combine(folder, "BeanCrewRequest.json");
            if (!File.Exists(path)) return;
            Request request = null;
            string result;
            try
            {
                request = JsonUtility.FromJson<Request>(File.ReadAllText(path));
                File.Delete(path);
                result = Execute(request);
            }
            catch (Exception e) { result = "ERROR: " + e; Debug.LogException(e); }
            File.WriteAllText(Path.Combine(folder, "BeanCrewResponse.txt"), (request?.id ?? "?") + "\n" + result);
        }
        static string Execute(Request r)
        {
            switch (r.command)
            {
                case "net":
                    var nm = Unity.Netcode.NetworkManager.Singleton;
                    if (nm == null) return "no NetworkManager";
                    var cfg = nm.NetworkConfig;
                    return "config=" + cfg.GetConfig(false) + " tick=" + cfg.TickRate + " force=" + cfg.ForceSamePrefabs + " prefabs=" +
                        string.Join(",", cfg.Prefabs.Prefabs.Select(p => (p.Prefab != null ? p.Prefab.name : "null") + ":" + (p.Prefab != null ? p.Prefab.GetComponent<Unity.Netcode.NetworkObject>().PrefabIdHash.ToString() : "-"))) +
                        " links=" + string.Join(",", cfg.Prefabs.NetworkPrefabOverrideLinks.Keys.OrderBy(k => k));
                case "steamsetup": return SteamSetup.Apply();
                case "steam":
                    var lobby = Object.FindFirstObjectByType<CoopPrototype.Steam.SteamLobby>();
                    return $"ready={CoopPrototype.Steam.SteamBootstrap.Ready} error='{CoopPrototype.Steam.SteamBootstrap.Error}' persona='{CoopPrototype.Steam.SteamBootstrap.PersonaName}' lobby={(lobby != null ? lobby.LobbyId : 0)} status='{(lobby != null ? lobby.Status : "no lobby component")}' listening={(Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)} transport={(Unity.Netcode.NetworkManager.Singleton != null ? Unity.Netcode.NetworkManager.Singleton.NetworkConfig.NetworkTransport?.GetType().Name : "-")}";
                case "steamhost": Object.FindFirstObjectByType<CoopPrototype.Steam.SteamLobby>().Host(); return "steam host requested";
                case "steamstop": Object.FindFirstObjectByType<NetworkSession>().Stop(); return "stopped";
                case "ping": return "pong play=" + EditorApplication.isPlaying + " compiling=" + EditorApplication.isCompiling;
                case "refresh": AssetDatabase.Refresh(); return "refreshed";
                case "apply": BeanCrewSetup.Apply(); return "applied";
                case "turnprops": return "turned " + BeanCrewSetup.TurnPropsAround();
                case "revertpickups":
                    int reverted = 0;
                    foreach (var item in Object.FindObjectsByType<PickupItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        var v = item.transform.Find(BeanCrewSetup.VisualName);
                        if (v == null) continue;
                        PrefabUtility.RevertObjectOverride(v, InteractionMode.AutomatedAction); reverted++;
                    }
                    var sc = SceneManager.GetActiveScene(); EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc);
                    return "reverted " + reverted;
                case "character": return BeanCrewSetup.RenderCharacter(r.variant, r.target, r.time, $"BeanCrew_{r.variant}_{r.target.Replace(" ", "")}_{r.time:F2}", r.yaw);
                case "scene": return BeanCrewSetup.CaptureScene(r.position, r.look, string.IsNullOrEmpty(r.target) ? "BeanCrewScene" : r.target);
                case "log":
                    var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Unity/Editor/Editor.log");
                    string[] lines;
                    using (var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var reader = new StreamReader(stream)) lines = reader.ReadToEnd().Split('\n');
                    return string.Join("\n", lines.Skip(Math.Max(0, lines.Length - 120)));
                case "open": EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath); return "opened";
                case "play": EditorApplication.isPlaying = true; return "play requested";
                case "stop": EditorApplication.isPlaying = false; return "stop requested";
            }
            throw new InvalidOperationException("Unknown command " + r.command);
        }
    }
}
