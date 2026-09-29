using Unity.Netcode;
using UnityEngine;

namespace CoopPrototype
{
    /// <summary>Player code only routes requests here; each object owns its rules.</summary>
    public abstract class NetworkInteractable : NetworkBehaviour
    {
        [SerializeField] string interactionLabel = "Interact";
        public virtual string Prompt => interactionLabel;
        public abstract bool TryInteract(PlayerInteractor player, out string reason);
    }
}
