using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    /// <summary>Clock, credits, case verdicts and the end-of-day summary. Presentation only.</summary>
    public sealed class ShiftHud : MonoBehaviour
    {
        public CheckpointSession checkpoint;
        public WorkShift shift;
        [Min(1)] public float verdictSeconds = 9;
        static readonly Color Gain = new(.45f, 1, .6f), Loss = new(1, .45f, .4f);
        void OnGUI()
        {
            if (checkpoint == null || !checkpoint.IsSpawned || NetworkSession.MenuVisible || LocalGameplayModal.IsOpen) return;
            var wallet = PlayerWallet.Local;
            var local = NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient?.PlayerObject != null ? NetworkManager.Singleton.LocalClient.PlayerObject : null;
            InspectionGui.Begin();
            // Top centre: time and money.
            InspectionGui.Fill(new Rect(540, 20, 360, 92), InspectionGui.Ink);
            InspectionGui.Fill(new Rect(540, 20, 360, 3), shift == null || shift.IsOpen ? InspectionGui.Mint : Loss);
            if (shift != null) InspectionGui.Text(new Rect(560, 30, 330, 32), $"DAY {shift.Day.Value}   ·   {shift.TimeText}", title: false);
            if (wallet != null) InspectionGui.Text(new Rect(560, 62, 330, 24), $"{wallet.Credits.Value} cr   ·   today {(wallet.Pending.Value >= 0 ? "+" : "")}{wallet.Pending.Value}", caption: true);
            var motor = local != null ? local.GetComponent<NetworkPlayerMotor>() : null;
            string status = shift != null && !shift.IsOpen && !shift.Closed.Value ? "4 PM: no more arrivals. Clear the last visitor." : motor != null && motor.Caffeinated ? "Coffee: moving faster today" : "";
            if (status.Length > 0) InspectionGui.Text(new Rect(560, 86, 340, 22), status, caption: true);
            // Case verdict, a few seconds after the decision.
            if (Time.unscaledTime - checkpoint.ReportReceivedAt < verdictSeconds && checkpoint.ReportTitle.Length > 0)
                DrawReport(new Rect(330, 128, 780, 0), checkpoint.ReportTitle, checkpoint.Report, local != null ? local.NetworkObjectId : ulong.MaxValue, "No credits changed hands for this case.");
            // End of day summary until the next day starts.
            if (checkpoint.Phase.Value == VisitPhase.Closed && checkpoint.SummaryTitle.Length > 0)
                DrawReport(new Rect(330, 320, 780, 0), checkpoint.SummaryTitle, checkpoint.Summary, local != null ? local.NetworkObjectId : ulong.MaxValue,
                    "", "Shop at the supply terminal, then press START NEXT DAY on the inspection terminal.");
            InspectionGui.End();
        }
        static void DrawReport(Rect area, string title, System.Collections.Generic.List<ReportLine> lines, ulong me, string empty, string footer = "")
        {
            float height = 64 + Mathf.Max(1, lines.Count) * 28 + (footer.Length > 0 ? 34 : 0);
            area.height = height;
            InspectionGui.Fill(area, new Color(.03f, .05f, .09f, .94f));
            InspectionGui.Fill(new Rect(area.x, area.y, area.width, 3), InspectionGui.Mint);
            InspectionGui.Text(new Rect(area.x + 18, area.y + 12, area.width - 36, 44), title);
            float y = area.y + 56;
            if (lines.Count == 0 && empty.Length > 0) { InspectionGui.Text(new Rect(area.x + 18, y, area.width - 36, 26), empty, caption: true); y += 28; }
            foreach (var line in lines.OrderByDescending(l => l.player == me))
            {
                var old = GUI.color; GUI.color = line.amount >= 0 ? Gain : Loss;
                InspectionGui.Text(new Rect(area.x + 18, y, 90, 26), (line.amount >= 0 ? "+" : "") + line.amount);
                GUI.color = old;
                InspectionGui.Text(new Rect(area.x + 110, y + 3, area.width - 130, 26), (line.player == me ? "YOU" : line.name) + "  ·  " + line.reason, caption: line.player != me);
                y += 28;
            }
            if (footer.Length > 0) InspectionGui.Text(new Rect(area.x + 18, y + 6, area.width - 36, 26), footer, caption: true);
        }
    }
}
