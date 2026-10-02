using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    [RequireComponent(typeof(ItemSocket))]
    public sealed class DocumentScanner : MonoBehaviour
    {
        public CheckpointSession checkpoint;
        public void Scan(bool occupied)
        {
            var slot = GetComponent<ItemSocket>();
            if (!occupied || !slot.IsServer || !slot.NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(slot.Occupant.Value, out var item)) return;
            checkpoint.Scan(item.GetComponent<DocumentItem>());
        }
    }
}
