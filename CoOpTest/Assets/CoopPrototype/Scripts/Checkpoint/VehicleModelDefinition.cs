using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    [CreateAssetMenu(menuName = "Coop/Checkpoint/Vehicle Model")]
    public sealed class VehicleModelDefinition : ScriptableObject
    {
        public string modelName = "Model";
        public VehicleManufacturer manufacturer;
        public VehicleKind kind;
        [Tooltip("Rendered picture for the terminal codex.")]
        public Texture2D picture;
        [TextArea] public string description;
    }
}
