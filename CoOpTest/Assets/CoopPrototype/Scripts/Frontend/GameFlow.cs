using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoopPrototype.Frontend
{
    public sealed class GameFlow : MonoBehaviour
    {
        public GameObject gameplayPlayerPrefab;
        public string menuScene = "MainMenu";
        public string gameScene = "GameWorld";
        public string[] characterNames = { "Orange", "Blue", "Green", "Yellow" };
        public bool Transitioning { get; private set; }
        public bool InGame { get; private set; }
        public bool SettingsOpen { get; set; }
        public string Message { get; private set; } = "The next shuttle is already on its way.";
        public int CharacterCount => characterNames.Length;
        public NetworkSession Session { get; private set; }
        public IPlayerPreferences Preferences { get; private set; }
        public IReadOnlyList<LobbyMember> Members => members;
        public LobbyMember LocalMember => members.FirstOrDefault(m => m != null && m.IsOwner);
        public bool CanStart => Session.manager.IsHost && !Transitioning && !InGame && members.Count > 0 &&
            members.Count == Session.manager.ConnectedClientsIds.Count && members.All(m => m != null && m.Ready.Value);
        readonly List<LobbyMember> members = new();
        readonly Dictionary<ulong, (int character, string name)> profiles = new();
        bool hookedSceneEvents, returning;
        GameSceneContext context;
        float connectingSince;

        public void Initialize(NetworkSession session, IPlayerPreferences preferences)
        {
            Session = session; Preferences = preferences;
            session.manager.OnClientConnectedCallback += Connected;
            session.manager.OnClientDisconnectCallback += Disconnected;
            SceneManager.sceneLoaded += SceneLoaded;
        }
        public void Register(LobbyMember member)
        {
            if (!members.Contains(member)) members.Add(member);
            member.Initialize(this, Preferences);
            HookSceneEvents();
        }
        public void Unregister(LobbyMember member) => members.Remove(member);
        void Connected(ulong id)
        {
            HookSceneEvents();
            Message = "Party open. Choose your character, then ready up.";
        }
        void HookSceneEvents()
        {
            if (hookedSceneEvents || Session.manager.SceneManager == null) return;
            Session.manager.SceneManager.OnLoadEventCompleted += LoadedForAll;
            Session.manager.SceneManager.OnSceneEvent += SceneEvent;
            hookedSceneEvents = true;
        }
        void SceneEvent(SceneEvent ev)
        {
            if (ev.SceneEventType == SceneEventType.Load && ev.SceneName == gameScene)
            { Transitioning = true; Message = "Loading the checkpoint for everyone…"; }
        }
        public void Connect(bool host, string address, ushort port, int capacity, bool steam)
        {
            if (Session.manager.IsListening || Transitioning) return;
            Session.address = address; Session.port = port; Session.maxPlayers = Mathf.Clamp(capacity, 2, 4);
            if (Session.steam != null) Session.steam.maxPlayers = Session.maxPlayers;
            Message = host ? "Opening your party…" : "Connecting…";
            connectingSince = Time.unscaledTime;
            if (steam && Session.steam != null) Session.steam.Host();
            else Session.Launch(host ? "Host" : "Client");
            if (!Session.manager.IsListening) Message = Session.Status;
            HookSceneEvents();
        }
        public void ConnectionStarted() => connectingSince = Time.unscaledTime;
        public void SelectCharacter(int index)
        {
            Preferences.Character = ((index % CharacterCount) + CharacterCount) % CharacterCount;
            Preferences.Save();
            if (LocalMember != null) LocalMember.SetProfileRpc(Preferences.DisplayName, Preferences.Character);
        }
        public bool TryStartGame()
        {
            if (!CanStart) { Message = "Everyone must be ready. Only the host can start."; return false; }
            profiles.Clear();
            foreach (var member in members) profiles[member.OwnerClientId] = (member.Character.Value, member.DisplayName.Value.ToString());
            Transitioning = true;
            var result = Session.manager.SceneManager.LoadScene(gameScene, LoadSceneMode.Single);
            if (result != SceneEventProgressStatus.Started) { Transitioning = false; Message = "Could not load the checkpoint: " + result; return false; }
            Message = "Loading the checkpoint for everyone…";
            Debug.Log("[Lobby] All ready. Shared scene load started.");
            return true;
        }
        void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != gameScene) return;
            context = scene.GetRootGameObjects().Select(r => r.GetComponent<GameSceneContext>()).FirstOrDefault(c => c != null);
            if (context == null) { Message = "Game scene is missing its GameSceneContext."; Leave(); return; }
            Session.spawnPoints = context.spawnPoints;
            Session.overviewCamera = context.loadingCamera;
            InGame = true;
        }
        void LoadedForAll(string sceneName, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (sceneName != gameScene || !Session.manager.IsServer) return;
            foreach (var id in timedOut) Session.manager.DisconnectClient(id, "Scene loading timed out.");
            foreach (var id in Session.manager.ConnectedClientsIds.ToArray())
            {
                if (!completed.Contains(id)) continue;
                var client = Session.manager.ConnectedClients[id];
                if (client.PlayerObject != null) client.PlayerObject.Despawn(true);
                var spawn = context.spawnPoints[(int)(id % (ulong)context.spawnPoints.Length)];
                var player = Instantiate(gameplayPlayerPrefab, spawn.position, spawn.rotation);
                player.GetComponent<NetworkPlayerMotor>().InitializeSpawn(spawn.position);
                player.GetComponent<NetworkObject>().SpawnAsPlayerObject(id, true);
                if (profiles.TryGetValue(id, out var profile))
                {
                    player.GetComponent<CharacterAppearance>().ServerSetPreferred(profile.character);
                    player.GetComponent<PlayerIdentity>().DisplayName.Value = new Unity.Collections.FixedString64Bytes(profile.name);
                }
            }
            Transitioning = false;
            Message = "Checkpoint shift started.";
        }
        public void BindPlayer(NetworkPlayerMotor motor)
        {
            if (motor.IsOwner) motor.lookSensitivity = Preferences.Sensitivity;
            Transitioning = false;
        }
        public void Leave()
        {
            if (returning) return;
            returning = true;
            StartCoroutine(ReturnToMenu());
        }
        IEnumerator ReturnToMenu()
        {
            Unhook(); Session.Stop(); Cursor.lockState = CursorLockMode.None;
            while (Session.manager.ShutdownInProgress) yield return null;
            members.Clear(); profiles.Clear(); Transitioning = false; InGame = false;
            // Replace the session scope as a unit. The new menu gets a clean manager and injected services.
            SceneManager.LoadSceneAsync(menuScene, LoadSceneMode.Single);
            if (Session.steam != null) Destroy(Session.steam.gameObject);
            Destroy(gameObject);
        }
        void Disconnected(ulong id)
        {
            Message = string.IsNullOrEmpty(Session.manager.DisconnectReason) ? "A player left the party." : Session.manager.DisconnectReason;
            if (id == Session.manager.LocalClientId && !returning)
            {
                if (InGame || Transitioning) Leave();
                else { Unhook(); members.Clear(); Message = "Disconnected. " + Session.manager.DisconnectReason; }
            }
        }
        void Update()
        {
            if (Session == null) return;
            if (Session.manager.IsListening) HookSceneEvents();
            if (Session.manager.IsClient && !Session.manager.IsConnectedClient && Time.unscaledTime - connectingSince > 15 && !InGame && !Transitioning)
            { Session.Stop(); Message = "Connection timed out. Check the host address and port."; Unhook(); }
        }
        void Unhook()
        {
            if (hookedSceneEvents && Session.manager.SceneManager != null)
            { Session.manager.SceneManager.OnLoadEventCompleted -= LoadedForAll; Session.manager.SceneManager.OnSceneEvent -= SceneEvent; }
            hookedSceneEvents = false;
        }
        void OnDestroy()
        {
            if (Session == null) return;
            Unhook(); SceneManager.sceneLoaded -= SceneLoaded;
            Session.manager.OnClientConnectedCallback -= Connected;
            Session.manager.OnClientDisconnectCallback -= Disconnected;
        }
    }
}
