using Unity.Netcode;
using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    /// <summary>A package from a cargo vehicle. Picked up, carried and socketed like any item; the contraband flag comes from the case asset and is revealed by the X-ray scanner.</summary>
    public sealed class CargoPackage : PickupItem
    {
        public NetworkVariable<int> CaseIndex = new(), PackageIndex = new(), Visit = new();
        [Tooltip("Body renderers tinted with the package colour from the case asset.")]
        public Renderer[] tinted;
        [Tooltip("Size of the package in the hands (first person). Inspection scales it to fit the view.")]
        [Min(.1f)] public float handheldScale = .7f;
        [Header("X-ray (only the scanner camera draws these layers)")]
        [Tooltip("Everything inside the crate as the X-ray sees it. Turned per package so crates look different.")]
        public Transform xrayContents;
        [Tooltip("Shown only when the package is contraband.")]
        public GameObject xrayContraband;
        CheckpointSession checkpoint;
        MaterialPropertyBlock block;
        Color appliedColour = Color.clear;
        int appliedLayout = -1;
        public CargoPackageDefinition Definition
        {
            get
            {
                if (checkpoint == null) checkpoint = FindFirstObjectByType<CheckpointSession>();
                if (checkpoint == null || checkpoint.cases == null || CaseIndex.Value < 0 || CaseIndex.Value >= checkpoint.cases.Length) return null;
                var packages = checkpoint.cases[CaseIndex.Value].packages;
                return packages != null && PackageIndex.Value >= 0 && PackageIndex.Value < packages.Length ? packages[PackageIndex.Value] : null;
            }
        }
        public bool IsContraband => Definition != null && Definition.contraband;
        public override string DisplayName => Definition != null ? Definition.label : "Package";
        public override string Prompt => "Take " + DisplayName.ToLowerInvariant() + "  /  Right click to inspect";
        protected override void Update()
        {
            base.Update();
            if (!IsSpawned) return;
            var definition = Definition;
            if (definition == null) return;
            int layout = PackageIndex.Value * 2 + (definition.contraband ? 1 : 0);
            if (layout != appliedLayout)
            {
                appliedLayout = layout;
                if (xrayContents != null) xrayContents.localRotation = Quaternion.Euler(0, PackageIndex.Value * 90 + CaseIndex.Value * 35, 0);
                if (xrayContraband != null) xrayContraband.SetActive(definition.contraband);
            }
            if (tinted == null || definition.colour == appliedColour) return;
            appliedColour = definition.colour;
            block ??= new MaterialPropertyBlock();
            block.SetColor("_BaseColor", appliedColour);
            foreach (var renderer in tinted) if (renderer != null) renderer.SetPropertyBlock(block);
        }
        public override float ViewScale => handheldScale;
    }
}
