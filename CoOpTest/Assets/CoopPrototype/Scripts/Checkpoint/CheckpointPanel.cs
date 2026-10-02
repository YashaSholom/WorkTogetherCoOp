using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoopPrototype.Checkpoint
{
    [DefaultExecutionOrder(-200)]
    public sealed class CheckpointPanel : MonoBehaviour
    {
        enum Tab { Case, Federations, Vehicles, Races }
        static readonly string[] TabNames = { "CASE", "FEDERATIONS", "VEHICLES", "RACES" };
        static readonly float[] TabWidths = { 116, 196, 156, 136 };
        static readonly Color Arrest = new(1, .72f, .28f);
        public static CheckpointPanel Instance { get; private set; }
        public CheckpointSession checkpoint;
        CheckpointEndpoint endpoint;
        int visit, selected;
        Tab tab;
        string message;
        public bool IsOpen => endpoint != null;
        void Awake() => Instance = this;
        public void Show(CheckpointEndpoint source, int currentVisit)
        {
            if (!LocalGameplayModal.Open(this)) return;
            endpoint = source; visit = currentVisit; selected = 0; tab = Tab.Case;
            message = source is TravellerInteractable ? checkpoint.Current.greeting : checkpoint.AtDesk ? "Ask the visitor for their papers: they go straight into the terminal's document slots and are scanned there." : "No visitor at the desk. Browse the codex tabs while you wait.";
        }
        /// <summary>Terminal tab: 0 case, 1 federations, 2 vehicles, 3 races.</summary>
        public void SelectTab(int index) => tab = (Tab)Mathf.Clamp(index, 0, TabNames.Length - 1);
        public void Feedback(CheckpointEndpoint source, string value) { if (endpoint == source) message = value; }
        public void Close() { endpoint = null; LocalGameplayModal.Close(this); }
        void OnDisable() { Close(); if (Instance == this) Instance = null; }
        void Update()
        {
            if (!IsOpen) return;
            var manager = NetworkManager.Singleton;
            var player = manager != null && manager.LocalClient?.PlayerObject != null ? manager.LocalClient.PlayerObject.GetComponent<PlayerInteractor>() : null;
            bool terminal = endpoint is CheckpointTerminal;
            // The terminal stays open between visitors (codex); it simply follows the current case.
            if (terminal && checkpoint.Visit.Value != visit) { visit = checkpoint.Visit.Value; selected = 0; message = "A new visitor is on the way."; }
            if (player == null || !endpoint.IsSpawned || (!terminal && (!checkpoint.AtDesk || checkpoint.Visit.Value != visit)) || NetworkSession.MenuVisible ||
                !player.CanReach(endpoint) || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)) Close();
        }
        void OnGUI()
        {
            if (checkpoint == null || !checkpoint.IsSpawned || NetworkSession.MenuVisible) return;
            InspectionGui.Begin();
            if (!IsOpen)
            {
                if (!LocalGameplayModal.IsOpen)
                {
                    InspectionGui.Fill(new Rect(990, 25, 420, 122), InspectionGui.Ink);
                    InspectionGui.Text(new Rect(1010, 38, 390, 24), "SECTOR 07  /  ARRIVALS CONTROL", caption: true);
                    InspectionGui.Text(new Rect(1010, 66, 390, 32), $"Visit {checkpoint.Visit.Value:000}  ·  {checkpoint.Phase.Value}");
                    var cell = checkpoint.holdingCell;
                    InspectionGui.Text(new Rect(1010, 105, 390, 25), $"Approved {checkpoint.ApprovedCount.Value}  /  Returned {checkpoint.RejectedCount.Value}  /  Arrested {checkpoint.ArrestedCount.Value}" +
                        (cell != null && cell.IsSpawned ? $"   ·   cell {cell.Occupied}/{cell.Capacity}" : ""), caption: true);
                }
                InspectionGui.End(); return;
            }
            var current = checkpoint.Current;
            InspectionGui.Frame($"SECTOR 07  /  CASE {visit:000}  /  {checkpoint.Date}", endpoint is TravellerInteractable ? "Arrival interview" : "Immigration terminal");
            if (endpoint is TravellerInteractable traveller) DrawInterview(traveller, current);
            else if (endpoint is CheckpointTerminal terminal)
            {
                float tabX = 588;
                for (int i = 0; i < TabNames.Length; i++)
                {
                    float width = TabWidths[i];
                    if (InspectionGui.Button(new Rect(tabX, 150, width, 46), TabNames[i], (int)tab == i ? InspectionGui.Mint : (Color?)null)) tab = (Tab)i;
                    tabX += width + 8;
                }
                switch (tab)
                {
                    case Tab.Case: DrawCase(terminal, current); break;
                    case Tab.Federations: DrawFederations(); break;
                    case Tab.Vehicles: DrawVehicles(); break;
                    case Tab.Races: DrawRaces(); break;
                }
            }
            if (InspectionGui.Button(new Rect(1005, 743, 215, 42), "Close  [Esc]")) Close();
            InspectionGui.End();
        }

        void DrawInterview(TravellerInteractable traveller, TravellerCase current)
        {
            InspectionGui.Fill(new Rect(215, 230, 280, 405), InspectionGui.Panel);
            if (current.traveller.portrait != null) GUI.DrawTexture(new Rect(235, 250, 240, 240), current.traveller.portrait, ScaleMode.ScaleToFit);
            InspectionGui.Text(new Rect(235, 505, 245, 70), current.traveller.displayName, title: true);
            InspectionGui.Text(new Rect(235, 580, 245, 26), current.traveller.vehicleRegistration, caption: true);
            if (current.CompanionCount > 0)
                InspectionGui.Text(new Rect(235, 604, 245, 26), "+ " + string.Join(", ", current.companions.Where(c => c != null).Select(c => c.displayName)), caption: true);
            InspectionGui.Text(new Rect(535, 240, 655, 120), message);
            if (InspectionGui.Button(new Rect(535, 370, 650, 58), "01   May I see your documents?", InspectionGui.Mint)) traveller.RequestPapersRpc(visit);
            if (InspectionGui.Button(new Rect(535, 438, 650, 58), "02   What is the purpose of your visit?")) message = current.purpose;
            if (InspectionGui.Button(new Rect(535, 506, 650, 58), "03   Tell me about your vehicle.")) message = current.vehicleAnswer;
            if (InspectionGui.Button(new Rect(535, 574, 650, 58), "04   Who is travelling with you?")) message = current.companionAnswer;
            InspectionGui.Text(new Rect(535, 650, 640, 70), "Requested papers go straight into the terminal's document slots. Take one out to inspect it closely or hand it to your crew.", caption: true);
        }

        void DrawCase(CheckpointTerminal terminal, TravellerCase current)
        {
            bool atDesk = checkpoint.AtDesk;
            if (!atDesk)
            {
                InspectionGui.Fill(new Rect(215, 230, 1010, 490), InspectionGui.Panel);
                InspectionGui.Text(new Rect(255, 340, 900, 50), checkpoint.Phase.Value == VisitPhase.Closed ? "The checkpoint is closed for the day" : "No visitor at the desk", title: true);
                InspectionGui.Text(new Rect(255, 400, 900, 90), message + "\nUse the FEDERATIONS, VEHICLES and RACES tabs to study seals, badges and species.");
                return;
            }
            InspectionGui.Text(new Rect(215, 229, 330, 40), $"{checkpoint.ScannedCount} / {current.documents.Length} documents scanned" + (current.PackageCount > 0 ? $"   ·   cargo {checkpoint.CargoAboard}/{current.PackageCount} on truck" : ""));
            // Compact list so more document types (receipts, permits...) still fit above the decision buttons.
            int count = current.documents.Length;
            float rowHeight = count <= 2 ? 60 : Mathf.Max(30, 180f / count);
            for (int i = 0; i < count; i++)
                if (InspectionGui.Button(new Rect(215, 281 + i * rowHeight, 330, rowHeight - 6), (checkpoint.IsScanned(i) ? "✓  " : "—  ") + current.documents[i].kind.displayName, selected == i ? InspectionGui.Mint : (Color?)null)) selected = i;
            InspectionGui.Text(new Rect(215, 287 + Mathf.Max(2, count) * rowHeight, 330, 515 - 287 - Mathf.Max(2, count) * rowHeight), message, caption: true);
            var cell = checkpoint.holdingCell;
            int people = 1 + current.CompanionCount;
            string cellText = cell != null && cell.IsSpawned ? $"Holding cell {cell.Occupied}/{cell.Capacity}" + (people > 1 ? $"  ·  this case holds {people}" : "") : "No holding cell";
            InspectionGui.Text(new Rect(215, 515, 330, 50), "Check photo, expiry, owner, plate, seal, badge and race against the codex tabs.", caption: true);
            InspectionGui.Text(new Rect(215, 570, 330, 26), cellText, caption: true);
            bool cargoOk = checkpoint.CargoReadyToLeave;
            bool cellOk = cell != null && cell.IsSpawned && cell.Free >= people;
            GUI.enabled = atDesk && cellOk;
            if (InspectionGui.Button(new Rect(215, 603, 330, 55), cellOk ? (people > 1 ? $"ARREST ALL {people}" : "ARREST") : cell == null ? "NO HOLDING CELL" : "HOLDING CELL FULL", Arrest)) terminal.ArrestRpc(visit);
            GUI.enabled = atDesk && cargoOk;
            if (InspectionGui.Button(new Rect(215, 670, 158, 55), cargoOk ? "APPROVE" : "LOAD CARGO", InspectionGui.Mint)) terminal.DecideRpc(visit, true);
            GUI.enabled = atDesk;
            if (InspectionGui.Button(new Rect(385, 670, 160, 55), "REJECT", new Color(1, .4f, .4f))) terminal.DecideRpc(visit, false);
            GUI.enabled = true;
            if (checkpoint.IsScanned(selected)) DocumentInspectionContent.DrawDocument(checkpoint, checkpoint.CaseIndex.Value, selected, new Rect(580, 230, 640, 490));
            else
            {
                InspectionGui.Fill(new Rect(580, 230, 640, 490), InspectionGui.Panel);
                InspectionGui.Text(new Rect(620, 365, 555, 110), "Awaiting physical document", title: true);
                InspectionGui.Text(new Rect(620, 470, 535, 100), checkpoint.Phase.Value == VisitPhase.Waiting ? "Ask the visitor for their papers. They arrive in the terminal's document slots." : "Put this paper back into a document slot to scan it.");
            }
        }

        // ------------------------------------------------------------------ codex
        CheckpointCodex Codex => checkpoint.codex;
        bool NoCodex()
        {
            if (Codex != null) return false;
            InspectionGui.Text(new Rect(255, 340, 900, 60), "No codex data is linked to this checkpoint.", title: true);
            return true;
        }
        void DrawFederations()
        {
            if (NoCodex()) return;
            var list = Codex.federations.Where(f => f != null).ToArray();
            for (int i = 0; i < list.Length && i < 6; i++)
            {
                var f = list[i];
                var card = new Rect(215 + (i % 2) * 515, 215 + (i / 2) * 156, 495, 146);
                InspectionGui.Fill(card, InspectionGui.Panel);
                InspectionGui.Fill(new Rect(card.x, card.y, 4, card.height), f.colour);
                if (f.emblem != null) GUI.DrawTexture(new Rect(card.x + 16, card.y + 18, 110, 110), f.emblem, ScaleMode.ScaleToFit);
                InspectionGui.Text(new Rect(card.x + 142, card.y + 12, 340, 28), f.displayName);
                InspectionGui.Text(new Rect(card.x + 142, card.y + 40, 340, 22), $"ID prefix {f.code}   ·   capital {f.capital}", caption: true);
                InspectionGui.Text(new Rect(card.x + 142, card.y + 64, 340, 80), f.description, caption: true);
            }
            InspectionGui.Text(new Rect(215, 690, 770, 50), "Every Galactic ID carries the seal of the federation named under ISSUED BY, and its ID number starts with that federation's prefix.", caption: true);
        }
        void DrawVehicles()
        {
            if (NoCodex()) return;
            var makers = Codex.manufacturers.Where(m => m != null).ToArray();
            for (int i = 0; i < makers.Length && i < 3; i++)
            {
                var m = makers[i];
                var card = new Rect(215 + i * 340, 215, 330, 150);
                InspectionGui.Fill(card, InspectionGui.Panel);
                if (m.emblem != null) GUI.DrawTexture(new Rect(card.x + 12, card.y + 14, 72, 72), m.emblem, ScaleMode.ScaleToFit);
                InspectionGui.Text(new Rect(card.x + 96, card.y + 14, 225, 28), m.displayName);
                InspectionGui.Text(new Rect(card.x + 96, card.y + 44, 225, 22), "Chassis prefix " + m.code, caption: true);
                InspectionGui.Text(new Rect(card.x + 12, card.y + 92, 310, 58), m.description, caption: true);
            }
            var models = Codex.vehicles.Where(v => v != null).ToArray();
            for (int i = 0; i < models.Length && i < 3; i++)
            {
                var v = models[i];
                var card = new Rect(215 + i * 340, 378, 330, 312);
                InspectionGui.Fill(card, InspectionGui.Panel);
                if (v.picture != null) GUI.DrawTexture(new Rect(card.x + 10, card.y + 10, 310, 175), v.picture, ScaleMode.ScaleAndCrop);
                if (v.manufacturer != null && v.manufacturer.emblem != null) GUI.DrawTexture(new Rect(card.x + 12, card.y + 196, 40, 40), v.manufacturer.emblem, ScaleMode.ScaleToFit);
                InspectionGui.Text(new Rect(card.x + 62, card.y + 194, 260, 26), v.modelName);
                InspectionGui.Text(new Rect(card.x + 62, card.y + 220, 260, 22), (v.manufacturer != null ? v.manufacturer.displayName : "Unknown maker") + "  ·  " + v.kind, caption: true);
                InspectionGui.Text(new Rect(card.x + 12, card.y + 246, 310, 66), v.description, caption: true);
            }
            InspectionGui.Text(new Rect(215, 700, 770, 40), "A registration shows the badge of the vehicle's maker. The badge must match the model's manufacturer.", caption: true);
        }
        void DrawRaces()
        {
            if (NoCodex()) return;
            var races = Codex.races.Where(r => r != null).ToArray();
            for (int i = 0; i < races.Length && i < 2; i++)
            {
                var r = races[i];
                var card = new Rect(215 + i * 515, 215, 495, 480);
                InspectionGui.Fill(card, InspectionGui.Panel);
                if (r.portrait != null) GUI.DrawTexture(new Rect(card.x + 16, card.y + 16, 200, 200), r.portrait, ScaleMode.ScaleToFit);
                InspectionGui.Text(new Rect(card.x + 232, card.y + 22, 250, 40), r.displayName, title: true);
                InspectionGui.Text(new Rect(card.x + 232, card.y + 70, 250, 24), "Homeworld: " + r.homeworld, caption: true);
                InspectionGui.Text(new Rect(card.x + 232, card.y + 100, 250, 116), r.description, caption: true);
                InspectionGui.Text(new Rect(card.x + 16, card.y + 232, 460, 26), "IDENTIFYING FEATURES", caption: true);
                InspectionGui.Text(new Rect(card.x + 16, card.y + 260, 460, 210), r.identifyingFeatures);
            }
            InspectionGui.Text(new Rect(215, 702, 770, 40), "The race printed on an ID must match the traveller in the photo and at the window.", caption: true);
        }
    }
}
