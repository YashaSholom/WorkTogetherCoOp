using System.IO;
using CoopPrototype;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CoopPrototype.Editor
{
    /// <summary>Authoring utility. All output is saved as ordinary editable assets, never generated at runtime.</summary>
    public static class PrototypeBuilder
    {
        const string Root = "Assets/CoopPrototype";
        public const string ScenePath = Root + "/Scenes/CoopWorkshop.unity";
        static Material Material(string name, Color color)
        {
            var path = Root + "/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.color = color;
            return mat;
        }
        static GameObject Cube(string name, Vector3 position, Vector3 size, Material mat)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name; obj.transform.position = position; obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = mat;
            return obj;
        }
        static void Networked(GameObject obj, bool moving = false)
        {
            obj.AddComponent<NetworkObject>();
            if (moving) obj.AddComponent<NetworkTransform>();
        }
        static Transform Point(string name, Vector3 position)
        {
            var obj = new GameObject(name); obj.transform.position = position; return obj.transform;
        }
        static void Label(string text, Vector3 position)
        {
            var obj = new GameObject(text); obj.transform.position = position;
            var mesh = obj.AddComponent<TextMesh>(); mesh.text = text; mesh.fontSize = 48; mesh.characterSize = .06f; mesh.anchor = TextAnchor.MiddleCenter;
        }
        static ItemDefinition Item(string name, string category)
        {
            var path = Root + "/Items/" + name + ".asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item == null) { item = ScriptableObject.CreateInstance<ItemDefinition>(); AssetDatabase.CreateAsset(item, path); }
            item.displayName = name; item.category = category; return item;
        }
        static GameObject PickupPrefab(string name, Material mat, ItemDefinition definition, Vector3 scale)
        {
            var obj = Cube(name, Vector3.zero, scale, mat);
            Networked(obj, true);
            obj.AddComponent<Rigidbody>().mass = 1;
            obj.AddComponent<PickupItem>().definition = definition;
            var prefab = PrefabUtility.SaveAsPrefabAsset(obj, Root + "/Prefabs/" + name + ".prefab");
            Object.DestroyImmediate(obj); return prefab;
        }
        static GameObject Instance(GameObject prefab, Vector3 position)
        {
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab); obj.transform.position = position; return obj;
        }
        static ItemSocket Slot(string name, Vector3 position, Material mat)
        {
            var obj = Cube(name, position, new Vector3(.8f, .15f, .8f), mat);
            Networked(obj);
            var socket = obj.AddComponent<ItemSocket>();
            socket.snapPoint = Point("Snap Point", position + Vector3.up * .4f);
            socket.snapPoint.SetParent(obj.transform, true);
            return socket;
        }
        [MenuItem("Coop Prototype/Create Workshop Scene")]
        public static void Create()
        {
            // Never overwrite a developer-edited scene through automatic regeneration.
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            foreach (var folder in new[] { "Scenes", "Prefabs", "Materials", "Items" }) Directory.CreateDirectory(Root + "/" + folder);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            var blue = Material("Player blue", new Color(.15f, .55f, .9f));
            var grey = Material("Concrete", new Color(.28f, .32f, .36f));
            var orange = Material("Crate orange", new Color(.9f, .45f, .15f));
            var green = Material("Battery green", new Color(.3f, .85f, .2f));
            var yellow = Material("Slot yellow", new Color(.9f, .8f, .15f));
            Cube("Floor", new Vector3(0, -.25f, 0), new Vector3(22, .5f, 22), grey);
            Cube("Back wall", new Vector3(0, 1.5f, 10), new Vector3(22, 3, .3f), grey);
            Cube("Left wall", new Vector3(-11, 1.5f, 0), new Vector3(.3f, 3, 22), grey);
            Cube("Right wall", new Vector3(11, 1.5f, 0), new Vector3(.3f, 3, 22), grey);
            Cube("Front wall", new Vector3(0, 1.5f, -11), new Vector3(22, 3, .3f), grey);
            var sun = new GameObject("Ambient directional light").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = .65f; sun.transform.rotation = Quaternion.Euler(55, -30, 0);
            RenderSettings.ambientLight = new Color(.3f, .3f, .35f);
            var player = new GameObject("Network Player"); Networked(player, true);
            var controller = player.AddComponent<CharacterController>(); controller.height = 2; controller.radius = .4f;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule); visual.name = "Capsule visual"; visual.transform.SetParent(player.transform, false); Object.DestroyImmediate(visual.GetComponent<Collider>()); visual.GetComponent<Renderer>().sharedMaterial = blue;
            // Layer 2 excludes one's own body from first-person camera without hiding remote capsules.
            var camera = new GameObject("Owner Camera").AddComponent<Camera>(); camera.transform.SetParent(player.transform, false); camera.transform.localPosition = new Vector3(0, .65f, .1f); camera.nearClipPlane = .05f; camera.gameObject.AddComponent<AudioListener>(); camera.gameObject.SetActive(false);
            var motor = player.AddComponent<NetworkPlayerMotor>(); motor.playerCamera = camera;
            motor.holdPoint = Point("Hold Point", new Vector3(0, .3f, 1.25f)); motor.holdPoint.SetParent(player.transform, false);
            player.AddComponent<PlayerInteractor>();
            var playerPrefab = PrefabUtility.SaveAsPrefabAsset(player, Root + "/Prefabs/NetworkPlayer.prefab"); Object.DestroyImmediate(player);
            var crate = PickupPrefab("Crate", orange, Item("Crate", "Cargo"), Vector3.one * .55f);
            var battery = PickupPrefab("Battery", green, Item("Battery", "Battery"), new Vector3(.35f, .6f, .35f));
            var tool = PickupPrefab("Tool", blue, Item("Tool", "Tool"), new Vector3(.6f, .18f, .25f));
            Instance(crate, new Vector3(-1, .5f, -1)); Instance(crate, new Vector3(1, .5f, -1)); Instance(tool, new Vector3(-2, .5f, 0)); Instance(battery, new Vector3(2, .5f, 0));
            Cube("Shelf", new Vector3(-4, .5f, 3), new Vector3(4, 1, 1.5f), grey);
            for (int i = 0; i < 3; i++) Slot("Shelf Slot " + (i + 1), new Vector3(-5.2f + i * 1.2f, 1.1f, 2.65f), yellow);
            Label("GENERIC SLOTS / E TO INSERT OR REMOVE", new Vector3(-4, 2.3f, 3.4f));
            Cube("Generator body", new Vector3(4, .6f, 3.6f), new Vector3(2, 1.2f, 1.2f), grey);
            var generator = Slot("Generator Battery Socket", new Vector3(4, .85f, 2.5f), green); generator.acceptedCategory = "Battery"; generator.lockAfterInsertion = true;
            var power = generator.gameObject.AddComponent<NetworkState>();
            UnityEventTools.AddPersistentListener(generator.onOccupancyChanged, power.SetState);
            var presentation = generator.gameObject.AddComponent<StatePresentation>(); presentation.lights = new Light[3]; presentation.activeObjects = new GameObject[0];
            for (int i = 0; i < 3; i++)
            {
                var light = new GameObject("Generator lamp " + i).AddComponent<Light>(); light.type = LightType.Point; light.range = 9; light.intensity = 5; light.color = new Color(.7f, 1, .5f); light.transform.position = new Vector3(-4 + i * 4, 3, 3); light.enabled = false; presentation.lights[i] = light;
            }
            UnityEventTools.AddPersistentListener(power.onStateApplied, presentation.Apply);
            Label("GENERATOR / BATTERY REQUIRED", new Vector3(4, 2.3f, 3.4f));
            var door = Cube("Door", new Vector3(0, 1.25f, 7), new Vector3(2, 2.5f, .25f), orange);
            var button = Cube("Door Button", new Vector3(-2, 1, 6), Vector3.one * .4f, blue); Networked(button);
            var action = button.AddComponent<ActionInteractable>(); var state = button.AddComponent<NetworkState>();
            var doorView = button.AddComponent<StatePresentation>(); doorView.lights = new Light[0]; doorView.activeObjects = new GameObject[0]; doorView.movingPart = door.transform; doorView.offLocalPosition = door.transform.position; doorView.onLocalPosition = door.transform.position + Vector3.up * 3;
            UnityEventTools.AddPersistentListener(action.onServerInteract, state.Toggle); UnityEventTools.AddPersistentListener(state.onStateApplied, doorView.Apply);
            Label("BUTTON / E TO TOGGLE DOOR", new Vector3(0, 4.8f, 7));
            var sessionObj = new GameObject("Network Session"); var transport = sessionObj.AddComponent<UnityTransport>(); var manager = sessionObj.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, PlayerPrefab = playerPrefab, TickRate = 30, EnableSceneManagement = true };
            var list = ScriptableObject.CreateInstance<NetworkPrefabsList>(); list.Add(new NetworkPrefab { Prefab = playerPrefab }); list.Add(new NetworkPrefab { Prefab = crate }); list.Add(new NetworkPrefab { Prefab = battery }); list.Add(new NetworkPrefab { Prefab = tool }); AssetDatabase.CreateAsset(list, Root + "/Prefabs/NetworkPrefabs.asset"); manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
            var session = sessionObj.AddComponent<NetworkSession>(); session.manager = manager; session.transport = transport;
            session.spawnPoints = new Transform[] { Point("Spawn A", new Vector3(-1.5f, 1.1f, -5)), Point("Spawn B", new Vector3(1.5f, 1.1f, -5)), Point("Spawn C", new Vector3(0, 1.1f, -7)) };
            var overview = new GameObject("Overview Camera").AddComponent<Camera>(); overview.transform.position = new Vector3(0, 9, -10); overview.transform.rotation = Quaternion.Euler(35, 0, 0); session.overviewCamera = overview;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultIsNativeResolution = false; PlayerSettings.defaultScreenWidth = 1100; PlayerSettings.defaultScreenHeight = 720; PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            AssetDatabase.SaveAssets();
            Debug.Log("[Coop] Workshop saved: " + ScenePath);
        }
        [MenuItem("Coop Prototype/Build Windows Prototype")]
        public static void Build()
        {
            EditorSceneManager.SaveOpenScenes();
            Directory.CreateDirectory("Builds/CoopWorkshop");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = System.Array.ConvertAll(System.Array.FindAll(EditorBuildSettings.scenes, s => s.enabled), s => s.path), locationPathName = "Builds/CoopWorkshop/CoopWorkshop.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new System.Exception("Build failed: " + report.summary.result);
        }
    }
}
