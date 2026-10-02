using UnityEngine;

namespace CoopPrototype
{
    public static class InspectionGui
    {
        // Shared station palette: warm porcelain type, graphite equipment, restrained cyan indicators.
        public static readonly Color Ink = new(.075f, .09f, .105f), Panel = new(.13f, .16f, .18f), Mint = new(.25f, .83f, .89f), Muted = new(.66f, .73f, .73f);
        public static readonly Color Porcelain = new(.96f, .92f, .82f), Orange = new(.95f, .48f, .18f);
        static GUIStyle body, heading, small, button;
        public static void Begin()
        {
            float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1440 * scale) / 2, (Screen.height - 900 * scale) / 2), Quaternion.identity, Vector3.one * scale);
            if (body != null) return;
            body = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true, padding = new RectOffset(0, 0, 0, 0) }; body.normal.textColor = Porcelain;
            heading = new GUIStyle(body) { fontSize = 32, fontStyle = FontStyle.Bold };
            small = new GUIStyle(body) { fontSize = 15 }; small.normal.textColor = Muted;
            button = new GUIStyle(GUI.skin.button) { fontSize = 19, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(18, 12, 10, 10), wordWrap = true };
            foreach (var state in new[] { button.normal, button.hover, button.active, button.focused, button.onNormal, button.onHover, button.onActive, button.onFocused })
            { state.background = null; state.textColor = Porcelain; }
        }
        public static void End() { GUI.matrix = Matrix4x4.identity; GUI.color = Color.white; }
        public static void Fill(Rect rect, Color color) { var old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }
        public static void Text(Rect rect, string value, bool title = false, bool caption = false) => GUI.Label(rect, value, title ? heading : caption ? small : body);
        public static bool Button(Rect rect, string value, Color? tint = null)
        {
            bool hover = GUI.enabled && rect.Contains(Event.current.mousePosition);
            Color color = tint ?? new Color(.19f, .26f, .28f);
            if (!GUI.enabled) color = Color.Lerp(color, Ink, .65f);
            else if (hover) color = Color.Lerp(color, Porcelain, .12f);
            Fill(rect, color);
            Fill(new Rect(rect.x, rect.y, 3, rect.height), hover ? Orange : Mint * .65f);
            return GUI.Button(rect, value, button);
        }
        public static void Frame(string eyebrow, string title)
        {
            var matrix = GUI.matrix; GUI.matrix = Matrix4x4.identity;
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(.03f, .04f, .045f, .96f)); GUI.matrix = matrix;
            Fill(new Rect(174, 89, 1092, 722), Panel);
            Fill(new Rect(180, 95, 1080, 710), Ink); Fill(new Rect(180, 95, 1080, 4), Orange);
            Fill(new Rect(215, 216, 1010, 1), Panel);
            Text(new Rect(215, 119, 900, 25), eyebrow, caption: true);
            Text(new Rect(215, 155, 900, 50), title, title: true);
        }
    }
}
