using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace CoopPrototype.Editor
{
    /// <summary>Editor-only asynchronous installer for the document typography dependency.</summary>
    public static class TmpPackageInstaller
    {
        static AddRequest request;
        public static string Status { get; private set; } = "Not requested";

        public static void Install()
        {
            if (request != null && !request.IsCompleted) return;
            request = Client.Add("com.unity.textmeshpro");
            Status = "Installing TextMeshPro";
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (request == null || !request.IsCompleted) return;
            EditorApplication.update -= Poll;
            Status = request.Status == StatusCode.Success ? "Installed " + request.Result.version : "Installation failed: " + request.Error.message;
            request = null;
        }
    }
}
