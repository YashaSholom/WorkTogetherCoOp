#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Netcode;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace CoopPrototype.Steam
{
    /// <summary>Netcode for GameObjects transport over Steam Networking Sockets (P2P through Steam Datagram Relay).
    /// No ports, no NAT setup: clients connect to the host's SteamID. The host listens on virtual port 0.</summary>
    public class SteamP2PTransport : NetworkTransport
    {
        [Tooltip("SteamID64 of the host a client connects to (set by SteamLobby from the lobby data).")]
        public ulong connectToSteamId;
        [Tooltip("Steam virtual port. Host and clients must match.")]
        public int virtualPort = 0;

        public override ulong ServerClientId => 0;

#if DISABLESTEAMWORKS
        public override bool IsSupported => false;
        public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery) { }
        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime) { clientId = 0; payload = default; receiveTime = Time.realtimeSinceStartup; return NetworkEvent.Nothing; }
        public override bool StartClient() => false;
        public override bool StartServer() => false;
        public override void DisconnectRemoteClient(ulong clientId) { }
        public override void DisconnectLocalClient() { }
        public override ulong GetCurrentRtt(ulong clientId) => 0;
        public override void Shutdown() { }
        public override void Initialize(NetworkManager networkManager = null) { }
        public ulong GetSteamId(ulong clientId) => 0;
#else
        struct PendingEvent { public NetworkEvent type; public ulong clientId; }

        Callback<SteamNetConnectionStatusChangedCallback_t> statusCallback;
        readonly Queue<SteamNetConnectionStatusChangedCallback_t> statusQueue = new();
        readonly Queue<PendingEvent> pendingEvents = new();
        readonly Dictionary<ulong, HSteamNetConnection> connections = new();      // transport id -> connection
        readonly Dictionary<HSteamNetConnection, ulong> ids = new();              // connection -> transport id
        readonly Dictionary<ulong, ulong> steamIds = new();                       // transport id -> SteamID64
        readonly IntPtr[] messages = new IntPtr[64];
        int messageCount, messageIndex;
        byte[] receiveBuffer = new byte[4096];
        HSteamListenSocket listenSocket = HSteamListenSocket.Invalid;
        HSteamNetPollGroup pollGroup = HSteamNetPollGroup.Invalid;
        HSteamNetConnection serverConnection = HSteamNetConnection.Invalid;
        bool isServer, running;
        ulong nextId = 1;

        public override bool IsSupported => SteamBootstrap.Ready;

        /// <summary>SteamID64 behind a transport client id (server side), for UI/logging.</summary>
        public ulong GetSteamId(ulong clientId) => steamIds.TryGetValue(clientId, out var id) ? id : 0;

        public override void Initialize(NetworkManager networkManager = null) { }

        public override bool StartServer()
        {
            if (!Prepare()) return false;
            isServer = true;
            listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(virtualPort, 0, null);
            pollGroup = SteamNetworkingSockets.CreatePollGroup();
            if (listenSocket == HSteamListenSocket.Invalid) { Debug.LogError("[Steam] Could not open P2P listen socket"); return false; }
            Debug.Log($"[Steam] Hosting on SteamID {SteamUser.GetSteamID()} (virtual port {virtualPort})");
            return true;
        }

        public override bool StartClient()
        {
            if (!Prepare()) return false;
            if (connectToSteamId == 0) { Debug.LogError("[Steam] No host SteamID to connect to"); return false; }
            isServer = false;
            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID(new CSteamID(connectToSteamId));
            serverConnection = SteamNetworkingSockets.ConnectP2P(ref identity, virtualPort, 0, null);
            if (serverConnection == HSteamNetConnection.Invalid) { Debug.LogError("[Steam] ConnectP2P failed"); return false; }
            connections[ServerClientId] = serverConnection;
            ids[serverConnection] = ServerClientId;
            steamIds[ServerClientId] = connectToSteamId;
            Debug.Log($"[Steam] Connecting to host {connectToSteamId}");
            return true;
        }

        bool Prepare()
        {
            if (!SteamBootstrap.Ready) { Debug.LogError("[Steam] Steam is not ready: " + SteamBootstrap.Error); return false; }
            ResetState();
            statusCallback ??= Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnStatusChanged);
            running = true;
            return true;
        }

        void OnStatusChanged(SteamNetConnectionStatusChangedCallback_t data)
        {
            if (running) statusQueue.Enqueue(data);
        }

        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            receiveTime = Time.realtimeSinceStartup;
            payload = default;
            clientId = 0;
            if (!running) return NetworkEvent.Nothing;

            while (statusQueue.Count > 0) HandleStatus(statusQueue.Dequeue());
            if (pendingEvents.Count > 0)
            {
                var e = pendingEvents.Dequeue();
                clientId = e.clientId;
                return e.type;
            }

            // Drain received messages in batches.
            if (messageIndex >= messageCount)
            {
                messageIndex = messageCount = 0;
                if (isServer) { if (pollGroup != HSteamNetPollGroup.Invalid) messageCount = SteamNetworkingSockets.ReceiveMessagesOnPollGroup(pollGroup, messages, messages.Length); }
                else if (serverConnection != HSteamNetConnection.Invalid) messageCount = SteamNetworkingSockets.ReceiveMessagesOnConnection(serverConnection, messages, messages.Length);
                if (messageCount <= 0) { messageCount = 0; return NetworkEvent.Nothing; }
            }
            while (messageIndex < messageCount)
            {
                var pointer = messages[messageIndex++];
                var message = Marshal.PtrToStructure<SteamNetworkingMessage_t>(pointer);
                bool known = ids.TryGetValue(message.m_conn, out var id);
                if (known)
                {
                    if (receiveBuffer.Length < message.m_cbSize) receiveBuffer = new byte[Mathf.NextPowerOfTwo(message.m_cbSize)];
                    Marshal.Copy(message.m_pData, receiveBuffer, 0, message.m_cbSize);
                }
                SteamNetworkingMessage_t.Release(pointer);
                if (!known) continue;
                // Netcode consumes the payload before polling again, so one reusable buffer is safe.
                clientId = id;
                payload = new ArraySegment<byte>(receiveBuffer, 0, message.m_cbSize);
                return NetworkEvent.Data;
            }
            return NetworkEvent.Nothing;
        }

        void HandleStatus(SteamNetConnectionStatusChangedCallback_t data)
        {
            var connection = data.m_hConn;
            var state = data.m_info.m_eState;
            switch (state)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                    if (!isServer || data.m_info.m_hListenSocket != listenSocket) break;
                    var result = SteamNetworkingSockets.AcceptConnection(connection);
                    if (result != EResult.k_EResultOK) { Debug.LogWarning("[Steam] Could not accept connection: " + result); SteamNetworkingSockets.CloseConnection(connection, 0, "Rejected", false); break; }
                    SteamNetworkingSockets.SetConnectionPollGroup(connection, pollGroup);
                    ulong id = nextId++;
                    connections[id] = connection;
                    ids[connection] = id;
                    steamIds[id] = data.m_info.m_identityRemote.GetSteamID64();
                    break;
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    if (ids.TryGetValue(connection, out var connected))
                    {
                        pendingEvents.Enqueue(new PendingEvent { type = NetworkEvent.Connect, clientId = connected });
                        Debug.Log($"[Steam] Connected: {data.m_info.m_identityRemote.GetSteamID64()}");
                    }
                    break;
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    if (ids.TryGetValue(connection, out var closed))
                    {
                        Debug.Log($"[Steam] Connection closed ({state}): {data.m_info.m_szEndDebug}");
                        Forget(closed);
                        pendingEvents.Enqueue(new PendingEvent { type = NetworkEvent.Disconnect, clientId = closed });
                    }
                    SteamNetworkingSockets.CloseConnection(connection, 0, "Closed", false);
                    break;
            }
        }

        void Forget(ulong id)
        {
            if (connections.TryGetValue(id, out var connection)) ids.Remove(connection);
            connections.Remove(id);
            steamIds.Remove(id);
            if (!isServer && id == ServerClientId) serverConnection = HSteamNetConnection.Invalid;
        }

        public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery delivery)
        {
            if (!running || !connections.TryGetValue(clientId, out var connection)) return;
            int flags = delivery == NetworkDelivery.Unreliable || delivery == NetworkDelivery.UnreliableSequenced
                ? Constants.k_nSteamNetworkingSend_UnreliableNoNagle
                : Constants.k_nSteamNetworkingSend_ReliableNoNagle;
            var handle = GCHandle.Alloc(payload.Array, GCHandleType.Pinned);
            try
            {
                var pointer = handle.AddrOfPinnedObject() + payload.Offset;
                var result = SteamNetworkingSockets.SendMessageToConnection(connection, pointer, (uint)payload.Count, flags, out _);
                if (result == EResult.k_EResultNoConnection || result == EResult.k_EResultInvalidParam)
                {
                    Debug.LogWarning($"[Steam] Send failed ({result}); dropping connection {clientId}");
                    SteamNetworkingSockets.CloseConnection(connection, 0, "Send failed", false);
                    Forget(clientId);
                    pendingEvents.Enqueue(new PendingEvent { type = NetworkEvent.Disconnect, clientId = clientId });
                }
                else if (result != EResult.k_EResultOK) Debug.LogWarning($"[Steam] Send result {result} ({payload.Count} bytes)");
            }
            finally { handle.Free(); }
        }

        public override ulong GetCurrentRtt(ulong clientId)
        {
            if (!running || !connections.TryGetValue(clientId, out var connection)) return 0;
            var status = new SteamNetConnectionRealTimeStatus_t();
            var lanes = new SteamNetConnectionRealTimeLaneStatus_t();
            return SteamNetworkingSockets.GetConnectionRealTimeStatus(connection, ref status, 0, ref lanes) == EResult.k_EResultOK ? (ulong)Mathf.Max(0, status.m_nPing) : 0;
        }

        public override void DisconnectRemoteClient(ulong clientId)
        {
            if (!connections.TryGetValue(clientId, out var connection)) return;
            // Linger so the final "you were disconnected" message is delivered.
            SteamNetworkingSockets.CloseConnection(connection, 0, "Disconnected by host", true);
            Forget(clientId);
        }

        public override void DisconnectLocalClient()
        {
            if (serverConnection != HSteamNetConnection.Invalid)
                SteamNetworkingSockets.CloseConnection(serverConnection, 0, "Client left", true);
            Forget(ServerClientId);
        }

        public override void Shutdown()
        {
            if (!running) return;
            running = false;
            if (SteamBootstrap.Ready)
            {
                foreach (var connection in connections.Values) SteamNetworkingSockets.CloseConnection(connection, 0, "Shutdown", false);
                if (listenSocket != HSteamListenSocket.Invalid) SteamNetworkingSockets.CloseListenSocket(listenSocket);
                if (pollGroup != HSteamNetPollGroup.Invalid) SteamNetworkingSockets.DestroyPollGroup(pollGroup);
                for (int i = messageIndex; i < messageCount; i++) SteamNetworkingMessage_t.Release(messages[i]);
            }
            ResetState();
            statusCallback?.Dispose();
            statusCallback = null;
            Debug.Log("[Steam] Transport shut down");
        }

        void ResetState()
        {
            statusQueue.Clear(); pendingEvents.Clear(); connections.Clear(); ids.Clear(); steamIds.Clear();
            messageCount = messageIndex = 0;
            listenSocket = HSteamListenSocket.Invalid;
            pollGroup = HSteamNetPollGroup.Invalid;
            serverConnection = HSteamNetConnection.Invalid;
            nextId = 1;
        }
#endif
    }
}
