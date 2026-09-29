using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoopPrototype
{
    /// <summary>The server owns wind-up, release time and launch velocity. Animation only presents it.</summary>
    [RequireComponent(typeof(PlayerInteractor))]
    public class PlayerThrow : NetworkBehaviour
    {
        [Min(0)] public float speed = 9;
        [Min(0)] public float upwardBoost = 1.5f;
        [Min(.05f)] public float releaseDelay = .25f;
        [Min(.1f)] public float duration = .7f;
        [Min(0)] public float spin = 5;
        public NetworkVariable<double> StartedAt = new(-100);
        PlayerInteractor interactor;
        PickupItem pending;
        public bool IsBusy => IsSpawned && NetworkManager.ServerTime.Time < StartedAt.Value + duration;

        public override void OnNetworkSpawn() => interactor = GetComponent<PlayerInteractor>();
        void Update()
        {
            if (!IsSpawned) return;
            if (IsOwner && IsClient && Cursor.lockState == CursorLockMode.Locked &&
                Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) RequestThrowRpc();
            if (!IsServer || pending == null || NetworkManager.ServerTime.Time < StartedAt.Value + releaseDelay) return;
            // Release can be cancelled by despawn/disconnect. A client never provides force or object identity.
            if (pending.IsSpawned && pending.Holder.Value == NetworkObjectId)
                pending.Throw(interactor, interactor.Motor.Aim * speed + Vector3.up * upwardBoost, spin);
            pending = null;
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestThrowRpc()
        {
            if (IsBusy || interactor.Held == null) return;
            pending = interactor.Held;
            StartedAt.Value = NetworkManager.ServerTime.Time;
            Debug.Log($"[Coop] Throw accepted: {OwnerClientId} -> {pending.name}");
        }
        public override void OnNetworkDespawn() => pending = null;
        void OnValidate() => duration = Mathf.Max(duration, releaseDelay + .1f);
    }
}
