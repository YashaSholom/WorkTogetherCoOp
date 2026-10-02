using System;
using System.Linq;
using CoopPrototype.Checkpoint;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CoopPrototype.Editor
{
    /// <summary>
    /// Additive authoring for the game loop: work day + giant clock, next-day button, payouts (player wallet), UV watermark on
    /// IDs, shop kiosk with its items. Existing objects are kept; running it twice changes nothing.
    /// </summary>
    public static partial class CheckpointSceneSetup
    {
        [MenuItem("Coop Prototype/Checkpoint/Add Work Day, Payouts and Shop")]
        public static void AddGameLoop()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            AddCargo(true); // conveyor with the lockable second slot
            Palette();
            var player = PrefabUtility.LoadPrefabContents(Root + "/Prefabs/NetworkPlayer.prefab");
            if (player.GetComponent<PlayerWallet>() == null) { player.AddComponent<PlayerWallet>(); PrefabUtility.SaveAsPrefabAsset(player, Root + "/Prefabs/NetworkPlayer.prefab"); }
            PrefabUtility.UnloadPrefabContents(player);
            AddWatermark();
            var lamp = ToolPrefab("UvLamp", "UV lamp", "Tool", BuildLamp);
            var coffee = ToolPrefab("CoffeeCup", "Coffee", "Consumable", BuildCoffee);
            AssetDatabase.SaveAssets();
            BuildLoopScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[Checkpoint] Work day, payouts and shop authored in GameWorld.");
        }

        static void AddWatermark()
        {
            string path = Root + "/Prefabs/CheckpointDocument.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var document = root.GetComponent<DocumentItem>();
            if (document.uvWatermark == null)
            {
                var uv = Mat("UV watermark", new Color(.6f, .25f, 1), true);
                var seal = Group("UV watermark", root.transform); seal.localPosition = new Vector3(.13f, .0285f, .24f);
                Shape(seal, "Seal ring", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.12f, .0006f, .12f), uv);
                Shape(seal, "Seal core", PrimitiveType.Cylinder, new Vector3(0, .0004f, 0), new Vector3(.06f, .0006f, .06f), dark);
                Shape(seal, "Seal star", PrimitiveType.Cylinder, new Vector3(0, .0008f, 0), new Vector3(.03f, .0006f, .03f), uv);
                seal.gameObject.SetActive(false); document.uvWatermark = seal.gameObject;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            PrefabUtility.UnloadPrefabContents(root);
        }

        static PickupItem ToolPrefab(string file, string displayName, string category, Func<GameObject, PickupItem> build)
        {
            string path = Root + "/Prefabs/" + file + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing == null)
            {
                string definitionPath = Root + "/Items/" + file + ".asset";
                var definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(definitionPath);
                if (definition == null) { definition = ScriptableObject.CreateInstance<ItemDefinition>(); definition.displayName = displayName; definition.category = category; AssetDatabase.CreateAsset(definition, definitionPath); }
                var go = new GameObject(displayName); go.AddComponent<NetworkObject>(); go.AddComponent<NetworkTransform>();
                var body = go.AddComponent<Rigidbody>(); body.mass = .4f; body.interpolation = RigidbodyInterpolation.Interpolate;
                var item = build(go); item.definition = definition;
                existing = PrefabUtility.SaveAsPrefabAsset(go, path); Object.DestroyImmediate(go);
            }
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(Root + "/Prefabs/NetworkPrefabs.asset");
            if (!list.PrefabList.Any(p => p.Prefab == existing)) { list.Add(new NetworkPrefab { Prefab = existing }); EditorUtility.SetDirty(list); }
            return existing.GetComponent<PickupItem>();
        }
        static PickupItem BuildLamp(GameObject go)
        {
            var collider = go.AddComponent<BoxCollider>(); collider.size = new Vector3(.12f, .12f, .36f); collider.center = new Vector3(0, 0, .02f);
            var handle = Shape(go.transform, "Handle", PrimitiveType.Cylinder, new Vector3(0, 0, -.06f), new Vector3(.06f, .12f, .06f), dark); handle.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var head = Shape(go.transform, "Head", PrimitiveType.Cylinder, new Vector3(0, 0, .12f), new Vector3(.11f, .05f, .11f), metal); head.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var lens = Shape(go.transform, "UV lens", PrimitiveType.Cylinder, new Vector3(0, 0, .17f), new Vector3(.09f, .005f, .09f), Mat("UV lens", new Color(.55f, .2f, 1), true)); lens.transform.localRotation = Quaternion.Euler(90, 0, 0);
            Box(go.transform, "Grip band", new Vector3(0, 0, -.1f), new Vector3(.065f, .065f, .03f), violet);
            var beam = new GameObject("UV beam").AddComponent<Light>(); beam.transform.SetParent(go.transform, false); beam.transform.localPosition = new Vector3(0, 0, .18f);
            beam.type = LightType.Spot; beam.spotAngle = 45; beam.range = 4; beam.intensity = 6; beam.color = new Color(.55f, .25f, 1); beam.enabled = false;
            var lamp = go.AddComponent<UvLamp>(); lamp.beam = beam; return lamp;
        }
        static PickupItem BuildCoffee(GameObject go)
        {
            var collider = go.AddComponent<BoxCollider>(); collider.size = new Vector3(.11f, .16f, .11f);
            Shape(go.transform, "Cup", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.1f, .075f, .1f), Mat("Coffee cup", new Color(.92f, .9f, .85f)));
            Shape(go.transform, "Sleeve", PrimitiveType.Cylinder, new Vector3(0, -.005f, 0), new Vector3(.104f, .03f, .104f), Mat("Coffee sleeve", new Color(.55f, .35f, .2f)));
            Shape(go.transform, "Lid", PrimitiveType.Cylinder, new Vector3(0, .08f, 0), new Vector3(.108f, .008f, .108f), dark);
            return go.AddComponent<CoffeeCup>();
        }

        static void BuildLoopScene()
        {
            var scene = EditorSceneManager.OpenScene(MainMenuSetup.GamePath);
            Palette();
            var world = GameObject.Find("Space Checkpoint").transform;
            var checkpoint = Object.FindFirstObjectByType<CheckpointSession>();
            var shift = checkpoint.GetComponent<WorkShift>();
            if (shift == null) shift = checkpoint.gameObject.AddComponent<WorkShift>();
            shift.firstDate = checkpoint.gameDate; checkpoint.shift = shift;
            var ui = Object.FindFirstObjectByType<CheckpointPanel>().gameObject;
            var hud = ui.GetComponent<ShiftHud>(); if (hud == null) hud = ui.AddComponent<ShiftHud>(); hud.checkpoint = checkpoint; hud.shift = shift;
            if (ui.GetComponent<ShopPanel>() == null) ui.AddComponent<ShopPanel>();
            if (world.Find("Station clock") == null) BuildClock(world, shift);
            BuildCargoSpots(world, checkpoint);
            // The sorting/inspection table takes anything a visitor brings (papers and crates), resting it on the tray.
            foreach (var tray in Object.FindObjectsByType<ItemSocket>(FindObjectsSortMode.None).Where(t => t.name.StartsWith("Sorting Tray")))
            {
                tray.acceptedCategory = ""; tray.restOnSurface = true; tray.insertPrompt = "Place held item on the table";
                tray.snapPoint.position = tray.transform.position + tray.transform.up * tray.transform.lossyScale.y * .5f;
                EditorUtility.SetDirty(tray); EditorUtility.SetDirty(tray.snapPoint);
            }
            // Spawn behind the X-ray conveyor, clear of it.
            var context = Object.FindFirstObjectByType<Frontend.GameSceneContext>();
            for (int i = 0; i < context.spawnPoints.Length; i++) { context.spawnPoints[i].position = new Vector3(-6 + i * 1.2f, 1.1f, -6f); EditorUtility.SetDirty(context.spawnPoints[i]); }
            // Rebuilt each run so layout fixes apply (it has no state worth keeping).
            foreach (var name in new[] { "Start Next Day Button", "Next day button base", "START NEXT DAY" }) { var old = world.Find(name); if (old != null) Object.DestroyImmediate(old.gameObject); }
            BuildNextDayButton(world, checkpoint);
            if (Object.FindFirstObjectByType<ShopTerminal>() == null) BuildShop(world);
            // The scanner is rebuilt by AddCargo(true): re-point the shop at the current one.
            var shop = Object.FindFirstObjectByType<ShopTerminal>(); shop.scanner = Object.FindFirstObjectByType<ContrabandScanner>(); EditorUtility.SetDirty(shop);
            ApplyWorldText(world);
            EditorUtility.SetDirty(checkpoint); EditorUtility.SetDirty(shift); EditorUtility.SetDirty(hud);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Cargo spots on the parked truck's bed: world-fixed at the inspection stop (crates exist only while the truck is parked).</summary>
        static void BuildCargoSpots(Transform world, CheckpointSession checkpoint)
        {
            var old = world.Find("Truck cargo spots"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var view = checkpoint.vehicleView; var group = Group("Truck cargo spots", world);
            var spots = new ItemSocket[view.cargoPoints.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                view.CargoPose(i, out var position, out var rotation);
                var pad = Box(group, "Truck cargo spot " + (i + 1), position - Vector3.up * .24f, new Vector3(.56f, .02f, .56f), amber, true);
                pad.transform.rotation = rotation;
                pad.AddComponent<NetworkObject>();
                var spot = pad.AddComponent<ItemSocket>(); spot.acceptedCategory = "Cargo"; spot.insertPrompt = "Load crate onto the truck"; spot.lockedPrompt = "No cargo spot here";
                spot.snapPoint = Group("Crate on bed " + (i + 1), group); spot.snapPoint.SetPositionAndRotation(position, rotation);
                spot.availableVisuals = new[] { pad.GetComponent<Renderer>() }; spot.hideColliderWhenUnavailable = true;
                pad.GetComponent<Renderer>().enabled = false; pad.GetComponent<Collider>().enabled = false;
                spots[i] = spot;
            }
            checkpoint.cargoSpots = spots;
        }
        static void BuildClock(Transform world, WorkShift shift)
        {
            // A tower clock facing the crew (who look towards +Z from the spawn area).
            var root = Group("Station clock", world); root.position = new Vector3(-9.5f, 0, 11.5f);
            Box(root, "Clock pillar", new Vector3(0, 3, .35f), new Vector3(.6f, 6, .6f), metal, true);
            var face = Group("Clock face", root); face.localPosition = new Vector3(0, 6.2f, 0);
            var disc = Shape(face, "Dial", PrimitiveType.Cylinder, Vector3.zero, new Vector3(3.8f, .08f, 3.8f), dark); disc.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var rim = Ring(face, "Dial rim", new Vector3(0, 0, -.05f), 1.95f, mint, 40);
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30 * Mathf.Deg2Rad;
                var tick = Box(face, "Hour mark", new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0) * 1.6f + new Vector3(0, 0, -.1f), i % 3 == 0 ? new Vector3(.1f, .36f, .04f) : new Vector3(.06f, .2f, .04f), white);
                tick.transform.localRotation = Quaternion.Euler(0, 0, -i * 30);
            }
            var hour = Group("Hour hand", face); hour.localPosition = new Vector3(0, 0, -.14f);
            Box(hour, "Hour blade", new Vector3(0, .45f, 0), new Vector3(.14f, .95f, .04f), hull);
            var minute = Group("Minute hand", face); minute.localPosition = new Vector3(0, 0, -.18f);
            Box(minute, "Minute blade", new Vector3(0, .7f, 0), new Vector3(.08f, 1.45f, .04f), mint);
            Shape(face, "Hub", PrimitiveType.Cylinder, new Vector3(0, 0, -.2f), new Vector3(.2f, .03f, .2f), amber).transform.localRotation = Quaternion.Euler(90, 0, 0);
            Box(root, "Readout panel", new Vector3(0, 3.75f, -.05f), new Vector3(2.8f, .95f, .12f), dark);
            var digital = Label(root, "8:00 AM", new Vector3(0, 3.9f, -.13f), .09f, new Color(.4f, 1, .6f));
            var day = Label(root, "DAY 1", new Vector3(0, 3.52f, -.13f), .018f, new Color(.7f, .85f, 1));
            var lamp = Box(root, "Shift lamp", new Vector3(0, 4.25f, -.05f), new Vector3(2.8f, .08f, .14f), white);
            var clock = root.gameObject.AddComponent<WorldClock>(); clock.shift = shift; clock.hourHand = hour; clock.minuteHand = minute; clock.digital = digital; clock.dayLabel = day; clock.lamp = lamp.GetComponent<Renderer>();
        }

        static void BuildNextDayButton(Transform world, CheckpointSession checkpoint)
        {
            // Far-left corner of the inspection terminal desk, clear of the scanner trays in front of it.
            var button = Box(world, "Start Next Day Button", new Vector3(-6.6f, 1.03f, 2.5f), new Vector3(.26f, .08f, .26f), mint, true);
            button.AddComponent<NetworkObject>(); button.AddComponent<NextDayButton>().checkpoint = checkpoint;
            Box(world, "Next day button base", new Vector3(-6.6f, .985f, 2.5f), new Vector3(.36f, .03f, .36f), dark);
            Label(world, "START NEXT DAY", new Vector3(-6.6f, 1.22f, 2.36f), .014f, new Color(.6f, 1, .74f));
        }

        static void BuildShop(Transform world)
        {
            // ATM-style kiosk facing +X, towards the middle of the deck.
            var kiosk = Group("Supply Terminal (Shop)", world); kiosk.position = new Vector3(-12.6f, 0, 3.5f); kiosk.rotation = Quaternion.Euler(0, -90, 0);
            kiosk.gameObject.AddComponent<NetworkObject>();
            var collider = kiosk.gameObject.AddComponent<BoxCollider>(); collider.center = new Vector3(0, 1.1f, 0); collider.size = new Vector3(1.2f, 2.2f, .8f);
            Box(kiosk, "Cabinet", new Vector3(0, 1.1f, .05f), new Vector3(1.2f, 2.2f, .7f), hull);
            Box(kiosk, "Top sign", new Vector3(0, 2.35f, 0), new Vector3(1.3f, .32f, .8f), mint);
            Label(kiosk, "CREW SUPPLY  /  ATM", new Vector3(0, 2.35f, -.41f), .014f, Color.black);
            Box(kiosk, "Screen bezel", new Vector3(0, 1.55f, -.31f), new Vector3(.9f, .6f, .08f), dark);
            Box(kiosk, "Screen", new Vector3(0, 1.57f, -.355f), new Vector3(.8f, .48f, .01f), Mat("ATM screen", new Color(.1f, .45f, .35f), true));
            Label(kiosk, "UPGRADES & TOOLS\n[E] OPEN", new Vector3(0, 1.57f, -.365f), .011f, new Color(.8f, 1, .9f));
            var keypad = Box(kiosk, "Keypad shelf", new Vector3(0, 1.1f, -.45f), new Vector3(.8f, .06f, .3f), metal); keypad.transform.localRotation = Quaternion.Euler(-20, 0, 0);
            for (int r = 0; r < 3; r++) for (int c = 0; c < 3; c++) Box(kiosk, "Key", new Vector3(-.2f + c * .12f, 1.14f + r * .035f, -.5f + r * .09f), new Vector3(.09f, .03f, .07f), r == 0 && c == 2 ? amber : white);
            Box(kiosk, "Card slot", new Vector3(.3f, 1.2f, -.36f), new Vector3(.16f, .02f, .03f), dark);
            // Dispenser tray under the screen: purchased items land here.
            Box(kiosk, "Dispenser recess", new Vector3(0, .62f, -.3f), new Vector3(.9f, .42f, .12f), dark);
            Box(kiosk, "Dispenser tray", new Vector3(0, .5f, -.55f), new Vector3(.9f, .05f, .5f), metal, true);
            Box(kiosk, "Tray lip", new Vector3(0, .56f, -.79f), new Vector3(.9f, .08f, .03f), metal, true);
            var dispenser = Group("Dispenser point", kiosk); dispenser.localPosition = new Vector3(0, .62f, -.55f);
            var shop = kiosk.gameObject.AddComponent<ShopTerminal>(); shop.dispenser = dispenser; shop.scanner = Object.FindFirstObjectByType<ContrabandScanner>();
            shop.offers = new[]
            {
                new ShopOffer { title = "X-ray belt: second slot", price = 120, kind = ShopOfferKind.BeltSlotUpgrade, description = "Adds a second load slot to the cargo X-ray conveyor, so two crates are scanned per run. Shared by the crew." },
                new ShopOffer { title = "UV lamp", price = 80, kind = ShopOfferKind.Item, prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/UvLamp.prefab").GetComponent<PickupItem>(), description = "Genuine IDs show a violet UV watermark under the lamp; forged ones don't. Hold it near a document, or keep it in a slot while inspecting an ID." },
                new ShopOffer { title = "Coffee", price = 15, kind = ShopOfferKind.Item, prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/CoffeeCup.prefab").GetComponent<PickupItem>(), description = "Left click to drink. You walk faster for the rest of the day." },
            };
        }
    }
}
