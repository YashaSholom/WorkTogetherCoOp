using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    [CreateAssetMenu(menuName = "Coop/Checkpoint/Document Kind")]
    public sealed class DocumentKind : ScriptableObject
    {
        public string kindId;
        public string displayName;
        public Color accent = Color.cyan;
        public bool showPortrait;
    }
}
