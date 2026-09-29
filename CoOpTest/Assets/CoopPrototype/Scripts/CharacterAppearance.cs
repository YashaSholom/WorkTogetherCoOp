using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoopPrototype
{
    /// <summary>Which Bean Crew outfit a player wears. The server picks a free outfit on spawn and owns the value;
    /// the owner can request the next one (C by default). Late joiners read the replicated value.</summary>
    public class CharacterAppearance : NetworkBehaviour
    {
        [Tooltip("One renderer per outfit, in order. Only the selected one is enabled.")]
        public Renderer[] variants;
        public string[] variantNames = { "Bolt", "Gus", "Pip", "Dot" };
        public Key cycleKey = Key.C;
        public NetworkVariable<int> Variant = new(-1);

        public string CurrentName => variantNames != null && Variant.Value >= 0 && Variant.Value < variantNames.Length ? variantNames[Variant.Value] : "?";

        void Awake() => Apply(0); // Prefab/offline preview shows the first outfit.

        public override void OnNetworkSpawn()
        {
            Variant.OnValueChanged += OnVariantChanged;
            if (IsServer && Variant.Value < 0) Variant.Value = PickUnusedVariant();
            Apply(Variant.Value);
        }

        public override void OnNetworkDespawn() => Variant.OnValueChanged -= OnVariantChanged;

        void OnVariantChanged(int previous, int current) => Apply(current);

        int PickUnusedVariant()
        {
            if (variants == null || variants.Length == 0) return 0;
            var used = new HashSet<int>();
            foreach (var other in FindObjectsByType<CharacterAppearance>(FindObjectsSortMode.None))
                if (other != this && other.IsSpawned) used.Add(other.Variant.Value);
            for (int i = 0; i < variants.Length; i++)
            {
                int candidate = (int)((OwnerClientId + (ulong)i) % (ulong)variants.Length);
                if (!used.Contains(candidate)) return candidate;
            }
            return (int)(OwnerClientId % (ulong)variants.Length);
        }

        void Update()
        {
            if (!IsSpawned || !IsOwner || !IsClient || Keyboard.current == null) return;
            if (Cursor.lockState == CursorLockMode.Locked && Keyboard.current[cycleKey].wasPressedThisFrame) RequestNextVariantRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestNextVariantRpc()
        {
            if (variants == null || variants.Length == 0) return;
            Variant.Value = (Mathf.Max(Variant.Value, 0) + 1) % variants.Length;
            Debug.Log($"[Coop] Outfit changed: client {OwnerClientId} -> {CurrentName}");
        }

        /// <summary>Presentation only; safe to call in edit mode.</summary>
        public void Apply(int index)
        {
            if (variants == null || variants.Length == 0) return;
            index = Mathf.Clamp(index < 0 ? 0 : index, 0, variants.Length - 1);
            for (int i = 0; i < variants.Length; i++)
                if (variants[i] != null) variants[i].enabled = i == index;
        }
    }
}
