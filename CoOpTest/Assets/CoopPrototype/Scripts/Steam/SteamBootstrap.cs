#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace CoopPrototype.Steam
{
    /// <summary>Starts the Steam API once per process, pumps its callbacks and shuts it down on quit.
    /// Steam is optional: if the client isn't running, Ready stays false and Direct (IP) networking still works.</summary>
    [DefaultExecutionOrder(-1000)]
    public class SteamBootstrap : MonoBehaviour
    {
        [Tooltip("Your Steam App ID. 480 is Valve's public test app (Spacewar); replace it with your own before release.")]
        public uint appId = 480;
        [Tooltip("In builds, relaunch through Steam if the game was started directly. Keep off while testing with 480.")]
        public bool restartThroughSteam = false;

        public static bool Ready { get; private set; }
        public static string Error { get; private set; } = "Steam not started";
        public static string PersonaName { get; private set; } = "";
        static SteamBootstrap instance;

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
#if DISABLESTEAMWORKS
            Error = "Steam is not supported on this platform";
#else
            if (Ready) return;
            if (!Packsize.Test()) { Error = "Steamworks.NET: wrong Packsize for this platform"; Debug.LogError("[Steam] " + Error); return; }
            if (!DllCheck.Test()) { Error = "Steamworks.NET: steam_api DLL version mismatch"; Debug.LogError("[Steam] " + Error); return; }
#if !UNITY_EDITOR
            if (restartThroughSteam && appId != 480 && SteamAPI.RestartAppIfNecessary(new AppId_t(appId))) { Application.Quit(); return; }
#endif
            try
            {
                var result = SteamAPI.InitEx(out string message);
                if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {
                    Error = result == ESteamAPIInitResult.k_ESteamAPIInitResult_NoSteamClient ? "Steam is not running" : "Steam init failed: " + message;
                    Debug.LogWarning("[Steam] " + Error + " (Direct/IP networking still available)");
                    return;
                }
            }
            catch (System.DllNotFoundException e)
            {
                Error = "steam_api library missing: " + e.Message;
                Debug.LogError("[Steam] " + Error);
                return;
            }
            Ready = true;
            Error = "";
            PersonaName = SteamFriends.GetPersonaName();
            // Warm up Steam Datagram Relay so the first P2P connection is quick.
            SteamNetworkingUtils.InitRelayNetworkAccess();
            Debug.Log($"[Steam] Ready as {PersonaName} ({SteamUser.GetSteamID()}), app {SteamUtils.GetAppID()}");
#endif
        }

#if !DISABLESTEAMWORKS
        void Update()
        {
            if (Ready) SteamAPI.RunCallbacks();
        }

        void OnApplicationQuit() => Shutdown();
        void OnDestroy() { if (instance == this) Shutdown(); }

        static void Shutdown()
        {
            if (!Ready) return;
            Ready = false;
            SteamAPI.Shutdown();
        }
#endif
    }
}
