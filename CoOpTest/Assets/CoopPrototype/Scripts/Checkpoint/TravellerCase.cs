using System;
using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    public enum VehicleKind { Shuttle, Truck }

    /// <summary>One package on a cargo vehicle. Whether it is contraband is authored here; players find out by scanning it.</summary>
    [Serializable]
    public sealed class CargoPackageDefinition
    {
        public string label = "Sealed crate";
        public bool contraband;
        public Color colour = new(.62f, .68f, .74f);
    }

    [CreateAssetMenu(menuName = "Coop/Checkpoint/Traveller Case")]
    public sealed class TravellerCase : ScriptableObject
    {
        public TravellerDefinition traveller;
        public DocumentDefinition[] documents;
        [TextArea] public string greeting = "Hello, officer. Is this the queue for the inner galaxy?";
        [TextArea] public string purpose = "Visiting family. The luggage is mostly snacks.";
        [TextArea] public string vehicleAnswer = "My Comet Hopper. It is registered, I promise.";
        public Color skinColor = new(.42f, .88f, .5f);
        [Tooltip("Which vehicle model arrives with this visitor.")]
        public VehicleKind vehicle = VehicleKind.Shuttle;
        [Tooltip("Packages on the back of a Truck. Empty for passenger shuttles.")]
        public CargoPackageDefinition[] packages = Array.Empty<CargoPackageDefinition>();
        public int PackageCount => packages == null ? 0 : packages.Length;
        [Tooltip("Travelling with the visitor. They answer for the same case: arresting the visitor holds them too.")]
        public TravellerDefinition[] companions = Array.Empty<TravellerDefinition>();
        [TextArea] public string companionAnswer = "Just me, officer.";
        public int CompanionCount => companions == null ? 0 : companions.Length;
        /// <summary>0 = the visitor, 1.. = companions.</summary>
        public TravellerDefinition Member(int index) => index <= 0 ? traveller : companions != null && index - 1 < companions.Length ? companions[index - 1] : null;
        public Color SkinOf(int index) => index <= 0 ? skinColor : Member(index) != null ? Member(index).skinColor : skinColor;
    }
}
