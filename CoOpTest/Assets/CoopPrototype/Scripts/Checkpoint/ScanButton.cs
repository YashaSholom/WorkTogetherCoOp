using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    /// <summary>The physical Scan button on a contraband scanner. Interaction is validated by PlayerInteractor (reach, obstruction); the station owns the rules.</summary>
    public sealed class ScanButton : NetworkInteractable
    {
        public ContrabandScanner station;
        [Tooltip("Visual cap pushed down while a scan runs.")]
        public Transform cap;
        public float pressDepth = .025f;
        Vector3 capOrigin;
        void Awake() { if (cap != null) capOrigin = cap.localPosition; }
        public override string Prompt => station != null ? station.ButtonPrompt : "Scan";
        public override bool TryInteract(PlayerInteractor player, out string reason)
        {
            reason = "Scanner unavailable";
            return station != null && station.TryStartScan(player, out reason);
        }
        void Update()
        {
            if (cap == null || station == null || !station.IsSpawned) return;
            cap.localPosition = capOrigin + Vector3.down * (station.State.Value == ScanState.Scanning ? pressDepth : 0);
        }
    }
}
