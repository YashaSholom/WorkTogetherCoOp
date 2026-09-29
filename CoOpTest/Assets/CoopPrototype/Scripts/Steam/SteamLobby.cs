#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace CoopPrototype.Steam
{
    /// <summary>Steam friends flow for the co-op session.
    /// Host: starts Netcode on the Steam transport and opens a friends-only lobby that advertises the host's SteamID.
    /// Friends join through an invite, the friends list ("Join Game"), or the in-game friends list.
    /// Joining a lobby reads the host SteamID and starts the Netcode client. Leaving the session leaves the lobby.</summary>
    public class SteamLobby : MonoBehaviour
    {
        public NetworkSession session;
        public SteamP2PTransport transport;
        [Range(2, 8)] public int maxPlayers = 4;
        [Tooltip("Lobby key used to reject lobbies from other games that share the test App ID 480.")]
        public string gameKey = "CoopWorkshop";

        public string Status { get; private set; } = "";
        public bool InLobby => lobbyId != 0;
        public ulong LobbyId => lobbyId;

        ulong lobbyId;
        bool hostingLobby;
        Vector2 friendScroll;
        float nextFriendRefresh;
        readonly List<(ulong id, string name, bool online, bool inGame)> friends = new();

#if !DISABLESTEAMWORKS
        const string HostKey = "host_steamid", VersionKey = "version", GameKey = "game";
        Callback<LobbyEnter_t> lobbyEntered;
        Callback<GameLobbyJoinRequested_t> joinRequested;
        Callback<GameRichPresenceJoinRequested_t> richPresenceJoin;
        CallResult<LobbyCreated_t> lobbyCreated;
        bool callbacksReady;
        ulong pendingLaunchLobby;

        void Start()
        {
            // Launched from a Steam invite while the game wasn't running: "+connect_lobby <id>".
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out var id)) pendingLaunchLobby = id;
            if (session != null && session.manager != null)
                session.manager.OnClientStopped += OnNetcodeStopped;
            EnsureCallbacks();
        }

        void EnsureCallbacks()
        {
            if (!SteamBootstrap.Ready || callbacksReady) return;
            {
                callbacksReady = true;
                lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
                joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
                richPresenceJoin = Callback<GameRichPresenceJoinRequested_t>.Create(OnRichPresenceJoin);
                lobbyCreated = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
            }
        }

        void Update()
        {
            if (!SteamBootstrap.Ready) return;
            EnsureCallbacks();
            if (pendingLaunchLobby != 0) { var id = pendingLaunchLobby; pendingLaunchLobby = 0; Join(id); }
            // A host that stopped Netcode some other way shouldn't keep advertising the lobby.
            if (hostingLobby && InLobby && session.manager != null && !session.manager.IsListening) Leave();
        }

        void OnDestroy()
        {
            if (session != null && session.manager != null) session.manager.OnClientStopped -= OnNetcodeStopped;
            lobbyEntered?.Dispose(); joinRequested?.Dispose(); richPresenceJoin?.Dispose(); lobbyCreated?.Dispose();
            if (SteamBootstrap.Ready && InLobby) Leave();
        }

        // ------------------------------------------------------------------ host
        public void Host()
        {
            if (!SteamBootstrap.Ready) { Status = SteamBootstrap.Error; return; }
            if (!session.StartWith(transport, "Host")) { Status = "Could not start host"; return; }
            hostingLobby = true;
            Status = "Opening friends-only lobby...";
            lobbyCreated.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxPlayers));
        }

        void OnLobbyCreated(LobbyCreated_t result, bool ioFailure)
        {
            if (ioFailure || result.m_eResult != EResult.k_EResultOK)
            {
                Status = "Lobby failed: " + (ioFailure ? "IO failure" : result.m_eResult.ToString()) + ". Session is running, but friends can't join via Steam.";
                Debug.LogWarning("[Steam] " + Status);
                hostingLobby = false;
                return;
            }
            lobbyId = result.m_ulSteamIDLobby;
            var lobby = new CSteamID(lobbyId);
            SteamMatchmaking.SetLobbyData(lobby, HostKey, SteamUser.GetSteamID().m_SteamID.ToString());
            SteamMatchmaking.SetLobbyData(lobby, VersionKey, Application.version);
            SteamMatchmaking.SetLobbyData(lobby, GameKey, gameKey);
            SetPresence("Hosting a workshop");
            Status = "Lobby open. Invite friends!";
            Debug.Log($"[Steam] Lobby {lobbyId} created (friends only, {maxPlayers} players)");
        }

        public void OpenInviteOverlay()
        {
            if (!InLobby) return;
            SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(lobbyId));
            if (!SteamUtils.IsOverlayEnabled()) Status = "Steam overlay is not available here (e.g. in the Editor). Use the friends list below.";
        }

        public void Invite(ulong friend)
        {
            if (!InLobby) return;
            bool sent = SteamMatchmaking.InviteUserToLobby(new CSteamID(lobbyId), new CSteamID(friend));
            Status = sent ? "Invite sent to " + SteamFriends.GetFriendPersonaName(new CSteamID(friend)) : "Invite failed";
            Debug.Log("[Steam] " + Status);
        }

        // ------------------------------------------------------------------ join
        void OnJoinRequested(GameLobbyJoinRequested_t data)
        {
            Debug.Log($"[Steam] Join requested via {SteamFriends.GetFriendPersonaName(data.m_steamIDFriend)}");
            Join(data.m_steamIDLobby.m_SteamID);
        }

        void OnRichPresenceJoin(GameRichPresenceJoinRequested_t data)
        {
            var parts = (data.m_rgchConnect ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int index = Array.IndexOf(parts, "+connect_lobby");
            if (index >= 0 && index + 1 < parts.Length && ulong.TryParse(parts[index + 1], out var id)) Join(id);
        }

        public void Join(ulong lobby)
        {
            if (!SteamBootstrap.Ready) { Status = SteamBootstrap.Error; return; }
            if (lobby == lobbyId && InLobby) return;
            if (session.manager.IsListening) { session.Stop(); }
            if (InLobby) Leave();
            hostingLobby = false;
            Status = "Joining friend's lobby...";
            SteamMatchmaking.JoinLobby(new CSteamID(lobby));
        }

        void OnLobbyEntered(LobbyEnter_t data)
        {
            if (hostingLobby) return; // the host's own lobby: already running
            var lobby = new CSteamID(data.m_ulSteamIDLobby);
            if (data.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Status = "Could not join lobby: " + (EChatRoomEnterResponse)data.m_EChatRoomEnterResponse;
                return;
            }
            lobbyId = data.m_ulSteamIDLobby;
            var game = SteamMatchmaking.GetLobbyData(lobby, GameKey);
            var version = SteamMatchmaking.GetLobbyData(lobby, VersionKey);
            if (game != gameKey) { Status = "That lobby belongs to a different game."; Leave(); return; }
            if (version != Application.version) { Status = $"Version mismatch: host {version}, you {Application.version}"; Leave(); return; }
            if (!ulong.TryParse(SteamMatchmaking.GetLobbyData(lobby, HostKey), out var host) || host == 0) { Status = "Lobby has no host"; Leave(); return; }
            transport.connectToSteamId = host;
            SetPresence("In a workshop");
            Status = "Connecting to " + SteamFriends.GetFriendPersonaName(new CSteamID(host)) + "...";
            Debug.Log($"[Steam] Entered lobby {lobbyId}, connecting to host {host}");
            if (!session.StartWith(transport, "Client")) { Status = "Could not start client"; Leave(); }
        }

        // ------------------------------------------------------------------ leave
        void OnNetcodeStopped(bool wasHost)
        {
            if (InLobby) Leave();
        }

        public void Leave()
        {
            if (!SteamBootstrap.Ready) { lobbyId = 0; return; }
            if (lobbyId != 0)
            {
                SteamMatchmaking.LeaveLobby(new CSteamID(lobbyId));
                Debug.Log($"[Steam] Left lobby {lobbyId}");
            }
            lobbyId = 0;
            hostingLobby = false;
            SteamFriends.ClearRichPresence();
        }

        void SetPresence(string text)
        {
            // "connect" makes "Join Game" appear on your name in friends' Steam lists.
            SteamFriends.SetRichPresence("connect", "+connect_lobby " + lobbyId);
            SteamFriends.SetRichPresence("status", text);
        }

        // ------------------------------------------------------------------ friends list
        void RefreshFriends()
        {
            friends.Clear();
            var flags = EFriendFlags.k_EFriendFlagImmediate;
            int count = SteamFriends.GetFriendCount(flags);
            var app = SteamUtils.GetAppID();
            for (int i = 0; i < count; i++)
            {
                var id = SteamFriends.GetFriendByIndex(i, flags);
                var state = SteamFriends.GetFriendPersonaState(id);
                bool inGame = SteamFriends.GetFriendGamePlayed(id, out var game) && game.m_gameID.AppID() == app;
                friends.Add((id.m_SteamID, SteamFriends.GetFriendPersonaName(id), state != EPersonaState.k_EPersonaStateOffline, inGame));
            }
            friends.Sort((a, b) => a.inGame != b.inGame ? b.inGame.CompareTo(a.inGame) : a.online != b.online ? b.online.CompareTo(a.online) : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        }
#else
        public void Host() => Status = "Steam is not supported on this platform";
        public void OpenInviteOverlay() { }
        public void Invite(ulong friend) { }
        public void Join(ulong lobby) { }
        public void Leave() { }
        void RefreshFriends() { }
#endif

        /// <summary>Steam tab of the session menu (IMGUI, drawn by NetworkSession).</summary>
        public void DrawGUI(float height)
        {
            if (!SteamBootstrap.Ready)
            {
                GUILayout.Label("Steam unavailable: " + SteamBootstrap.Error);
                GUILayout.Label("Start the Steam client and restart the game,\nor use the Direct (IP) tab.");
                return;
            }
            GUILayout.Label("Signed in as " + SteamBootstrap.PersonaName);
            if (!string.IsNullOrEmpty(Status)) GUILayout.Label(Status);
            var manager = session.manager;
            if (!manager.IsListening)
            {
                if (GUILayout.Button("Host (friends can join)", GUILayout.Height(28))) Host();
                GUILayout.Label("To join: accept a Steam invite, or use\n\"Join Game\" on a friend in your Steam friends list.");
                return;
            }
            if (!manager.IsServer) { GUILayout.Label("Connected via Steam"); return; }
            if (!InLobby) return;
            if (GUILayout.Button("Invite friends (Steam overlay)  Shift+Tab")) OpenInviteOverlay();
            if (Time.unscaledTime >= nextFriendRefresh) { RefreshFriends(); nextFriendRefresh = Time.unscaledTime + 5; }
            GUILayout.Label($"Friends ({friends.Count(f => f.online)} online)");
            friendScroll = GUILayout.BeginScrollView(friendScroll, GUILayout.Height(height));
            foreach (var friend in friends)
            {
                if (!friend.online) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label((friend.inGame ? "● " : "○ ") + friend.name, GUILayout.Width(170));
                if (GUILayout.Button("Invite")) Invite(friend.id);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }
        /// <summary>Read-only menu data. Sending an invitation still requires an explicit Invite call.</summary>
        public IEnumerable<(ulong id, string name)> OnlineFriends()
        {
            if (!SteamBootstrap.Ready) return Array.Empty<(ulong, string)>();
            RefreshFriends();
            return friends.Where(f => f.online).Select(f => (f.id, f.name)).ToArray();
        }
    }
}
