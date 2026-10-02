using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CoopPrototype.Checkpoint;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CoopPrototype.Editor
{
    /// <summary>
    /// Additive authoring for: the terminal codex (federations and seals, vehicle makers and models, races), a second race,
    /// travelling companions, richer documents (issuing federation, seal/badge, ID and chassis numbers, security marks),
    /// and the detention wing with the holding cell and its tablet. Existing assets keep their GUIDs; GameWorld is backed up
    /// to Tools/Checkpoint/BeforeHoldingCell before the first run. Running it again does not duplicate anything.
    /// </summary>
    public static partial class CheckpointSceneSetup
    {
        const string DataFolder = Root + "/CheckpointData", CodexFolder = DataFolder + "/Codex", CodexArt = Art + "/Codex";

        sealed class TravellerSpec
        {
            public string asset, federation, race, vehicle; public int bounty; public Color skin;
        }

        [MenuItem("Coop Prototype/Checkpoint/Add Holding Cell, Codex and Document Seals")]
        public static string AddDetention()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            EditorSceneManager.SaveOpenScenes();
            string backup = "Tools/Checkpoint/BeforeHoldingCell"; Directory.CreateDirectory(backup);
            string backupScene = backup + "/" + Path.GetFileName(MainMenuSetup.GamePath);
            if (!File.Exists(backupScene)) File.Copy(MainMenuSetup.GamePath, backupScene);
            var scene = EditorSceneManager.OpenScene(MainMenuSetup.GamePath);
            Palette();
            if (!AssetDatabase.IsValidFolder(CodexFolder)) AssetDatabase.CreateFolder(DataFolder, "Codex");
            ImportEmblems();
            var session = Object.FindFirstObjectByType<CheckpointSession>();
            var view = session.vehicleView;
            var drivers = LegacyDrivers(view);
            var veloranModel = VeloranRaceModel(drivers);
            var brakkanModel = BrakkanRaceModel();
            var codex = BuildCodex(view, veloranModel, brakkanModel);
            UpdateTravellers(codex);
            UpdateDocumentPrefab();
            AssetDatabase.SaveAssets();
            // Scene: reload asset references after the asset work.
            session = Object.FindFirstObjectByType<CheckpointSession>();
            codex = AssetDatabase.LoadAssetAtPath<CheckpointCodex>(CodexFolder + "/Checkpoint Codex.asset");
            session.codex = codex;
            var world = GameObject.Find("Space Checkpoint").transform;
            var cell = PlanetArtRedesign.BuildDetentionWing(world, session);
            session.holdingCell = cell;
            var ui = Object.FindFirstObjectByType<CheckpointPanel>().gameObject;
            if (ui.GetComponent<HoldingCellPanel>() == null) ui.AddComponent<HoldingCellPanel>();
            BuildSeats(session, LegacyDrivers(session.vehicleView));
            ApplyWorldText(world);
            EditorUtility.SetDirty(session);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "Codex, second race, companions, document seals and the detention wing authored in GameWorld (backup: " + backupScene + ").";
        }

        // ------------------------------------------------------------------ emblems
        static void ImportEmblems()
        {
            foreach (var path in Directory.GetFiles(CodexArt, "*.png").Select(p => p.Replace('\\', '/')))
            {
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer == null) continue;
                bool changed = importer.alphaIsTransparency != true || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.wrapMode != TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.wrapMode = TextureWrapMode.Clamp;
                if (changed) importer.SaveAndReimport();
            }
        }
        static Texture2D Emblem(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(CodexArt + "/" + name + ".png");

        // ------------------------------------------------------------------ race models
        static GameObject[] LegacyDrivers(CheckpointVehicleView view)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Veloran.prefab");
            return view.vehicle.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject)
                .Where(g => PrefabUtility.IsAnyPrefabInstanceRoot(g) && PrefabUtility.GetCorrespondingObjectFromSource(g) == source).ToArray();
        }
        /// <summary>The Veloran race model is copied from the station-styled shuttle driver, so it matches the art pass.</summary>
        static GameObject VeloranRaceModel(GameObject[] drivers)
        {
            string path = Art + "/Race_Veloran.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing != null) return existing;
            var driver = drivers.FirstOrDefault();
            GameObject copy = driver != null ? Object.Instantiate(driver) : (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Veloran.prefab"));
            if (PrefabUtility.IsAnyPrefabInstanceRoot(copy)) PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            copy.name = "Veloran"; copy.SetActive(true);
            copy.transform.SetParent(null, false); copy.transform.localPosition = Vector3.zero; copy.transform.localRotation = Quaternion.identity; copy.transform.localScale = Vector3.one;
            foreach (var collider in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            var prefab = PrefabUtility.SaveAsPrefabAsset(copy, path); Object.DestroyImmediate(copy); return prefab;
        }
        static Material PlanetMat(string name) => AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/Planet/" + name + ".mat");
        /// <summary>Stocky one-eyed hauler race with horns and tusks. Skin renderers are tinted per traveller.</summary>
        static GameObject BrakkanRaceModel()
        {
            string path = Art + "/Race_Brakkan.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing != null) return existing;
            var bone = PlanetMat("Porcelain hull") ?? hull; var ink = PlanetMat("Graphite rubber") ?? black; var fitting = PlanetMat("Tangerine fittings") ?? amber;
            var suit = Mat("Brakkan workwear", new Color(.36f, .27f, .2f));
            var root = Group("Brakkan");
            Shape(root, "Suit", PrimitiveType.Capsule, new Vector3(0, .5f, 0), new Vector3(.95f, .52f, .78f), suit);
            Box(root, "Work belt", new Vector3(0, .5f, 0), new Vector3(.93f, .1f, .74f), ink);
            Box(root, "Belt buckle", new Vector3(0, .5f, -.37f), new Vector3(.16f, .12f, .04f), fitting);
            Shape(root, "Skin Head", PrimitiveType.Sphere, new Vector3(0, 1.08f, 0), new Vector3(1.08f, .74f, .82f), skin);
            Shape(root, "Skin Jaw", PrimitiveType.Sphere, new Vector3(0, .88f, -.08f), new Vector3(.86f, .42f, .7f), skin);
            Shape(root, "Eye white", PrimitiveType.Sphere, new Vector3(0, 1.17f, -.33f), new Vector3(.44f, .44f, .2f), bone);
            Shape(root, "Pupil", PrimitiveType.Sphere, new Vector3(0, 1.15f, -.42f), new Vector3(.2f, .26f, .06f), ink);
            Shape(root, "Eye glint", PrimitiveType.Sphere, new Vector3(-.05f, 1.22f, -.45f), Vector3.one * .06f, white);
            var brow = Box(root, "Heavy brow", new Vector3(0, 1.41f, -.3f), new Vector3(.58f, .09f, .12f), ink); brow.transform.localRotation = Quaternion.Euler(-12, 0, 0);
            for (int side = -1; side <= 1; side += 2)
            {
                var horn = Shape(root, "Horn", PrimitiveType.Capsule, new Vector3(side * .42f, 1.47f, .02f), new Vector3(.12f, .2f, .12f), bone);
                horn.transform.localRotation = Quaternion.Euler(0, 0, -side * 38);
                Shape(root, "Horn tip", PrimitiveType.Sphere, new Vector3(side * .55f, 1.63f, .02f), Vector3.one * .09f, bone);
                var tusk = Box(root, "Tusk", new Vector3(side * .15f, .86f, -.4f), new Vector3(.06f, .12f, .05f), bone); tusk.transform.localRotation = Quaternion.Euler(0, 0, side * 10);
                Shape(root, "Skin Ear", PrimitiveType.Sphere, new Vector3(side * .56f, 1.08f, .02f), new Vector3(.12f, .26f, .2f), skin);
                Shape(root, "Skin Hand", PrimitiveType.Sphere, new Vector3(side * .6f, .42f, -.1f), Vector3.one * .27f, skin);
                Box(root, "Shoulder pad", new Vector3(side * .38f, .82f, 0), new Vector3(.3f, .12f, .5f), fitting);
            }
            Box(root, "Mouth", new Vector3(0, .8f, -.39f), new Vector3(.26f, .025f, .02f), ink);
            Box(root, "Visitor badge", new Vector3(.2f, .62f, -.37f), new Vector3(.17f, .11f, .025f), fitting);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path); Object.DestroyImmediate(root.gameObject); return prefab;
        }
        /// <summary>Renders a race model (skin tinted) to a PNG. Overwrites the image in place, keeping its GUID.</summary>
        static Texture2D RenderFigure(GameObject model, Color skinColor, string path)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            var figure = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
            var tempSkin = new Material(skin) { color = skinColor };
            foreach (var r in figure.GetComponentsInChildren<Renderer>()) if (r.name.StartsWith("Skin")) r.sharedMaterial = tempSkin;
            var camera = new GameObject("Portrait camera").AddComponent<Camera>(); SceneManager.MoveGameObjectToScene(camera.gameObject, preview); camera.scene = preview;
            camera.transform.position = new Vector3(0, 1.05f, -4); camera.transform.LookAt(new Vector3(0, 1.05f, 0)); camera.orthographic = true; camera.orthographicSize = 1.12f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.07f, .12f, .18f);
            var light = new GameObject("Portrait key").AddComponent<Light>(); SceneManager.MoveGameObjectToScene(light.gameObject, preview);
            light.type = LightType.Directional; light.intensity = 2; light.transform.rotation = Quaternion.Euler(28, 25, 0);
            SaveCamera(camera, path, 512, 512); EditorSceneManager.ClosePreviewScene(preview); Object.DestroyImmediate(tempSkin);
            AssetDatabase.ImportAsset(path); return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        /// <summary>Codex picture of a vehicle, rendered from a copy of the scene's vehicle assembly.</summary>
        static Texture2D RenderVehicle(Transform source, string path)
        {
            if (source == null) return null;
            var preview = EditorSceneManager.NewPreviewScene();
            var copy = Object.Instantiate(source.gameObject); SceneManager.MoveGameObjectToScene(copy, preview);
            copy.SetActive(true); copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); copy.transform.localScale = source.lossyScale;
            foreach (var t in copy.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Cargo crate")) t.gameObject.SetActive(true);
            // Occupants vary by visitor: the codex picture shows the empty vehicle.
            foreach (var t in copy.GetComponentsInChildren<Transform>(true)) if (t.Find("Skin Head") != null) t.gameObject.SetActive(false);
            var renderers = copy.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
            var bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var camera = new GameObject("Vehicle camera").AddComponent<Camera>(); SceneManager.MoveGameObjectToScene(camera.gameObject, preview); camera.scene = preview;
            camera.fieldOfView = 28; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.09f, .12f, .19f);
            Vector3 direction = new Vector3(-1.1f, .62f, -1.25f).normalized;
            float distance = bounds.extents.magnitude / Mathf.Sin(camera.fieldOfView * .5f * Mathf.Deg2Rad) * .78f;
            camera.transform.position = bounds.center + direction * distance; camera.transform.LookAt(bounds.center);
            foreach (var (angles, intensity) in new[] { (new Vector3(35, 30, 0), 1.9f), (new Vector3(20, 200, 0), .8f) })
            { var light = new GameObject("Key").AddComponent<Light>(); SceneManager.MoveGameObjectToScene(light.gameObject, preview); light.type = LightType.Directional; light.intensity = intensity; light.transform.rotation = Quaternion.Euler(angles); }
            SaveCamera(camera, path, 640, 360); EditorSceneManager.ClosePreviewScene(preview);
            AssetDatabase.ImportAsset(path); return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ codex data
        static T Asset<T>(string path, Action<T> fill) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            bool created = asset == null;
            if (created) asset = ScriptableObject.CreateInstance<T>();
            fill(asset);
            if (created) AssetDatabase.CreateAsset(asset, path); else EditorUtility.SetDirty(asset);
            return asset;
        }
        static CheckpointCodex BuildCodex(CheckpointVehicleView view, GameObject veloranModel, GameObject brakkanModel)
        {
            FederationDefinition Fed(string code, string name, Color colour, string capital, string description) =>
                Asset<FederationDefinition>($"{CodexFolder}/Federation_{code}.asset", f => { f.code = code; f.displayName = name; f.colour = colour; f.capital = capital; f.description = description; f.emblem = Emblem("Fed_" + code); });
            var federations = new[]
            {
                Fed("VFW", "Velora Free Worlds", new Color(.16f, .70f, .67f), "Velora Station", "Loose union of Veloran colony stations and orbital farms. Seal: a ringed planet under three moons."),
                Fed("ORC", "Orion Concord", new Color(.46f, .34f, .94f), "Orion Exchange", "Trade council of the Orion belt. Seal: a four-point star with three small stars."),
                Fed("BIC", "Brakkan Iron Compact", new Color(.93f, .52f, .12f), "Ferro Dock", "Mining and hauling clans under one forge charter. Seal: a gear around a hex nut."),
                Fed("OTR", "Ossari Tide Republic", new Color(.93f, .31f, .51f), "Mirror Harbour", "Ocean-moon republic of market towns and preserve makers. Seal: a crescent over waves."),
                Fed("NTS", "Nebula Trade Syndicate", new Color(.2f, .7f, .33f), "none (drifting fleet)", "Merchant fleet with no fixed world. Seal: a three-armed spiral. Forgers love it."),
            };
            VehicleManufacturer Maker(string code, string name, Color colour, string description) =>
                Asset<VehicleManufacturer>($"{CodexFolder}/Maker_{code}.asset", m => { m.code = code; m.displayName = name; m.colour = colour; m.description = description; m.emblem = Emblem("Maker_" + code); });
            var makers = new[]
            {
                Maker("CW", "Comet Works", new Color(.23f, .59f, .96f), "Orion-belt shuttle builder. Passenger shuttles only. Badge: a comet."),
                Maker("HG", "Haulgard Industries", new Color(.89f, .31f, .2f), "Brakkan heavy-vehicle works. Cargo haulers. Badge: two chevrons."),
                Maker("ZD", "Zephyr Drive Co.", new Color(.67f, .51f, .98f), "Racing gliders. No Zephyr model is certified for Sector 07."),
            };
            var comet = Asset<VehicleModelDefinition>(CodexFolder + "/Vehicle_CometHopper.asset", v =>
            {
                v.modelName = "Comet Hopper"; v.manufacturer = makers[0]; v.kind = VehicleKind.Shuttle;
                v.description = "Round two-seat hover shuttle. Bobs gently while parked. Carries no cargo.";
                v.picture = RenderVehicle(view.hoverVisual, CodexArt + "/Vehicle_CometHopper.png");
            });
            var haul = Asset<VehicleModelDefinition>(CodexFolder + "/Vehicle_HaulHopper.asset", v =>
            {
                v.modelName = "Haul Hopper"; v.manufacturer = makers[1]; v.kind = VehicleKind.Truck;
                v.description = "Flat-bed hover truck for up to four crates. Stops level so cargo can be unloaded.";
                v.picture = RenderVehicle(view.truckAssembly, CodexArt + "/Vehicle_HaulHopper.png");
            });
            var veloran = Asset<RaceDefinition>(CodexFolder + "/Race_Veloran.asset", r =>
            {
                r.displayName = "Veloran"; r.model = veloranModel; r.homeworld = "Velora Prime";
                r.description = "Cheerful, big-headed wanderers from the lavender deserts. Most visitors to Sector 07 are Veloran.";
                r.identifyingFeatures = "• Two thin antennae with glowing tips\n• Two large, glossy black eyes\n• Smooth round head, small hands\n• No horns and no tusks";
                r.portrait = RenderFigure(veloranModel, new Color(.42f, .88f, .5f), CodexArt + "/Race_Veloran.png");
            });
            var brakkan = Asset<RaceDefinition>(CodexFolder + "/Race_Brakkan.asset", r =>
            {
                r.displayName = "Brakkan"; r.model = brakkanModel; r.homeworld = "The Brakka forge belt";
                r.description = "Stocky hauler folk from the forge asteroids. Gruff, strong, excellent at lifting crates.";
                r.identifyingFeatures = "• One large central eye under a heavy brow\n• Two curved horns\n• Small lower tusks\n• Wide flat head and broad shoulder pads";
                r.portrait = RenderFigure(brakkanModel, new Color(.38f, .62f, .92f), CodexArt + "/Race_Brakkan.png");
            });
            return Asset<CheckpointCodex>(CodexFolder + "/Checkpoint Codex.asset", c =>
            { c.federations = federations; c.manufacturers = makers; c.vehicles = new[] { comet, haul }; c.races = new[] { veloran, brakkan }; });
        }

        // ------------------------------------------------------------------ travellers, companions and documents
        static void UpdateTravellers(CheckpointCodex codex)
        {
            FederationDefinition F(string code) => codex.federations.First(f => f.code == code);
            RaceDefinition R(string name) => codex.races.First(r => r.displayName == name);
            var specs = new[]
            {
                new TravellerSpec { asset = "Traveller_Zuri", federation = "VFW", race = "Veloran", vehicle = "Comet Hopper", bounty = 80 },
                new TravellerSpec { asset = "Traveller_1", federation = "ORC", race = "Veloran", vehicle = "Comet Hopper", bounty = 90 },
                new TravellerSpec { asset = "Traveller_2", federation = "VFW", race = "Veloran", vehicle = "Comet Hopper", bounty = 70 },
                new TravellerSpec { asset = "Traveller_3", federation = "BIC", race = "Brakkan", vehicle = "Haul Hopper", bounty = 140 },
                new TravellerSpec { asset = "Traveller_4", federation = "OTR", race = "Brakkan", vehicle = "Haul Hopper", bounty = 110 },
            };
            foreach (var s in specs)
            {
                var t = AssetDatabase.LoadAssetAtPath<TravellerDefinition>($"{DataFolder}/{s.asset}.asset");
                t.federation = F(s.federation); t.raceInfo = R(s.race); t.race = s.race; t.vehicle = codex.Vehicle(s.vehicle); t.vehicleModel = s.vehicle; t.bounty = s.bounty;
                EditorUtility.SetDirty(t);
            }
            // The two cargo visitors become Brakkan: their portraits are re-rendered in place (same texture GUIDs).
            foreach (var n in new[] { 3, 4 })
            {
                var record = AssetDatabase.LoadAssetAtPath<TravellerCase>($"{DataFolder}/Case_{n}.asset");
                string portraitPath = AssetDatabase.GetAssetPath(record.traveller.portrait);
                if (string.IsNullOrEmpty(portraitPath)) portraitPath = Art + $"/Portrait_{n}.png";
                string marker = "Tools/Checkpoint/BeforeHoldingCell/" + Path.GetFileName(portraitPath) + ".brakkan.txt";
                if (!File.Exists(marker))
                {
                    File.Copy(portraitPath, "Tools/Checkpoint/BeforeHoldingCell/" + Path.GetFileName(portraitPath), true);
                    RenderFigure(R("Brakkan").model, record.skinColor, portraitPath);
                    File.WriteAllText(marker, "Portrait re-rendered as Brakkan by CheckpointDetentionSetup; the original is beside this file.");
                }
                record.traveller.portrait = AssetDatabase.LoadAssetAtPath<Texture2D>(portraitPath); EditorUtility.SetDirty(record.traveller);
            }
            // Companions.
            var lumi = Companion("Traveller_Lumi", "Lumi Quark", "2179-11-02", F("ORC"), R("Veloran"), 50, new Color(.55f, .78f, .98f));
            var grob = Companion("Traveller_Grob", "Grob Tull", "2172-04-21", F("BIC"), R("Brakkan"), 90, new Color(.62f, .5f, .9f));
            for (int n = 0; n <= 4; n++)
            {
                var record = AssetDatabase.LoadAssetAtPath<TravellerCase>($"{DataFolder}/Case_{n}.asset");
                record.companions = n == 1 ? new[] { lumi } : n == 3 ? new[] { grob } : Array.Empty<TravellerDefinition>();
                record.companionAnswer = n == 1 ? "My partner Lumi is in the passenger seat. Wave, Lumi!" :
                    n == 3 ? "My cousin Grob rides along. He does the lifting, I do the talking." : "Just me, officer.";
                EditorUtility.SetDirty(record);
                foreach (var document in record.documents) UpdateDocument(document, record);
            }
        }
        static TravellerDefinition Companion(string asset, string name, string born, FederationDefinition federation, RaceDefinition race, int bounty, Color skinColor)
        {
            var t = Asset<TravellerDefinition>($"{DataFolder}/{asset}.asset", c =>
            {
                c.travellerId = asset.ToLowerInvariant(); c.displayName = name; c.birthDate = born; c.federation = federation; c.raceInfo = race; c.race = race.displayName;
                c.bounty = bounty; c.skinColor = skinColor;
            });
            if (t.portrait == null) { t.portrait = RenderFigure(race.model, skinColor, Art + "/Portrait_" + asset.Replace("Traveller_", "") + ".png"); EditorUtility.SetDirty(t); }
            return t;
        }
        static string Digits(string value) { var d = new string(value.Where(char.IsDigit).ToArray()); return d.Length == 0 ? "0" : d; }
        static string Initials(string name) => new string(name.Split(' ').Where(p => p.Length > 0).Select(p => p[0]).ToArray()).ToUpperInvariant();
        static void UpdateDocument(DocumentDefinition document, TravellerCase record)
        {
            var t = document.traveller != null ? document.traveller : record.traveller;
            DocumentField[] standard;
            if (document.kind != null && document.kind.showPortrait)
                standard = new[]
                {
                    new DocumentField("name", "Name", t.displayName),
                    new DocumentField("birth-date", "Date of birth", t.birthDate),
                    new DocumentField("issuer", "Issued by", t.federation != null ? t.federation.displayName : "Unknown"),
                    new DocumentField("race", "Race", t.raceInfo != null ? t.raceInfo.displayName : t.race),
                    new DocumentField("id-number", "ID number", $"{(t.federation != null ? t.federation.code : "XXX")}-{Digits(t.vehicleRegistration).PadLeft(4, '0')}-{Initials(t.displayName)}"),
                };
            else
            {
                var maker = t.vehicle != null ? t.vehicle.manufacturer : null;
                string passengers = record.CompanionCount == 0 ? "None" : string.Join(", ", record.companions.Where(c => c != null).Select(c => c.displayName));
                standard = new[]
                {
                    new DocumentField("owner", "Owner", t.displayName),
                    new DocumentField("manufacturer", "Manufacturer", maker != null ? maker.displayName : "Unknown"),
                    new DocumentField("model", "Vehicle model", t.vehicleModel),
                    new DocumentField("registration", "Registration", t.vehicleRegistration),
                    new DocumentField("chassis", "Chassis no.", $"{(maker != null ? maker.code : "XX")}-{int.Parse(Digits(t.vehicleRegistration)) * 37 % 90000 + 10000}"),
                    new DocumentField("passengers", "Passengers", passengers),
                };
            }
            var keys = new HashSet<string>(standard.Select(f => f.key)) { "birth-place" };
            document.fields = standard.Concat(document.fields.Where(f => !keys.Contains(f.key))).ToArray();
            EditorUtility.SetDirty(document);
        }

        // ------------------------------------------------------------------ physical document
        static void UpdateDocumentPrefab()
        {
            string path = Root + "/Prefabs/CheckpointDocument.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var document = root.GetComponent<DocumentItem>();
                if (document.emblemImage == null && document.portraitImage != null)
                {
                    var source = document.portraitImage.GetComponentInParent<Canvas>().gameObject;
                    float height = root.transform.InverseTransformPoint(source.transform.position).y;
                    var copy = Object.Instantiate(source, source.transform.parent); copy.name = "Document emblem canvas";
                    copy.transform.position = root.transform.TransformPoint(new Vector3(.205f, height, .305f));
                    var raw = copy.GetComponentInChildren<RawImage>(); raw.texture = null;
                    document.emblemImage = raw;
                }
                if (document.emblemImage != null && document.portraitImage != null)
                {
                    // World-space canvas roots take x/y from anchoredPosition and depth from localPosition.z: sit beside the portrait, top-right corner.
                    var source = (RectTransform)document.portraitImage.GetComponentInParent<Canvas>().transform;
                    var rect = (RectTransform)document.emblemImage.GetComponentInParent<Canvas>().transform;
                    rect.sizeDelta = new Vector2(100, 100); rect.localRotation = source.localRotation; rect.localScale = source.localScale;
                    rect.anchoredPosition = new Vector2(.205f, source.anchoredPosition.y);
                    rect.localPosition = new Vector3(rect.localPosition.x, rect.localPosition.y, .305f);
                    document.emblemImage.rectTransform.sizeDelta = new Vector2(100, 100);
                }
                if (root.transform.Find("Security marks") == null)
                {
                    var marks = Group("Security marks", root.transform);
                    float top = .029f;
                    Box(marks, "Hologram strip", new Vector3(-.13f, top, -.362f), new Vector3(.26f, .0015f, .026f), violet);
                    Box(marks, "Barcode backing", new Vector3(.155f, top - .0003f, -.362f), new Vector3(.22f, .0012f, .034f), white);
                    float x = .055f; int i = 0;
                    while (x < .255f)
                    {
                        float w = (i * 7 % 3 == 0) ? .006f : .0028f;
                        Box(marks, "Barcode bar", new Vector3(x + w * .5f, top + .0004f, -.362f), new Vector3(w, .0012f, .026f), dark);
                        x += w + .004f; i++;
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ------------------------------------------------------------------ vehicle seats
        static void BuildSeats(CheckpointSession session, GameObject[] drivers)
        {
            var view = session.vehicleView;
            var occupants = session.GetComponent<VehicleOccupants>();
            if (occupants == null) occupants = session.gameObject.AddComponent<VehicleOccupants>();
            occupants.checkpoint = session; occupants.legacyDrivers = drivers;
            foreach (var driver in drivers)
            {
                bool truck = view.truckAssembly != null && driver.transform.IsChildOf(view.truckAssembly);
                var parent = driver.transform.parent;
                var old = parent.Find("Occupant seats"); if (old != null) Object.DestroyImmediate(old.gameObject);
                var group = Group("Occupant seats", parent);
                Transform Seat(string name, Vector3 offset)
                {
                    var seat = Group(name, group);
                    seat.localPosition = driver.transform.localPosition + offset; seat.localScale = driver.transform.localScale;
                    // Truck crews face the officers' side of the lane (the original driver faced the truck bed).
                    seat.localRotation = truck ? Quaternion.Euler(0, 90, 0) : driver.transform.localRotation;
                    return seat;
                }
                var seats = truck
                    ? new[] { Seat("Seat solo driver", Vector3.zero), Seat("Seat driver", new Vector3(-.42f, 0, 0)), Seat("Seat passenger", new Vector3(.62f, 0, -.1f)) }
                    : new[] { Seat("Seat solo driver", Vector3.zero), Seat("Seat driver", new Vector3(-.45f, 0, .3f)), Seat("Seat passenger", new Vector3(.45f, -.03f, -.35f)) };
                if (truck) occupants.truckSeats = seats; else occupants.shuttleSeats = seats;
            }
            EditorUtility.SetDirty(occupants);
        }

        // ------------------------------------------------------------------ validation and previews
        public static string ValidateDetention()
        {
            var sb = new StringBuilder();
            void Check(bool ok, string what) { if (!ok) throw new InvalidOperationException("FAIL: " + what); sb.AppendLine("PASS: " + what); }
            var session = Object.FindFirstObjectByType<CheckpointSession>();
            var codex = session.codex;
            Check(codex != null && codex.federations.Length >= 4 && codex.federations.All(f => f.emblem != null), "codex federations with seals");
            Check(codex.manufacturers.Length >= 2 && codex.manufacturers.All(m => m.emblem != null), "codex manufacturers with badges");
            Check(codex.vehicles.Length >= 2 && codex.vehicles.All(v => v.picture != null && v.manufacturer != null), "codex vehicle models with pictures");
            Check(codex.races.Length >= 2 && codex.races.All(r => r.portrait != null && r.model != null), "codex races with pictures and models");
            foreach (var record in session.cases)
            {
                Check(record.traveller.federation != null && record.traveller.raceInfo != null && record.traveller.vehicle != null, record.traveller.displayName + ": federation, race and vehicle set");
                var id = record.documents.First(d => d.kind.showPortrait);
                Check(id.fields.Any(f => f.key == "issuer") && !id.fields.Any(f => f.key == "birth-place"), record.traveller.displayName + ": ID shows issuing federation instead of birth place");
                for (int i = 0; i < record.documents.Length; i++) Check(session.EmblemFor(Array.IndexOf(session.cases, record), i) != null, $"{record.traveller.displayName}: {record.documents[i].kind.displayName} has a seal/badge");
            }
            Check(session.cases.Sum(c => c.CompanionCount) >= 2 && session.cases.SelectMany(c => c.companions).All(c => c.portrait != null && c.raceInfo != null), "companions with portraits");
            var cell = session.holdingCell;
            Check(cell != null && cell.GetComponent<NetworkObject>() != null && cell.Capacity == 6 && cell.checkpoint == session, "holding cell networked with 6 spots");
            var tablet = Object.FindFirstObjectByType<HoldingCellTablet>();
            Check(tablet != null && tablet.cell == cell && tablet.GetComponent<NetworkObject>() != null && tablet.GetComponent<Collider>() != null, "cell tablet wired");
            Check(Object.FindFirstObjectByType<HoldingCellPanel>() != null, "tablet UI panel present");
            var occupants = session.GetComponent<VehicleOccupants>();
            Check(occupants != null && occupants.shuttleSeats.Length == 3 && occupants.truckSeats.Length == 3, "vehicle seats for driver and companions");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/CheckpointDocument.prefab").GetComponent<DocumentItem>();
            Check(prefab.emblemImage != null && prefab.transform.Find("Security marks") != null, "document prefab has seal slot and security marks");
            int missing = SceneManager.GetActiveScene().GetRootGameObjects().Sum(o => o.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
            Check(missing == 0, "no missing scripts");
            return sb.ToString();
        }
        public static string PreviewDetention(string target)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            var camera = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(c => !c.name.Contains("X-ray"));
            Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation; float fov = camera.fieldOfView;
            (Vector3 eye, Vector3 look) = target switch
            {
                "cell" => (new Vector3(8, 2.2f, 21.5f), new Vector3(11, 1.4f, 31)),
                "hall" => (new Vector3(-4, 3, 8), new Vector3(6, 1.5f, 26)),
                _ => (new Vector3(30, 26, 2), new Vector3(2, 0, 26)),
            };
            camera.transform.position = eye; camera.transform.LookAt(look); camera.fieldOfView = 60;
            string output = "TestResults/Expansion/" + (string.IsNullOrEmpty(target) ? "wing" : target) + ".png";
            SaveCamera(camera, output, 1600, 1000);
            camera.transform.SetPositionAndRotation(position, rotation); camera.fieldOfView = fov;
            return Path.GetFullPath(output);
        }
        /// <summary>Renders the physical document prefab for each case (ID and registration) to check the printed layout.</summary>
        public static string PreviewDocuments()
        {
            var preview = EditorSceneManager.NewPreviewScene();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/CheckpointDocument.prefab");
            var doc = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            var item = doc.GetComponent<DocumentItem>();
            var session = Object.FindFirstObjectByType<CheckpointSession>();
            var record = session.cases[1];
            var camera = new GameObject("Doc camera").AddComponent<Camera>(); SceneManager.MoveGameObjectToScene(camera.gameObject, preview); camera.scene = preview;
            camera.transform.position = new Vector3(0, 1.1f, 0); camera.transform.rotation = Quaternion.Euler(90, 0, 0); camera.orthographic = true; camera.orthographicSize = .42f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.3f, .3f, .34f);
            var light = new GameObject("Key").AddComponent<Light>(); SceneManager.MoveGameObjectToScene(light.gameObject, preview); light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(70, 20, 0);
            var outputs = new List<string>();
            for (int d = 0; d < record.documents.Length; d++)
            {
                var content = record.documents[d];
                var text = new StringBuilder(content.kind.displayName.ToUpperInvariant()).Append('\n').Append(content.traveller.displayName);
                foreach (var field in content.fields) text.Append('\n').Append(field.label.ToUpperInvariant()).Append(": ").Append(field.value);
                text.Append("\nVALID: ").Append(content.expiresOn);
                item.coverLabel.text = text.ToString(); item.coverLabel.color = content.kind.accent;
                item.portraitImage.texture = content.Portrait; item.portraitImage.enabled = content.Portrait != null;
                item.emblemImage.texture = session.EmblemFor(1, d); item.emblemImage.enabled = item.emblemImage.texture != null;
                foreach (var canvas in doc.GetComponentsInChildren<Canvas>(true)) { canvas.worldCamera = camera; }
                Canvas.ForceUpdateCanvases(); item.coverLabel.ForceMeshUpdate();
                string output = $"TestResults/Expansion/document_{d}.png"; SaveCamera(camera, output, 900, 900); outputs.Add(Path.GetFullPath(output));
            }
            EditorSceneManager.ClosePreviewScene(preview);
            return string.Join("\n", outputs);
        }
    }

    public static partial class PlanetArtRedesign
    {
        static TextMesh WingLabel(Transform parent, string text, Vector3 position, float size, Color color)
        {
            var t = Group(text.Replace('\n', ' '), parent); t.localPosition = position;
            var label = t.gameObject.AddComponent<TextMesh>(); label.text = text; label.fontSize = 64; label.characterSize = size;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center; label.color = color;
            return label;
        }
        /// <summary>Extends the shared operations hall backwards with a detention wing: deck, roof, rails, holding cell and tablet.</summary>
        internal static HoldingCell BuildDetentionWing(Transform root, CheckpointSession session)
        {
            Palette();
            var layout = root.Find(LayoutName);
            var existingCell = Object.FindFirstObjectByType<HoldingCell>(FindObjectsInactive.Include);
            if (layout.Find("Detention wing") != null && existingCell != null)
            {
                existingCell.checkpoint = session;
                var note = layout.Find("Detention wing/GALACTIC POLICE COLLECT PRISONERS 2 DAYS AFTER ARREST");
                if (note != null) { note.position = new Vector3(11, 2.5f, 33.9f); note.GetComponent<TextMesh>().characterSize = .04f; }
                // Keep the middle rail below eye height so prisoners' faces are visible through the bars.
                foreach (Transform t in existingCell.transform) if (t.name == "Bar rail" && Mathf.Abs(t.localPosition.y - 1.55f) < .01f) t.localPosition = new Vector3(0, .95f, t.localPosition.z);
                return existingCell;
            }
            var wing = Group("Detention wing", layout);
            // Deck extension behind the hall (z 24 -> 36), same height and finish as the shared deck.
            Solid(wing, "Detention wing deck", new Vector3(0, -.45f, 30), new Vector3(44, .9f, 12), deck);
            for (int x = -20; x <= 20; x += 4) for (int z = 26; z <= 34; z += 4)
            {
                Box(wing, "Deck tile", new Vector3(x, .006f, z), new Vector3(3.96f, .012f, 3.96f), deck);
                Box(wing, "Panel fastener", new Vector3(x - 1.8f, .022f, z - 1.8f), new Vector3(.09f, .025f, .09f), ivory);
            }
            // The old back rail of the hall becomes the opening into the wing.
            foreach (Transform t in layout)
                if ((t.name == "Perimeter guard" || t.name == "Rail cyan trim") && t.position.z > 20) t.gameObject.SetActive(false);
            Solid(wing, "Perimeter guard", new Vector3(0, .6f, 35.8f), new Vector3(44, 1.2f, .2f), ivory);
            Box(wing, "Rail cyan trim", new Vector3(0, 1.23f, 35.8f), new Vector3(44, .06f, .08f), cyan);
            foreach (float x in new[] { -21.8f, 21.8f })
            {
                Solid(wing, "Side guard", new Vector3(x, .6f, 28.5f), new Vector3(.2f, 1.2f, 15), ivory);
                Box(wing, "Rail cyan trim", new Vector3(x, 1.23f, 28.5f), new Vector3(.08f, .06f, 15), cyan);
            }
            for (int x = -18; x <= 18; x += 12)
            {
                Solid(wing, "Deck support pier", new Vector3(x, -4.7f, 32), new Vector3(1.5f, 8.5f, 1.5f), ivory);
                Box(wing, "Pier orange ring", new Vector3(x, -3.8f, 32), new Vector3(1.8f, .5f, 1.8f), orange);
            }
            // Roof continues from the hall.
            Box(wing, "Detention wing roof", new Vector3(0, 7.5f, 29.6f), new Vector3(41, .4f, 12.2f), ivory);
            foreach (float z in new[] { 27f, 32f })
            {
                Box(wing, "Graphite roof rib", new Vector3(0, 7.22f, z), new Vector3(40, .3f, .3f), charcoal);
                Box(wing, "Amber ceiling strip", new Vector3(0, 7.02f, z), new Vector3(35, .06f, .12f), warm);
            }
            for (int x = -18; x <= 18; x += 9)
            {
                Solid(wing, "Porcelain column", new Vector3(x, 3.7f, 35.2f), new Vector3(.45f, 7.4f, .45f), ivory);
                Box(wing, "Orange column collar", new Vector3(x, 6.8f, 35.2f), new Vector3(.8f, .65f, .8f), orange);
                Box(wing, "Orange footing", new Vector3(x, .4f, 35.2f), new Vector3(.75f, .65f, .75f), orange);
            }
            foreach (float x in new[] { -10f, 2f, 12f })
            {
                var lamp = new GameObject("Wing ceiling light").AddComponent<Light>(); lamp.transform.SetParent(wing, false); lamp.transform.localPosition = new Vector3(x, 6.4f, 29.5f);
                lamp.type = LightType.Point; lamp.range = 14; lamp.intensity = 2.2f; lamp.color = new Color(1, .82f, .62f);
            }
            var sign = Group("Wing sign", wing); sign.position = new Vector3(0, 5.6f, 24.2f);
            Box(sign, "Sign board", Vector3.zero, new Vector3(9, .9f, .14f), charcoal);
            Box(sign, "Sign trim", new Vector3(0, -.5f, 0), new Vector3(9, .08f, .16f), orange);
            WingLabel(sign, "DETENTION  &  HOLDING", new Vector3(0, 0, -.09f), .1f, new Color(1, .75f, .45f));
            WingLabel(sign, "DETENTION  &  HOLDING", new Vector3(0, 0, .09f), .1f, new Color(1, .75f, .45f)).transform.localRotation = Quaternion.Euler(0, 180, 0);
            // Evidence storage along the back rail on the west side.
            for (int i = 0; i < 6; i++) Solid(wing, "Evidence locker", new Vector3(-19 + i * 1.3f, 1.2f, 34.6f), new Vector3(1.15f, 2.4f, .9f), i % 2 == 0 ? ivory : orange);
            WingLabel(wing, "EVIDENCE", new Vector3(-15.75f, 2.7f, 34.1f), .06f, new Color(.95f, .9f, .8f));
            for (int i = 0; i < 4; i++) Crate(wing, new Vector3(-9 + i * 1.2f, .5f, 34.4f), i == 2);
            var pad = Box(wing, "Future expansion pad", new Vector3(-6, .03f, 29.5f), new Vector3(6, .03f, 4), charcoal);
            WingLabel(wing, "FUTURE EXPANSION BAY", new Vector3(-6, .06f, 29.5f), .07f, new Color(.6f, .65f, .8f)).transform.localRotation = Quaternion.Euler(90, 0, 0);

            // ------------------------------------------------ holding cell (front faces the hall, towards -Z)
            var cellRoot = Group("Holding Cell", wing); cellRoot.position = new Vector3(11, 0, 30.6f);
            Box(cellRoot, "Cell floor", new Vector3(0, .025f, 0), new Vector3(10, .04f, 7), charcoal);
            Solid(cellRoot, "Cell back wall", new Vector3(0, 1.7f, 3.6f), new Vector3(10.4f, 3.4f, .25f), ivory);
            foreach (int side in new[] { -1, 1 }) Solid(cellRoot, "Cell side wall", new Vector3(side * 5.1f, 1.7f, 0), new Vector3(.25f, 3.4f, 7.4f), ivory);
            Box(cellRoot, "Cell ceiling", new Vector3(0, 3.5f, 0), new Vector3(10.45f, .25f, 7.45f), ivory);
            Box(cellRoot, "Cell header", new Vector3(0, 3.2f, -3.62f), new Vector3(10.45f, .5f, .4f), orange);
            for (float x = -4.8f; x <= 4.81f; x += .4f) Solid(cellRoot, "Containment bar", new Vector3(x, 1.5f, -3.6f), new Vector3(.07f, 2.95f, .07f), cyan);
            foreach (float y in new[] { .12f, .95f, 2.9f }) Solid(cellRoot, "Bar rail", new Vector3(0, y, -3.6f), new Vector3(10, .1f, .12f), ivory);
            var door = Group("Cell door", cellRoot); door.localPosition = new Vector3(-3.6f, 0, -3.72f);
            foreach (float x in new[] { -.75f, .75f }) Box(door, "Door frame", new Vector3(x, 1.5f, 0), new Vector3(.12f, 3, .12f), orange);
            Box(door, "Door lock", new Vector3(.62f, 1.25f, -.05f), new Vector3(.18f, .3f, .1f), charcoal);
            Box(door, "Lock light", new Vector3(.62f, 1.33f, -.11f), new Vector3(.08f, .06f, .02f), warm);
            Solid(cellRoot, "Prisoner bench", new Vector3(0, .45f, 2.95f), new Vector3(9.4f, .16f, .9f), orange);
            for (int i = -2; i <= 2; i++) Box(cellRoot, "Bench leg", new Vector3(i * 2.2f, .2f, 2.95f), new Vector3(.2f, .4f, .7f), charcoal);
            var cellLight = new GameObject("Cell light").AddComponent<Light>(); cellLight.transform.SetParent(cellRoot, false); cellLight.transform.localPosition = new Vector3(0, 3.1f, 0);
            cellLight.type = LightType.Point; cellLight.range = 8; cellLight.intensity = 2.6f; cellLight.color = new Color(.75f, .92f, 1);
            WingLabel(cellRoot, "HOLDING CELL", new Vector3(0, 3.2f, -3.84f), .07f, Color.white);
            var board = Group("Cell status board", cellRoot); board.localPosition = new Vector3(0, 4.25f, -3.62f);
            Box(board, "Board", Vector3.zero, new Vector3(4.2f, 1, .14f), charcoal);
            Box(board, "Board trim", new Vector3(0, .52f, 0), new Vector3(4.2f, .05f, .16f), cyan);
            var status = WingLabel(board, "HOLDING CELL  0/6\nEMPTY", new Vector3(0, 0, -.09f), .055f, new Color(.55f, 1, .8f));
            var spots = new Transform[6];
            for (int i = 0; i < spots.Length; i++) { spots[i] = Group("Prisoner spot " + (i + 1), cellRoot); spots[i].localPosition = new Vector3(-3.75f + i * 1.5f, 0, 1.95f); }
            cellRoot.gameObject.AddComponent<NetworkObject>();
            var cell = cellRoot.gameObject.AddComponent<HoldingCell>();
            cell.checkpoint = session; cell.prisonerSpots = spots; cell.statusBoard = status;
            WingLabel(wing, "GALACTIC POLICE COLLECT PRISONERS 2 DAYS AFTER ARREST", new Vector3(11, 2.5f, 33.9f), .04f, new Color(.95f, .9f, .8f));

            // ------------------------------------------------ tablet at the entrance (separate network object)
            var tabletRoot = Group("Holding Cell Tablet", wing); tabletRoot.position = new Vector3(4.9f, 0, 26.3f);
            tabletRoot.gameObject.AddComponent<NetworkObject>();
            var collider = tabletRoot.gameObject.AddComponent<BoxCollider>(); collider.center = new Vector3(0, 1.1f, 0); collider.size = new Vector3(.8f, 1.3f, .5f);
            Box(tabletRoot, "Tablet post", new Vector3(0, .6f, .08f), new Vector3(.16f, 1.2f, .16f), ivory);
            Box(tabletRoot, "Tablet foot", new Vector3(0, .04f, .08f), new Vector3(.5f, .08f, .5f), orange);
            var screen = Group("Tablet head", tabletRoot); screen.localPosition = new Vector3(0, 1.35f, 0); screen.localRotation = Quaternion.Euler(22, 0, 0);
            Box(screen, "Tablet bezel", Vector3.zero, new Vector3(.66f, .46f, .06f), charcoal);
            Box(screen, "Tablet screen", new Vector3(0, 0, -.033f), new Vector3(.58f, .38f, .01f), cyan);
            WingLabel(screen, "CELL TABLET\n[E] MANAGE", new Vector3(0, 0, -.042f), .012f, Color.black);
            tabletRoot.gameObject.AddComponent<HoldingCellTablet>().cell = cell;

            // The first layout pass left a non-functional cell mock-up in the hall; the real one replaces it.
            var mockup = layout.Find("Shared operations hall/Holding cells - layout prototype");
            if (mockup != null) mockup.gameObject.SetActive(false);
            return cell;
        }
    }
}
