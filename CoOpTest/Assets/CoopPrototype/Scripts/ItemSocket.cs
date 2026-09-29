using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace CoopPrototype
{
    public class ItemSocket : NetworkInteractable
    {
        [Tooltip("Empty accepts any category. Exact definition takes priority.")]
        public string acceptedCategory;
        public ItemDefinition requiredDefinition;
        public bool lockAfterInsertion;
        public Transform snapPoint;
        [Tooltip("Runs only on the server. Wire to authoritative state components.")]
        public UnityEvent<bool> onOccupancyChanged = new();
        public NetworkVariable<ulong> Occupant = new(ulong.MaxValue);
        public override string Prompt => Occupant.Value == ulong.MaxValue ? "Insert held item" : lockAfterInsertion ? "Occupied (locked)" : "Remove item";
        public override bool TryInteract(PlayerInteractor player, out string reason)
        {
            reason = "Socket occupied or hands empty";
            if (!IsServer) return false;
            if (Occupant.Value != ulong.MaxValue)
            {
                if (player.Held == null && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(Occupant.Value, out var placed))
                    return placed.GetComponent<PickupItem>().TryInteract(player, out reason);
                return false;
            }
            var item = player.Held;
            if (item == null || item.Holder.Value != player.NetworkObjectId) return false;
            if ((requiredDefinition != null && item.definition != requiredDefinition) ||
                (requiredDefinition == null && !string.IsNullOrEmpty(acceptedCategory) && (item.definition == null || item.definition.category != acceptedCategory)))
            { reason = "This slot does not accept that item"; return false; }
            Occupant.Value = item.NetworkObjectId;
            item.Place(player, this);
            onOccupancyChanged.Invoke(true);
            reason = "";
            return true;
        }
        public bool TryRemove(PickupItem item)
        {
            if (!IsServer || lockAfterInsertion || Occupant.Value != item.NetworkObjectId) return false;
            Occupant.Value = ulong.MaxValue;
            item.Socket.Value = ulong.MaxValue;
            onOccupancyChanged.Invoke(false);
            return true;
        }
    }
}
