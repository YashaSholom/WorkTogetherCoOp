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
        [Tooltip("The snap point is the tray surface: items of any size are lifted by half their height so they rest on it.")]
        public bool restOnSurface;
        /// <summary>Where an item's pivot goes when seated here.</summary>
        public Vector3 SnapPositionFor(PickupItem item)
        {
            if (!restOnSurface || item == null) return snapPoint.position;
            var box = item.GetComponent<BoxCollider>();
            float half = box != null ? (box.size.y * .5f - box.center.y) * item.transform.lossyScale.y : .05f;
            return snapPoint.position + snapPoint.up * half;
        }
        [Tooltip("Runs only on the server. Wire to authoritative state components.")]
        public UnityEvent<bool> onOccupancyChanged = new();
        public NetworkVariable<ulong> Occupant = new(ulong.MaxValue);
        /// <summary>Set by a station (e.g. a conveyor) while the occupant must not be taken out. Checked on the server.</summary>
        [System.NonSerialized] public bool removalLocked;
        /// <summary>False while the slot has not been unlocked (e.g. an upgrade not yet bought). Server-written.</summary>
        public NetworkVariable<bool> Available = new(true);
        [Tooltip("Shown while the slot is unavailable (e.g. a LOCKED cover plate).")]
        public GameObject lockedIndicator;
        [Tooltip("Shown only while the slot is available.")]
        public Renderer[] availableVisuals;
        [Tooltip("Turn this slot's collider off while unavailable (e.g. spots on a vehicle that is not parked).")]
        public bool hideColliderWhenUnavailable;
        Collider ownCollider;
        void Update()
        {
            if (!IsSpawned) return;
            bool available = Available.Value;
            if (lockedIndicator != null && lockedIndicator.activeSelf == available) lockedIndicator.SetActive(!available);
            if (availableVisuals != null) foreach (var visual in availableVisuals) if (visual != null && visual.enabled != available) visual.enabled = available;
            if (hideColliderWhenUnavailable) { if (ownCollider == null) ownCollider = GetComponent<Collider>(); if (ownCollider != null && ownCollider.enabled != available) ownCollider.enabled = available; }
        }
        public string insertPrompt = "Insert held item";
        public string lockedPrompt = "Slot locked (upgrade at the shop)";
        public override string Prompt => !Available.Value ? lockedPrompt : Occupant.Value == ulong.MaxValue ? insertPrompt : lockAfterInsertion || removalLocked ? "Occupied (locked)" : "Remove item";
        public override bool TryInteract(PlayerInteractor player, out string reason)
        {
            reason = "Socket occupied or hands empty";
            if (!IsServer) return false;
            if (!Available.Value) { reason = lockedPrompt; return false; }
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
        /// <summary>Server. Seats a free (not carried) item in this empty slot without a player, e.g. cargo that arrives loaded.</summary>
        public bool ServerInsert(PickupItem item)
        {
            if (!IsServer || Occupant.Value != ulong.MaxValue || item == null || item.Holder.Value != ulong.MaxValue) return false;
            Occupant.Value = item.NetworkObjectId;
            item.Seat(this);
            onOccupancyChanged.Invoke(true);
            return true;
        }
        /// <summary>Server. Lets go of the occupant as a free physics object where it is (no player, no occupancy event).</summary>
        public void ReleaseToWorld(PickupItem item)
        {
            if (!IsServer || item == null || Occupant.Value != item.NetworkObjectId) return;
            Occupant.Value = ulong.MaxValue;
            item.Unseat();
        }
        public bool TryRemove(PickupItem item)
        {
            if (!IsServer || lockAfterInsertion || removalLocked || Occupant.Value != item.NetworkObjectId) return false;
            Occupant.Value = ulong.MaxValue;
            item.Socket.Value = ulong.MaxValue;
            onOccupancyChanged.Invoke(false);
            return true;
        }
    }
}
