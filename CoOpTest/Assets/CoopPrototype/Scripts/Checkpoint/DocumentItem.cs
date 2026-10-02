using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CoopPrototype.Checkpoint
{
    public sealed class DocumentItem : PickupItem
    {
        public NetworkVariable<int> CaseIndex = new(), DocumentIndex = new(), Visit = new();
        public TextMeshProUGUI coverLabel;
        public RawImage portraitImage;
        [Tooltip("Issuing federation's seal on IDs, manufacturer's badge on vehicle registrations.")]
        public RawImage emblemImage;
        [Tooltip("Violet seal printed in UV ink on genuine IDs. Shown only under a UV lamp.")]
        public GameObject uvWatermark;
        [Tooltip("Size of the document in the hands (first person). Inspection scales it to fit the view.")]
        [Min(.1f)] public float handheldScale = .5f;
        CheckpointSession checkpoint;
        string shownText;
        CheckpointSession Session => checkpoint != null ? checkpoint : checkpoint = FindFirstObjectByType<CheckpointSession>();
        public DocumentDefinition Content
        {
            get
            {
                var session = Session;
                if (session == null || CaseIndex.Value < 0 || CaseIndex.Value >= session.cases.Length) return null;
                var documents = session.cases[CaseIndex.Value].documents;
                return DocumentIndex.Value >= 0 && DocumentIndex.Value < documents.Length ? documents[DocumentIndex.Value] : null;
            }
        }
        public override string DisplayName => Content != null ? Content.kind.displayName : "Document";
        public override string Prompt => "Take " + DisplayName + "  /  Right click to inspect";
        /// <summary>The printed text of this document, including any flaw in this visit's papers.</summary>
        public string PrintedText()
        {
            var content = Content; var session = Session;
            var text = new System.Text.StringBuilder(content.kind.displayName.ToUpperInvariant()).Append('\n').Append(content.traveller.displayName);
            foreach (var field in content.fields) text.Append('\n').Append(field.label.ToUpperInvariant()).Append(": ").Append(session.FieldValue(CaseIndex.Value, DocumentIndex.Value, field));
            return text.Append("\nVALID: ").Append(session.ExpiryText(CaseIndex.Value, DocumentIndex.Value)).ToString();
        }
        protected override void Update()
        {
            base.Update();
            if (!IsSpawned || coverLabel == null || Content == null) return;
            var text = PrintedText();
            if (text != shownText) { shownText = text; coverLabel.text = text; coverLabel.color = Content.kind.accent; }
            if (uvWatermark != null)
            {
                bool show = !IsStowed && Session.HasWatermark(CaseIndex.Value, DocumentIndex.Value) && UvLamp.Illuminates(this);
                if (uvWatermark.activeSelf != show) uvWatermark.SetActive(show);
            }
            if (emblemImage != null)
            {
                var emblem = Session.EmblemFor(CaseIndex.Value, DocumentIndex.Value);
                emblemImage.enabled = emblem != null && !IsStowed;
                if (emblem != null && emblemImage.texture != emblem) emblemImage.texture = emblem;
            }
            if (portraitImage == null) return;
            var portrait = Session.PortraitFor(CaseIndex.Value, DocumentIndex.Value);
            // Registrations print a picture of the registered model in the photo window (centre crop of the wide picture).
            var picture = portrait == null ? Session.VehiclePictureFor(CaseIndex.Value, DocumentIndex.Value) : null;
            var shown = portrait != null ? portrait : picture;
            portraitImage.enabled = shown != null && !IsStowed;
            if (shown != null) { portraitImage.texture = shown; portraitImage.uvRect = picture != null ? new Rect(.29f, 0, .42f, 1) : new Rect(0, 0, 1, 1); }
        }
        protected override Quaternion HeldRotation(NetworkPlayerMotor holder) => holder.DocumentHoldRotation;
        /// <summary>The front (+Y) face, with the text, faces the camera and the top edge (+Z) points up.</summary>
        public override Quaternion ViewRotation => Quaternion.LookRotation(Vector3.up, Vector3.back);
        public override float ViewScale => handheldScale;
    }
}
