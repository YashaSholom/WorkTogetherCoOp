using Unity.Netcode;
using UnityEngine;

namespace CoopPrototype
{
    [RequireComponent(typeof(Rigidbody))]
    public class PickupItem : NetworkInteractable
    {
        public ItemDefinition definition;
        public NetworkVariable<ulong> Holder = new(ulong.MaxValue);
        public NetworkVariable<ulong> Socket = new(ulong.MaxValue);
        Rigidbody body;
        Collider shape;
        float releaseRadius;
        /// <summary>Server only: the last player object that carried this item (used to credit work such as tray scans).</summary>
        [System.NonSerialized] public ulong LastHolder = ulong.MaxValue;
        /// <summary>Server only: set by machines (e.g. while on the X-ray belt) to stop players grabbing the item.</summary>
        [System.NonSerialized] public bool pickupLocked;
        Renderer[] renderers; bool[] rendererEnabled; Canvas[] canvases;
        bool hiddenAsStowed;
        public virtual string DisplayName => definition != null ? definition.displayName : name;
        public override string Prompt => "Pick up " + DisplayName;
        /// <summary>Whether left click does something while this item is in the hands.</summary>
        public virtual bool CanUse => false;
        public virtual string UseVerb => "use";
        /// <summary>Server. Left click with this item in the active slot.</summary>
        public virtual bool TryUse(PlayerInteractor player, out string reason) { reason = "Nothing to use"; return false; }
        public override void OnNetworkSpawn()
        {
            body = GetComponent<Rigidbody>();
            shape = GetComponent<Collider>();
            releaseRadius = Mathf.Max(.1f, shape.bounds.extents.magnitude);
            renderers = GetComponentsInChildren<Renderer>(true); rendererEnabled = System.Array.ConvertAll(renderers, r => r.enabled);
            canvases = GetComponentsInChildren<Canvas>(true);
            Holder.OnValueChanged += Changed;
            Socket.OnValueChanged += Changed;
            ApplyPhysics();
        }
        public override void OnNetworkDespawn()
        {
            Holder.OnValueChanged -= Changed;
            Socket.OnValueChanged -= Changed;
        }
        void Changed(ulong previous, ulong current) => ApplyPhysics();
        void ApplyPhysics()
        {
            bool attached = Holder.Value != ulong.MaxValue || Socket.Value != ulong.MaxValue;
            body.isKinematic = !IsServer || attached;
            shape.enabled = Holder.Value == ulong.MaxValue;
        }
        public override bool TryInteract(PlayerInteractor player, out string reason)
        {
            reason = player.HasFreeSlot ? "Already held or locked" : "All three slots are full";
            if (pickupLocked) { reason = "It is on the scanner belt"; return false; }
            if (!IsServer || Holder.Value != ulong.MaxValue || !player.HasFreeSlot) return false;
            if (Socket.Value != ulong.MaxValue)
            {
                if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(Socket.Value, out var obj) || !obj.GetComponent<ItemSocket>().TryRemove(this)) return false;
            }
            Holder.Value = player.NetworkObjectId;
            player.Store(this);
            LastHolder = player.NetworkObjectId;
            ApplyPhysics();
            Debug.Log($"[Coop] Picked up {name} by {player.OwnerClientId}");
            reason = "";
            return true;
        }
        /// <summary>Server. Seat a free item in a socket (the socket has already recorded it as its occupant).</summary>
        public void Seat(ItemSocket socket)
        {
            if (!IsServer) return;
            Socket.Value = socket.NetworkObjectId;
            transform.SetPositionAndRotation(socket.SnapPositionFor(this), socket.snapPoint.rotation);
            ApplyPhysics();
        }
        /// <summary>Server. Leave a socket as a free physics object at the current pose.</summary>
        public void Unseat()
        {
            if (!IsServer) return;
            Socket.Value = ulong.MaxValue;
            ApplyPhysics();
            body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
        }
        public void Place(PlayerInteractor player, ItemSocket socket)
        {
            if (!IsServer) return;
            player.Release(this);
            Socket.Value = socket.NetworkObjectId;
            Holder.Value = ulong.MaxValue;
            transform.SetPositionAndRotation(socket.SnapPositionFor(this), socket.snapPoint.rotation);
            ApplyPhysics();
            Debug.Log($"[Coop] Placed {name} in {socket.name}");
        }
        public void Drop(PlayerInteractor player) => Drop(player, player.Motor.HeldItemPosition, null);
        /// <summary>Release the item at a world position (and optionally rotation), e.g. exactly where the owner sees it in their hands.</summary>
        public void Drop(PlayerInteractor player, Vector3 target, Quaternion? rotation)
        {
            if (!IsServer || Holder.Value != player.NetworkObjectId) return;
            var origin = player.transform.position + Vector3.up * .08f;
            var offset = target - origin;
            float distance = offset.magnitude;
            // A carried item may overlap a wall because its collider is disabled. Release on the near side.
            foreach (var hit in Physics.SphereCastAll(origin, releaseRadius, offset.normalized, distance, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(player.transform) && !hit.collider.transform.IsChildOf(transform))
                    distance = Mathf.Min(distance, Mathf.Max(.05f, hit.distance - .03f));
            transform.position = origin + offset.normalized * distance;
            if (rotation.HasValue) transform.rotation = rotation.Value;
            Holder.Value = ulong.MaxValue;
            player.Release(this);
            ApplyPhysics();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            Debug.Log($"[Coop] Dropped {name}");
        }
        public void Throw(PlayerInteractor player, Vector3 velocity, float spin)
        {
            if (!IsServer || Holder.Value != player.NetworkObjectId) return;
            Drop(player);
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = velocity;
            body.angularVelocity = player.transform.right * spin;
            Debug.Log($"[Coop] Thrown {name} by {player.OwnerClientId}");
        }
        protected PlayerInteractor HolderPlayer => Holder.Value != ulong.MaxValue && IsSpawned && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(Holder.Value, out var obj) ? obj.GetComponent<PlayerInteractor>() : null;
        /// <summary>Carried but not in the holder's active slot: hidden and parked in the holder's body.</summary>
        public bool IsStowed { get { var holder = HolderPlayer; return holder != null && holder.IsStowed(this); } }
        protected virtual void Update()
        {
            if (!IsSpawned || renderers == null) return;
            bool hide = IsStowed;
            if (hide == hiddenAsStowed) return;
            hiddenAsStowed = hide;
            for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = !hide && rendererEnabled[i];
            foreach (var canvas in canvases) if (canvas != null) canvas.enabled = !hide;
        }
        void LateUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            if (Holder.Value != ulong.MaxValue)
            {
                if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(Holder.Value, out var obj))
                {
                    var holder = obj.GetComponent<NetworkPlayerMotor>();
                    if (obj.GetComponent<PlayerInteractor>().IsStowed(this)) transform.SetPositionAndRotation(holder.transform.position + Vector3.up * .9f, holder.transform.rotation);
                    else transform.SetPositionAndRotation(holder.HeldItemPosition, HeldRotation(holder));
                }
                else { Holder.Value = ulong.MaxValue; ApplyPhysics(); }
            }
            else if (Socket.Value != ulong.MaxValue && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(Socket.Value, out var obj))
            {
                var slot = obj.GetComponent<ItemSocket>();
                transform.SetPositionAndRotation(slot.SnapPositionFor(this), slot.snapPoint.rotation);
            }
            else if (transform.position.y < -10) { transform.position = new Vector3(0, 2, 0); body.linearVelocity = Vector3.zero; }
        }
        protected virtual Quaternion HeldRotation(NetworkPlayerMotor holder) => holder.HeldItemRotation;
        /// <summary>Orientation of the item relative to the camera while carried in first person and while inspecting (local presentation only).</summary>
        public virtual Quaternion ViewRotation => Quaternion.identity;
        /// <summary>Size multiplier for the item's visuals while carried in first person (inspection fits the item to the view instead).</summary>
        public virtual float ViewScale => 1f;
    }
}
