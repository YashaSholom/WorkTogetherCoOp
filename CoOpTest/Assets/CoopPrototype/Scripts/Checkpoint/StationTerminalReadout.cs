using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    /// <summary>Local screen presentation of the existing replicated checkpoint; never writes game state.</summary>
    public sealed class StationTerminalReadout : MonoBehaviour
    {
        public CheckpointSession session;
        public TextMesh display;
        [Min(.1f)] public float refreshSeconds = .25f;
        float nextRefresh;
        string previous;

        void OnEnable() { nextRefresh = 0; previous = null; }
        void Update()
        {
            if (display == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + refreshSeconds;
            string text = "SECTOR 07\nINSPECTION / STANDBY\n[E] ACCESS";
            if (session != null && session.IsSpawned)
            {
                string progress = session.AtDesk && session.Current != null
                    ? $"DOCUMENTS  {session.ScannedCount} / {session.Current.documents.Length}"
                    : "LANE CONTROL";
                text = $"SECTOR 07  /  DAY {session.Day}\n{session.Phase.Value.ToString().ToUpperInvariant()}\n{progress}  /  [E] ACCESS";
            }
            if (text == previous) return;
            previous = text;
            display.text = text;
        }
    }
}
