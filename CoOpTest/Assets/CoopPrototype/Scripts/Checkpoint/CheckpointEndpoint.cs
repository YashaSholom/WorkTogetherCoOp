using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    /// <summary>Shared RPC validation for world panels. The caller is derived from the connection, never supplied as a player ID.</summary>
    public abstract class CheckpointEndpoint : NetworkInteractable
    {
        public CheckpointSession checkpoint;
        readonly Dictionary<ulong, float> nextRequest = new();
        protected bool Validate(ulong client, int visit, out PlayerInteractor player)
        {
            player = null;
            if (!IsServer || checkpoint == null || checkpoint.Visit.Value != visit || !checkpoint.AtDesk ||
                !NetworkManager.ConnectedClients.TryGetValue(client, out var connection) || connection.PlayerObject == null) return false;
            player = connection.PlayerObject.GetComponent<PlayerInteractor>();
            if (player == null || !player.CanReach(this) || player.GetComponent<PlayerThrow>().IsBusy) return false;
            if (nextRequest.TryGetValue(client, out var until) && Time.unscaledTime < until) return false;
            nextRequest[client] = Time.unscaledTime + .15f;
            return true;
        }
        protected void Open(PlayerInteractor player) => OpenPanelRpc(checkpoint.Visit.Value, RpcTarget.Single(player.OwnerClientId, RpcTargetUse.Temp));
        [Rpc(SendTo.SpecifiedInParams)]
        void OpenPanelRpc(int visit, RpcParams rpcParams = default)
        { if (checkpoint != null && checkpoint.Visit.Value == visit) CheckpointPanel.Instance?.Show(this, visit); }
        protected void Reply(ulong client, string message) => ReplyRpc(message, RpcTarget.Single(client, RpcTargetUse.Temp));
        [Rpc(SendTo.SpecifiedInParams)]
        void ReplyRpc(string message, RpcParams rpcParams = default) => CheckpointPanel.Instance?.Feedback(this, message);
        public override void OnNetworkDespawn() { nextRequest.Clear(); }
    }
}
