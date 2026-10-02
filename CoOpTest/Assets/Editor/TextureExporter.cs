using System.IO;
using UnityEditor;
using UnityEngine;

public static class TextureExporter
{
    [MenuItem("Tools/Export Selected Texture as PNG")]
    static void ExportSelectedTexture()
    {
        var texture = Selection.activeObject as Texture2D;
        if (texture == null)
        {
            Debug.LogError("Select a Texture2D first.");
            return;
        }

        var path = EditorUtility.SaveFilePanel(
            "Export PNG",
            Application.dataPath,
            texture.name,
            "png"
        );

        if (string.IsNullOrEmpty(path)) return;

        File.WriteAllBytes(path, texture.EncodeToPNG());
        AssetDatabase.Refresh();
    }
}