using System;
using System.IO;
using System.Linq;
using CoopPrototype.Checkpoint;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CoopPrototype.Editor
{
    /// <summary>
    /// Additive authoring for the cargo truck visitor, its packages and the contraband scanner station.
    /// Existing assets and scene objects are preserved; running it twice changes nothing.
    /// </summary>
    public static partial class CheckpointSceneSetup
    {
        const string CargoTruckName = "Cargo truck", ScannerName = "Contraband Scanner";

        [MenuItem("Coop Prototype/Checkpoint/Add Cargo Truck and Contraband Scanner")]
        public static void AddCargo() => AddCargo(false);
        /// <summary>Rebuild = delete the scanner station in GameWorld and author it again (use after changing its layout here).</summary>
        public static void AddCargo(bool rebuildScanner)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            EditorSceneManager.SaveOpenScenes();
            Palette(); CheckpointDataSetup.Create();
            var alien = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Veloran.prefab");
            int xray = EnsureXrayLayer();
            var package = CargoPackagePrefab();
            AddXrayContents(xray);
            HideXrayFromPlayerCamera(xray);
            var cases = CargoCases(alien);
            BuildCargoScene(cases, package, xray, rebuildScanner);
            AssetDatabase.SaveAssets();
            Debug.Log("[Checkpoint] Cargo truck, packages and contraband scanner authored in GameWorld.");
        }

        static Renderer Crate(Transform parent)
        {
            var body = Box(parent, "Package body", Vector3.zero, Vector3.one * .5f, Mat("Cargo crate", new Color(.62f, .68f, .74f)));
            Box(parent, "Strap horizontal", Vector3.zero, new Vector3(.52f, .09f, .52f), dark);
            Box(parent, "Strap vertical", Vector3.zero, new Vector3(.09f, .52f, .52f), dark);
            Box(parent, "Shipping label", new Vector3(0, .255f, 0), new Vector3(.2f, .01f, .14f), white);
            return body.GetComponent<Renderer>();
        }

        static CargoPackage CargoPackagePrefab()
        {
            string path = Root + "/Prefabs/CargoPackage.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing == null)
            {
                var definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Root + "/Items/CargoPackage.asset");
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<ItemDefinition>(); definition.displayName = "Cargo package"; definition.category = "Cargo";
                    AssetDatabase.CreateAsset(definition, Root + "/Items/CargoPackage.asset");
                }
                var go = new GameObject("Cargo Package"); go.AddComponent<NetworkObject>(); go.AddComponent<NetworkTransform>();
                go.AddComponent<BoxCollider>().size = Vector3.one * .5f;
                var body = go.AddComponent<Rigidbody>(); body.mass = 2; body.interpolation = RigidbodyInterpolation.Interpolate;
                var item = go.AddComponent<CargoPackage>(); item.definition = definition;
                item.tinted = new[] { Crate(go.transform) };
                existing = PrefabUtility.SaveAsPrefabAsset(go, path); Object.DestroyImmediate(go);
            }
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(Root + "/Prefabs/NetworkPrefabs.asset");
            if (!list.PrefabList.Any(p => p.Prefab == existing)) { list.Add(new NetworkPrefab { Prefab = existing }); EditorUtility.SetDirty(list); }
            return existing.GetComponent<CargoPackage>();
        }

        static TravellerCase[] CargoCases(GameObject alien)
        {
            var specs = new[]
            {
                new { id = 3, name = "Brakka Tull", born = "2169-01-30", place = "Ferro Dock", race = "Brakkan", reg = "HH-5510", cargo = "4 sealed crates - spare parts", skin = new Color(.35f, .7f, .95f),
                      greeting = "Morning, officer. Hauling spare parts to the orbital yards. Same route every week.",
                      purpose = "Delivery run. Four crates, all sealed at the depot. Ask anyone at Ferro Dock.",
                      vehicle = "A Haul Hopper, registration HH-5510. Big, slow, and very reliable.",
                      packages = new[] { new CargoPackageDefinition { label = "Parts crate", colour = new Color(.62f, .68f, .74f) },
                                         new CargoPackageDefinition { label = "Parts crate", colour = new Color(.7f, .62f, .5f) },
                                         new CargoPackageDefinition { label = "Parts crate", colour = new Color(.55f, .7f, .6f), contraband = true },
                                         new CargoPackageDefinition { label = "Parts crate", colour = new Color(.68f, .6f, .72f) } } },
                new { id = 4, name = "Ossa Rend", born = "2183-08-17", place = "Mirror Harbour", race = "Ossari", reg = "HH-2034", cargo = "3 sealed crates - moon-fruit preserves", skin = new Color(.95f, .45f, .62f),
                      greeting = "Hello there! Preserves for the market. The jars are only slightly glowing.",
                      purpose = "Selling preserves at the orbital market. Three crates. Free sample?",
                      vehicle = "A Haul Hopper, registration HH-2034. It smells like fruit, sorry.",
                      packages = new[] { new CargoPackageDefinition { label = "Preserves crate", colour = new Color(.9f, .62f, .4f) },
                                         new CargoPackageDefinition { label = "Preserves crate", colour = new Color(.85f, .55f, .62f) },
                                         new CargoPackageDefinition { label = "Preserves crate", colour = new Color(.9f, .75f, .4f) } } },
            };
            var result = new TravellerCase[specs.Length];
            var identity = AssetDatabase.LoadAssetAtPath<DocumentKind>(Root + "/CheckpointData/Kind_Identity.asset");
            var registration = AssetDatabase.LoadAssetAtPath<DocumentKind>(Root + "/CheckpointData/Kind_Vehicle.asset");
            for (int n = 0; n < specs.Length; n++)
            {
                var s = specs[n];
                string casePath = Root + "/CheckpointData/Case_" + s.id + ".asset";
                result[n] = AssetDatabase.LoadAssetAtPath<TravellerCase>(casePath);
                if (result[n] != null) continue;
                var traveller = ScriptableObject.CreateInstance<TravellerDefinition>();
                traveller.travellerId = "traveller-" + s.id; traveller.displayName = s.name; traveller.birthDate = s.born; traveller.birthPlace = s.place; traveller.race = s.race;
                traveller.vehicleModel = "Haul Hopper"; traveller.vehicleRegistration = s.reg;
                AssetDatabase.CreateAsset(traveller, Root + "/CheckpointData/Traveller_" + s.id + ".asset");
                traveller.portrait = Portrait(alien, s.skin, "Portrait_" + s.id); EditorUtility.SetDirty(traveller);
                var documents = new DocumentDefinition[2];
                for (int d = 0; d < 2; d++)
                {
                    var doc = ScriptableObject.CreateInstance<DocumentDefinition>();
                    doc.documentId = $"visitor-{s.id}-{d}"; doc.traveller = traveller; doc.expiresOn = "2220-12-31"; doc.kind = d == 0 ? identity : registration;
                    doc.fields = d == 0
                        ? new[] { new DocumentField("name", "Name", traveller.displayName), new DocumentField("birth-date", "Date of birth", traveller.birthDate), new DocumentField("birth-place", "Place of birth", traveller.birthPlace), new DocumentField("race", "Race", traveller.race) }
                        : new[] { new DocumentField("owner", "Owner", traveller.displayName), new DocumentField("model", "Vehicle model", traveller.vehicleModel), new DocumentField("registration", "Registration", traveller.vehicleRegistration), new DocumentField("cargo", "Declared cargo", s.cargo) };
                    AssetDatabase.CreateAsset(doc, Root + $"/CheckpointData/Document_{s.id}_{d}.asset"); documents[d] = doc;
                }
                var record = ScriptableObject.CreateInstance<TravellerCase>();
                record.traveller = traveller; record.documents = documents; record.skinColor = s.skin; record.vehicle = VehicleKind.Truck; record.packages = s.packages;
                record.greeting = s.greeting; record.purpose = s.purpose; record.vehicleAnswer = s.vehicle;
                AssetDatabase.CreateAsset(record, casePath); result[n] = record;
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        static void BuildCargoScene(TravellerCase[] newCases, CargoPackage packagePrefab, int xray, bool rebuildScanner)
        {
            var scene = EditorSceneManager.OpenScene(MainMenuSetup.GamePath);
            // Scene switching can unload asset objects held only in managed locals: reload everything from disk.
            Palette();
            newCases = new[] { 3, 4 }.Select(i => AssetDatabase.LoadAssetAtPath<TravellerCase>(Root + "/CheckpointData/Case_" + i + ".asset")).ToArray();
            packagePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/CargoPackage.prefab").GetComponent<CargoPackage>();
            var alien = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Veloran.prefab");
            var checkpoint = Object.FindFirstObjectByType<CheckpointSession>();
            var view = Object.FindFirstObjectByType<CheckpointVehicleView>();
            var world = GameObject.Find("Space Checkpoint").transform;
            if (view.truckAssembly == null) BuildTruck(view, alien);
            // The first flat scanner is replaced by the conveyor X-ray machine.
            var flat = world.Find(ScannerName);
            if (flat != null && (rebuildScanner || flat.Find("X-ray tunnel") == null)) { Object.DestroyImmediate(flat.gameObject); flat = null; }
            if (flat == null) BuildScanner(world, xray);
            var context = Object.FindFirstObjectByType<Frontend.GameSceneContext>();
            if (context != null && context.loadingCamera != null) context.loadingCamera.cullingMask &= ~(1 << xray);
            checkpoint.packagePrefab = packagePrefab; checkpoint.vehicleView = view;
            checkpoint.cases = checkpoint.cases.Concat(newCases.Where(c => !checkpoint.cases.Contains(c))).ToArray();
            ApplyWorldText(world);
            EditorUtility.SetDirty(checkpoint); EditorUtility.SetDirty(view);
            EditorSceneManager.SaveScene(scene);
        }

        // ------------------------------------------------------------------ X-ray
        const string XrayLayerName = "XRay";
        static int EnsureXrayLayer()
        {
            var manager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = manager.FindProperty("layers");
            for (int i = 8; i < 32; i++) if (layers.GetArrayElementAtIndex(i).stringValue == XrayLayerName) return i;
            // Prefer 30; never touch 8 (first-person view model) or 31 (third-person inspection).
            foreach (int i in new[] { 30, 29, 28, 27, 26, 25 })
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                { layers.GetArrayElementAtIndex(i).stringValue = XrayLayerName; manager.ApplyModifiedPropertiesWithoutUndo(); return i; }
            throw new InvalidOperationException("No free layer for the X-ray view.");
        }
        static void SetLayerRecursive(GameObject go, int layer) { go.layer = layer; foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer); }
        /// <summary>Additive unlit material: overlapping parts read brighter, like density on an X-ray.</summary>
        static Material XrayMat(string name, Color colour)
        {
            string path = Art + "/Xray " + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 2);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One); material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One); material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0); material.SetFloat("_Cull", 0);
            material.SetOverrideTag("RenderType", "Transparent"); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetColor("_BaseColor", colour);
            AssetDatabase.CreateAsset(material, path); return material;
        }
        static void AddXrayContents(int xray)
        {
            string path = Root + "/Prefabs/CargoPackage.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var package = root.GetComponent<CargoPackage>();
            if (package.xrayContents == null)
            {
                var shell = XrayMat("shell", new Color(.10f, .16f, .26f)); var metalParts = XrayMat("metal", new Color(.18f, .38f, .7f));
                var organic = XrayMat("organic", new Color(.55f, .3f, .08f)); var danger = XrayMat("contraband", new Color(.95f, .16f, .1f));
                var contents = Group("X-ray contents", root.transform);
                // Crate walls: four slabs read as a bright outline from above.
                foreach (var (p, sz) in new[] { (new Vector3(0, 0, .24f), new Vector3(.5f, .5f, .02f)), (new Vector3(0, 0, -.24f), new Vector3(.5f, .5f, .02f)), (new Vector3(.24f, 0, 0), new Vector3(.02f, .5f, .5f)), (new Vector3(-.24f, 0, 0), new Vector3(.02f, .5f, .5f)) })
                    Box(contents, "Crate wall", p, sz, shell);
                Box(contents, "Crate fill", Vector3.zero, new Vector3(.46f, .46f, .46f), shell);
                // Ordinary cargo: machine parts, a coil and some packing.
                Shape(contents, "Pipe section", PrimitiveType.Cylinder, new Vector3(-.1f, 0, .08f), new Vector3(.08f, .16f, .08f), metalParts).transform.localRotation = Quaternion.Euler(90, 30, 0);
                Shape(contents, "Coil", PrimitiveType.Cylinder, new Vector3(.12f, -.05f, -.1f), new Vector3(.14f, .04f, .14f), metalParts);
                Box(contents, "Gearbox", new Vector3(.1f, .05f, .12f), new Vector3(.12f, .1f, .09f), metalParts);
                for (int i = 0; i < 4; i++) Shape(contents, "Bolt", PrimitiveType.Cylinder, new Vector3(-.15f + i * .05f, -.1f, -.14f), new Vector3(.025f, .04f, .025f), metalParts).transform.localRotation = Quaternion.Euler(90, 0, 0);
                Shape(contents, "Packing", PrimitiveType.Sphere, new Vector3(-.12f, .08f, -.05f), new Vector3(.12f, .1f, .16f), organic);
                Shape(contents, "Packing", PrimitiveType.Sphere, new Vector3(.02f, -.1f, .15f), new Vector3(.14f, .08f, .1f), organic);
                // Contraband: an unmistakable blaster silhouette (shown only for contraband packages).
                var blaster = Group("Contraband blaster", contents); blaster.localPosition = new Vector3(0, .06f, -.02f); blaster.localRotation = Quaternion.Euler(0, -25, 0);
                Box(blaster, "Body", new Vector3(0, 0, 0), new Vector3(.2f, .06f, .07f), danger);
                Shape(blaster, "Barrel", PrimitiveType.Cylinder, new Vector3(.16f, 0, 0), new Vector3(.035f, .07f, .035f), danger).transform.localRotation = Quaternion.Euler(0, 0, 90);
                Box(blaster, "Grip", new Vector3(-.06f, 0, -.08f), new Vector3(.05f, .05f, .12f), danger);
                Shape(blaster, "Power cell", PrimitiveType.Sphere, new Vector3(-.02f, 0, .06f), new Vector3(.08f, .08f, .08f), danger);
                SetLayerRecursive(contents.gameObject, xray);
                package.xrayContents = contents; package.xrayContraband = blaster.gameObject;
                blaster.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            PrefabUtility.UnloadPrefabContents(root);
        }
        static void HideXrayFromPlayerCamera(int xray)
        {
            string path = Root + "/Prefabs/NetworkPlayer.prefab";
            var player = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            foreach (var camera in player.GetComponentsInChildren<Camera>(true))
                if ((camera.cullingMask & (1 << xray)) != 0) { camera.cullingMask &= ~(1 << xray); changed = true; }
            if (changed) PrefabUtility.SaveAsPrefabAsset(player, path);
            PrefabUtility.UnloadPrefabContents(player);
        }

        static void ApplyWorldText(Transform root)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "/Depth tested text.mat");
            if (material == null) return;
            foreach (var text in root.GetComponentsInChildren<TextMesh>(true))
            {
                var renderer = text.GetComponent<Renderer>();
                if (renderer.sharedMaterial != material) renderer.sharedMaterial = material;
            }
        }

        static void BuildTruck(CheckpointVehicleView view, GameObject alien)
        {
            var vehicle = view.vehicle;
            var truck = Group(CargoTruckName, vehicle); truck.localPosition = Vector3.zero;
            // Frame: bed at the back (-Z), open cab at the front (+Z, direction of travel).
            Box(truck, "Chassis", new Vector3(0, .9f, -.2f), new Vector3(2.5f, .3f, 5.6f), metal);
            Box(truck, "Bed floor", new Vector3(0, 1.08f, -1.3f), new Vector3(2.4f, .06f, 3.3f), hull);
            foreach (float x in new[] { -1.2f, 1.2f }) Box(truck, "Bed rail", new Vector3(x, 1.2f, -1.3f), new Vector3(.1f, .25f, 3.4f), hull);
            Box(truck, "Bed rail back", new Vector3(0, 1.2f, -2.95f), new Vector3(2.5f, .25f, .1f), hull);
            Box(truck, "Bed rail front", new Vector3(0, 1.2f, .35f), new Vector3(2.5f, .25f, .1f), hull);
            Box(truck, "Cab body", new Vector3(0, 1.5f, 1.6f), new Vector3(2.5f, .9f, 2.1f), hull);
            Box(truck, "Cab dash", new Vector3(0, 2.05f, 2.35f), new Vector3(2.3f, .2f, .3f), dark);
            foreach (float x in new[] { -1.15f, 1.15f }) Box(truck, "Windshield post", new Vector3(x, 2.45f, 2.45f), new Vector3(.1f, 1f, .1f), dark);
            Box(truck, "Windshield bar", new Vector3(0, 2.95f, 2.45f), new Vector3(2.4f, .1f, .1f), dark);
            Box(truck, "Front lights", new Vector3(0, 1.2f, 2.68f), new Vector3(2.3f, .18f, .1f), amber);
            foreach (float z in new[] { -2f, 1.6f }) foreach (int side in new[] { -1, 1 })
            {
                Shape(truck, "Ion pod", PrimitiveType.Capsule, new Vector3(side * 1.4f, .75f, z), new Vector3(.5f, .45f, .5f), metal);
                Shape(truck, "Thruster glow", PrimitiveType.Sphere, new Vector3(side * 1.4f, .35f, z), new Vector3(.42f, .16f, .42f), violet);
            }
            var driver = (GameObject)PrefabUtility.InstantiatePrefab(alien); driver.transform.SetParent(truck, false);
            driver.transform.localPosition = new Vector3(0, 1f, 1.6f); driver.transform.localRotation = Quaternion.Euler(0, 25, 0);
            Box(truck, "Rear plate", new Vector3(0, .95f, -3.03f), new Vector3(1.4f, .42f, .05f), white);
            var registration = Label(truck, "HH-0000", new Vector3(0, .99f, -3.07f), .075f, Color.black);
            Label(truck, "HAUL HOPPER", new Vector3(0, .84f, -3.07f), .032f, Color.black);
            // Cargo slots: 2 x 2 on the bed. Each has a cosmetic crate that stands in while the truck is moving.
            var points = new Transform[4]; var dummies = new GameObject[4];
            var offsets = new[] { new Vector3(-.6f, 1.36f, -.8f), new Vector3(.6f, 1.36f, -.8f), new Vector3(-.6f, 1.36f, -2.1f), new Vector3(.6f, 1.36f, -2.1f) };
            var yaws = new[] { 4f, -6f, -3f, 5f };
            for (int i = 0; i < 4; i++)
            {
                points[i] = Group("Cargo point " + (i + 1), truck); points[i].localPosition = offsets[i]; points[i].localRotation = Quaternion.Euler(0, yaws[i], 0);
                var dummy = Group("Cargo crate (moving)", points[i]); Crate(dummy); dummy.gameObject.SetActive(false); dummies[i] = dummy.gameObject;
            }
            var bed = Group("Truck bed collision", vehicle);
            void Solid(string n, Vector3 c, Vector3 size) { var part = Group(n, bed); var box = part.gameObject.AddComponent<BoxCollider>(); box.center = c; box.size = size; }
            Solid("Bed floor", new Vector3(0, 1f, -1.3f), new Vector3(2.5f, .22f, 3.4f));
            foreach (float x in new[] { -1.2f, 1.2f }) Solid("Bed lip", new Vector3(x, 1.2f, -1.3f), new Vector3(.1f, .25f, 3.4f));
            Solid("Bed lip back", new Vector3(0, 1.2f, -2.95f), new Vector3(2.5f, .25f, .1f));
            Solid("Bed lip front", new Vector3(0, 1.2f, .35f), new Vector3(2.5f, .25f, .1f));
            bed.gameObject.SetActive(false);
            view.truckAssembly = truck; view.truckRegistration = registration; view.cargoPoints = points; view.cargoDummies = dummies; view.bedCollision = bed.gameObject;
            view.alienSkin = view.alienSkin.Concat(driver.GetComponentsInChildren<Renderer>().Where(r => r.name.StartsWith("Skin"))).ToArray();
            truck.gameObject.SetActive(false);
        }

        static void BuildScanner(Transform world, int xray)
        {
            // Built along local X: load slot at +X, X-ray tunnel at 0, exit at -X; the operator stands at local -Z.
            // Rotated so the belt runs from the truck side (north) towards the players' spawn (south), operator side facing the lane.
            var station = Group(ScannerName, world); station.position = new Vector3(-1.2f, 0, -3.3f); station.rotation = Quaternion.Euler(0, -90, 0);
            const float top = .95f;
            // Belt from x = 1.75 (load end) to x = -2.45 (far end), then a catch bin the crates drop into.
            const float beltStart = 1.75f, beltEnd = -2.45f, beltMid = (beltStart + beltEnd) / 2, beltLength = beltStart - beltEnd;
            foreach (float x in new[] { beltStart - .2f, 0.6f, -1.3f, beltEnd + .2f }) foreach (float z in new[] { -.35f, .35f }) Box(station, "Leg", new Vector3(x, top * .5f, z), new Vector3(.08f, top, .08f), metal);
            Box(station, "Belt frame", new Vector3(beltMid, top - .06f, 0), new Vector3(beltLength, .12f, .86f), hull, true);
            Box(station, "Belt", new Vector3(beltMid, top + .005f, 0), new Vector3(beltLength - .1f, .01f, .72f), dark);
            foreach (float z in new[] { -.41f, .41f }) Box(station, "Belt guide", new Vector3(beltMid, top + .06f, z), new Vector3(beltLength, .1f, .04f), metal, true);
            var rollers = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var roller = Shape(station, "Roller", PrimitiveType.Cylinder, new Vector3(i == 0 ? beltStart - .03f : beltEnd + .03f, top - .04f, 0), new Vector3(.16f, .38f, .16f), metal);
                roller.transform.localRotation = Quaternion.Euler(90, 0, 0); rollers[i] = roller.transform;
            }
            var slats = new Transform[18];
            for (int i = 0; i < slats.Length; i++) slats[i] = Box(station, "Belt slat", new Vector3(beltStart - .05f - i * .23f, top + .013f, 0), new Vector3(.03f, .006f, .7f), metal).transform;
            // Catch bin: crates roll off the belt end and drop a little into it.
            var bin = Group("Catch bin", station);
            const float binFloor = top - .27f, binStart = beltEnd, binEnd = beltEnd - 1.6f;
            // Slightly tilted, slippery floor: each crate slides to the far wall and leaves room for the next one.
            var floor = Box(bin, "Bin floor", new Vector3((binStart + binEnd) / 2, binFloor - .02f, 0), new Vector3(binStart - binEnd, .04f, .9f), metal, true);
            floor.transform.localRotation = Quaternion.Euler(0, 0, 6);
            string slickPath = Art + "/Slick bin.physicMaterial";
            var slick = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(slickPath);
            if (slick == null) { slick = new PhysicsMaterial("Slick bin") { dynamicFriction = .05f, staticFriction = .05f, frictionCombine = PhysicsMaterialCombine.Minimum, bounciness = 0 }; AssetDatabase.CreateAsset(slick, slickPath); }
            floor.GetComponent<Collider>().sharedMaterial = slick;
            foreach (float z in new[] { -.47f, .47f }) Box(bin, "Bin side", new Vector3((binStart + binEnd) / 2, binFloor + .12f, z), new Vector3(binStart - binEnd, .28f, .04f), hull, true);
            Box(bin, "Bin end", new Vector3(binEnd - .02f, binFloor + .12f, 0), new Vector3(.04f, .28f, .98f), hull, true);
            Box(bin, "Bin padding", new Vector3(binEnd + .02f, binFloor + .12f, 0), new Vector3(.02f, .24f, .9f), amber);
            foreach (float x in new[] { binStart - .15f, binEnd + .15f }) foreach (float z in new[] { -.4f, .4f }) Box(bin, "Bin leg", new Vector3(x, (binFloor - .04f) * .5f, z), new Vector3(.06f, binFloor - .04f, .06f), metal);
            Label(bin, "OUT", new Vector3((binStart + binEnd) / 2, binFloor + .12f, -.495f), .02f, new Color(.6f, 1, .74f));
            // Load slots at the start of the belt. Slot 1 works from the start; slot 2 is unlocked by the shop upgrade.
            var sockets = new ItemSocket[2];
            for (int i = 0; i < 2; i++)
            {
                // Flush with the belt, so released crates ride straight off it.
                var loadPosition = new Vector3(i == 0 ? 1.4f : .8f, top + .004f, 0);
                var pad = Box(station, "Load slot " + (i + 1), loadPosition, new Vector3(.56f, .008f, .7f), mint, true);
                pad.AddComponent<NetworkObject>();
                var slot = pad.AddComponent<ItemSocket>(); slot.acceptedCategory = "Cargo";
                slot.snapPoint = Group("Package on belt " + (i + 1), station); slot.snapPoint.localPosition = loadPosition + Vector3.up * .26f;
                slot.availableVisuals = new[] { pad.GetComponent<Renderer>() };
                var cover = Group("Locked cover", pad.transform); cover.localPosition = Vector3.zero; cover.localRotation = Quaternion.identity; cover.localScale = new Vector3(1 / .56f, 1 / .008f, 1 / .7f);
                Box(cover, "Cover plate", new Vector3(0, .02f, 0), new Vector3(.56f, .012f, .7f), dark);
                Label(cover, "LOCKED", new Vector3(0, .03f, 0), .012f, new Color(1, .6f, .3f)).transform.localRotation = Quaternion.Euler(90, 0, 0);
                cover.gameObject.SetActive(false); slot.lockedIndicator = cover.gameObject;
                sockets[i] = slot;
            }
            var socket = sockets[0];
            // X-ray tunnel over the middle of the belt, with lead curtains at both openings.
            var tunnel = Group("X-ray tunnel", station);
            foreach (float z in new[] { -.47f, .47f }) Box(tunnel, "Tunnel wall", new Vector3(0, top + .42f, z), new Vector3(1.1f, .84f, .08f), hull, true);
            Box(tunnel, "Tunnel roof", new Vector3(0, top + .88f, 0), new Vector3(1.1f, .1f, 1.02f), hull, true);
            Box(tunnel, "Roof housing", new Vector3(0, top + 1.0f, 0), new Vector3(.8f, .14f, .7f), metal);
            foreach (float x in new[] { -.56f, .56f })
            {
                Box(tunnel, "Hazard trim", new Vector3(x, top + .86f, 0), new Vector3(.03f, .06f, 1.02f), amber);
                for (int i = 0; i < 6; i++) Box(tunnel, "Lead curtain", new Vector3(x, top + .72f, -.33f + i * .132f), new Vector3(.015f, .22f, .12f), dark);
            }
            Label(tunnel, "X-RAY", new Vector3(0, top + .6f, -.52f), .03f, new Color(1, .75f, .3f));
            var beam = Box(tunnel, "Scan beam", new Vector3(0, top + .8f, 0), new Vector3(.025f, .015f, .86f), mint);
            beam.SetActive(false);
            // X-ray camera in the roof, looking down at the package; it only draws the X-ray layer.
            var cameraObject = Group("X-ray camera", tunnel); cameraObject.localPosition = new Vector3(0, top + .8f, 0);
            cameraObject.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward); // image right = load end, as the operator sees the belt
            var camera = cameraObject.gameObject.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = .45f; camera.nearClipPlane = .02f; camera.farClipPlane = 1f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.02f, .05f, .1f);
            camera.cullingMask = 1 << xray; camera.allowHDR = false; camera.allowMSAA = false; camera.depth = -10;
            // Belt edges and scale marks drawn only for the X-ray camera, so the screen always shows the tunnel.
            var reference = Group("X-ray belt reference", tunnel);
            var guide = XrayMat("guide", new Color(.07f, .16f, .2f));
            foreach (float z in new[] { -.36f, .36f }) Box(reference, "Belt edge", new Vector3(0, top + .01f, z), new Vector3(1.1f, .01f, .015f), guide);
            for (int i = -4; i <= 4; i++) Box(reference, "Scale mark", new Vector3(i * .12f, top + .01f, -.33f), new Vector3(.008f, .01f, i % 2 == 0 ? .05f : .025f), guide);
            SetLayerRecursive(reference.gameObject, xray);
            // Monitor beside the belt at eye height, past the tunnel (so it never blocks the load slots), facing the operator.
            var monitorCentre = new Vector3(-1.15f, top + .82f, -.74f);
            Box(station, "Monitor pole", new Vector3(-1.15f, top * .5f + .2f, -.62f), new Vector3(.07f, top + .4f, .07f), metal, true);
            Box(station, "Monitor frame", monitorCentre + new Vector3(0, 0, .04f), new Vector3(1.0f, .8f, .06f), dark, true);
            var screen = GameObject.CreatePrimitive(PrimitiveType.Quad); screen.name = "X-ray screen"; screen.transform.SetParent(station, false);
            Object.DestroyImmediate(screen.GetComponent<Collider>());
            screen.transform.localPosition = monitorCentre + new Vector3(0, .05f, -.0f); screen.transform.localScale = new Vector3(.84f, .6f, 1);
            screen.GetComponent<Renderer>().sharedMaterial = ScreenMaterial();
            var display = Label(station, "PLACE CRATE IN SLOT", monitorCentre + new Vector3(0, -.32f, -.005f), .011f, new Color(.6f, .8f, 1));
            var light = Box(station, "Status light", monitorCentre + new Vector3(0, .42f, .04f), new Vector3(1.0f, .04f, .07f), white);
            Label(station, "CARGO X-RAY  /  CONTRABAND CONTROL", new Vector3(-.6f, top - .08f, -.44f), .012f, new Color(.6f, 1, .74f));
            // Scan button on a small console at the load end, on the operator side.
            Box(station, "Button console", new Vector3(1.5f, top - .1f, -.62f), new Vector3(.36f, .2f, .3f), metal, true);
            var buttonObject = Box(station, "Scan button", new Vector3(1.5f, top + .03f, -.62f), new Vector3(.2f, .06f, .2f), amber, true);
            buttonObject.AddComponent<NetworkObject>();
            var button = buttonObject.AddComponent<ScanButton>(); button.cap = buttonObject.transform;
            Label(station, "SCAN", new Vector3(1.5f, top - .1f, -.78f), .014f, Color.white);
            // Station logic on its own networked object (nested NetworkObjects are avoided).
            var controller = Group("Scanner controller", station); controller.gameObject.AddComponent<NetworkObject>();
            var scanner = controller.gameObject.AddComponent<ContrabandScanner>();
            scanner.slots = sockets; scanner.initialSlots = 1; scanner.frame = station; scanner.beltAxis = Vector3.left;
            scanner.scanX = 0; scanner.beltEndX = beltEnd; // The drive zone reaches a little past the belt end so crates are pushed clear and drop into the bin.
            scanner.beltZoneCentre = new Vector3(beltMid - .2f, top + .3f, 0); scanner.beltZoneSize = new Vector3(beltLength + .4f, .6f, .72f);
            scanner.checkpoint = Object.FindFirstObjectByType<CheckpointSession>();
            scanner.slats = slats; scanner.slatStart = beltStart - .05f; scanner.slatEnd = beltEnd + .05f; scanner.rollers = rollers;
            scanner.xrayCamera = camera; scanner.screen = screen.GetComponent<Renderer>(); scanner.beam = beam.transform;
            scanner.display = display; scanner.statusLight = light.GetComponent<Renderer>();
            button.station = scanner;
            foreach (var slot in sockets) UnityEventTools.AddPersistentListener(slot.onOccupancyChanged, scanner.OnSlotsChanged);
        }

        static Material ScreenMaterial()
        {
            string path = Art + "/Xray screen.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); material.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(material, path); return material;
        }
    }
}
