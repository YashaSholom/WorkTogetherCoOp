using UnityEngine.Events;

namespace CoopPrototype
{
    public class ActionInteractable : NetworkInteractable
    {
        public UnityEvent onServerInteract = new();
        public override bool TryInteract(PlayerInteractor player, out string reason)
        {
            reason = "";
            if (!IsServer) return false;
            onServerInteract.Invoke();
            return true;
        }
    }
}
