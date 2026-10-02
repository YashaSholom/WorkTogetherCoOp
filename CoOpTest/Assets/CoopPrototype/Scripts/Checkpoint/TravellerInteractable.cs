using Unity.Netcode;

namespace CoopPrototype.Checkpoint
{
    public sealed class TravellerInteractable : CheckpointEndpoint
    {
        public override string Prompt => checkpoint.AtDesk ? "Speak to " + checkpoint.Current.traveller.displayName : "Wait for the shuttle to stop";
        public override bool TryInteract(PlayerInteractor player, out string reason)
        {
            reason = "The shuttle is moving";
            if (!IsServer || !checkpoint.AtDesk) return false;
            Open(player); reason = ""; return true;
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestPapersRpc(int visit, RpcParams rpcParams = default)
        {
            ulong caller = rpcParams.Receive.SenderClientId;
            if (!Validate(caller, visit, out var player)) return;
            string cargo = checkpoint.Current.PackageCount > 0 ? " My cargo is on the back of the truck. Run it through the contraband scanner." : "";
            Reply(caller, checkpoint.IssueDocuments(visit, player.NetworkObjectId) ? "Here you go. My papers are already in your terminal's document slots." + cargo : "I already handed them over. Your crew has the papers.");
        }
    }
}
