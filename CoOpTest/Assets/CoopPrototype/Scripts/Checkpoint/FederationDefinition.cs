using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    /// <summary>A government that issues Galactic IDs. Its seal is printed on every ID it issues; the terminal codex lists them all.</summary>
    [CreateAssetMenu(menuName = "Coop/Checkpoint/Federation")]
    public sealed class FederationDefinition : ScriptableObject
    {
        public string displayName = "Federation";
        [Tooltip("Prefix of every ID number this federation issues, e.g. VFW.")]
        public string code = "FED";
        public Texture2D emblem;
        public Color colour = Color.cyan;
        public string capital;
        [TextArea] public string description;
    }
}
