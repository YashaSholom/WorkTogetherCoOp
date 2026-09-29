using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoopPrototype
{
    public class PlayerInteractor : NetworkBehaviour
    {
        [Min(1)] public float reach = 3;
        public LayerMask interactionMask = ~0;
        public NetworkVariable<ulong> HeldItem = new(ulong.MaxValue);
        public NetworkPlayerMotor Motor { get; private set; }
        NetworkInteractable target;
        float nextRequest;
        string feedback = "";
        PlayerThrow throwing;
        public override void OnNetworkSpawn()
        {
            Motor = GetComponent<NetworkPlayerMotor>();
            throwing = GetComponent<PlayerThrow>();
        }
        public PickupItem Held => HeldItem.Value != ulong.MaxValue && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(HeldItem.Value, out var obj) ? obj.GetComponent<PickupItem>() : null;
        void Update()
        {
            if (!IsSpawned || !IsOwner || !IsClient) return;
            target = null;
            var camera = Motor.playerCamera;
            float nearest = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(camera.transform.position, camera.transform.forward, reach + Motor.CameraDistance, interactionMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || (Held != null && hit.collider.transform.IsChildOf(Held.transform))) continue;
                if (hit.distance >= nearest) continue;
                nearest = hit.distance;
                target = hit.collider.GetComponentInParent<NetworkInteractable>();
            }
            if (Keyboard.current == null || Cursor.lockState != CursorLockMode.Locked) return;
            if (Keyboard.current.eKey.wasPressedThisFrame && target != null) RequestInteractionRpc(new NetworkObjectReference(target.NetworkObject));
            if (Keyboard.current.qKey.wasPressedThisFrame) RequestDropRpc();
        }
        // Ownership is required by default: a client can only invoke requests on its own player.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestInteractionRpc(NetworkObjectReference reference)
        {
            if (throwing != null && throwing.IsBusy) return;
            if (Time.unscaledTime < nextRequest) return;
            nextRequest = Time.unscaledTime + .15f;
            if (!reference.TryGet(out var obj) || !obj.TryGetComponent<NetworkInteractable>(out var interactable)) return;
            Debug.Log($"[Coop] Interaction requested: {OwnerClientId} -> {obj.name}");
            string reason;
            if (!CanReach(interactable)) reason = "Out of reach or obstructed";
            else if (interactable.TryInteract(this, out reason))
            {
                Debug.Log($"[Coop] Interaction accepted: {obj.name}");
                FeedbackRpc("Accepted");
                return;
            }
            Debug.Log($"[Coop] Interaction rejected: {reason}");
            FeedbackRpc(reason);
        }
        public bool CanReach(NetworkInteractable targetObject)
        {
            var collider = targetObject.GetComponent<Collider>();
            if (collider == null) return false;
            var point = collider.ClosestPoint(Motor.Eye);
            if (Vector3.Distance(Motor.Eye, point) > reach + .25f) return false;
            var direction = collider.bounds.center - Motor.Eye;
            foreach (var hit in Physics.RaycastAll(Motor.Eye, direction.normalized, direction.magnitude, interactionMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (Held != null && hit.collider.transform.IsChildOf(Held.transform)) continue;
                if (hit.collider.GetComponentInParent<NetworkInteractable>() != targetObject) return false;
            }
            return true;
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestDropRpc()
        {
            if (throwing != null && throwing.IsBusy) return;
            if (Time.unscaledTime < nextRequest) return;
            nextRequest = Time.unscaledTime + .15f;
            if (Held != null) Held.Drop(this);
        }
        [Rpc(SendTo.Owner)] void FeedbackRpc(string text) => feedback = text;
        public override void OnNetworkDespawn()
        {
            if (IsServer && Held != null) Held.Drop(this);
            if (IsOwner) Cursor.lockState = CursorLockMode.None;
        }
        void OnGUI()
        {
            if (!IsSpawned || !IsOwner || !IsClient || NetworkSession.MenuVisible) return;
            GUI.Label(new Rect(Screen.width / 2 - 5, Screen.height / 2 - 12, 20, 24), "+");
            GUI.Box(new Rect(Screen.width / 2 - 295, Screen.height - 100, 590, 85),
                $"WASD move | E interact | Q drop | Right mouse throw | V camera | Esc menu/cursor\n{(target != null ? "E: " + target.Prompt : "Look at an object") }\n{feedback}");
        }
    }
}


