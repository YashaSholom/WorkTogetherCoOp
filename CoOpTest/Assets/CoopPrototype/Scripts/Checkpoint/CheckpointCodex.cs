using System;
using System.Linq;
using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    /// <summary>Reference knowledge shown on the inspection terminal: federations and their seals, vehicle makers and models, races.</summary>
    [CreateAssetMenu(menuName = "Coop/Checkpoint/Codex")]
    public sealed class CheckpointCodex : ScriptableObject
    {
        public FederationDefinition[] federations = Array.Empty<FederationDefinition>();
        public VehicleManufacturer[] manufacturers = Array.Empty<VehicleManufacturer>();
        public VehicleModelDefinition[] vehicles = Array.Empty<VehicleModelDefinition>();
        public RaceDefinition[] races = Array.Empty<RaceDefinition>();
        public VehicleModelDefinition Vehicle(string modelName) => vehicles.FirstOrDefault(v => v != null && v.modelName == modelName);
    }
}
