using System;
using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    [Serializable]
    public struct DocumentField
    {
        public string key;
        public string label;
        public string value;
        public DocumentField(string key, string label, string value)
        { this.key = key; this.label = label; this.value = value; }
    }

    /// <summary>Authored document content, not mutable session/ownership state.</summary>
    [CreateAssetMenu(menuName = "Coop/Checkpoint/Document")]
    public sealed class DocumentDefinition : ScriptableObject
    {
        public string documentId;
        public DocumentKind kind;
        public TravellerDefinition traveller;
        [Tooltip("ISO date YYYY-MM-DD; compare against an explicit game date later.")]
        public string expiresOn;
        public DocumentField[] fields = Array.Empty<DocumentField>();
        public Texture2D Portrait => kind != null && kind.showPortrait && traveller != null ? traveller.portrait : null;
    }
}
