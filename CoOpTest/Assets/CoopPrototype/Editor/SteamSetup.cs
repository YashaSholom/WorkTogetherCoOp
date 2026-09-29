using System.IO;
using CoopPrototype.Steam;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoopPrototype.Editor
{
    /// <summary>Adds the Steam bootstrap, P2P transport and friends lobby to the workshop scene (idempotent),
    /// and copies steam_appid.txt next to Windows/Linux builds while the test App ID is in use.</summary>
    public class SteamSetup : IPostprocessBuildWithReport
    {
        [MenuItem("Coop Prototype/Steam/Add Steam Networking To Scene")]
        public static string Apply()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != PrototypeBuilder.ScenePath)
            {
                if (scene.isDirty) throw new System.InvalidOperationException("Save the open scene first.");
                scene = EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);
            }
            var session = Object.FindFirstObjectByType<NetworkSession>();
            if (session == null || session.manager == null) throw new System.InvalidOperationException("NetworkSession with a NetworkManager not found in the scene.");

            var transport = session.manager.GetComponent<SteamP2PTransport>();
            if (transport == null) transport = Undo.AddComponent<SteamP2PTransport>(session.manager.gameObject);
            // Direct (IP) stays the serialized default so MPPM/tests keep working; Steam is chosen at runtime.
            session.manager.NetworkConfig.NetworkTransport = session.transport;

            var steamRoot = GameObject.Find("Steam");
            if (steamRoot == null) steamRoot = new GameObject("Steam");
            if (steamRoot.GetComponent<SteamBootstrap>() == null) steamRoot.AddComponent<SteamBootstrap>();
            if (!steamRoot.TryGetComponent(out SteamLobby lobby)) lobby = steamRoot.AddComponent<SteamLobby>();
            lobby.session = session;
            lobby.transport = transport;
            session.steam = lobby;
            EditorUtility.SetDirty(session); EditorUtility.SetDirty(lobby); EditorUtility.SetDirty(session.manager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // Replicated player names for nameplates / the host's player list.
            const string playerPath = "Assets/CoopPrototype/Prefabs/NetworkPlayer.prefab";
            var player = PrefabUtility.LoadPrefabContents(playerPath);
            try
            {
                if (player.GetComponent<PlayerIdentity>() == null) { player.AddComponent<PlayerIdentity>(); PrefabUtility.SaveAsPrefabAsset(player, playerPath); }
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }

            string appIdFile = Path.GetFullPath("steam_appid.txt");
            if (!File.Exists(appIdFile)) File.WriteAllText(appIdFile, steamRoot.GetComponent<SteamBootstrap>().appId.ToString());
            Debug.Log("[Steam] Steam networking added to " + scene.path);
            return "steam setup done";
        }

        public int callbackOrder => 100;
        public void OnPostprocessBuild(BuildReport report)
        {
            var target = report.summary.platform;
            if (target != BuildTarget.StandaloneWindows64 && target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneLinux64) return;
            // Only needed when the game is NOT launched by Steam (test App ID / local builds). Don't ship it with a real release.
            string source = Path.GetFullPath("steam_appid.txt");
            if (!File.Exists(source)) return;
            string destination = Path.Combine(Path.GetDirectoryName(report.summary.outputPath), "steam_appid.txt");
            File.Copy(source, destination, true);
            Debug.Log("[Steam] Copied steam_appid.txt to " + destination);
        }
    }
}
