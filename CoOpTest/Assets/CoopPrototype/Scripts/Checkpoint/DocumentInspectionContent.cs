using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    public sealed class DocumentInspectionContent : InspectableContent
    {
        public override string Title => GetComponent<DocumentItem>().Content?.kind.displayName ?? "Document";
        public override void DrawDetails(Rect area)
        {
            var item = GetComponent<DocumentItem>(); var session = FindFirstObjectByType<CheckpointSession>();
            if (item != null && session != null) DrawDocument(session, item.CaseIndex.Value, item.DocumentIndex.Value, area);
        }
        /// <summary>Draws the document as printed for this visit (flaws included).</summary>
        public static void DrawDocument(CheckpointSession session, int caseIndex, int documentIndex, Rect area)
        {
            if (session == null || caseIndex < 0 || caseIndex >= session.cases.Length) return;
            var documents = session.cases[caseIndex].documents;
            if (documentIndex < 0 || documentIndex >= documents.Length) return;
            var document = documents[documentIndex];
            InspectionGui.Fill(area, InspectionGui.Panel);
            InspectionGui.Fill(new Rect(area.x, area.y, 4, area.height), document.kind.accent);
            float x = area.x + 20, y = area.y + 18;
            InspectionGui.Text(new Rect(x, y, area.width - 40, 28), document.kind.displayName.ToUpperInvariant(), caption: true);
            var emblem = session.EmblemFor(caseIndex, documentIndex);
            if (emblem != null) GUI.DrawTexture(new Rect(area.xMax - 128, area.y + 14, 108, 108), emblem, ScaleMode.ScaleToFit);
            y += 34;
            var portrait = session.PortraitFor(caseIndex, documentIndex);
            var picture = session.VehiclePictureFor(caseIndex, documentIndex);
            if (portrait == null && picture != null)
            {
                GUI.DrawTexture(new Rect(x, y, 200, 112), picture, ScaleMode.ScaleAndCrop);
                InspectionGui.Text(new Rect(x + 218, y + 12, area.width - 380, 80), document.traveller.displayName, title: true); y += 124;
            }
            if (portrait != null)
            {
                GUI.DrawTexture(new Rect(x, y, 112, 112), portrait, ScaleMode.ScaleToFit);
                InspectionGui.Text(new Rect(x + 130, y + 12, area.width - 170, 80), document.traveller.displayName, title: true); y += 124;
            }
            foreach (var field in document.fields)
            {
                InspectionGui.Text(new Rect(x, y, 160, 26), field.label.ToUpperInvariant(), caption: true);
                InspectionGui.Text(new Rect(x + 165, y, area.width - 310, 40), session.FieldValue(caseIndex, documentIndex, field)); y += 40;
            }
            InspectionGui.Text(new Rect(x, y + 4, area.width - 40, 30), "VALID UNTIL   " + session.ExpiryText(caseIndex, documentIndex) + "      TODAY   " + session.Date);
            // Security strip: a barcode printed from the document id, plus the holographic band.
            int hash = document.documentId.GetHashCode();
            float bx = area.xMax - 250;
            for (int i = 0; i < 44; i++) { int bit = (hash >> (i % 31)) & 1; float w = bit == 1 ? 3 : 1.5f; InspectionGui.Fill(new Rect(bx, area.yMax - 34, w, 22), Color.white); bx += w + 2.2f; }
            InspectionGui.Fill(new Rect(area.x + 4, area.yMax - 44, area.width - 4, 3), Color.Lerp(document.kind.accent, Color.white, .35f));
            InspectionGui.Text(new Rect(x, area.yMax - 30, area.width - 300, 24), "DOCUMENT  /  " + document.documentId, caption: true);
        }
    }
}
