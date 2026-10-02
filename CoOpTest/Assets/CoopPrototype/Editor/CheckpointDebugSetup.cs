using System;
using System.Linq;
using CoopPrototype.Checkpoint;
using CoopPrototype.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CoopPrototype.Editor
{
    /// <summary>Adds the F1 debug panel to GameWorld: DebugTools on the Checkpoint Authority network object, DebugMenu on the
    /// checkpoint interface, and editable teleport points. Running it again only re-wires references and adds missing points.</summary>
    public static partial class CheckpointSceneSetup
    {
        [MenuItem("Coop Prototype/Checkpoint/Add Debug Panel (F1)")]
        public static string AddDebugPanel()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            EditorSceneManager.SaveOpenScenes();
            var scene = EditorSceneManager.OpenScene(MainMenuSetup.GamePath);
            var session = Object.FindFirstObjectByType<CheckpointSession>();
            var tools = session.GetComponent<DebugTools>(); if (tools == null) tools = session.gameObject.AddComponent<DebugTools>();
            tools.checkpoint = session; tools.shop = Object.FindFirstObjectByType<ShopTerminal>(); tools.scanner = Object.FindFirstObjectByType<ContrabandScanner>();
            var ui = Object.FindFirstObjectByType<CheckpointPanel>().gameObject;
            var menu = ui.GetComponent<DebugMenu>(); if (menu == null) menu = ui.AddComponent<DebugMenu>();
            menu.checkpoint = session;
            var world = GameObject.Find("Space Checkpoint").transform;
            var group = world.Find("Debug teleport points"); if (group == null) group = Group("Debug teleport points", world);
            Transform Point(string name, Vector3 at, Vector3 lookAt)
            {
                var t = group.Find(name); if (t == null) t = Group(name, group);
                at.y = 1.1f; t.position = at;
                var flat = lookAt - at; flat.y = 0; if (flat.sqrMagnitude > .01f) t.rotation = Quaternion.LookRotation(flat);
                return t;
            }
            Vector3 Front(Transform target, float distance) => target.position + target.rotation * new Vector3(0, 0, -distance);
            var terminal = Object.FindFirstObjectByType<CheckpointTerminal>().transform;
            var cell = Object.FindFirstObjectByType<HoldingCell>();
            var tablet = Object.FindFirstObjectByType<HoldingCellTablet>();
            var context = Object.FindFirstObjectByType<Frontend.GameSceneContext>();
            var points = new[]
            {
                Point("Inspection terminal", terminal.position + new Vector3(0, 0, 1.5f), terminal.position),
                Point("Terminal front (document slots)", terminal.position + new Vector3(0, 0, -1.9f), terminal.position),
                Point("Cargo X-ray", tools.scanner != null ? Front(tools.scanner.transform, 1.3f) : Vector3.zero, tools.scanner != null ? tools.scanner.transform.position : Vector3.zero),
                Point("Supply ATM", tools.shop != null ? Front(tools.shop.transform, 1.4f) : Vector3.zero, tools.shop != null ? tools.shop.transform.position : Vector3.zero),
                Point("Holding cell tablet", tablet != null ? Front(tablet.transform, 1f) : Vector3.zero, tablet != null ? tablet.transform.position : Vector3.zero),
                Point("Holding cell (view)", cell != null ? cell.transform.position + new Vector3(0, 0, -5.6f) : Vector3.zero, cell != null ? cell.transform.position : Vector3.zero),
                Point("Crew spawn", context != null && context.spawnPoints.Length > 0 ? context.spawnPoints[0].position : new Vector3(-5, 0, 7), terminal.position),
            };
            tools.teleportPoints = points;
            EditorUtility.SetDirty(tools); EditorUtility.SetDirty(menu);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            return "Debug panel added to GameWorld (F1 in editor/development builds); " + points.Length + " teleport points.";
        }
    }
}
