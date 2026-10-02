using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoopPrototype
{
    /// <summary>Which suit colour a player wears. The server owns the value and guarantees that no two players in the
    /// same game share a colour: a joining player gets their preferred colour if it is free, otherwise the next free
    /// one, and cycling (C by default) skips colours that are taken. Late joiners read the replicated value.</summary>
    public class CharacterAppearance : NetworkBehaviour
    {
        [Tooltip("Renderer whose material is swapped per colour.")]
        public Renderer body;
        [Tooltip("One material per colour, in order.")]
        public Material[] colours;
        [Tooltip("Legacy: one renderer per outfit; only the selected one is enabled. Used when no colours are set.")]
        public Renderer[] variants;
        public string[] variantNames = { "Orange", "Blue", "Green", "Yellow" };
        public Key cycleKey = Key.C;
        public NetworkVariable<int> Variant = new(-1);

        public int Count => colours != null && colours.Length > 0 && body != null ? colours.Length : variants != null ? variants.Length : 0;
        public string CurrentName => variantNames != null && Variant.Value >= 0 && Variant.Value < variantNames.Length ? variantNames[Variant.Value] : "?";

        void Awake() => Apply(0); // Prefab/offline preview shows the first colour.

        public override void OnNetworkSpawn()
        {
            Variant.OnValueChanged += OnVariantChanged;
            if (IsServer && Variant.Value < 0) Variant.Value = FirstFree((int)(OwnerClientId % (ulong)Mathf.Max(1, Count)));
            Apply(Variant.Value);
        }

        public override void OnNetworkDespawn() => Variant.OnValueChanged -= OnVariantChanged;

        void OnVariantChanged(int previous, int current) => Apply(current);

        /// <summary>Server: wear the preferred colour, or the next free one if another player already has it.</summary>
        public void ServerSetPreferred(int preferred)
        {
            if (!IsServer || Count == 0) return;
            Variant.Value = FirstFree(((preferred % Count) + Count) % Count);
        }

        /// <summary>First colour at or after <paramref name="start"/> that no other spawned player wears.
        /// Falls back to <paramref name="start"/> only when there are more players than colours.</summary>
        int FirstFree(int start)
        {
            int count = Count; if (count == 0) return 0;
            var used = new HashSet<int>();
            foreach (var other in FindObjectsByType<CharacterAppearance>(FindObjectsSortMode.None))
                if (other != this && other.IsSpawned) used.Add(other.Variant.Value);
            for (int i = 0; i < count; i++)
            {
                int candidate = (start + i) % count;
                if (!used.Contains(candidate)) return candidate;
            }
            return start % count;
        }

        void Update()
        {
            if (!IsSpawned || !IsOwner || !IsClient || Keyboard.current == null) return;
            if (Cursor.lockState == CursorLockMode.Locked && Keyboard.current[cycleKey].wasPressedThisFrame) RequestNextVariantRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestNextVariantRpc()
        {
            if (Count == 0) return;
            int current = Mathf.Max(Variant.Value, 0);
            int next = FirstFree((current + 1) % Count);
            // Every other colour taken: FirstFree wraps back to our own, so nothing changes.
            if (next == current) return;
            Variant.Value = next;
            Debug.Log($"[Coop] Suit colour changed: client {OwnerClientId} -> {CurrentName}");
        }

        /// <summary>Presentation only; safe to call in edit mode.</summary>
        public void Apply(int index)
        {
            int count = Count; if (count == 0) return;
            index = Mathf.Clamp(index < 0 ? 0 : index, 0, count - 1);
            if (colours != null && colours.Length > 0 && body != null) { body.sharedMaterial = colours[index]; return; }
            for (int i = 0; i < variants.Length; i++)
                if (variants[i] != null) variants[i].enabled = i == index;
        }
    }
}
