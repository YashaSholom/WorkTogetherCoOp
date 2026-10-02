using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    /// <summary>Vehicle maker. Its badge is printed on the registrations of the vehicles it builds.</summary>
    [CreateAssetMenu(menuName = "Coop/Checkpoint/Vehicle Manufacturer")]
    public sealed class VehicleManufacturer : ScriptableObject
    {
        public string displayName = "Manufacturer";
        [Tooltip("Prefix of chassis numbers, e.g. CW.")]
        public string code = "MK";
        public Texture2D emblem;
        public Color colour = Color.white;
        [TextArea] public string description;
    }
}
