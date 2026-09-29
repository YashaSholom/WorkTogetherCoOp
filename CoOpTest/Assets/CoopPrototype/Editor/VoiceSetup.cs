using CoopPrototype.Frontend;
using CoopPrototype.Voice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CoopPrototype.Editor
{
    public static class VoiceSetup
    {
        [MenuItem("Coop Prototype/Voice/Configure Main Menu Voice")]
        public static void Configure()
        {
            if(EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode before wiring voice.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!=MainMenuSetup.MenuPath)
            {
                if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene=EditorSceneManager.OpenScene(MainMenuSetup.MenuPath);
            }
            var root=Object.FindFirstObjectByType<MenuCompositionRoot>();
            if(!root.TryGetComponent<ProximityVoice>(out var voice)) voice=Undo.AddComponent<ProximityVoice>(root.gameObject);
            Undo.RecordObject(root,"Wire proximity voice"); root.voice=voice;
            EditorUtility.SetDirty(root); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("[Voice] Session voice wired. No microphone opened.");
        }
    }
}
