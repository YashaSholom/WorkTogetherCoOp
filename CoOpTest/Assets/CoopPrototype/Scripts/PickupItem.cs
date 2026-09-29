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
        public override string Prompt => "Pick up " + (definition != null ? definition.displayName : name);
        public override void OnNetworkSpawn()
        {
            body = GetComponent<Rigidbody>();
            shape = GetComponent<Collider>();
            releaseRadius = Mathf.Max(.1f, shape.bounds.extents.magnitude);
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
            reason = "Already held, locked, or your hands are full";
            if (!IsServer || Holder.Value != ulong.MaxValue || player.Held != null) return false;
            if (Socket.Value != ulong.MaxValue)
            {
                if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(Socket.Value, out var obj) || !obj.GetComponent<ItemSocket>().TryRemove(this)) return false;
            }
            Holder.Value = player.NetworkObjectId;
            player.HeldItem.Value = NetworkObjectId;
            ApplyPhysics();
            Debug.Log($"[Coop] Picked up {name} by {player.OwnerClientId}");
            reason = "";
            return true;
        }
        public void Place(PlayerInteractor player, ItemSocket socket)
        {
            if (!IsServer) return;
            player.HeldItem.Value = ulong.MaxValue;
            Socket.Value = socket.NetworkObjectId;
            Holder.Value = ulong.MaxValue;
            transform.SetPositionAndRotation(socket.snapPoint.position, socket.snapPoint.rotation);
            ApplyPhysics();
            Debug.Log($"[Coop] Placed {name} in {socket.name}");
        }
        public void Drop(PlayerInteractor player)
        {
            if (!IsServer || Holder.Value != player.NetworkObjectId) return;
            var origin = player.transform.position + Vector3.up * .08f;
            var offset = player.Motor.holdPoint.position - origin;
            float distance = offset.magnitude;
            // A carried item may overlap a wall because its collider is disabled. Release on the near side.
            foreach (var hit in Physics.SphereCastAll(origin, releaseRadius, offset.normalized, distance, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(player.transform) && !hit.collider.transform.IsChildOf(transform))
                    distance = Mathf.Min(distance, Mathf.Max(.05f, hit.distance - .03f));
            transform.position = origin + offset.normalized * distance;
            Holder.Value = ulong.MaxValue;
            player.HeldItem.Value = ulong.MaxValue;
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
        void LateUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            if (Holder.Value != ulong.MaxValue)
            {
                if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(Holder.Value, out var obj))
                {
                    var anchor = obj.GetComponent<NetworkPlayerMotor>().holdPoint;
                    transform.SetPositionAndRotation(anchor.position, anchor.rotation);
                }
                else { Holder.Value = ulong.MaxValue; ApplyPhysics(); }
            }
            else if (Socket.Value != ulong.MaxValue && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(Socket.Value, out var obj))
            {
                var anchor = obj.GetComponent<ItemSocket>().snapPoint;
                transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            }
            else if (transform.position.y < -10) { transform.position = new Vector3(0, 2, 0); body.linearVelocity = Vector3.zero; }
        }
    }
}
