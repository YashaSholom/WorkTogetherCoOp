using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    [CreateAssetMenu(menuName = "Coop/Checkpoint/Traveller")]
    public sealed class TravellerDefinition : ScriptableObject
    {
        public string travellerId;
        public string displayName;
        [Tooltip("Shared portrait for dialogue and legitimate identity documents.")]
        public Texture2D portrait;
        public string birthDate;
        [Tooltip("Legacy field; IDs now show the issuing federation instead.")]
        public string birthPlace;
        [UnityEngine.Serialization.FormerlySerializedAs("sex")] public string race;
        public string vehicleModel;
        public string vehicleRegistration;
        [Header("Identity and vehicle references")]
        [Tooltip("Government that issued this traveller's Galactic ID.")]
        public FederationDefinition federation;
        public RaceDefinition raceInfo;
        public VehicleModelDefinition vehicle;
        [Header("Holding cell")]
        [Tooltip("Credits the Galactic Police pay for this traveller if the arrest was justified.")]
        [Min(0)] public int bounty = 80;
        [Tooltip("Skin colour when this traveller is a companion (the case's skin colour is used for the main traveller).")]
        public Color skinColor = new(.42f, .88f, .5f);
    }
}
