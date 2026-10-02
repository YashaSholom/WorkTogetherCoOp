using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    /// <summary>A species. The model is a visual-only prefab; renderers whose name starts with "Skin" take each traveller's skin colour.</summary>
    [CreateAssetMenu(menuName = "Coop/Checkpoint/Race")]
    public sealed class RaceDefinition : ScriptableObject
    {
        public string displayName = "Race";
        public GameObject model;
        [Tooltip("Reference picture for the terminal codex.")]
        public Texture2D portrait;
        public string homeworld;
        [TextArea] public string description;
        [TextArea] public string identifyingFeatures;
    }
}
