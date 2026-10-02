using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    public enum ScanState { Idle, Conveying, Scanning, Exiting, Clear, Contraband }

    /// <summary>
    /// Physics conveyor X-ray scanner. Crates are placed in the load slot(s); SCAN releases them onto the belt one at a time
    /// (nearest the tunnel first). The belt pushes the loose crate into the X-ray tunnel, stops while a camera that only draws
    /// the X-ray layer shows its contents on the monitor, then carries it out and drops it into the catch bin at the far end.
    /// The server simulates the crates; the result is VALID or CONTRABAND DETECTED for the whole run.
    /// Upgrade path: add ItemSockets to <see cref="slots"/> (a shop upgrade unlocks them) and wire their Occupancy Changed to
    /// <see cref="OnSlotsChanged"/>.
    /// </summary>
    public sealed class ContrabandScanner : NetworkBehaviour
    {
        public ItemSocket[] slots;
        public CheckpointSession checkpoint;
        [Tooltip("How many of the slots work before any upgrade is bought.")]
        [Min(1)] public int initialSlots = 1;
        public NetworkVariable<int> UnlockedSlots = new(1);
        [Header("Belt (in the station frame; the belt runs along -X from the load end)")]
        [Tooltip("The station root. Belt positions below are in its local space.")]
        public Transform frame;
        public Vector3 beltAxis = Vector3.left;
        [Tooltip("Local X of the X-ray centre and of the belt's far end.")]
        public float scanX = 0, beltEndX = -2.45f;
        [Tooltip("Box above the belt surface in which loose items are driven (local space).")]
        public Vector3 beltZoneCentre = new(-.35f, 1.25f, 0), beltZoneSize = new(4.2f, .6f, .72f);
        [Min(.1f)] public float beltSpeed = .9f;
        [Min(.2f)] public float scanSeconds = 1.6f;
        [Tooltip("Give up on a crate that does not arrive/leave within this time (stuck or removed).")]
        [Min(1)] public float stepTimeout = 8;
        [Tooltip("Belt slats that slide while the belt runs, wrapping between the two ends.")]
        public Transform[] slats;
        public float slatStart = 1.7f, slatEnd = -2.4f;
        public Transform[] rollers;
        [Header("X-ray")]
        [Tooltip("Draws only the X-ray layer. Its image is frozen on the screen while a crate leaves and with the result.")]
        public Camera xrayCamera;
        public Renderer screen;
        public Vector2Int imageSize = new(384, 288);
        public Transform beam;
        public float beamTravel = .38f;
        [Header("Status")]
        public TextMesh display;
        public Renderer statusLight;
        public Color idleColour = new(.6f, .8f, 1), scanningColour = new(1, .8f, .25f), clearColour = new(.35f, 1, .5f), contrabandColour = new(1, .3f, .3f);
        public NetworkVariable<ScanState> State = new(ScanState.Idle);
        public NetworkVariable<double> StateStarted = new();
        /// <summary>Position of the crate being processed in this run, the run's size, and whether the current/any crate was contraband.</summary>
        public NetworkVariable<int> RunIndex = new(), RunCount = new();
        public NetworkVariable<bool> CurrentContraband = new(), RunContraband = new();
        MaterialPropertyBlock block, screenBlock;
        RenderTexture image;
        Vector3 beamOrigin;
        float slatPhase;
        ulong presser = ulong.MaxValue;
        readonly Queue<ItemSocket> queue = new();
        PickupItem current;
        readonly Collider[] overlap = new Collider[16];

        public RenderTexture Image => image;
        public bool CanUpgrade => UnlockedSlots.Value < slots.Length;
        public int OccupiedCount
        {
            get
            {
                int count = 0;
                if (slots != null) foreach (var slot in slots) if (slot != null && slot.IsSpawned && slot.Available.Value && slot.Occupant.Value != ulong.MaxValue) count++;
                return count;
            }
        }
        double Elapsed => IsSpawned ? NetworkManager.ServerTime.Time - StateStarted.Value : 0;
        bool Result => State.Value == ScanState.Clear || State.Value == ScanState.Contraband;
        /// <summary>True while a run is in progress.</summary>
        public bool Busy => State.Value == ScanState.Conveying || State.Value == ScanState.Scanning || State.Value == ScanState.Exiting;
        bool BeltRunning => State.Value == ScanState.Conveying || State.Value == ScanState.Exiting;
        public string ButtonPrompt => Busy ? "Scanner running..." : OccupiedCount == 0 ? "Put a crate in the slot to scan" : "Scan";
        Transform Frame => frame != null ? frame : transform.parent;

        void Awake()
        {
            if (beam != null) beamOrigin = beam.localPosition;
            if (xrayCamera != null)
            {
                image = new RenderTexture(imageSize.x, imageSize.y, 16) { name = "X-ray image" };
                xrayCamera.targetTexture = image;
                screenBlock = new MaterialPropertyBlock();
                screenBlock.SetTexture("_BaseMap", image); screenBlock.SetColor("_BaseColor", Color.white);
                if (screen != null) screen.SetPropertyBlock(screenBlock);
            }
        }
        public override void OnDestroy()
        {
            if (image != null) { if (xrayCamera != null) xrayCamera.targetTexture = null; image.Release(); Destroy(image); }
            base.OnDestroy();
        }
        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            Set(ScanState.Idle);
            UnlockedSlots.Value = Mathf.Clamp(initialSlots, 1, slots.Length);
            ApplyUnlocks();
        }
        void ApplyUnlocks() { for (int i = 0; i < slots.Length; i++) if (slots[i] != null && slots[i].IsSpawned) slots[i].Available.Value = i < UnlockedSlots.Value; }
        /// <summary>Server. Shop upgrade: one more load slot.</summary>
        public bool TryUpgrade()
        {
            if (!IsServer || !CanUpgrade || Busy) return false;
            UnlockedSlots.Value++; ApplyUnlocks();
            Debug.Log($"[Checkpoint] X-ray belt upgraded to {UnlockedSlots.Value} slot(s)");
            return true;
        }

        /// <summary>Server only. Called by the scan button.</summary>
        public bool TryStartScan(PlayerInteractor player, out string reason)
        {
            reason = "";
            if (!IsServer) { reason = "Server only"; return false; }
            if (Busy) { reason = "The scanner is already running"; return false; }
            if (OccupiedCount == 0) { reason = "Put a crate in the slot first"; return false; }
            presser = player != null ? player.NetworkObjectId : ulong.MaxValue;
            queue.Clear();
            // One at a time, the crate nearest the tunnel first (the others would be in its way).
            foreach (var slot in slots.Where(s => s != null && s.Available.Value && s.Occupant.Value != ulong.MaxValue).OrderBy(s => Local(s.snapPoint.position).x)) queue.Enqueue(slot);
            RunCount.Value = queue.Count; RunIndex.Value = 0; RunContraband.Value = false; CurrentContraband.Value = false;
            Debug.Log($"[Checkpoint] X-ray run started ({queue.Count} crate(s))");
            Next();
            return true;
        }
        /// <summary>Server only. Wired to each slot's Occupancy Changed event: loading a crate after a run clears the old result.</summary>
        public void OnSlotsChanged(bool occupied)
        {
            if (!IsServer || !IsSpawned || Busy) return;
            if (occupied || OccupiedCount == 0) Set(ScanState.Idle);
        }

        void Set(ScanState state) { StateStarted.Value = NetworkManager.ServerTime.Time; State.Value = state; }
        Vector3 Local(Vector3 world) => Frame.InverseTransformPoint(world);

        // Release the next queued crate onto the belt, or finish the run.
        void Next()
        {
            if (current != null) current.pickupLocked = false;
            current = null;
            while (queue.Count > 0 && current == null)
            {
                var slot = queue.Dequeue();
                if (slot == null || slot.Occupant.Value == ulong.MaxValue || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(slot.Occupant.Value, out var obj)) continue;
                var item = obj.GetComponent<PickupItem>();
                if (item == null) continue;
                slot.ReleaseToWorld(item); // becomes a physics object resting on the belt
                item.pickupLocked = true;
                current = item;
            }
            if (current != null) { RunIndex.Value++; Set(ScanState.Conveying); return; }
            Set(RunContraband.Value ? ScanState.Contraband : ScanState.Clear);
            Debug.Log("[Checkpoint] X-ray run result: " + (RunContraband.Value ? "CONTRABAND DETECTED" : "VALID"));
        }

        void Update()
        {
            if (!IsSpawned) return;
            if (IsServer) Step();
            bool busy = Busy;
            foreach (var slot in slots) if (slot != null) slot.removalLocked = busy;
            Present();
        }

        void Step()
        {
            bool lost = current == null || !current.IsSpawned || current.Holder.Value != ulong.MaxValue;
            switch (State.Value)
            {
                case ScanState.Conveying:
                    if (lost || Elapsed > stepTimeout) { Next(); break; }
                    if (Local(current.transform.position).x <= scanX + .02f) { StopBelt(); Set(ScanState.Scanning); }
                    break;
                case ScanState.Scanning:
                    if (lost) { Next(); break; }
                    if (Elapsed >= scanSeconds)
                    {
                        bool found = current is CargoPackage package && package.IsContraband;
                        CurrentContraband.Value = found; if (found) RunContraband.Value = true;
                        if (checkpoint != null && current is CargoPackage scanned) checkpoint.RecordPackageScan(scanned, presser);
                        current.pickupLocked = false; // it can be grabbed as it leaves
                        Set(ScanState.Exiting);
                    }
                    break;
                case ScanState.Exiting:
                    if (lost || Elapsed > stepTimeout || Local(current.transform.position).x < beltEndX - .15f) Next();
                    break;
            }
        }

        // Server physics: the running belt drives loose items lying on it; a stopped belt holds them still.
        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            var f = Frame;
            int count = Physics.OverlapBoxNonAlloc(f.TransformPoint(beltZoneCentre), Vector3.Scale(beltZoneSize, f.lossyScale) * .5f, overlap, f.rotation, ~0, QueryTriggerInteraction.Ignore);
            var direction = f.TransformDirection(beltAxis.normalized);
            bool running = BeltRunning;
            for (int i = 0; i < count; i++)
            {
                var body = overlap[i].attachedRigidbody;
                if (body == null || body.isKinematic || body.GetComponent<PickupItem>() == null) continue;
                // Past the belt end the crate is falling into the bin: leave it to physics.
                if (Local(body.worldCenterOfMass).x < beltEndX) continue;
                var v = body.linearVelocity;
                var along = running ? direction * beltSpeed : Vector3.zero;
                body.linearVelocity = new Vector3(along.x, v.y, along.z);
                if (!running) body.angularVelocity = Vector3.zero;
            }
        }
        void StopBelt()
        {
            if (current != null && current.TryGetComponent<Rigidbody>(out var body) && !body.isKinematic) { var v = body.linearVelocity; body.linearVelocity = new Vector3(0, v.y, 0); }
        }

        void Present()
        {
            var state = State.Value;
            if (BeltRunning)
            {
                slatPhase += beltSpeed * Time.deltaTime;
                if (rollers != null) foreach (var roller in rollers) if (roller != null) roller.Rotate(Vector3.up, beltSpeed * Time.deltaTime * 360 / (Mathf.PI * .16f), Space.Self);
            }
            if (slats != null && slats.Length > 0)
            {
                float length = Mathf.Abs(slatStart - slatEnd), spacing = length / slats.Length;
                for (int i = 0; i < slats.Length; i++)
                {
                    if (slats[i] == null) continue;
                    float d = Mathf.Repeat(i * spacing + slatPhase, length);
                    var p = slats[i].localPosition; p.x = slatStart + Mathf.Sign(slatEnd - slatStart) * d; slats[i].localPosition = p;
                }
            }
            string count = RunCount.Value > 1 ? $"  {RunIndex.Value}/{RunCount.Value}" : "";
            string text; Color colour;
            switch (state)
            {
                case ScanState.Conveying: text = "LOADING" + count; colour = scanningColour; break;
                case ScanState.Scanning: text = "X-RAY SCANNING" + count; colour = scanningColour; break;
                case ScanState.Exiting: text = (CurrentContraband.Value ? "CONTRABAND" : "CLEAR") + count; colour = CurrentContraband.Value ? contrabandColour : clearColour; break;
                case ScanState.Clear: text = "VALID"; colour = clearColour; break;
                case ScanState.Contraband: text = "CONTRABAND DETECTED"; colour = contrabandColour; break;
                default: text = OccupiedCount == 0 ? "PLACE CRATE IN SLOT" : "READY  -  PRESS SCAN"; colour = idleColour; break;
            }
            if (display != null) { display.text = text; display.color = colour; }
            if (statusLight != null)
            {
                block ??= new MaterialPropertyBlock();
                block.SetColor("_BaseColor", colour * .6f); block.SetColor("_EmissionColor", colour * 2f);
                statusLight.SetPropertyBlock(block);
            }
            if (beam != null)
            {
                bool scanning = state == ScanState.Scanning;
                if (beam.gameObject.activeSelf != scanning) beam.gameObject.SetActive(scanning);
                if (scanning) beam.localPosition = beamOrigin + beltAxis.normalized * (Mathf.Sin((float)Elapsed / scanSeconds * Mathf.PI * 4) * beamTravel);
            }
            // Live image while idle/loading/scanning; the last scanned crate stays on screen while it leaves and with the result.
            if (xrayCamera != null) xrayCamera.enabled = state == ScanState.Idle || state == ScanState.Conveying || state == ScanState.Scanning;
        }
    }
}
