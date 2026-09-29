using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CoopPrototype.Frontend
{
    /// <summary>A lightweight lobby player. Gameplay players are spawned only after the shared scene loads.</summary>
    public sealed class LobbyMember : NetworkBehaviour
    {
        public NetworkVariable<FixedString64Bytes> DisplayName = new(new FixedString64Bytes("Crewmate"));
        public NetworkVariable<int> Character = new(0);
        public NetworkVariable<bool> Ready = new(false);
        GameFlow flow;
        public override void OnNetworkSpawn()
        {
            // Netcode creates this prefab. Its manager is the explicit scope boundary, not a global service locator.
            NetworkManager.GetComponent<GameFlow>().Register(this);
        }
        internal void Initialize(GameFlow owner, IPlayerPreferences profile)
        {
            flow = owner;
            if (IsOwner) SetProfileRpc(profile.DisplayName, profile.Character);
        }
        public override void OnNetworkDespawn() { if (flow != null) flow.Unregister(this); }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void SetProfileRpc(string requestedName, int character)
        {
            if (flow == null || flow.Transitioning || flow.InGame) return;
            if (character < 0 || character >= flow.CharacterCount) return;
            var clean = new System.Text.StringBuilder();
            foreach (var c in requestedName ?? "")
            {
                if (char.IsControl(c) || c == '<' || c == '>' || char.IsSurrogate(c)) continue;
                if (System.Text.Encoding.UTF8.GetByteCount(clean.ToString() + c) > 48) break;
                clean.Append(c);
                if (clean.Length >= 20) break;
            }
            DisplayName.Value = new FixedString64Bytes(clean.Length > 0 ? clean.ToString() : "Crewmate");
            Character.Value = character;
            Ready.Value = false;
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void SetReadyRpc(bool ready)
        {
            if (flow == null || flow.Transitioning || flow.InGame) return;
            Ready.Value = ready;
            Debug.Log($"[Lobby] {OwnerClientId} ready={ready}");
        }
    }
}
