using System;
using System.IO;
using System.Linq;
using CoopPrototype.Checkpoint;
using CoopPrototype.Frontend;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

namespace CoopPrototype.Editor
{
    /// <summary>One-time additive authoring. Saves ordinary scenes and prefabs, preserving previous scenery in inactive roots.</summary>
    public static partial class CheckpointSceneSetup
    {
        const string Root = "Assets/CoopPrototype", Art = Root + "/Art/Checkpoint";
        static Material hull, dark, metal, mint, violet, amber, skin, black, white;
        static TMP_FontAsset documentFont;
        static TMP_FontAsset DocumentFont()
        {
            if (documentFont != null) return documentFont;
            if (TMP_Settings.defaultFontAsset != null) return documentFont = TMP_Settings.defaultFontAsset;
            string path = Art + "/Document TMP Font.asset";
            documentFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (documentFont != null) return documentFont;
            Directory.CreateDirectory(Art);
            if (TMP_Settings.instance == null)
            {
                Directory.CreateDirectory("Assets/Resources");
                var settingsPath = "Assets/Resources/TMP Settings.asset";
                if (AssetDatabase.LoadAssetAtPath<TMP_Settings>(settingsPath) == null)
                    AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<TMP_Settings>(), settingsPath);
                AssetDatabase.SaveAssets();
            }
            documentFont = TMP_FontAsset.CreateFontAsset("Arial", "Regular", 48);
            if (documentFont == null) throw new InvalidOperationException("Unity could not create the document TMP font asset.");
            AssetDatabase.CreateAsset(documentFont, path);
            return documentFont;
        }
        static Material Mat(string name, Color color, bool glow = false)
        {
            string path = Art + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            material.SetFloat("_Smoothness", .35f);
            if (glow) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * 2); }
            AssetDatabase.CreateAsset(material, path); return material;
        }
        static Transform Group(string name, Transform parent = null)
        { var t = new GameObject(name).transform; if (parent != null) t.SetParent(parent, false); return t; }
        static GameObject Shape(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool collide = false)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collide) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        static GameObject Box(Transform p, string n, Vector3 pos, Vector3 size, Material m, bool collision = false) => Shape(p, n, PrimitiveType.Cube, pos, size, m, collision);
        static TextMesh Label(Transform parent, string text, Vector3 position, float size = .12f, Color? color = null)
        {
            var t = Group(text.Replace('\n', ' '), parent); t.localPosition = position;
            var label = t.gameObject.AddComponent<TextMesh>(); label.text = text; label.fontSize = 64; label.characterSize = size;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center; label.color = color ?? Color.white;
            return label;
        }
        static TextMeshProUGUI DocumentLabel(Transform parent, string text, Vector3 position)
        {
            var go = new GameObject("Document text canvas", typeof(RectTransform), typeof(Canvas)); var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false); rect.localPosition = position; rect.localRotation = Quaternion.Euler(90, 0, 0); rect.localScale = Vector3.one * .0001f; rect.sizeDelta = new Vector2(420, 620);
            var worldCanvas = go.GetComponent<Canvas>(); worldCanvas.renderMode = RenderMode.WorldSpace; worldCanvas.overrideSorting = true; worldCanvas.sortingOrder = 30;
            var label = go.AddComponent<TextMeshProUGUI>(); label.text = text; label.font = DocumentFont(); label.fontSize = 18; label.color = Color.white;
            label.alignment = TextAlignmentOptions.TopLeft; label.enableWordWrapping = true;
            label.rectTransform.sizeDelta = rect.sizeDelta; label.rectTransform.anchoredPosition = Vector2.zero;
            return label;
        }
        static RawImage DocumentPortrait(Transform parent, Vector3 position)
        {
            var go = new GameObject("Document portrait canvas", typeof(RectTransform), typeof(Canvas)); var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false); rect.localPosition = position; rect.localRotation = Quaternion.Euler(90, 0, 0); rect.localScale = Vector3.one * .0001f; rect.sizeDelta = new Vector2(130, 165);
            var worldCanvas = go.GetComponent<Canvas>(); worldCanvas.renderMode = RenderMode.WorldSpace; worldCanvas.overrideSorting = true; worldCanvas.sortingOrder = 31;
            var image = go.AddComponent<RawImage>(); image.rectTransform.sizeDelta = rect.sizeDelta; image.rectTransform.anchoredPosition = Vector2.zero;
            return image;
        }
        static Transform Ring(Transform parent, string name, Vector3 position, float radius, Material material, int segments = 48)
        {
            var ring = Group(name, parent); ring.localPosition = position;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                var part = Box(ring, "Arc " + i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius, new Vector3(.18f, radius * 2 * Mathf.PI / segments * 1.12f, .22f), material);
                part.transform.localRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg);
            }
            return ring;
        }
        static void Palette()
        {
            Directory.CreateDirectory(Art); AssetDatabase.Refresh();
            hull = Mat("Hull porcelain", new Color(.68f, .8f, .82f)); dark = Mat("Midnight panels", new Color(.035f, .055f, .1f));
            metal = Mat("Deck blue", new Color(.10f, .17f, .24f)); mint = Mat("Portal mint", new Color(.38f, 1, .54f), true);
            violet = Mat("Ion lavender", new Color(.51f, .33f, 1), true); amber = Mat("Safety amber", new Color(1, .5f, .12f), true);
            skin = Mat("Alien green", new Color(.42f, .88f, .5f)); black = Mat("Alien eyes", new Color(.012f, .025f, .04f)); white = Mat("Starlight", new Color(.7f, .88f, 1), true);
        }
        [MenuItem("Coop Prototype/Checkpoint/Set Up Space Checkpoint")]
        public static void Setup()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            EditorSceneManager.SaveOpenScenes();
            Directory.CreateDirectory("Tools/Checkpoint/BeforeSpaceTheme");
            foreach (var path in new[] { MainMenuSetup.MenuPath, MainMenuSetup.GamePath })
            { var backup = "Tools/Checkpoint/BeforeSpaceTheme/" + Path.GetFileName(path); if (!File.Exists(backup)) File.Copy(path, backup); }
            Palette(); CheckpointDataSetup.Create();
            var alien = AlienPrefab(); var cases = Cases(alien); var document = DocumentPrefab();
            var player = PrefabUtility.LoadPrefabContents(Root + "/Prefabs/NetworkPlayer.prefab");
            if (player.GetComponent<HeldItemInspector>() == null) player.AddComponent<HeldItemInspector>();
            player.GetComponent<NetworkPlayerMotor>().playerCamera.cullingMask |= 1 << 31;
            player.GetComponent<NetworkPlayerMotor>().playerCamera.clearFlags = CameraClearFlags.SolidColor;
            player.GetComponent<NetworkPlayerMotor>().playerCamera.backgroundColor = new Color(.012f, .018f, .04f);
            PrefabUtility.SaveAsPrefabAsset(player, Root + "/Prefabs/NetworkPlayer.prefab"); PrefabUtility.UnloadPrefabContents(player);
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(Root + "/Prefabs/NetworkPrefabs.asset");
            if (!list.PrefabList.Any(p => p.Prefab == document.gameObject)) { list.Add(new NetworkPrefab { Prefab = document.gameObject }); EditorUtility.SetDirty(list); }
            AssetDatabase.SaveAssets();
            BuildGame(cases, document, alien); BuildMenu(); AssetDatabase.SaveAssets();
            Debug.Log("[Checkpoint] MainMenu and GameWorld authored. Legacy scenery preserved; all checkpoint objects are editable.");
        }
        static GameObject AlienPrefab()
        {
            string path = Art + "/Veloran.prefab"; var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing != null) return existing;
            var root = Group("Veloran traveller");
            Shape(root, "Suit", PrimitiveType.Capsule, new Vector3(0, .45f, 0), new Vector3(.65f, .52f, .55f), metal);
            Shape(root, "Skin Head", PrimitiveType.Sphere, new Vector3(0, 1.12f, 0), new Vector3(.9f, .92f, .7f), skin);
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = Shape(root, "Obsidian eye", PrimitiveType.Sphere, new Vector3(side * .23f, 1.18f, -.29f), new Vector3(.29f, .4f, .13f), black);
                eye.transform.localRotation = Quaternion.Euler(0, 0, -side * 20);
                Shape(root, "Eye glint", PrimitiveType.Sphere, new Vector3(side * .23f - .045f, 1.26f, -.355f), Vector3.one * .065f, white);
                Shape(root, "Skin Hand", PrimitiveType.Sphere, new Vector3(side * .46f, .4f, -.12f), Vector3.one * .22f, skin);
                var antenna = Shape(root, "Antenna", PrimitiveType.Cylinder, new Vector3(side * .25f, 1.68f, .03f), new Vector3(.045f, .23f, .045f), skin);
                antenna.transform.localRotation = Quaternion.Euler(0, 0, -side * 20);
                Shape(root, "Antenna light", PrimitiveType.Sphere, new Vector3(side * .33f, 1.9f, .03f), Vector3.one * .14f, mint);
            }
            Box(root, "Mouth", new Vector3(0, .92f, -.345f), new Vector3(.16f, .025f, .018f), black);
            Box(root, "Visitor badge", new Vector3(.12f, .54f, -.282f), new Vector3(.17f, .11f, .025f), amber);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path); Object.DestroyImmediate(root.gameObject); return prefab;
        }
        static TravellerCase[] Cases(GameObject alien)
        {
            var result = new TravellerCase[3];
            string[] names = { "Zuri Vex", "Milo Quark", "Nima Sol" };
            Color[] colors = { new(.42f, .88f, .5f), new(.68f, .46f, .91f), new(.95f, .6f, .32f) };
            for (int i = 0; i < result.Length; i++)
            {
                string casePath = Root + "/CheckpointData/Case_" + i + ".asset";
                result[i] = AssetDatabase.LoadAssetAtPath<TravellerCase>(casePath);
                if (result[i] != null)
                {
                    if (result[i].traveller.portrait == null) { result[i].traveller.portrait = Portrait(alien, colors[i], "Portrait_" + i); EditorUtility.SetDirty(result[i].traveller); }
                    continue;
                }
                var traveller = i == 0 ? AssetDatabase.LoadAssetAtPath<TravellerDefinition>(Root + "/CheckpointData/Traveller_Zuri.asset") : ScriptableObject.CreateInstance<TravellerDefinition>();
                if (i > 0)
                {
                    traveller.travellerId = "traveller-" + i; traveller.displayName = names[i]; traveller.birthDate = i == 1 ? "2177-06-08" : "2191-11-24";
                    traveller.birthPlace = i == 1 ? "Orion Exchange" : "Lunar Orchard"; traveller.race = i == 1 ? "Orionite" : "Lunari";
                    traveller.vehicleModel = "Comet Hopper"; traveller.vehicleRegistration = i == 1 ? "OR-718" : "LS-209";
                    AssetDatabase.CreateAsset(traveller, Root + "/CheckpointData/Traveller_" + i + ".asset");
                }
                traveller.portrait = Portrait(alien, colors[i], "Portrait_" + i); EditorUtility.SetDirty(traveller);
                var documents = new DocumentDefinition[2];
                if (i == 0)
                {
                    documents[0] = AssetDatabase.LoadAssetAtPath<DocumentDefinition>(Root + "/CheckpointData/Document_Zuri_ID.asset");
                    documents[1] = AssetDatabase.LoadAssetAtPath<DocumentDefinition>(Root + "/CheckpointData/Document_Zuri_Vehicle.asset");
                }
                else for (int d = 0; d < 2; d++)
                {
                    var doc = ScriptableObject.CreateInstance<DocumentDefinition>(); doc.documentId = $"visitor-{i}-{d}"; doc.traveller = traveller; doc.expiresOn = "2220-12-31";
                    doc.kind = AssetDatabase.LoadAssetAtPath<DocumentKind>(Root + "/CheckpointData/Kind_" + (d == 0 ? "Identity" : "Vehicle") + ".asset");
                    doc.fields = d == 0 ? new[] { new DocumentField("name", "Name", traveller.displayName), new DocumentField("birth-date", "Date of birth", traveller.birthDate), new DocumentField("birth-place", "Place of birth", traveller.birthPlace), new DocumentField("race", "Race", traveller.race) }
                        : new[] { new DocumentField("owner", "Owner", traveller.displayName), new DocumentField("model", "Vehicle model", traveller.vehicleModel), new DocumentField("registration", "Registration", traveller.vehicleRegistration) };
                    AssetDatabase.CreateAsset(doc, Root + $"/CheckpointData/Document_{i}_{d}.asset"); documents[d] = doc;
                }
                var record = ScriptableObject.CreateInstance<TravellerCase>(); record.traveller = traveller; record.documents = documents; record.skinColor = colors[i];
                record.greeting = i == 0 ? "Hi, officer! First time through this portal. Please tell me this is the right galaxy." : i == 1 ? "Good shift, officer. My navigation system insists this is a shortcut." : "Hello! I hope your quarantine rules allow moon-fruit.";
                record.purpose = i == 0 ? "Visiting my cousins on Velora. I promised to bring snacks." : i == 1 ? "A conference about unusually small black holes. Three days, maximum." : "Delivering fruit to the orbital market. It only glows a little.";
                record.vehicleAnswer = $"A {traveller.vehicleModel}, registration {traveller.vehicleRegistration}. No wheels, no problem.";
                AssetDatabase.CreateAsset(record, casePath); result[i] = record;
            }
            return result;
        }
        static Texture2D Portrait(GameObject alien, Color color, string name)
        {
            string path = Art + "/" + name + ".png"; var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path); if (existing != null) return existing;
            var preview = EditorSceneManager.NewPreviewScene();
            var model = (GameObject)PrefabUtility.InstantiatePrefab(alien, preview);
            var tempSkin = new Material(skin) { color = color };
            foreach (var r in model.GetComponentsInChildren<Renderer>()) if (r.name.StartsWith("Skin")) r.sharedMaterial = tempSkin;
            var camera = new GameObject("Portrait camera").AddComponent<Camera>(); SceneManager.MoveGameObjectToScene(camera.gameObject, preview); camera.scene = preview;
            camera.transform.position = new Vector3(0, 1.05f, -4); camera.transform.LookAt(new Vector3(0, 1.05f, 0)); camera.orthographic = true; camera.orthographicSize = 1.12f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.07f, .12f, .18f);
            var light = new GameObject("Portrait key").AddComponent<Light>(); SceneManager.MoveGameObjectToScene(light.gameObject, preview);
            light.type = LightType.Directional; light.intensity = 2; light.transform.rotation = Quaternion.Euler(28, 25, 0);
            SaveCamera(camera, path, 512, 512); EditorSceneManager.ClosePreviewScene(preview); Object.DestroyImmediate(tempSkin);
            AssetDatabase.ImportAsset(path); return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static DocumentItem DocumentPrefab()
        {
            string path = Root + "/Prefabs/CheckpointDocument.prefab"; var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing != null) return existing.GetComponent<DocumentItem>();
            var item = ScriptableObject.CreateInstance<ItemDefinition>(); item.displayName = "Travel document"; item.category = "Document";
            AssetDatabase.CreateAsset(item, Root + "/Items/TravelDocument.asset");
            var go = new GameObject("Travel Document"); go.AddComponent<NetworkObject>(); go.AddComponent<NetworkTransform>();
            go.AddComponent<BoxCollider>().size = new Vector3(.58f, .06f, .78f);
            var body = go.AddComponent<Rigidbody>(); body.mass = .15f; body.interpolation = RigidbodyInterpolation.Interpolate;
            var document = go.AddComponent<DocumentItem>(); document.definition = item;
            Box(go.transform, "Document cover", Vector3.zero, new Vector3(.58f, .055f, .78f), dark);
            Box(go.transform, "Document spine", new Vector3(-.25f, .031f, 0), new Vector3(.04f, .007f, .74f), mint);
            document.portraitImage = DocumentPortrait(go.transform, new Vector3(-.18f, .034f, .12f));
            document.coverLabel = DocumentLabel(go.transform, "GALACTIC ID", new Vector3(.08f, .034f, -.04f));
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path); Object.DestroyImmediate(go); return prefab.GetComponent<DocumentItem>();
        }
        static void SpaceLighting()
        {
            RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.28f, .34f, .46f); RenderSettings.fog = false;
            var key = new GameObject("Orbital key light").AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.5f; key.color = new Color(.73f, .86f, 1); key.transform.rotation = Quaternion.Euler(48, -30, 0); key.shadows = LightShadows.Soft;
        }
        static void Stars(Transform parent)
        {
            var random = new System.Random(709);
            for (int i = 0; i < 160; i++)
            {
                float yaw = (float)random.NextDouble() * Mathf.PI * 2, height = 5 + (float)random.NextDouble() * 38;
                float radius = 42 + (float)random.NextDouble() * 25;
                Shape(parent, "Star " + i, PrimitiveType.Sphere, new Vector3(Mathf.Cos(yaw) * radius, height, Mathf.Sin(yaw) * radius), Vector3.one * (.07f + (float)random.NextDouble() * .14f), i % 5 == 0 ? violet : white);
            }
            Shape(parent, "Distant lavender moon", PrimitiveType.Sphere, new Vector3(-28, 23, 42), Vector3.one * 14, Mat("Moon lilac", new Color(.24f, .18f, .38f)));
            var orbit = Ring(parent, "Moon orbit", new Vector3(-28, 23, 41), 9, violet); orbit.localRotation = Quaternion.Euler(65, 15, 0);
        }
        static void Portal(Transform parent, Vector3 position, float radius)
        {
            var root = Group("Arrival portal", parent); root.localPosition = position;
            Ring(root, "Containment ring", Vector3.zero, radius + .25f, metal);
            Ring(root, "Outer plasma", Vector3.zero, radius, mint).gameObject.AddComponent<PortalMotion>().degreesPerSecond = 12;
            var inner = Ring(root, "Inner plasma", new Vector3(0, 0, .04f), radius * .83f, mint); inner.gameObject.AddComponent<PortalMotion>().degreesPerSecond = -21;
            for (int i = 0; i < 9; i++)
            {
                var swirl = Ring(root, "Vortex " + i, new Vector3(Mathf.Sin(i) * .12f, Mathf.Cos(i) * .14f, .12f + i * .045f), radius * (1 - (i + 1) * .09f), i % 3 == 0 ? violet : mint, 28);
                swirl.localScale = new Vector3(1, .86f, 1); swirl.gameObject.AddComponent<PortalMotion>().degreesPerSecond = 8 + i * 4;
            }
            var glow = new GameObject("Portal glow").AddComponent<Light>(); glow.transform.SetParent(root, false); glow.type = LightType.Point; glow.range = 12; glow.intensity = 12; glow.color = new Color(.3f, 1, .5f);
        }
        static void BuildGame(TravellerCase[] cases, DocumentItem document, GameObject alien)
        {
            var scene = EditorSceneManager.OpenScene(MainMenuSetup.GamePath);
            if (GameObject.Find("Space Checkpoint") != null) return;
            // Scene switching can unload native asset objects held only in managed locals.
            Palette();
            cases = Enumerable.Range(0, 3).Select(i => AssetDatabase.LoadAssetAtPath<TravellerCase>(Root + "/CheckpointData/Case_" + i + ".asset")).ToArray();
            document = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/CheckpointDocument.prefab").GetComponent<DocumentItem>();
            alien = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Veloran.prefab");
            var context = Object.FindFirstObjectByType<GameSceneContext>();
            var retained = context.spawnPoints.Select(p => p.gameObject).Append(context.gameObject).Append(context.loadingCamera.gameObject).ToArray();
            var oldRoots = scene.GetRootGameObjects(); var legacy = Group("Legacy Workshop (preserved)");
            foreach (var root in oldRoots) if (!retained.Contains(root)) root.transform.SetParent(legacy, true);
            legacy.gameObject.SetActive(false);
            var world = Group("Space Checkpoint"); SpaceLighting(); Stars(world);
            Box(world, "30m station deck", new Vector3(0, -.3f, 0), new Vector3(30, .6f, 30), metal, true);
            for (int x = -14; x < 15; x += 2) for (int z = -14; z < 15; z += 2)
                Box(world, "Deck panel", new Vector3(x, .008f, z), new Vector3(1.94f, .012f, 1.94f), dark);
            Box(world, "Hover lane", new Vector3(4, .025f, 0), new Vector3(5.4f, .025f, 29.8f), metal);
            foreach (float x in new[] { 1.25f, 6.75f }) Box(world, "Lane edge", new Vector3(x, .05f, 0), new Vector3(.07f, .05f, 29.8f), mint);
            for (int z = -11; z < 15; z += 3) Box(world, "Lane guidance", new Vector3(4, .05f, z), new Vector3(.15f, .035f, 1.1f), amber);
            for (int i = -14; i <= 14; i += 4)
            {
                foreach (float x in new[] { -14.7f, 14.7f }) Box(world, "Rail post", new Vector3(x, .7f, i), new Vector3(.16f, 1.4f, .16f), hull, true);
            }
            foreach (float x in new[] { -14.7f, 14.7f }) { Box(world, "Rail", new Vector3(x, 1.2f, 0), new Vector3(.12f, .12f, 30), mint); Box(world, "Boundary", new Vector3(x, 1, 0), new Vector3(.15f, 2, 30), metal, true); }
            foreach (float z in new[] { -14.9f, 14.9f }) Box(world, "End barrier", new Vector3(-6.8f, .6f, z), new Vector3(16, 1.2f, .2f), metal, true);
            // Invisible safety walls cover the visual vehicle gates without constraining scripted shuttles.
            foreach (float z in new[] { -15f, 15f }) { var boundary = Box(world, "Safety boundary", new Vector3(0, 2, z), new Vector3(30, 4, .1f), dark, true); boundary.GetComponent<Renderer>().enabled = false; }
            Portal(world, new Vector3(4, 3, -13.8f), 3.1f);
            Label(world, "SECTOR 07   /   GALACTIC ARRIVALS", new Vector3(-3, 5.1f, 8), .2f, new Color(.64f, 1, .8f));
            Box(world, "Customs canopy", new Vector3(-5, 4.2f, 4), new Vector3(12, .25f, 6), metal);
            foreach (float x in new[] { -10.5f, .5f }) Box(world, "Canopy column", new Vector3(x, 2, 6.5f), new Vector3(.3f, 4, .3f), hull, true);
            Box(world, "Canopy trim", new Vector3(-5, 4.08f, 1), new Vector3(12, .08f, .12f), mint);
            Label(world, "DOCUMENT CONTROL", new Vector3(-5, 3.5f, 4), .16f);
            var manager = Group("Checkpoint Authority", world).gameObject; manager.AddComponent<NetworkObject>();
            var checkpoint = manager.AddComponent<CheckpointSession>(); checkpoint.cases = cases; checkpoint.documentPrefab = document;
            var panel = Group("Checkpoint interface", world).gameObject.AddComponent<CheckpointPanel>(); panel.checkpoint = checkpoint;
            Box(world, "Document collection shelf", new Vector3(-.15f, .85f, 1), new Vector3(2.1f, .2f, 1.5f), hull, true);
            Box(world, "Collection pedestal", new Vector3(-.15f, .4f, 1), new Vector3(.25f, .8f, .7f), metal, true);
            Label(world, "COLLECT PAPERS", new Vector3(-.15f, 1.55f, 1.7f), .065f, new Color(.6f, 1, .75f));
            checkpoint.documentDeliveryPoints = new Transform[2];
            for (int i = 0; i < 2; i++) { var p = Group("Document delivery " + i, world); p.position = new Vector3(-.65f + i, 1.04f, 1); checkpoint.documentDeliveryPoints[i] = p; }
            Terminal(world, checkpoint, new Vector3(-5, 0, 3));
            for (int i = 0; i < 2; i++) Slot(world, "Scanner Tray " + (i + 1), new Vector3(-6 + i * 2, 1.06f, 1.8f), checkpoint, true);
            Box(world, "Sorting table", new Vector3(-9, .85f, -2), new Vector3(3.5f, .2f, 1.6f), hull, true);
            for (int i = 0; i < 3; i++) Slot(world, "Sorting Tray " + (i + 1), new Vector3(-10.1f + i * 1.1f, 1.04f, -2), checkpoint, false);
            Label(world, "SORT / SHARE / INSPECT", new Vector3(-9, 1.65f, -1.2f), .075f);
            Box(world, "Expansion pad", new Vector3(-9, .04f, 8), new Vector3(4, .06f, 3), metal);
            Label(world, "FUTURE TERMINAL BAY", new Vector3(-9, .12f, 8), .08f, new Color(.6f, .65f, .8f)).transform.localRotation = Quaternion.Euler(90, 0, 0);
            var vehicle = Group("Traveller Shuttle", world); vehicle.position = new Vector3(4, 0, 1); vehicle.gameObject.AddComponent<NetworkObject>();
            var collider = vehicle.gameObject.AddComponent<BoxCollider>(); collider.center = new Vector3(0, 1.6f, 0); collider.size = new Vector3(3.6f, 2.8f, 3.4f);
            var traveller = vehicle.gameObject.AddComponent<TravellerInteractable>(); traveller.checkpoint = checkpoint;
            var hover = Group("Hover assembly", vehicle);
            Shape(hover, "Hull", PrimitiveType.Sphere, new Vector3(0, 1.05f, 0), new Vector3(3.5f, .9f, 3.5f), hull);
            Shape(hover, "Cockpit base", PrimitiveType.Cylinder, new Vector3(0, 1.45f, 0), new Vector3(1.7f, .18f, 1.7f), dark);
            var driver = (GameObject)PrefabUtility.InstantiatePrefab(alien); driver.transform.SetParent(hover, false); driver.transform.localPosition = new Vector3(0, 1.5f, 0); driver.transform.localRotation = Quaternion.Euler(0, 65, 0);
            for (int side = -1; side <= 1; side += 2)
            {
                Shape(hover, "Ion pod", PrimitiveType.Capsule, new Vector3(side * 1.5f, .8f, .15f), new Vector3(.55f, .5f, .55f), metal);
                Shape(hover, "Thruster glow", PrimitiveType.Sphere, new Vector3(side * 1.5f, .35f, .15f), new Vector3(.45f, .18f, .45f), violet);
            }
            var registration = Label(hover, cases[0].traveller.vehicleRegistration, new Vector3(0, 1.05f, -1.7f), .08f, Color.black);
            Label(hover, "COMET HOPPER", new Vector3(0, 1.3f, -1.68f), .035f, Color.black);
            var boot = Group("Giant rejection boot", world); boot.position = new Vector3(4, 5, 5);
            Box(boot, "Boot shaft", new Vector3(0, 1.1f, .55f), new Vector3(1.6f, 2.5f, 1.7f), amber);
            Box(boot, "Boot toe", new Vector3(0, -.1f, -.65f), new Vector3(1.9f, 1, 3.8f), hull);
            Box(boot, "Boot sole", new Vector3(0, -.65f, -.65f), new Vector3(2, .25f, 4), dark);
            for (int i = 0; i < 4; i++) Box(boot, "Boot lace", new Vector3(0, .5f + i * .35f, -.33f), new Vector3(1.25f, .08f, .08f), dark);
            boot.gameObject.SetActive(false);
            var view = manager.AddComponent<CheckpointVehicleView>(); view.checkpoint = checkpoint; view.vehicle = vehicle; view.hoverVisual = hover; view.boot = boot; view.registration = registration;
            view.alienSkin = driver.GetComponentsInChildren<Renderer>().Where(r => r.name.StartsWith("Skin")).ToArray();
            view.entry = Group("Arrival waypoint", world); view.entry.position = new Vector3(4, 0, -13);
            view.stop = Group("Inspection waypoint", world); view.stop.position = new Vector3(4, 0, 1);
            view.exit = Group("Exit waypoint", world); view.exit.position = new Vector3(4, 0, 18);
            for (int i = 0; i < context.spawnPoints.Length; i++) context.spawnPoints[i].position = new Vector3(-3 + i * 1.5f, 1.1f, -5);
            context.loadingCamera.transform.position = new Vector3(-18, 19, -24); context.loadingCamera.transform.LookAt(new Vector3(0, 0, 1)); context.loadingCamera.backgroundColor = new Color(.012f, .018f, .04f); context.loadingCamera.clearFlags = CameraClearFlags.SolidColor;
            EditorSceneManager.SaveScene(scene);
        }
        static void Terminal(Transform root, CheckpointSession checkpoint, Vector3 position)
        {
            var desk = Group("Inspection Terminal", root); desk.position = position;
            desk.gameObject.AddComponent<NetworkObject>(); var collider = desk.gameObject.AddComponent<BoxCollider>(); collider.center = new Vector3(0, 1.4f, 0); collider.size = new Vector3(2.8f, 2.6f, 1);
            desk.gameObject.AddComponent<CheckpointTerminal>().checkpoint = checkpoint;
            Box(desk, "Desk", new Vector3(0, .85f, 0), new Vector3(3.6f, .25f, 2.5f), hull);
            Box(desk, "Desk support", new Vector3(0, .4f, 0), new Vector3(2.5f, .8f, 1.3f), metal);
            Box(desk, "Monitor frame", new Vector3(0, 1.9f, -.1f), new Vector3(2.4f, 1.25f, .2f), dark);
            Box(desk, "Screen", new Vector3(0, 1.9f, -.215f), new Vector3(2.2f, 1.07f, .025f), metal);
            Label(desk, "SECTOR 07\nINSPECTION TERMINAL\n[E] ACCESS / SCAN", new Vector3(0, 1.9f, -.24f), .066f, new Color(.6f, 1, .74f));
            Box(desk, "Keyboard", new Vector3(0, 1.02f, -.8f), new Vector3(1.4f, .07f, .45f), dark);
        }
        static void Slot(Transform root, string name, Vector3 position, CheckpointSession checkpoint, bool scanner)
        {
            var go = Box(root, name, position, new Vector3(.85f, .10f, .95f), scanner ? mint : violet, true);
            go.AddComponent<NetworkObject>(); var socket = go.AddComponent<ItemSocket>(); socket.acceptedCategory = "Document";
            socket.snapPoint = Group("Paper snap", go.transform); socket.snapPoint.position = position + Vector3.up * .09f;
            if (scanner) { var scan = go.AddComponent<DocumentScanner>(); scan.checkpoint = checkpoint; UnityEventTools.AddPersistentListener(socket.onOccupancyChanged, scan.Scan); }
        }
        static void BuildMenu()
        {
            var scene = EditorSceneManager.OpenScene(MainMenuSetup.MenuPath);
            if (GameObject.Find("Spaceport Menu") != null) return;
            Palette();
            var backdrop = Object.FindFirstObjectByType<MenuBackdrop>();
            foreach (Transform child in backdrop.transform) if (child.GetComponent<CharacterPreview>() == null) child.gameObject.SetActive(false);
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) light.gameObject.SetActive(false);
            var world = Group("Spaceport Menu"); SpaceLighting(); Stars(world);
            Box(world, "Observation deck", new Vector3(2, -.2f, 0), new Vector3(25, .4f, 18), metal);
            for (int i = 0; i < 8; i++) Box(world, "Floor seams", new Vector3(-8 + i * 3, .015f, 0), new Vector3(.025f, .02f, 17), mint);
            Portal(world, new Vector3(5, 2.6f, 7), 2.8f);
            Box(world, "Back rail", new Vector3(1, .5f, 5), new Vector3(20, .12f, .12f), mint);
            for (int i = 0; i < 4; i++)
            {
                Shape(world, "Crew platform " + i, PrimitiveType.Cylinder, new Vector3(1.7f + i * 1.35f, -.02f, i * .45f), new Vector3(1.2f, .08f, 1.2f), dark);
                Shape(world, "Platform glow " + i, PrimitiveType.Cylinder, new Vector3(1.7f + i * 1.35f, -.07f, i * .45f), new Vector3(1.3f, .04f, 1.3f), mint);
            }
            Label(world, "BORDER CONTROL  /  07", new Vector3(4, 4.7f, 6), .16f, new Color(.65f, 1, .78f));
            var camera = Object.FindFirstObjectByType<Camera>(); camera.backgroundColor = new Color(.012f, .018f, .04f); camera.clearFlags = CameraClearFlags.SolidColor;
            EditorSceneManager.SaveScene(scene);
        }
        public static void SaveCamera(Camera camera, string path, int width = 1440, int height = 900)
        {
            var rt = new RenderTexture(width, height, 24); var previousTarget = camera.targetTexture; var previous = RenderTexture.active;
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, image.EncodeToPNG());
            camera.targetTexture = previousTarget; RenderTexture.active = previous; Object.DestroyImmediate(image); Object.DestroyImmediate(rt);
        }
        public static void Preview(string target)
        {
            var scene = EditorSceneManager.OpenScene(target == "menu" ? MainMenuSetup.MenuPath : MainMenuSetup.GamePath);
            var camera = Object.FindFirstObjectByType<Camera>(); SaveCamera(camera, "TestResults/Checkpoint/" + target + ".png");
        }
        public static void FixWorldText()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            var shader = Shader.Find("Coop/Checkpoint World Text");
            if (shader == null) throw new InvalidOperationException("Import WorldText.shader first.");
            string path = Art + "/Depth tested text.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            foreach (var scenePath in new[] { MainMenuSetup.GamePath, TestScenePath, MainMenuSetup.MenuPath })
            {
                var scene = EditorSceneManager.OpenScene(scenePath);
                string rootName = scenePath == MainMenuSetup.MenuPath ? "Spaceport Menu" : "Space Checkpoint";
                foreach (var text in GameObject.Find(rootName).GetComponentsInChildren<TextMesh>(true))
                {
                    var renderer = text.GetComponent<Renderer>();
                    if (renderer.sharedMaterial != material && renderer.sharedMaterial.mainTexture != null) material.mainTexture = renderer.sharedMaterial.mainTexture;
                    renderer.sharedMaterial = material;
                }
                EditorSceneManager.SaveScene(scene);
            }
            var prefab = PrefabUtility.LoadPrefabContents(Root + "/Prefabs/CheckpointDocument.prefab");
            prefab.GetComponentInChildren<TextMesh>().GetComponent<Renderer>().sharedMaterial = material;
            PrefabUtility.SaveAsPrefabAsset(prefab, Root + "/Prefabs/CheckpointDocument.prefab"); PrefabUtility.UnloadPrefabContents(prefab);
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
        }
        [MenuItem("Coop Prototype/Checkpoint/Repair Inspection Presentation")]
        public static void RepairInspectionPresentation()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            Palette();
            var player = PrefabUtility.LoadPrefabContents(Root + "/Prefabs/NetworkPlayer.prefab");
            player.GetComponent<NetworkPlayerMotor>().playerCamera.cullingMask |= 1 << 31;
            PrefabUtility.SaveAsPrefabAsset(player, Root + "/Prefabs/NetworkPlayer.prefab");
            PrefabUtility.UnloadPrefabContents(player);
            var document = PrefabUtility.LoadPrefabContents(Root + "/Prefabs/CheckpointDocument.prefab");
            var legacyDetails = document.GetComponent<DocumentInspectionContent>();
            if (legacyDetails != null) Object.DestroyImmediate(legacyDetails);
            foreach (var legacyLabel in document.GetComponentsInChildren<TextMesh>(true)) Object.DestroyImmediate(legacyLabel.gameObject);
            var item = document.GetComponent<DocumentItem>();
            if (item.coverLabel == null) item.coverLabel = DocumentLabel(document.transform, "GALACTIC ID", new Vector3(.08f, .034f, -.04f));
            item.coverLabel.font = DocumentFont();
            item.coverLabel.transform.localPosition = new Vector3(.08f, .034f, -.04f);
            item.coverLabel.transform.localScale = Vector3.one * .0001f;
            if (item.portraitImage == null) item.portraitImage = DocumentPortrait(document.transform, new Vector3(-.18f, .034f, .12f));
            item.portraitImage.transform.localPosition = new Vector3(-.18f, .034f, .12f);
            item.portraitImage.transform.localScale = Vector3.one * .0001f;
            PrefabUtility.SaveAsPrefabAsset(document, Root + "/Prefabs/CheckpointDocument.prefab");
            PrefabUtility.UnloadPrefabContents(document);
            AssetDatabase.SaveAssets();
        }
        public const string TestScenePath = Root + "/Scenes/CheckpointNetworkTest.unity";
        [MenuItem("Coop Prototype/Checkpoint/Create Direct Network Test Scene")]
        public static void CreateTestScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            if (File.Exists(TestScenePath)) { EditorSceneManager.OpenScene(TestScenePath); return; }
            AssetDatabase.CopyAsset(MainMenuSetup.GamePath, TestScenePath);
            var test = EditorSceneManager.OpenScene(TestScenePath);
            var context = Object.FindFirstObjectByType<GameSceneContext>();
            var workshop = EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath, OpenSceneMode.Additive);
            var source = workshop.GetRootGameObjects().Select(r => r.GetComponent<NetworkSession>()).First(s => s != null);
            var scope = Object.Instantiate(source.gameObject); scope.name = "Direct Checkpoint Test Session";
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(scope, test);
            var session = scope.GetComponent<NetworkSession>(); session.steam = null; session.spawnPoints = context.spawnPoints; session.overviewCamera = context.loadingCamera;
            EditorSceneManager.CloseScene(workshop, true); UnityEngine.SceneManagement.SceneManager.SetActiveScene(test);
            EditorSceneManager.SaveScene(test);
            if (!EditorBuildSettings.scenes.Any(s => s.path == TestScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(TestScenePath, true)).ToArray();
            AssetDatabase.SaveAssets();
        }
    }
}
