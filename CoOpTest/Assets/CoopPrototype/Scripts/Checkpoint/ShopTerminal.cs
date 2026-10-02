using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    public enum ShopOfferKind { Item, BeltSlotUpgrade }

    [Serializable]
    public sealed class ShopOffer
    {
        public string title = "Item";
        [TextArea] public string description;
        [Min(0)] public int price = 50;
        public ShopOfferKind kind;
        [Tooltip("Spawned on the dispenser tray for Item offers. Must be a registered network prefab.")]
        public PickupItem prefab;
    }

    /// <summary>ATM-style supply terminal. Players spend their own banked credits; the server validates every purchase.</summary>
    public sealed class ShopTerminal : NetworkInteractable
    {
        public ShopOffer[] offers = Array.Empty<ShopOffer>();
        [Tooltip("Purchased items appear here.")]
        public Transform dispenser;
        public ContrabandScanner scanner;
        readonly Dictionary<ulong, float> nextRequest = new();
        public override string Prompt => "Use supply terminal";
        public override bool TryInteract(PlayerInteractor player, out string reason)
        {
            reason = "";
            if (!IsServer) return false;
            OpenRpc(RpcTarget.Single(player.OwnerClientId, RpcTargetUse.Temp));
            return true;
        }
        [Rpc(SendTo.SpecifiedInParams)] void OpenRpc(RpcParams rpcParams = default) => ShopPanel.Instance?.Show(this);
        /// <summary>Whether an offer can be bought at all right now (independent of money).</summary>
        public bool IsAvailable(int index, out string why)
        {
            why = "";
            if (index < 0 || index >= offers.Length) { why = "Unknown item"; return false; }
            var offer = offers[index];
            if (offer.kind == ShopOfferKind.BeltSlotUpgrade && (scanner == null || !scanner.CanUpgrade)) { why = "Already installed"; return false; }
            if (offer.kind == ShopOfferKind.BeltSlotUpgrade && scanner.Busy) { why = "Wait for the scanner to stop"; return false; }
            if (offer.kind == ShopOfferKind.Item && offer.prefab == null) { why = "Out of stock"; return false; }
            return true;
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void BuyRpc(int index, RpcParams rpcParams = default)
        {
            ulong client = rpcParams.Receive.SenderClientId;
            if (!NetworkManager.ConnectedClients.TryGetValue(client, out var connection) || connection.PlayerObject == null) return;
            var player = connection.PlayerObject.GetComponent<PlayerInteractor>();
            var wallet = connection.PlayerObject.GetComponent<PlayerWallet>();
            if (player == null || wallet == null || !player.CanReach(this)) { Reply(client, "Step closer to the terminal."); return; }
            if (nextRequest.TryGetValue(client, out var until) && Time.unscaledTime < until) return;
            nextRequest[client] = Time.unscaledTime + .3f;
            if (!IsAvailable(index, out var why)) { Reply(client, why); return; }
            var offer = offers[index];
            if (!wallet.TrySpend(offer.price)) { Reply(client, $"Not enough credits. {offer.title} costs {offer.price}; you have {wallet.Credits.Value}. Today's pay arrives at the end of the day."); return; }
            if (offer.kind == ShopOfferKind.BeltSlotUpgrade) scanner.TryUpgrade();
            else
            {
                var point = dispenser != null ? dispenser : transform;
                var item = Instantiate(offer.prefab, point.position + Vector3.up * .15f, point.rotation);
                item.NetworkObject.Spawn(true);
            }
            Debug.Log($"[Checkpoint] Shop: client {client} bought {offer.title} for {offer.price}");
            Reply(client, offer.kind == ShopOfferKind.Item ? $"Bought {offer.title}. Collect it from the tray below the screen." : $"{offer.title} installed.");
        }
        void Reply(ulong client, string message) => ReplyRpc(message, RpcTarget.Single(client, RpcTargetUse.Temp));
        [Rpc(SendTo.SpecifiedInParams)] void ReplyRpc(string message, RpcParams rpcParams = default) => ShopPanel.Instance?.Feedback(this, message);
        public override void OnNetworkDespawn() => nextRequest.Clear();
    }
}
