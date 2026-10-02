using System;
using System.IO;
using System.Linq;
using CoopPrototype.Checkpoint;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CoopPrototype.Editor
{
    public static partial class CheckpointSceneSetup
    {
        /// <summary>Number of document slots on the terminal. Raise it (and re-run) when new document types are added.</summary>
        const int DocumentSlotCount = 6;

        /// <summary>
        /// Requested papers go straight into the terminal: builds a row of document slots (scanner sockets) along the terminal
        /// desk, with side wings for the outer slots, removes the old collection shelf and delivery points, and puts the
        /// START NEXT DAY button on the right wing. The two existing scanner trays are kept (same network objects) and reused.
        /// </summary>
        [MenuItem("Coop Prototype/Checkpoint/Add Terminal Document Slots")]
        public static string AddDocumentDock()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            EditorSceneManager.SaveOpenScenes();
            string backup = "Tools/Checkpoint/BeforeDocumentDock"; Directory.CreateDirectory(backup);
            string backupScene = backup + "/" + Path.GetFileName(MainMenuSetup.GamePath);
            if (!File.Exists(backupScene)) File.Copy(MainMenuSetup.GamePath, backupScene);
            var scene = EditorSceneManager.OpenScene(MainMenuSetup.GamePath);
            Palette();
            var world = GameObject.Find("Space Checkpoint").transform;
            var session = Object.FindFirstObjectByType<CheckpointSession>();
            var terminal = Object.FindFirstObjectByType<CheckpointTerminal>().transform;
            var desk = terminal.Find("Desk");
            var deskMaterial = desk.GetComponent<Renderer>().sharedMaterial;
            Bounds deskBounds = desk.GetComponent<Renderer>().bounds;
            float top = deskBounds.max.y, front = deskBounds.min.z;

            // Keyboard moves back to make room for the middle slots.
            var keyboard = terminal.Find("Keyboard");
            if (keyboard != null) keyboard.position = new Vector3(keyboard.position.x, keyboard.position.y, front + 1.05f);

            // Side wings carry the outer slots (and the next-day button on the right).
            Transform Wing(string name, float x)
            {
                var existing = terminal.Find(name); if (existing != null) return existing;
                var wing = Box(terminal, name, Vector3.zero, new Vector3(1.05f, deskBounds.size.y, 1.7f), dark, true);
                wing.GetComponent<Renderer>().sharedMaterial = deskMaterial;
                wing.transform.position = new Vector3(x, deskBounds.center.y, front + .82f);
                var leg = Box(wing.transform.parent, name + " support", Vector3.zero, new Vector3(.8f, deskBounds.min.y, 1.2f), dark);
                leg.transform.position = new Vector3(x, deskBounds.min.y * .5f, front + .9f);
                return wing.transform;
            }
            float deskHalf = deskBounds.extents.x;
            Wing("Document wing left", -(deskHalf + .525f));
            Wing("Document wing right", deskHalf + .525f);

            // Slots: reuse the existing scanner trays, add the rest. Fill order goes from the middle outwards.
            var existingTrays = Object.FindObjectsByType<DocumentScanner>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(s => s.transform.parent == world).Select(s => s.GetComponent<ItemSocket>()).OrderBy(s => s.name).ToList();
            var trayMaterial = existingTrays.Count > 0 ? existingTrays[0].GetComponent<Renderer>().sharedMaterial : mint;
            float spacing = .92f;
            var xs = Enumerable.Range(0, DocumentSlotCount).Select(i => (i - (DocumentSlotCount - 1) * .5f) * spacing).OrderBy(Mathf.Abs).ThenBy(x => x).ToArray();
            for (int i = 0; i < DocumentSlotCount; i++)
            {
                ItemSocket socket;
                if (i < existingTrays.Count) socket = existingTrays[i];
                else
                {
                    var go = Box(world, "Scanner Tray " + (i + 1), Vector3.zero, Vector3.one, trayMaterial, true);
                    go.AddComponent<NetworkObject>(); socket = go.AddComponent<ItemSocket>(); socket.acceptedCategory = "Document";
                    socket.snapPoint = Group("Paper snap", go.transform);
                    var scan = go.AddComponent<DocumentScanner>(); scan.checkpoint = session;
                    UnityEventTools.AddPersistentListener(socket.onOccupancyChanged, scan.Scan);
                }
                socket.name = "Scanner Tray " + (i + 1);
                var position = new Vector3(xs[i], top + .04f, front + .38f);
                socket.transform.SetPositionAndRotation(position, Quaternion.identity);
                socket.transform.localScale = new Vector3(.82f, .07f, .86f);
                socket.snapPoint.position = position + Vector3.up * .085f; socket.snapPoint.rotation = Quaternion.identity;
                socket.insertPrompt = "Put paper in document slot " + (i + 1);
                socket.GetComponent<DocumentScanner>().checkpoint = session;
                EditorUtility.SetDirty(socket); EditorUtility.SetDirty(socket.snapPoint);
                // Slot number on the desk edge facing the officer.
                string number = "Slot number " + (i + 1);
                var label = world.Find(number);
                if (label == null) { label = Label(world, (i + 1).ToString(), Vector3.zero, .03f, new Color(.55f, 1, .8f)).transform; label.name = number; }
                label.position = new Vector3(xs[i], top - .065f, front - .01f);
                var mesh = label.GetComponent<TextMesh>(); mesh.characterSize = .024f; mesh.color = new Color(.05f, .07f, .12f);
            }
            session.documentSlots = Enumerable.Range(1, DocumentSlotCount).Select(n => world.Find("Scanner Tray " + n).GetComponent<ItemSocket>()).ToArray();
            var titleLabel = world.Find("DOCUMENT SLOTS label");
            if (titleLabel == null) { titleLabel = Label(world, "DOCUMENT SLOTS  ·  PAPERS ARRIVE HERE", Vector3.zero, .012f, Color.black).transform; titleLabel.name = "DOCUMENT SLOTS label"; }
            titleLabel.position = new Vector3(0, top - .17f, front - .01f);
            var titleMesh = titleLabel.GetComponent<TextMesh>(); titleMesh.characterSize = .011f; titleMesh.color = new Color(.35f, .25f, .2f);

            // The collection shelf and its delivery points are no longer used.
            foreach (var name in new[] { "Document collection shelf", "Collection pedestal", "Document delivery 0", "Document delivery 1" })
            { var t = world.Find(name); if (t != null) Object.DestroyImmediate(t.gameObject); }
            session.documentDeliveryPoints = Array.Empty<Transform>();

            // START NEXT DAY sat on a desk that the layout pass moved away: put it on the right wing.
            var nextDay = world.Find("Start Next Day Button");
            var rightWing = terminal.Find("Document wing right");
            if (nextDay != null && rightWing != null)
            {
                var wingTop = rightWing.GetComponent<Renderer>().bounds.max.y;
                var at = new Vector3(rightWing.position.x, wingTop + .045f, front + 1.25f);
                nextDay.position = at;
                var plate = world.Find("Next day button plate") ?? Box(world, "Next day button plate", Vector3.zero, new Vector3(.38f, .02f, .38f), dark).transform;
                plate.position = new Vector3(at.x, wingTop + .01f, at.z);
                var caption = world.Find("START NEXT DAY (wing)");
                if (caption == null) { caption = Label(world, "START NEXT DAY", Vector3.zero, .012f, new Color(.6f, 1, .74f)).transform; caption.name = "START NEXT DAY (wing)"; }
                caption.position = new Vector3(at.x, wingTop + .012f, at.z - .26f); caption.rotation = Quaternion.Euler(90, 0, 0);
            }
            var shelfPoint = world.Find("Debug teleport points/Paper shelf and vehicle stop");
            if (shelfPoint != null) Object.DestroyImmediate(shelfPoint.gameObject);
            ApplyWorldText(world);
            EditorUtility.SetDirty(session);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AddDebugPanel(); // re-wire teleport points (the shelf point became "Terminal front")
            return $"{DocumentSlotCount} terminal document slots; collection shelf removed; next-day button on the right wing (backup: {backupScene}).";
        }
    }
}
