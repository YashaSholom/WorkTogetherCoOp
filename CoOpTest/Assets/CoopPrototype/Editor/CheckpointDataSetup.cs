using CoopPrototype.Checkpoint;
using UnityEditor;
using UnityEngine;

namespace CoopPrototype.Editor
{
    public static class CheckpointDataSetup
    {
        const string Folder = "Assets/CoopPrototype/CheckpointData";

        [MenuItem("Coop Prototype/Checkpoint/Create Sample Data")]
        public static void Create()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/CoopPrototype", "CheckpointData");
            var traveller = Create<TravellerDefinition>("Traveller_Zuri", a =>
            {
                a.travellerId = "traveller-zuri"; a.displayName = "Zuri Vex";
                a.birthDate = "2184-03-12"; a.birthPlace = "Velora Station"; a.race = "Velorian";
                a.vehicleModel = "Comet Hopper"; a.vehicleRegistration = "VX-042";
            });
            var identity = Create<DocumentKind>("Kind_Identity", a =>
            { a.kindId = "identity"; a.displayName = "Galactic ID"; a.showPortrait = true; });
            var registration = Create<DocumentKind>("Kind_Vehicle", a =>
            { a.kindId = "vehicle-registration"; a.displayName = "Vehicle Registration"; a.accent = new Color(1, .65f, .2f); });
            Create<DocumentDefinition>("Document_Zuri_ID", a =>
            {
                a.documentId = "zuri-id"; a.kind = identity; a.traveller = traveller; a.expiresOn = "2220-12-31";
                a.fields = new[] { new DocumentField("name", "Name", traveller.displayName),
                    new DocumentField("birth-date", "Date of birth", traveller.birthDate),
                    new DocumentField("birth-place", "Place of birth", traveller.birthPlace),
                    new DocumentField("race", "Race", traveller.race) };
            });
            Create<DocumentDefinition>("Document_Zuri_Vehicle", a =>
            {
                a.documentId = "zuri-vehicle"; a.kind = registration; a.traveller = traveller; a.expiresOn = "2220-12-31";
                a.fields = new[] { new DocumentField("owner", "Owner", traveller.displayName),
                    new DocumentField("model", "Vehicle model", traveller.vehicleModel),
                    new DocumentField("registration", "Registration", traveller.vehicleRegistration) };
            });
            AssetDatabase.SaveAssets();
            Debug.Log("[Checkpoint] Sample data ready. Existing assets preserved. Portrait artwork and runtime integration are pending.");
        }

        static T Create<T>(string name, System.Action<T> initialize) where T : ScriptableObject
        {
            string path = $"{Folder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new System.InvalidOperationException("Asset type conflict: " + path);
            var asset = ScriptableObject.CreateInstance<T>();
            initialize(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
