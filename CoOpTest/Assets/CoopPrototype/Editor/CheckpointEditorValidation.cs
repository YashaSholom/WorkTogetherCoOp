using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CoopPrototype.Checkpoint;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace CoopPrototype.Editor
{
    /// <summary>Editor-only integration probes; gameplay actions use the normal, validated RPCs.</summary>
    public static class CheckpointEditorValidation
    {
        [Serializable] class State
        {
            public int visit, caseIndex, scanned, approved, rejected;
            public string phase, traveller;
            public bool panel, modal, inspecting;
            public Vector3 vehicle;
            public Paper[] documents;
        }
        [Serializable] class Parcel { public ulong id, holder, socket; public int index; public bool contraband, kinematic; public Vector3 position; }
        [Serializable] class CargoState { public string phase, vehicle, scanner, display; public int caseIndex, scannerOccupied; public Parcel[] packages; }
        [Serializable] class Paper { public ulong id, holder, socket; public int visit, index; public Vector3 position; }
        [Serializable] class VisitorPose { public int id; public string name; public bool seated; public string target; public Quaternion rotation; public Vector3 position; }
        [Serializable] class VisitorPoses { public VisitorPose[] actors; }
        public static string Execute(WorkshopEditorValidation.Request r)
        {
            var checkpoint = Object.FindFirstObjectByType<CheckpointSession>();
            var manager = NetworkManager.Singleton;
            var local = manager != null && manager.LocalClient?.PlayerObject != null ? manager.LocalClient.PlayerObject.GetComponent<PlayerInteractor>() : null;
            switch (r.command)
            {
                case "cpstationart": return StationArtValidation.Run();
                case "cpvisitorpose":
                    return JsonUtility.ToJson(new VisitorPoses { actors = Object.FindObjectsByType<AlienPassengerPresentation>(FindObjectsSortMode.None)
                        .Where(a => a.head != null).OrderBy(a => a.GetInstanceID()).Select(a => new VisitorPose {
                            id = a.GetInstanceID(), name = a.name, seated = a.seated,
                            target = a.LookTarget != null ? a.LookPlayerId.ToString() : "none",
                            rotation = a.PresentedHeadLocalRotation, position = a.head.position }).ToArray() });
                case "cpvisitorart":
                {
                    var view = Object.FindFirstObjectByType<CheckpointVehicleView>();
                    if (view == null) return "No visitor vehicle in this scene";
                    var actors = view.vehicle.GetComponentsInChildren<AlienPassengerPresentation>(true);
                    return $"phase={checkpoint.Phase.Value} kind={checkpoint.Current?.vehicle} actors={actors.Length}\n" +
                        string.Join("\n", actors.Select(a => $"{a.name}: active={a.gameObject.activeInHierarchy} seated={a.seated} seatedClip={(a.animator != null && a.animator.GetCurrentAnimatorStateInfo(0).IsName("Seated Idle"))} range={a.lookRange} target={(a.LookTarget != null ? "player " + a.LookPlayerId : "none")} head={a.head?.position}"));
                }
                case "cptestscene": CheckpointSceneSetup.CreateTestScene(); return "Direct checkpoint test scene ready";
                case "cpinspectionrepair": CheckpointSceneSetup.RepairInspectionPresentation(); return "Inspection presentation repaired";
                case "cptmpinstall": TmpPackageInstaller.Install(); return TmpPackageInstaller.Status;
                case "cptmpstatus": return TmpPackageInstaller.Status;
                case "cptextfix": CheckpointSceneSetup.FixWorldText(); return "Depth-tested world text applied";
                case "cpvalidateassets": return ValidateAssets();
                case "cploopsetup": CheckpointSceneSetup.AddGameLoop(); return "Work day, payouts and shop set up";
                case "cpshift":
                {
                    var shift = WorkShift.Instance;
                    return $"day={shift.Day.Value} time={shift.TimeText} open={shift.IsOpen} closed={shift.Closed.Value} phase={checkpoint.Phase.Value} visit={checkpoint.Visit.Value} case={checkpoint.CaseIndex.Value} flaw={checkpoint.Flaw.Value}\n" +
                        string.Join("\n", Object.FindObjectsByType<PlayerWallet>(FindObjectsSortMode.None).Select(w => $"wallet {w.NetworkObjectId}: credits={w.Credits.Value} pending={w.Pending.Value} lastDay={w.LastDayEarned.Value}")) +
                        (local != null ? $"\nslots active={local.ActiveSlot} [{string.Join(", ", Enumerable.Range(0, 3).Select(i => local.ItemInSlot(i) != null ? local.ItemInSlot(i).DisplayName : "-"))}] held={(local.Held != null ? local.Held.DisplayName : "-")} coffee={local.Motor.Caffeinated}" : "");
                }
                case "cpskip": WorkShift.Instance.SkipHourRpc(); return "Hour skipped";
                case "cpflawvisit": checkpoint.ForceVisit((int)r.player, Enum.Parse<DocumentFlaw>(string.IsNullOrEmpty(r.target) ? "None" : r.target)); return "Visit forced with flaw " + r.target;
                case "cpslot": local.SelectSlotRpc((int)r.player); return "Slot selected";
                case "cpuse": local.UseHeldRpc(); return "Use requested";
                case "cpbuy": Object.FindFirstObjectByType<ShopTerminal>().BuyRpc((int)r.player); return "Purchase requested";
                case "cpnextday": local.RequestInteractionRpc(new NetworkObjectReference(Object.FindFirstObjectByType<NextDayButton>().NetworkObject)); return "Next day button pressed";
                case "cpnextdaydebug":
                {
                    var b = Object.FindFirstObjectByType<NextDayButton>();
                    var col = b.GetComponent<Collider>();
                    var hits = string.Join(", ", Physics.RaycastAll(local.Motor.Eye, (col.bounds.center - local.Motor.Eye).normalized, (col.bounds.center - local.Motor.Eye).magnitude).Select(h => h.collider.name));
                    return $"spawned={b.IsSpawned} checkpoint={(b.checkpoint != null)} collider={(col != null)} reach={local.CanReach(b)} eye={local.Motor.Eye} centre={col.bounds.center} hits=[{hits}] prompt={b.Prompt}";
                }
                case "cpgive": local.GetComponent<PlayerWallet>().Credits.Value += (int)r.player; return "Test credits granted";
                case "cpuvstate":
                    return string.Join("\n", Object.FindObjectsByType<DocumentItem>(FindObjectsSortMode.None).Select(d => $"{d.DisplayName}: watermark={(d.uvWatermark != null && d.uvWatermark.activeSelf)} genuine={checkpoint.HasWatermark(d.CaseIndex.Value, d.DocumentIndex.Value)} dist={Vector3.Distance(local.Motor.Eye, d.transform.position):F1}")) +
                        $"\nunlocked={Object.FindFirstObjectByType<ContrabandScanner>().UnlockedSlots.Value} slot2={Object.FindFirstObjectByType<ContrabandScanner>().slots[1].Available.Value}";
                case "cptruckspot": local.RequestInteractionRpc(new NetworkObjectReference(checkpoint.cargoSpots[(int)r.player].NetworkObject)); return "Truck spot interaction requested";
                case "cpcargoaboard": return $"aboard={checkpoint.CargoAboard}/{checkpoint.Current.PackageCount} ready={checkpoint.CargoReadyToLeave} spots=[{string.Join(",", checkpoint.cargoSpots.Select(x => (x.Available.Value ? "A" : "-") + (x.Occupant.Value != ulong.MaxValue ? "X" : "_")))}]";
                case "cpshop": local.RequestInteractionRpc(new NetworkObjectReference(Object.FindFirstObjectByType<ShopTerminal>().NetworkObject)); return "Shop interaction requested";
                case "cppickuptype":
                {
                    var item = Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None).Where(p => p.GetType().Name == r.target && p.Holder.Value == ulong.MaxValue).OrderBy(p => Vector3.Distance(p.transform.position, local.transform.position)).First();
                    local.RequestInteractionRpc(new NetworkObjectReference(item.NetworkObject)); return "Pickup requested: " + item.name + " at " + item.transform.position;
                }
                case "cpreport":
                    return "REPORT: " + checkpoint.ReportTitle + "\n" + string.Join("\n", checkpoint.Report.Select(l => $"  {l.amount:+#;-#;0} {l.name}: {l.reason}")) + "\nSUMMARY: " + checkpoint.SummaryTitle + "\n" + string.Join("\n", checkpoint.Summary.Select(l => $"  {l.amount:+#;-#;0} {l.name}: {l.reason}"));
                case "cpcargorebuild": CheckpointSceneSetup.AddCargo(true); return "Scanner station rebuilt";
                case "cpxray":
                {
                    var rt = Object.FindFirstObjectByType<ContrabandScanner>().Image; var active = RenderTexture.active; RenderTexture.active = rt;
                    var png = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); png.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); png.Apply(); RenderTexture.active = active;
                    Directory.CreateDirectory("TestResults/Checkpoint"); File.WriteAllBytes("TestResults/Checkpoint/Xray.png", png.EncodeToPNG()); Object.DestroyImmediate(png);
                    return "X-ray image saved";
                }
                case "cpcargosetup": CheckpointSceneSetup.AddCargo(); return "Cargo truck, packages and contraband scanner set up";
                case "cpforcevisit": checkpoint.ForceVisit((int)r.player); return "Visit forced to case " + r.player;
                case "cpcargostate":
                {
                    var scanner = Object.FindFirstObjectByType<ContrabandScanner>();
                    return JsonUtility.ToJson(new CargoState { phase = checkpoint.Phase.Value.ToString(), caseIndex = checkpoint.CaseIndex.Value, vehicle = checkpoint.Current.vehicle.ToString(),
                        scanner = scanner == null ? "none" : scanner.State.Value.ToString(), scannerOccupied = scanner == null ? -1 : scanner.OccupiedCount, display = scanner != null && scanner.display != null ? scanner.display.text : "",
                        packages = Object.FindObjectsByType<CargoPackage>(FindObjectsSortMode.None).Select(p => new Parcel { id = p.NetworkObjectId, index = p.PackageIndex.Value, holder = p.Holder.Value, socket = p.Socket.Value, contraband = p.IsContraband, position = p.transform.position, kinematic = p.GetComponent<Rigidbody>().isKinematic }).OrderBy(p => p.index).ToArray() }, true);
                }
                case "cppickuppackage":
                    local.RequestInteractionRpc(new NetworkObjectReference(Object.FindObjectsByType<CargoPackage>(FindObjectsSortMode.None).First(p => p.PackageIndex.Value == (int)r.player).NetworkObject)); return "Package pickup requested via RPC";
                case "cpscanpad":
                    local.RequestInteractionRpc(new NetworkObjectReference(Object.FindFirstObjectByType<ContrabandScanner>().slots[(int)r.player].NetworkObject)); return "Scanner pad interaction requested via RPC";
                case "cpscanbutton":
                    local.RequestInteractionRpc(new NetworkObjectReference(Object.FindFirstObjectByType<ScanButton>().NetworkObject)); return "Scan button pressed via RPC";
                case "cpdetention": return CheckpointSceneSetup.AddDetention();
                case "cpdetentionvalidate": return CheckpointSceneSetup.ValidateDetention();
                case "cpdetentionpreview": return CheckpointSceneSetup.PreviewDetention(r.target);
                case "cpdocpreview": return CheckpointSceneSetup.PreviewDocuments();
                case "cparrest": Object.FindFirstObjectByType<CheckpointTerminal>().ArrestRpc(r.player == 0 ? checkpoint.Visit.Value : (int)r.player); return "Arrest requested via RPC";
                case "cptablet": local.RequestInteractionRpc(new NetworkObjectReference(Object.FindFirstObjectByType<HoldingCellTablet>().NetworkObject)); return "Tablet interaction requested";
                case "cpterminal": local.RequestInteractionRpc(new NetworkObjectReference(Object.FindFirstObjectByType<CheckpointTerminal>().NetworkObject)); return "Terminal interaction requested";
                case "cptab": CheckpointPanel.Instance.SelectTab((int)r.player); return "Tab selected";
                case "cprelease": Object.FindFirstObjectByType<HoldingCellTablet>().ReleaseRpc((int)r.player); return "Release requested via RPC";
                case "cpshot":
                {
                    // Play-safe world render from a fixed viewpoint (no gameplay changes).
                    (Vector3 eye, Vector3 look) = r.target switch
                    {
                        "shuttle" => (new Vector3(-3.5f, 3.2f, -4.5f), new Vector3(0, 1.6f, 0)),
                        "truck" => (new Vector3(5.5f, 3.4f, -4.5f), new Vector3(1.4f, 1.6f, 0)),
                        "dock" => (new Vector3(.6f, 2.5f, 1.1f), new Vector3(0, .95f, 3.7f)),
                        "cellclose" => (new Vector3(11, 1.9f, 24.6f), new Vector3(11, 1.1f, 32.5f)),
                        "exit" => (new Vector3(12, 8, -12), new Vector3(20, 0, 2)),
                        _ => (new Vector3(8, 2.2f, 21.5f), new Vector3(11, 1.4f, 31)),
                    };
                    var cameraObject = new GameObject("Validation camera"); var shot = cameraObject.AddComponent<Camera>();
                    shot.transform.position = eye; shot.transform.LookAt(look); shot.fieldOfView = 55;
                    string output = "TestResults/Expansion/play_" + r.target + ".png";
                    CheckpointSceneSetup.SaveCamera(shot, output, 1280, 800); Object.DestroyImmediate(cameraObject);
                    return Path.GetFullPath(output);
                }
                case "cpdump":
                {
                    // Lists every object whose name starts with one of the comma-separated prefixes: world position, bounds and components.
                    var prefixes = r.target.Split(',');
                    var sb = new System.Text.StringBuilder();
                    foreach (var t in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => prefixes.Any(p => t.name.StartsWith(p))))
                    {
                        var rend = t.GetComponent<Renderer>(); var col = t.GetComponent<Collider>();
                        sb.AppendLine($"{t.name} active={t.gameObject.activeInHierarchy} parent={(t.parent ? t.parent.name : "-")} pos={t.position:F2} rot={t.eulerAngles:F0} scale={t.lossyScale:F2}" +
                            (rend ? $" bounds={rend.bounds.center:F2}/{rend.bounds.size:F2}" : "") + (col ? $" col={col.bounds.center:F2}/{col.bounds.size:F2}" : "") +
                            " [" + string.Join(",", t.GetComponents<Component>().Where(c => c != null && !(c is Transform) && !(c is MeshFilter)).Select(c => c.GetType().Name)) + "]");
                    }
                    return sb.ToString();
                }
                case "cpdocdock": return CheckpointSceneSetup.AddDocumentDock();
                case "cphostport": { var ns = Object.FindFirstObjectByType<NetworkSession>(); ns.port = (ushort)r.player; ns.Launch(string.IsNullOrEmpty(r.target) ? "Host" : r.target); return $"{(string.IsNullOrEmpty(r.target) ? "Host" : r.target)} on port {r.player}: listening={manager.IsListening}"; }
                case "cpcompileprefs": return "ScriptCompilationDuringPlay=" + EditorPrefs.GetInt("ScriptCompilationDuringPlay", -1) + " (0 recompile and continue, 1 recompile after finished playing, 2 stop playing and recompile)";
                case "cprunstate": { var m = local.Motor; var anim = local.GetComponent<PlayerAnimation>(); var info = anim.animator.GetCurrentAnimatorStateInfo(0);
                    return $"running={m.Running.Value} speed={anim.NormalizedSpeed:F2} state={new[]{"Idle","Walk","Carry","Carry Walk","Run","Carry Run","Pick Up","Throw","Wave"}.FirstOrDefault(info.IsName) ?? "?"} rate={anim.animator.GetFloat("MoveRate"):F2} pos={local.transform.position:F2}"; }
                case "cpcolour":
                {
                    // target "ghost": spawn a second, server-owned player to prove colours never collide.
                    if (r.target == "ghost") { var ghost = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CoopPrototype/Prefabs/NetworkPlayer.prefab"), local.transform.position + Vector3.right * 2, Quaternion.identity); ghost.name = "Colour ghost"; ghost.GetComponent<NetworkObject>().Spawn(true); if (r.player > 0) ghost.GetComponent<CharacterAppearance>().ServerSetPreferred((int)r.player - 1); }
                    else if (r.target == "next") local.GetComponent<CharacterAppearance>().RequestNextVariantRpc();
                    return string.Join(" | ", Object.FindObjectsByType<CharacterAppearance>(FindObjectsSortMode.None).Select(a => $"{a.name}: {a.CurrentName} mat={(a.body != null ? a.body.sharedMaterial.name : "-")}"));
                }
                case "cpport": return MppmCloneRepair.PortOwners(r.player == 0 ? 7777 : (int)r.player);
                case "cpmppmlinks": return MppmCloneRepair.List();
                case "cpmppmrepair": return MppmCloneRepair.Repair();
                case "cpdebugsetup": return CheckpointSceneSetup.AddDebugPanel();
                case "cpdebug": Debugging.DebugTools.Instance.Run(r.target, (int)r.player, (int)r.position.x); return "Debug command sent: " + r.target;
                case "cpdebugreply": return Debugging.DebugTools.Instance.LastReply;
                case "cpdebugmenu": Debugging.DebugMenu.Instance.Toggle(); Debugging.DebugMenu.Instance.ShowSection((int)r.player); return "Debug panel open=" + Debugging.DebugMenu.Instance.IsOpen;
                case "cpforcenextday": checkpoint.ForceNextDay(); return "Day forced forward";
                case "cpcell":
                {
                    var cell = checkpoint.holdingCell;
                    var lines = Enumerable.Range(0, cell.Prisoners.Count).Select(i => cell.Prisoners[i]).Select(p => $"#{p.id} {cell.TravellerOf(p)?.displayName} case={p.caseIndex} member={p.member} visit={p.visit} day={p.arrestedDay} pickup={p.pickupDay} bounty={p.bounty} by={p.arresterName}");
                    return $"day={cell.Today} occupied={cell.Occupied}/{cell.Capacity} figures={cell.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Prisoner ") && !t.name.StartsWith("Prisoner spot"))} tabletOpen={(HoldingCellPanel.Instance != null && HoldingCellPanel.Instance.IsOpen)} arrested={checkpoint.ArrestedCount.Value}\n" + string.Join("\n", lines);
                }
                case "cpstate":
                    return JsonUtility.ToJson(new State { visit = checkpoint.Visit.Value, caseIndex = checkpoint.CaseIndex.Value,
                        scanned = checkpoint.ScannedCount, approved = checkpoint.ApprovedCount.Value, rejected = checkpoint.RejectedCount.Value,
                        phase = checkpoint.Phase.Value.ToString(), traveller = checkpoint.Current.traveller.displayName,
                        panel = CheckpointPanel.Instance != null && CheckpointPanel.Instance.IsOpen, modal = LocalGameplayModal.IsOpen,
                        inspecting = local != null && local.GetComponent<HeldItemInspector>().IsInspecting,
                        vehicle = Object.FindFirstObjectByType<CheckpointVehicleView>().vehicle.position,
                        documents = Object.FindObjectsByType<DocumentItem>(FindObjectsSortMode.None).Select(d => new Paper { id = d.NetworkObjectId, holder = d.Holder.Value, socket = d.Socket.Value, visit = d.Visit.Value, index = d.DocumentIndex.Value, position = d.transform.position }).ToArray() }, true);
                case "cppapers": Object.FindFirstObjectByType<TravellerInteractable>().RequestPapersRpc(r.player == 0 ? checkpoint.Visit.Value : (int)r.player); return "Papers requested via RPC";
                case "cpdecide": Object.FindFirstObjectByType<CheckpointTerminal>().DecideRpc(r.player == 0 ? checkpoint.Visit.Value : (int)r.player, r.target != "reject"); return "Decision requested via RPC";
                case "cppickup":
                    var doc = Object.FindObjectsByType<DocumentItem>(FindObjectsSortMode.None).First(d => d.DocumentIndex.Value == (int)r.player);
                    local.RequestInteractionRpc(new NetworkObjectReference(doc.NetworkObject)); return "Document pickup requested via RPC";
                case "cpinspect": local.GetComponent<HeldItemInspector>().OpenHeld(true); return "Inspection requested";
                case "cpclose": CheckpointPanel.Instance?.Close(); if (local != null) local.GetComponent<HeldItemInspector>().Close(); return "Panels closed";
                case "cpscreen":
                    Directory.CreateDirectory("TestResults/Checkpoint");
                    EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Repaint();
                    ScreenCapture.CaptureScreenshot("TestResults/Checkpoint/" + (string.IsNullOrEmpty(r.target) ? "Gameplay" : Path.GetFileName(r.target)) + ".png"); return "Screen capture scheduled";
                case "cpconsoleclear":
                    typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntries").GetMethod("Clear", BindingFlags.Public | BindingFlags.Static).Invoke(null, null); return "Console cleared";
                case "cpconsole": return ConsoleErrors();
                case "cpinput":
                    var keyboard = Keyboard.current;
                    if (keyboard == null) throw new InvalidOperationException("No keyboard");
                    EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Enum.Parse<Key>(r.target)));
                    double releaseAt = EditorApplication.timeSinceStartup + .2;
                    EditorApplication.CallbackFunction release = null;
                    release = () => { if (EditorApplication.timeSinceStartup < releaseAt) return; InputSystem.QueueStateEvent(keyboard, new KeyboardState()); EditorApplication.update -= release; };
                    EditorApplication.update += release;
                    return "Keyboard press queued";
                case "cpfocus":
                    var flow = manager.GetComponent<Frontend.GameFlow>();
                    return $"modal={LocalGameplayModal.IsOpen} settings={(flow != null && flow.SettingsOpen)} cursor={UnityEngine.Cursor.lockState}";
                case "cpmpopen":
                    return "Opened: " + EditorApplication.ExecuteMenuItem("Window/Multiplayer/Multiplayer Play Mode");
                case "cpmpui":
                    return string.Join("\n", Resources.FindObjectsOfTypeAll<EditorWindow>().Select(w => w.GetType().FullName + " : " + w.titleContent.text + "\n" +
                        string.Join("\n", w.rootVisualElement.Query<Toggle>().ToList().Select(t => $"name={t.name} label={t.label} value={t.value} parent={t.parent.name} text={string.Join("|", t.parent.Query<Label>().ToList().Select(l=>l.text))}"))));
                case "cpmptoggle":
                    var window = Resources.FindObjectsOfTypeAll<EditorWindow>().First(w => w.GetType().FullName == "Unity.Multiplayer.PlayMode.Editor.MultiplayerWindow");
                    var toggle = window.rootVisualElement.Query<Toggle>().ToList().First(t => t.parent.Query<Label>().ToList().Any(l => l.text == r.target));
                    toggle.value = r.player != 0; return "Player activation changed";
                case "cpartfix":
                    if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop first");
                    var game = EditorSceneManager.OpenScene(MainMenuSetup.GamePath);
                    foreach (var text in GameObject.Find("Space Checkpoint").GetComponentsInChildren<TextMesh>(true))
                    {
                        float width = text.text.StartsWith("SECTOR 07   /") ? 13 : text.text == "DOCUMENT CONTROL" ? 6 : text.text.Contains("INSPECTION TERMINAL") ? 2.1f : text.text.Contains("COLLECT") ? 1.8f : text.text.Contains("SORT /") ? 3 : text.text.Contains("FUTURE") ? 3 : text.text == "COMET HOPPER" ? 1.2f : 1.4f;
                        var renderer = text.GetComponent<Renderer>();
                        if (renderer.bounds.size.x > width) { text.characterSize *= width / renderer.bounds.size.x; EditorUtility.SetDirty(text); }
                    }
                    EditorSceneManager.SaveScene(game);
                    CheckpointSceneSetup.Setup(); return "World signs fitted and camera updated";
                case "cpmenufix":
                    if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop first");
                    var scene = EditorSceneManager.OpenScene(MainMenuSetup.MenuPath);
                    var label = GameObject.Find("BORDER CONTROL  /  07").GetComponent<TextMesh>(); label.characterSize = .07f; label.transform.position = new Vector3(4, 3.9f, 6);
                    EditorSceneManager.SaveScene(scene); return "Menu sign fitted";
            }
            throw new InvalidOperationException("Unknown checkpoint probe: " + r.command);
        }
        static string ConsoleErrors()
        {
            var assembly = typeof(EditorWindow).Assembly; var logs = assembly.GetType("UnityEditor.LogEntries"); var entryType = assembly.GetType("UnityEditor.LogEntry");
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            int count = (int)logs.GetMethod("StartGettingEntries", flags).Invoke(null, null);
            var entry = Activator.CreateInstance(entryType); var output = new System.Collections.Generic.List<string>();
            try
            {
                for (int i = 0; i < count; i++)
                {
                    logs.GetMethod("GetEntryInternal", flags).Invoke(null, new[] { (object)i, entry });
                    int mode = (int)entryType.GetField("mode").GetValue(entry);
                    if ((mode & (1 | 2 | 16 | 64 | 256 | 2048)) != 0) output.Add(entryType.GetField("message").GetValue(entry).ToString());
                }
            }
            finally { logs.GetMethod("EndGettingEntries", flags).Invoke(null, null); }
            return output.Count == 0 ? "No Console errors" : string.Join("\n", output);
        }
        static string ValidateAssets()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            var report = new System.Collections.Generic.List<string>();
            foreach (var path in new[] { MainMenuSetup.MenuPath, MainMenuSetup.GamePath, CheckpointSceneSetup.TestScenePath })
            {
                var scene = EditorSceneManager.OpenScene(path);
                foreach (var root in scene.GetRootGameObjects()) foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0) throw new InvalidOperationException("Missing script: " + t.name);
                if (path != MainMenuSetup.MenuPath)
                {
                    var checkpoint = Object.FindFirstObjectByType<CheckpointSession>();
                    if (checkpoint == null || checkpoint.documentPrefab == null || checkpoint.documentDeliveryPoints.Any(p => p == null)) throw new InvalidOperationException("Incomplete checkpoint references");
                    foreach (var record in checkpoint.cases)
                        if (record == null || record.traveller == null || record.traveller.portrait == null || record.documents.Any(d => d == null || d.kind == null || d.traveller != record.traveller)) throw new InvalidOperationException("Incomplete traveller case");
                    foreach (var endpoint in Object.FindObjectsByType<CheckpointEndpoint>(FindObjectsSortMode.None))
                        if (endpoint.checkpoint != checkpoint || endpoint.GetComponent<Collider>() == null || endpoint.GetComponent<NetworkObject>() == null) throw new InvalidOperationException("Endpoint wiring: " + endpoint.name);
                    foreach (var socket in Object.FindObjectsByType<ItemSocket>(FindObjectsSortMode.None))
                        if (socket.snapPoint == null) throw new InvalidOperationException("Missing socket anchor");
                    var context = Object.FindFirstObjectByType<Frontend.GameSceneContext>();
                    if (context == null || context.loadingCamera == null || context.spawnPoints.Any(p => p == null)) throw new InvalidOperationException("Invalid gameplay context");
                }
                report.Add("PASS: " + path + " scripts and required references");
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CoopPrototype/Prefabs/CheckpointDocument.prefab");
            var prefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/CoopPrototype/Prefabs/NetworkPrefabs.asset");
            if (!prefabs.PrefabList.Any(p => p.Prefab == prefab) || prefab.GetComponent<DocumentItem>() == null || prefab.GetComponent<DocumentInspectionContent>() != null) throw new InvalidOperationException("Document prefab registration/wiring");
            report.Add("PASS: document prefab and network registration");
            var document = prefab.GetComponent<DocumentItem>();
            var cover = prefab.transform.Find("Document cover");
            float frontY = cover.localPosition.y + cover.localScale.y * .5f; // front face = +Y
            foreach (var front in new Component[] { document.coverLabel, document.portraitImage, prefab.transform.Find("Document spine"), prefab.transform.Find("Traveller portrait") })
            {
                if (front == null) throw new InvalidOperationException("Document front-face element is missing");
                if (front.transform.localPosition.y < frontY - .001f) throw new InvalidOperationException("Document element is not on the front face: " + front.name);
            }
            foreach (var canvas in new Component[] { document.coverLabel, document.portraitImage })
            {
                var rect = (RectTransform)canvas.transform;
                if (rect.localPosition.y <= frontY) throw new InvalidOperationException("Document " + rect.name + " sits inside the cover");
                if (Vector3.Dot(prefab.transform.InverseTransformDirection(rect.forward), Vector3.down) < .99f) throw new InvalidOperationException("Document " + rect.name + " does not face the front");
                if (rect.sizeDelta.x * rect.lossyScale.x < .1f) throw new InvalidOperationException("Document " + rect.name + " is too small to read");
            }
            if (!document.coverLabel.enableAutoSizing) throw new InvalidOperationException("Document text should auto-size to fit the cover");
            if (Vector3.Dot(document.ViewRotation * Vector3.up, Vector3.back) < .99f || Vector3.Dot(document.ViewRotation * Vector3.forward, Vector3.up) < .99f) throw new InvalidOperationException("Document view rotation must face the front toward the camera, top edge up");
            report.Add("PASS: document text and portrait sit on the readable front face, and the held view faces it toward the camera");
            var cargo = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CoopPrototype/Prefabs/CargoPackage.prefab");
            if (cargo != null)
            {
                if (!prefabs.PrefabList.Any(p => p.Prefab == cargo) || cargo.GetComponent<CargoPackage>() == null || cargo.GetComponent<Rigidbody>() == null || cargo.GetComponent<NetworkObject>() == null ||
                    cargo.GetComponent<CargoPackage>().definition == null || cargo.GetComponent<CargoPackage>().definition.category != "Cargo" || cargo.GetComponent<CargoPackage>().tinted.Length == 0)
                    throw new InvalidOperationException("Cargo package prefab registration/wiring");
                var gameScene = EditorSceneManager.OpenScene(MainMenuSetup.GamePath);
                var checkpoint = Object.FindFirstObjectByType<CheckpointSession>();
                var view = Object.FindFirstObjectByType<CheckpointVehicleView>();
                int largest = checkpoint.cases.Max(c => c.PackageCount);
                if (checkpoint.packagePrefab != cargo.GetComponent<CargoPackage>() || checkpoint.vehicleView != view || view.cargoPoints.Length < largest || view.cargoDummies.Length != view.cargoPoints.Length || view.truckAssembly == null || view.bedCollision == null)
                    throw new InvalidOperationException("Cargo truck wiring");
                foreach (var record in checkpoint.cases.Where(c => c.vehicle == VehicleKind.Truck))
                    if (record.PackageCount == 0) throw new InvalidOperationException("Truck case without packages: " + record.name);
                if (!checkpoint.cases.Any(c => c.vehicle == VehicleKind.Shuttle) || !checkpoint.cases.Any(c => c.vehicle == VehicleKind.Truck) || !checkpoint.cases.Any(c => c.packages != null && c.packages.Any(p => p.contraband)) || !checkpoint.cases.Any(c => c.vehicle == VehicleKind.Truck && c.packages.All(p => !p.contraband)))
                    throw new InvalidOperationException("Test data needs a shuttle, a truck with contraband and a clean truck");
                var scanner = Object.FindFirstObjectByType<ContrabandScanner>();
                if (scanner == null || scanner.slots.Length == 0 || scanner.slots.Any(x => x == null || x.acceptedCategory != "Cargo" || x.snapPoint == null || x.onOccupancyChanged.GetPersistentEventCount() == 0) || scanner.display == null || Object.FindFirstObjectByType<ScanButton>()?.station != scanner)
                    throw new InvalidOperationException("Contraband scanner wiring");
                int xrayLayer = LayerMask.NameToLayer("XRay");
                var package = cargo.GetComponent<CargoPackage>();
                if (xrayLayer < 0 || scanner.xrayCamera == null || scanner.screen == null || scanner.xrayCamera.cullingMask != 1 << xrayLayer || scanner.slats.Length == 0)
                    throw new InvalidOperationException("X-ray camera/screen wiring");
                if (package.xrayContents == null || package.xrayContraband == null || package.xrayContents.GetComponentsInChildren<Transform>(true).Any(t => t.gameObject.layer != xrayLayer) || package.xrayContents.GetComponentsInChildren<Collider>(true).Length > 0)
                    throw new InvalidOperationException("Package X-ray contents must be collider-free and on the XRay layer");
                var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CoopPrototype/Prefabs/NetworkPlayer.prefab");
                if (playerPrefab.GetComponentsInChildren<Camera>(true).Any(c => (c.cullingMask & (1 << xrayLayer)) != 0) || (Object.FindFirstObjectByType<Frontend.GameSceneContext>().loadingCamera.cullingMask & (1 << xrayLayer)) != 0)
                    throw new InvalidOperationException("Gameplay cameras must not draw the XRay layer");
                report.Add("PASS: cargo package prefab, truck, conveyor X-ray scanner (camera, screen, X-ray-only layer) and case data are wired");
            }
            var shader = Shader.Find("Coop/Checkpoint World Text");
            var errors = ShaderUtil.GetShaderMessages(shader).Where(m => m.severity.ToString() == "Error").ToArray();
            if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors.Select(e => e.message)));
            report.Add("PASS: depth-tested text shader has no compilation errors");
            EditorSceneManager.OpenScene(MainMenuSetup.MenuPath);
            Directory.CreateDirectory("TestResults/Checkpoint"); File.WriteAllLines("TestResults/Checkpoint/Assets-results.txt", report);
            return string.Join("\n", report);
        }
    }
}
