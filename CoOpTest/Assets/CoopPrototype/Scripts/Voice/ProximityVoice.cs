using System.Collections;
using System.Collections.Generic;
using CoopPrototype.Frontend;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoopPrototype.Voice
{
    /// <summary>Session-scoped speech relay over the existing NGO transport. The server supplies identity,
    /// enforces packet/rate bounds and selects recipients using authoritative player positions.</summary>
    public sealed class ProximityVoice : MonoBehaviour
    {
        [Header("Host rules (metres)")]
        [Range(3,40)] public float hearingDistance=18;
        [Min(.1f)] public float fullVolumeDistance=2;
        public bool lobbyVoice=true;
        [Header("Local input")]
        public Key pushToTalkKey=Key.T;
        const string Up="crew.voice.up.v1", Down="crew.voice.down.v1", Rules="crew.voice.rules.v1";
        NetworkManager manager;
        GameFlow flow;
        CustomMessagingManager messages;
        public VoicePreferences Preferences { get; private set; }
        public bool SettingsOpen { get; set; }
        public bool TestMicrophone { get; set; }
        public bool Capturing => capture.Running;
        public float InputLevel { get; private set; }
        public bool Transmitting => Time.unscaledTime-lastSent<.2f;
        public string Status { get; private set; } = "Voice ready · hold T to talk";
        public int SentFrames { get; private set; }
        public int ReceivedFrames { get; private set; }
        public int RelayedFrames { get; private set; }
        public int RejectedFrames { get; private set; }
        public int OutputSamples { get { int n=0; foreach(var p in outputs.Values) n+=p.PlayedSamples; return n; } }
        readonly MicrophoneCapture capture=new();
        readonly float[] samples=new float[VoiceCodec.Samples];
        readonly byte[] encoded=new byte[VoiceCodec.Bytes];
        byte[] incoming=new byte[VoiceCodec.Bytes];
        readonly Dictionary<ulong,VoicePlayback> outputs=new();
        readonly Dictionary<ulong,Transform> players=new();
        readonly Dictionary<ulong,string> names=new();
        readonly Dictionary<ulong,Gate> gates=new();
        readonly Dictionary<ulong,uint> receivedSequence=new();
        readonly List<ulong> stale=new();
        uint sequence;
        float refreshAt, retryAt, lastSent=-100, speechUntil;
        string selectedDevice;
        class Gate { public float tokens=12, time; public uint sequence; public bool seen; }

        public void Initialize(GameFlow owner, VoicePreferences preferences)
        {
            flow=owner; manager=owner.Session.manager; Preferences=preferences;
            manager.OnServerStarted+=Attach; manager.OnClientStarted+=Attach;
            manager.OnServerStopped+=Stopped; manager.OnClientStopped+=Stopped;
            manager.OnClientConnectedCallback+=Connected; manager.OnClientDisconnectCallback+=Disconnected;
        }
        void Attach()
        {
            if(!isActiveAndEnabled || messages!=null || manager.CustomMessagingManager==null) return;
            messages=manager.CustomMessagingManager;
            messages.RegisterNamedMessageHandler(Up,ReceiveFromClient);
            messages.RegisterNamedMessageHandler(Down,ReceiveFromServer);
            messages.RegisterNamedMessageHandler(Rules,ReceiveRules);
            sequence=0;
        }
        void Connected(ulong id) { Attach(); if(manager.IsServer) SendRules(id); }
        void Disconnected(ulong id)
        {
            gates.Remove(id); receivedSequence.Remove(id); players.Remove(id); names.Remove(id);
            if(outputs.Remove(id,out var output) && output!=null) Destroy(output.gameObject);
        }
        void Stopped(bool wasHost) => Detach();
        void Detach()
        {
            if(messages!=null)
            {
                messages.UnregisterNamedMessageHandler(Up); messages.UnregisterNamedMessageHandler(Down); messages.UnregisterNamedMessageHandler(Rules);
                messages=null;
            }
            capture.Dispose(); TestMicrophone=false; InputLevel=0;
            foreach(var output in outputs.Values) if(output!=null) Destroy(output.gameObject);
            outputs.Clear(); players.Clear(); names.Clear(); gates.Clear(); receivedSequence.Clear();
        }
        public void SetHostRules(float range, bool inLobby)
        {
            if(manager.IsListening && !manager.IsHost) return;
            hearingDistance=Mathf.Clamp(range,3,40); lobbyVoice=inLobby;
            if(manager.IsServer) foreach(var id in manager.ConnectedClientsIds) SendRules(id);
        }
        void SendRules(ulong id)
        {
            if(id==manager.LocalClientId || messages==null) return;
            using var writer=new FastBufferWriter(8,Allocator.Temp);
            writer.WriteValueSafe(hearingDistance); writer.WriteValueSafe(lobbyVoice);
            messages.SendNamedMessage(Rules,id,writer,NetworkDelivery.ReliableSequenced);
        }
        void ReceiveRules(ulong sender, FastBufferReader reader)
        {
            if(manager.IsServer || sender!=NetworkManager.ServerClientId || reader.Length-reader.Position!=5) return;
            reader.ReadValueSafe(out float range); reader.ReadValueSafe(out bool lobby);
            if(float.IsNaN(range) || float.IsInfinity(range)) return;
            hearingDistance=Mathf.Clamp(range,3,40); lobbyVoice=lobby;
        }
        void Update()
        {
            if(manager==null || Preferences==null) return;
            if(manager.IsListening) Attach();
            if(Time.unscaledTime>=refreshAt) { refreshAt=Time.unscaledTime+.25f; RefreshPlayers(); }
            bool connected=manager.IsConnectedClient && !flow.Transitioning;
            bool allowed=Preferences.Enabled && !Preferences.Muted && !Preferences.Deafened && connected && (flow.InGame || lobbyVoice);
            bool key=Keyboard.current!=null && Keyboard.current[pushToTalkKey].isPressed;
            bool wantCapture=Application.isFocused && (TestMicrophone || (allowed && !SettingsOpen && (!Preferences.PushToTalk || key)));
            if(selectedDevice!=Preferences.Device) { capture.Dispose(); selectedDevice=Preferences.Device; retryAt=0; }
            if(!wantCapture) { capture.Dispose(); InputLevel=0; }
            else
            {
                if(!capture.Running && Time.unscaledTime>=retryAt) { capture.Start(Preferences.Device); retryAt=Time.unscaledTime+3; }
                for(int i=0;i<4 && capture.TryRead(samples,Preferences.Gain,out var level);i++)
                {
                    InputLevel=level;
                    if(level>=Preferences.Threshold) speechUntil=Time.unscaledTime+.18f;
                    if(allowed && !SettingsOpen && !TestMicrophone && (Preferences.PushToTalk || Time.unscaledTime<speechUntil)) SendSamples(samples);
                }
            }
            foreach(var pair in outputs)
            {
                players.TryGetValue(pair.Key,out var speaker);
                pair.Value.Configure(speaker,flow.InGame,hearingDistance,fullVolumeDistance,Preferences.Volume,
                    !Preferences.Enabled || Preferences.Deafened || flow.Transitioning || (flow.InGame && speaker==null));
            }
            Status=TestMicrophone ? "Local mic meter only · nothing is transmitted" : !Preferences.Enabled ? "Voice disabled" : Preferences.Deafened ? "Deafened · microphone also muted" : Preferences.Muted ? "Microphone muted" : !connected ? "Voice connects with your party" : !flow.InGame && !lobbyVoice ? "Lobby voice disabled by host" : SettingsOpen ? "Mic paused while Settings is open" : Transmitting ? "Speaking" : Preferences.PushToTalk ? "Hold "+pushToTalkKey+" to talk" : "Open mic · voice activation";
            if(wantCapture && !string.IsNullOrEmpty(capture.Error)) Status=capture.Error;
        }
        void RefreshPlayers()
        {
            players.Clear(); names.Clear();
            if(manager.SpawnManager==null) return;
            foreach(var obj in manager.SpawnManager.SpawnedObjectsList)
            {
                if(!obj.IsPlayerObject) continue;
                if(obj.TryGetComponent<NetworkPlayerMotor>(out var motor))
                {
                    players[obj.OwnerClientId]=motor.transform;
                    names[obj.OwnerClientId]=obj.TryGetComponent<PlayerIdentity>(out var identity) ? identity.Name : "Crewmate";
                }
                else if(obj.TryGetComponent<LobbyMember>(out var member)) names[obj.OwnerClientId]=member.DisplayName.Value.ToString();
            }
            stale.Clear();
            foreach(var id in outputs.Keys) if(!names.ContainsKey(id)) stale.Add(id);
            foreach(var id in stale) { Destroy(outputs[id].gameObject); outputs.Remove(id); receivedSequence.Remove(id); }
        }
        void SendSamples(float[] pcm)
        {
            if(messages==null || !manager.IsConnectedClient) return;
            VoiceCodec.Encode(pcm,encoded); sequence++; SentFrames++; lastSent=Time.unscaledTime;
            if(manager.IsServer) Relay(manager.LocalClientId,sequence,encoded);
            else
            {
                using var writer=new FastBufferWriter(4+VoiceCodec.Bytes,Allocator.Temp);
                writer.WriteValueSafe(sequence); writer.WriteBytesSafe(encoded);
                messages.SendNamedMessage(Up,NetworkManager.ServerClientId,writer,NetworkDelivery.Unreliable);
            }
        }
        void ReceiveFromClient(ulong sender, FastBufferReader reader)
        {
            if(!manager.IsServer || reader.Length-reader.Position!=4+VoiceCodec.Bytes || !manager.ConnectedClients.ContainsKey(sender)) { RejectedFrames++; return; }
            reader.ReadValueSafe(out uint seq); reader.ReadBytesSafe(ref incoming,VoiceCodec.Bytes);
            Relay(sender,seq,incoming);
        }
        void Relay(ulong sender, uint seq, byte[] block)
        {
            if(flow.Transitioning || (!flow.InGame && !lobbyVoice) || block[2]>88 || block[3]!=0) { RejectedFrames++; return; }
            if(!gates.TryGetValue(sender,out var gate)) gates[sender]=gate=new Gate{time=Time.unscaledTime};
            gate.tokens=Mathf.Min(12,gate.tokens+(Time.unscaledTime-gate.time)*55); gate.time=Time.unscaledTime;
            if(gate.tokens<1 || (gate.seen && unchecked((int)(seq-gate.sequence))<=0)) { RejectedFrames++; return; }
            gate.tokens--; gate.sequence=seq; gate.seen=true;
            var speaker=AuthoritativePlayer(sender);
            if(flow.InGame && speaker==null) return;
            foreach(var id in manager.ConnectedClientsIds)
            {
                if(id==sender) continue; // no self echo
                var listener=AuthoritativePlayer(id);
                if(flow.InGame && (listener==null || Vector3.SqrMagnitude(speaker.position-listener.position)>hearingDistance*hearingDistance)) continue;
                RelayedFrames++;
                if(id==manager.LocalClientId) Play(sender,seq,block);
                else
                {
                    using var writer=new FastBufferWriter(12+VoiceCodec.Bytes,Allocator.Temp);
                    writer.WriteValueSafe(sender); writer.WriteValueSafe(seq); writer.WriteBytesSafe(block);
                    messages.SendNamedMessage(Down,id,writer,NetworkDelivery.Unreliable);
                }
            }
        }
        Transform AuthoritativePlayer(ulong id)
        {
            if(manager.ConnectedClients.TryGetValue(id,out var client) && client.PlayerObject!=null && client.PlayerObject.TryGetComponent<NetworkPlayerMotor>(out var motor)) return motor.transform;
            return null;
        }
        void ReceiveFromServer(ulong sender, FastBufferReader reader)
        {
            if(manager.IsServer || sender!=NetworkManager.ServerClientId || reader.Length-reader.Position!=12+VoiceCodec.Bytes) return;
            reader.ReadValueSafe(out ulong speaker); reader.ReadValueSafe(out uint seq); reader.ReadBytesSafe(ref incoming,VoiceCodec.Bytes);
            Play(speaker,seq,incoming);
        }
        void Play(ulong speaker, uint seq, byte[] block)
        {
            if(speaker==manager.LocalClientId || !Preferences.Enabled || Preferences.Deafened || flow.Transitioning) return;
            if(receivedSequence.TryGetValue(speaker,out var previous) && unchecked((int)(seq-previous))<=0) return;
            if(!names.ContainsKey(speaker)) RefreshPlayers();
            if(!names.ContainsKey(speaker)) return;
            receivedSequence[speaker]=seq;
            if(!outputs.TryGetValue(speaker,out var output))
            {
                var go=new GameObject("Voice · "+speaker); go.transform.SetParent(transform);
                output=go.AddComponent<VoicePlayback>(); outputs[speaker]=output;
            }
            players.TryGetValue(speaker,out var position);
            output.Configure(position,flow.InGame,hearingDistance,fullVolumeDistance,Preferences.Volume,flow.InGame && position==null);
            output.Enqueue(block); ReceivedFrames++;
        }
        public string SpeakingNames()
        {
            string result="";
            foreach(var pair in outputs)
                if(Time.unscaledTime-pair.Value.LastPacket<.25f && Preferences.Enabled && !Preferences.Deafened)
                    result+=(result.Length==0 ? "Hearing: " : ", ")+(names.TryGetValue(pair.Key,out var name) ? name : "Crewmate");
            return result;
        }
        void OnDisable() => Detach();
        void OnDestroy()
        {
            Preferences?.Save();
            Detach();
            if(manager==null) return;
            manager.OnServerStarted-=Attach; manager.OnClientStarted-=Attach;
            manager.OnServerStopped-=Stopped; manager.OnClientStopped-=Stopped;
            manager.OnClientConnectedCallback-=Connected; manager.OnClientDisconnectCallback-=Disconnected;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Explicit synthetic input for repeatable two-peer tests; never opens a microphone.
        public void ProbeTone(float seconds=1) => StartCoroutine(Tone(seconds));
        IEnumerator Tone(float seconds)
        {
            var pcm=new float[VoiceCodec.Samples]; float until=Time.unscaledTime+Mathf.Clamp(seconds,.1f,3);
            while(Time.unscaledTime<until && manager.IsConnectedClient)
            {
                for(int i=0;i<pcm.Length;i++) pcm[i]=.1f*Mathf.Sin(i*2*Mathf.PI*400/VoiceCodec.SampleRate);
                if(Preferences.Enabled && !Preferences.Muted && !Preferences.Deafened) SendSamples(pcm);
                yield return new WaitForSecondsRealtime(.02f);
            }
        }
#endif
    }
}
