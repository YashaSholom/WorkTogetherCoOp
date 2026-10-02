using Unity.Netcode;
using UnityEngine;

namespace CoopPrototype
{
    /// <summary>
    /// Per-player money. Work earns (or loses) <see cref="Pending"/> credits during the day; they are paid into
    /// <see cref="Credits"/> when the day ends. Only banked credits can be spent. Server-written, replicated to everyone.
    /// </summary>
    public sealed class PlayerWallet : NetworkBehaviour
    {
        [Min(0)] public int startingCredits = 60;
        public NetworkVariable<int> Credits = new(), Pending = new(), LastDayEarned = new();
        public override void OnNetworkSpawn() { if (IsServer) Credits.Value = startingCredits; }
        /// <summary>Server. Adds to today's pending payout (negative = fine).</summary>
        public void AddPending(int amount) { if (IsServer) Pending.Value += amount; }
        /// <summary>Server. End of day: pending becomes banked credits. Credits may go negative (debt).</summary>
        public int Payout()
        {
            if (!IsServer) return 0;
            int earned = Pending.Value;
            Credits.Value += earned; LastDayEarned.Value = earned; Pending.Value = 0;
            return earned;
        }
        /// <summary>Server. Spends banked credits.</summary>
        public bool TrySpend(int price)
        {
            if (!IsServer || price < 0 || Credits.Value < price) return false;
            Credits.Value -= price; return true;
        }
        public static PlayerWallet Local
        {
            get
            {
                var manager = NetworkManager.Singleton;
                return manager != null && manager.LocalClient != null && manager.LocalClient.PlayerObject != null ? manager.LocalClient.PlayerObject.GetComponent<PlayerWallet>() : null;
            }
        }
    }
}
