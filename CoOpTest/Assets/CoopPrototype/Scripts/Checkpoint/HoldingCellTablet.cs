using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    /// <summary>Wall tablet at the holding cell entrance. Lists prisoners, their bounties and the police pickup; sets prisoners free.</summary>
    public sealed class HoldingCellTablet : NetworkInteractable
    {
        public HoldingCell cell;
        readonly Dictionary<ulong, float> nextRequest = new();
        public override string Prompt => cell != null ? $"Use holding cell tablet ({cell.Occupied}/{cell.Capacity})" : "Use holding cell tablet";
        public override bool TryInteract(PlayerInteractor player, out string reason)
        {
            reason = "";
            if (!IsServer || cell == null) return false;
            OpenRpc(RpcTarget.Single(player.OwnerClientId, RpcTargetUse.Temp));
            return true;
        }
        [Rpc(SendTo.SpecifiedInParams)] void OpenRpc(RpcParams rpcParams = default) => HoldingCellPanel.Instance?.Show(this);
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ReleaseRpc(int prisonerId, RpcParams rpcParams = default)
        {
            ulong client = rpcParams.Receive.SenderClientId;
            if (!NetworkManager.ConnectedClients.TryGetValue(client, out var connection) || connection.PlayerObject == null) return;
            var player = connection.PlayerObject.GetComponent<PlayerInteractor>();
            if (player == null || !player.CanReach(this)) { Reply(client, "Step closer to the tablet."); return; }
            if (nextRequest.TryGetValue(client, out var until) && Time.unscaledTime < until) return;
            nextRequest[client] = Time.unscaledTime + .3f;
            cell.Release(prisonerId, out var message);
            Reply(client, message);
        }
        void Reply(ulong client, string message) => ReplyRpc(message, RpcTarget.Single(client, RpcTargetUse.Temp));
        [Rpc(SendTo.SpecifiedInParams)] void ReplyRpc(string message, RpcParams rpcParams = default) => HoldingCellPanel.Instance?.Feedback(this, message);
        public override void OnNetworkDespawn() => nextRequest.Clear();
    }
}
