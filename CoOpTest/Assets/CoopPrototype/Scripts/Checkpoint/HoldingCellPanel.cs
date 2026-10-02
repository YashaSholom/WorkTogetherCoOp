using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoopPrototype.Checkpoint
{
    /// <summary>Local UI for the holding cell tablet. Releases go through <see cref="HoldingCellTablet.ReleaseRpc"/>.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class HoldingCellPanel : MonoBehaviour
    {
        public static HoldingCellPanel Instance { get; private set; }
        HoldingCellTablet tablet;
        string message = "";
        public bool IsOpen => tablet != null;
        void Awake() => Instance = this;
        void OnDisable() { Close(); if (Instance == this) Instance = null; }
        public void Show(HoldingCellTablet source)
        {
            if (!LocalGameplayModal.Open(this)) return;
            tablet = source;
            message = $"Justified arrests pay each prisoner's bounty when the Galactic Police collect them. Every prisoner found falsely imprisoned costs {source.cell.falseArrestFine} cr.";
        }
        public void Feedback(HoldingCellTablet source, string value) { if (tablet == source) message = value; }
        public void Close() { if (tablet == null) return; tablet = null; LocalGameplayModal.Close(this); }
        void Update()
        {
            if (!IsOpen) return;
            var manager = NetworkManager.Singleton;
            var player = manager != null && manager.LocalClient?.PlayerObject != null ? manager.LocalClient.PlayerObject.GetComponent<PlayerInteractor>() : null;
            if (player == null || !tablet.IsSpawned || NetworkSession.MenuVisible || !player.CanReach(tablet) ||
                (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)) Close();
        }
        void OnGUI()
        {
            if (!IsOpen || NetworkSession.MenuVisible) return;
            var cell = tablet.cell;
            InspectionGui.Begin();
            InspectionGui.Frame($"SECTOR 07  /  DETENTION  /  DAY {cell.Today}", "Holding cell");
            int next = cell.NextPoliceDay;
            InspectionGui.Text(new Rect(760, 150, 465, 30), $"{cell.Occupied} / {cell.Capacity} occupied", title: false);
            InspectionGui.Text(new Rect(760, 182, 465, 24), next < 0 ? "No Galactic Police visit scheduled" : $"Next Galactic Police visit: day {next}  ·  {HoldingCell.PickupText(next - cell.Today)}", caption: true);
            if (cell.Occupied == 0)
            {
                InspectionGui.Fill(new Rect(215, 240, 1010, 300), InspectionGui.Panel);
                InspectionGui.Text(new Rect(255, 330, 900, 50), "The cell is empty", title: true);
                InspectionGui.Text(new Rect(255, 392, 900, 80), "Arrest a visitor from the inspection terminal. Everyone travelling with them is held too.");
            }
            for (int i = 0; i < cell.Occupied && i < 6; i++)
            {
                var p = cell.Prisoners[i];
                var traveller = cell.TravellerOf(p);
                var row = new Rect(215, 232 + i * 76, 1010, 68);
                InspectionGui.Fill(row, InspectionGui.Panel);
                if (traveller != null && traveller.portrait != null) GUI.DrawTexture(new Rect(row.x + 8, row.y + 4, 60, 60), traveller.portrait, ScaleMode.ScaleToFit);
                string race = traveller != null && traveller.raceInfo != null ? traveller.raceInfo.displayName : traveller != null ? traveller.race : "";
                InspectionGui.Text(new Rect(row.x + 84, row.y + 8, 330, 28), traveller != null ? traveller.displayName : "Unknown prisoner");
                InspectionGui.Text(new Rect(row.x + 84, row.y + 38, 330, 24), race + (p.member > 0 ? "  ·  companion" : "  ·  visitor") + $"  ·  case {p.visit:000}", caption: true);
                InspectionGui.Text(new Rect(row.x + 390, row.y + 8, 230, 28), $"Bounty {p.bounty} cr");
                InspectionGui.Text(new Rect(row.x + 390, row.y + 38, 230, 24), $"Arrested day {p.arrestedDay} by {p.arresterName}", caption: true);
                InspectionGui.Text(new Rect(row.x + 630, row.y + 8, 200, 28), HoldingCell.PickupText(cell.DaysUntilPickup(p)), caption: false);
                InspectionGui.Text(new Rect(row.x + 630, row.y + 38, 200, 24), $"pickup on day {p.pickupDay}", caption: true);
                if (InspectionGui.Button(new Rect(row.xMax - 180, row.y + 10, 168, 48), "SET FREE", InspectionGui.Mint)) tablet.ReleaseRpc(p.id);
            }
            InspectionGui.Text(new Rect(215, 700, 760, 90), message, caption: true);
            if (InspectionGui.Button(new Rect(1005, 743, 215, 42), "Close  [Esc]")) Close();
            InspectionGui.End();
        }
    }
}
