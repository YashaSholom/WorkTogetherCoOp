using System;
using System.Linq;
using CoopPrototype.Checkpoint;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoopPrototype.Debugging
{
    /// <summary>
    /// F1 debug panel (Unity Editor and Development builds only). Shows the hidden truth of the current case and sends
    /// commands to <see cref="DebugTools"/> on the server. Sections are plain methods: add a button to the section that fits
    /// a new feature (see FEATURE_IMPLEMENTATION.md).
    /// </summary>
    [DefaultExecutionOrder(-190)]
    public sealed class DebugMenu : MonoBehaviour
    {
        enum Section { Visitors, Decisions, Day, Economy, Player }
        static readonly string[] SectionNames = { "Visitors", "Decide", "Day / Police", "Money", "Player" };
        public static DebugMenu Instance { get; private set; }
        public CheckpointSession checkpoint;
        Section section;
        int flawOption = -1;
        Vector2 scroll;
        bool open;
        float contentHeight = 900;
        GUIStyle small, label, header;
        static readonly Color Panel = new(.03f, .05f, .08f, .95f), Accent = new(1, .62f, .2f);
        public bool IsOpen => open;
        void Awake() => Instance = this;
        void OnDisable() { Close(); if (Instance == this) Instance = null; }
        public void Toggle() { if (open) Close(); else Open(); }
        public void Open()
        {
            if (!DebugTools.Allowed || open || DebugTools.Instance == null || !DebugTools.Instance.IsSpawned) return;
            if (LocalGameplayModal.IsOpen) return;
            if (!LocalGameplayModal.Open(this)) return;
            open = true;
        }
        public void Close() { if (!open) return; open = false; LocalGameplayModal.Close(this); }
        public void ShowSection(int index) => section = (Section)Mathf.Clamp(index, 0, SectionNames.Length - 1);
        void Update()
        {
            if (!DebugTools.Allowed || Keyboard.current == null) return;
            if (Keyboard.current.f1Key.wasPressedThisFrame && !NetworkSession.MenuVisible) Toggle();
            else if (open && (Keyboard.current.escapeKey.wasPressedThisFrame || NetworkSession.MenuVisible || DebugTools.Instance == null)) Close();
        }

        void OnGUI()
        {
            if (!open || checkpoint == null || !checkpoint.IsSpawned) return;
            var tools = DebugTools.Instance;
            InspectionGui.Begin();
            if (small == null)
            {
                small = new GUIStyle(GUI.skin.button) { fontSize = 15, wordWrap = true, alignment = TextAnchor.MiddleCenter };
                label = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true }; label.normal.textColor = new Color(.85f, .9f, .95f);
                header = new GUIStyle(label) { fontSize = 20, fontStyle = FontStyle.Bold }; header.normal.textColor = Accent;
            }
            var area = new Rect(880, 40, 540, 820);
            InspectionGui.Fill(area, Panel); InspectionGui.Fill(new Rect(area.x, area.y, area.width, 3), Accent);
            GUI.Label(new Rect(area.x + 16, area.y + 10, 400, 28), "DEBUG  ·  editor / development only", header);
            if (GUI.Button(new Rect(area.xMax - 96, area.y + 10, 80, 28), "Close", small)) Close();
            float y = area.y + 46;
            GUI.Label(new Rect(area.x + 16, y, area.width - 32, 120), Status(), label); y += 124;
            for (int i = 0; i < SectionNames.Length; i++)
            {
                var old = GUI.backgroundColor; if ((int)section == i) GUI.backgroundColor = Accent;
                if (GUI.Button(new Rect(area.x + 16 + i * 102, y, 98, 32), SectionNames[i], small)) section = (Section)i;
                GUI.backgroundColor = old;
            }
            y += 42;
            var view = new Rect(area.x + 8, y, area.width - 16, area.yMax - y - 70);
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, view.width - 20, contentHeight));
            float cy = 0;
            switch (section)
            {
                case Section.Visitors: Visitors(tools, ref cy); break;
                case Section.Decisions: Decisions(tools, ref cy); break;
                case Section.Day: Day(tools, ref cy); break;
                case Section.Economy: Economy(tools, ref cy); break;
                case Section.Player: PlayerSection(tools, ref cy); break;
            }
            contentHeight = cy + 10;
            GUI.EndScrollView();
            bool fresh = Time.unscaledTime - tools.LastReplyAt < 8;
            GUI.Label(new Rect(area.x + 16, area.yMax - 62, area.width - 32, 56), fresh ? "› " + tools.LastReply : "F1 or Esc closes. Commands run on the host and replicate to everyone.", label);
            InspectionGui.End();
        }

        // ------------------------------------------------------------------ layout helpers
        const float Width = 500, Gap = 6, Row = 38;
        void Title(string text, ref float y) { GUI.Label(new Rect(8, y + 4, Width, 24), text, header); y += 32; }
        void Note(string text, ref float y, float height = 22) { GUI.Label(new Rect(8, y, Width, height), text, label); y += height + 2; }
        /// <summary>A row of equal buttons; returns the index clicked or -1.</summary>
        int Buttons(ref float y, params string[] names)
        {
            int clicked = -1; float w = (Width - Gap * (names.Length - 1)) / names.Length;
            for (int i = 0; i < names.Length; i++) if (GUI.Button(new Rect(8 + i * (w + Gap), y, w, Row - 4), names[i], small)) clicked = i;
            y += Row; return clicked;
        }
        bool Button(ref float y, string name) => Buttons(ref y, name) == 0;

        string Status()
        {
            var shift = checkpoint.shift; var current = checkpoint.Current; var wallet = PlayerWallet.Local; var cell = checkpoint.holdingCell;
            int contraband = current != null && current.packages != null ? current.packages.Count(p => p.contraband) : 0;
            return (shift != null ? $"Day {shift.Day.Value}  {shift.TimeText}  {(shift.Closed.Value ? "CLOSED" : shift.IsOpen ? "open" : "closing")}" : "No work shift") +
                   $"   ·   Visit {checkpoint.Visit.Value:000}  {checkpoint.Phase.Value}\n" +
                   (current != null ? $"Case {checkpoint.CaseIndex.Value}: {current.traveller.displayName} ({current.vehicle}{(current.CompanionCount > 0 ? $", +{current.CompanionCount} companion" : "")})\n" : "\n") +
                   $"TRUTH: flaw {checkpoint.Flaw.Value}   ·   contraband crates {contraband}/{current?.PackageCount ?? 0}   ·   {(checkpoint.Flaw.Value == DocumentFlaw.None && contraband == 0 ? "VALID visitor" : "INVALID")}\n" +
                   $"Papers scanned {checkpoint.ScannedCount}/{current?.documents.Length ?? 0}   ·   cell {(cell != null ? $"{cell.Occupied}/{cell.Capacity}" : "—")}   ·   flaw chance {checkpoint.flawChance:P0}\n" +
                   (wallet != null ? $"You: {wallet.Credits.Value} cr banked, {wallet.Pending.Value:+#;-#;0} today" : "");
        }

        // ------------------------------------------------------------------ sections
        void Visitors(DebugTools tools, ref float y)
        {
            Title("Visitor art / gaze (this peer)", ref y);
            var actors = checkpoint.vehicleView.vehicle.GetComponentsInChildren<AlienPassengerPresentation>();
            Note("Seated idle; nearest player in 5 m; head turns smoothly.", ref y);
            foreach (var actor in actors)
                Note(actor.name + " → " + (actor.LookTarget != null ? "player " + actor.LookPlayerId : "looking forward"), ref y);
            Title("Flaw for the next called visitor", ref y);
            var flaws = new[] { "Rolled" }.Concat(Enum.GetNames(typeof(DocumentFlaw))).ToArray();
            for (int start = 0; start < flaws.Length; start += 4)
            {
                var chunk = flaws.Skip(start).Take(4).Select((name, i) => (start + i - 1 == flawOption ? "● " : "") + name).ToArray();
                int pick = Buttons(ref y, chunk);
                if (pick >= 0) flawOption = start + pick - 1;
            }
            Title("Call a visitor now (ends the current visit)", ref y);
            switch (Buttons(ref y, "Random", "Shuttle", "Truck"))
            {
                case 0: tools.Run("visitor", 0, flawOption); break;
                case 1: tools.Run("visitorkind", (int)VehicleKind.Shuttle, flawOption); break;
                case 2: tools.Run("visitorkind", (int)VehicleKind.Truck, flawOption); break;
            }
            switch (Buttons(ref y, "With companions", "Carrying contraband"))
            {
                case 0: tools.Run("visitorcompanions", 0, flawOption); break;
                case 1: tools.Run("visitorcontraband", 0, flawOption); break;
            }
            Title("Specific traveller", ref y);
            for (int i = 0; i < checkpoint.cases.Length; i++)
            {
                var c = checkpoint.cases[i];
                string extra = (c.CompanionCount > 0 ? $" +{c.CompanionCount}" : "") + (c.packages != null && c.packages.Any(p => p.contraband) ? " · contraband" : "");
                if (Button(ref y, $"{i}: {c.traveller.displayName}  ({c.vehicle}, {(c.traveller.raceInfo != null ? c.traveller.raceInfo.displayName : c.traveller.race)}{extra})")) tools.Run("visitorcase", i, flawOption);
            }
            Title("Current visit", ref y);
            switch (Buttons(ref y, "Stop vehicle now", "Papers into slots", "Scan all papers"))
            {
                case 0: tools.Run("arrive"); break;
                case 1: tools.Run("papers"); break;
                case 2: tools.Run("scanall"); break;
            }
            if (Button(ref y, "Send visitor away (no verdict)")) tools.Run("sendaway");
            Title("Flaw chance for rolled visitors", ref y);
            int[] chances = { 0, 40, 100 };
            int c2 = Buttons(ref y, "0%", "40% (default)", "100%"); if (c2 >= 0) tools.Run("flawchance", chances[c2]);
        }
        void Decisions(DebugTools tools, ref float y)
        {
            Title("Decide the current case (as you)", ref y);
            Note("Same rules and payouts as the terminal; approve still needs a truck's cargo back aboard.", ref y, 40);
            switch (Buttons(ref y, "Approve", "Reject", "Arrest"))
            {
                case 0: tools.Run("approve"); break;
                case 1: tools.Run("reject"); break;
                case 2: tools.Run("arrest"); break;
            }
            Title("Holding cell", ref y);
            switch (Buttons(ref y, "Police visit now", "Release all prisoners"))
            {
                case 0: tools.Run("police"); break;
                case 1: tools.Run("releaseall"); break;
            }
            Note("Police visit now collects every prisoner (due or not) and settles bounties and fines.", ref y, 40);
        }
        void Day(DebugTools tools, ref float y)
        {
            Title("Clock", ref y);
            switch (Buttons(ref y, "+1 hour", "Jump to 3:55 PM"))
            {
                case 0: tools.Run("hour"); break;
                case 1: tools.Run("endsoon"); break;
            }
            Title("Day", ref y);
            switch (Buttons(ref y, "Close day now (payout)", "Start next day"))
            {
                case 0: tools.Run("closeday"); break;
                case 1: tools.Run("nextday"); break;
            }
            Note("Start next day skips the payout if the day was still open. Police due that day arrive at its start.", ref y, 40);
        }
        void Economy(DebugTools tools, ref float y)
        {
            Title("Your credits (banked)", ref y);
            int[] amounts = { 100, 1000, -100 };
            int pick = Buttons(ref y, "+100", "+1000", "-100"); if (pick >= 0) tools.Run("credits", amounts[pick]);
            switch (Buttons(ref y, "+50 today's pay", "Everyone +100", "Reset to start"))
            {
                case 0: tools.Run("pending", 50); break;
                case 1: tools.Run("creditsall", 100); break;
                case 2: tools.Run("resetcredits"); break;
            }
            Title("Items and upgrades (free)", ref y);
            var shop = tools.shop;
            if (shop != null)
                for (int i = 0; i < shop.offers.Length; i++)
                    if (shop.offers[i].kind == ShopOfferKind.Item && Button(ref y, "Spawn " + shop.offers[i].title)) tools.Run("spawnitem", i);
            switch (Buttons(ref y, "Unlock X-ray belt slot 2", "Toggle coffee speed"))
            {
                case 0: tools.Run("upgradebelt"); break;
                case 1: tools.Run("coffee"); break;
            }
        }
        void PlayerSection(DebugTools tools, ref float y)
        {
            Title("Movement", ref y);
            var motor = NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient?.PlayerObject != null ? NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<NetworkPlayerMotor>() : null;
            Note(motor != null ? $"Running now: {(motor.Running.Value ? "yes" : "no")}  ·  run speed ×{motor.runSpeed:F2}  ·  hold Shift + W to run" : "", ref y);
            if (Button(ref y, "Toggle always run")) tools.Run("alwaysrun");
            if (Button(ref y, "Next free suit colour (C)")) tools.Run("colour");
            Title("Teleport", ref y);
            for (int i = 0; i < tools.teleportPoints.Length; i++)
                if (tools.teleportPoints[i] != null && Button(ref y, tools.teleportPoints[i].name)) { tools.Run("teleport", i); }
            Note("Teleport points are editable children of 'Debug teleport points' in GameWorld.", ref y, 40);
        }
    }
}
