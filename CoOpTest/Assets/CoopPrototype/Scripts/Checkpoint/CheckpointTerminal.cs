using Unity.Netcode;

namespace CoopPrototype.Checkpoint
{
    public sealed class CheckpointTerminal : CheckpointEndpoint
    {
        public override string Prompt => "Open inspection terminal";
        public override bool TryInteract(PlayerInteractor player, out string reason)
        {
            // Always opens: the codex tabs are useful between visitors. Decisions still need a visitor at the desk.
            reason = "";
            if (!IsServer) return false;
            Open(player); return true;
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void DecideRpc(int visit, bool approve, RpcParams rpcParams = default)
        {
            ulong caller = rpcParams.Receive.SenderClientId;
            if (!Validate(caller, visit, out var player)) return;
            Reply(caller, checkpoint.Decide(visit, approve, player.NetworkObjectId, out var reason) ? (approve ? "ENTRY APPROVED. The verdict arrives in a moment..." : "ENTRY DENIED. Any cargo is confiscated. The verdict arrives in a moment...") : reason);
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ArrestRpc(int visit, RpcParams rpcParams = default)
        {
            ulong caller = rpcParams.Receive.SenderClientId;
            if (!Validate(caller, visit, out var player)) return;
            checkpoint.Arrest(visit, player.NetworkObjectId, out var reason);
            Reply(caller, reason);
        }
    }
}
