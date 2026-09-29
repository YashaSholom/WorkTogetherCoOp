using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace CoopPrototype
{
    /// <summary>Persistent authoritative boolean with Inspector-composed local presentation.</summary>
    public class NetworkState : NetworkBehaviour
    {
        public bool initialValue;
        [Tooltip("Runs on every peer, including late joiners. Wire lights, animation, audio, etc.")]
        public UnityEvent<bool> onStateApplied = new();
        public NetworkVariable<bool> Value = new(false);
        public override void OnNetworkSpawn()
        {
            if (IsServer) Value.Value = initialValue;
            Value.OnValueChanged += Changed;
            onStateApplied.Invoke(Value.Value);
        }
        public override void OnNetworkDespawn() => Value.OnValueChanged -= Changed;
        void Changed(bool oldValue, bool value)
        {
            onStateApplied.Invoke(value);
            Debug.Log($"[Coop] State changed: {name} = {value}");
        }
        public void SetState(bool value) { if (IsServer && IsSpawned) Value.Value = value; }
        public void Toggle() => SetState(!Value.Value);
    }
}
