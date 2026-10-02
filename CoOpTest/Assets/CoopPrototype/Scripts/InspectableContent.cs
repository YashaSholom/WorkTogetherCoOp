using UnityEngine;

namespace CoopPrototype
{
    /// <summary>Optional item-owned content. The player inspector remains unaware of document/gameplay types.</summary>
    public abstract class InspectableContent : MonoBehaviour
    {
        public abstract string Title { get; }
        public abstract void DrawDetails(Rect area);
    }
}
