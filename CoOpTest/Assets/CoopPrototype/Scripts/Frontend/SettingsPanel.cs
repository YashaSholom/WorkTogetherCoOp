using System.Collections.Generic;
using CoopPrototype.Voice;
using UnityEngine;
using UnityEngine.UIElements;

namespace CoopPrototype.Frontend
{
    /// <summary>The same settings view is used before connecting, in the party and during gameplay.</summary>
    public sealed class SettingsPanel
    {
        readonly VisualElement root;
        readonly GameFlow flow;
        readonly IPlayerPreferences profile;
        readonly ProximityVoice voice;
        readonly VoicePreferences prefs;
        public SettingsPanel(VisualElement view, GameFlow game, IPlayerPreferences local, ProximityVoice audio)
        {
            root=view; flow=game; profile=local; voice=audio; prefs=voice.Preferences;
            Slider("volume",profile.Volume,v=> { profile.Volume=v; AudioListener.volume=v; });
            Slider("sensitivity",profile.Sensitivity,v=>
            {
                profile.Sensitivity=v;
                var player=flow.Session.manager.LocalClient?.PlayerObject;
                if(player!=null && player.TryGetComponent<NetworkPlayerMotor>(out var motor)) motor.lookSensitivity=v;
            });
            Toggle("fullscreen",Screen.fullScreen,v=>Screen.fullScreen=v);
            Toggle("voiceEnabled",prefs.Enabled,v=>prefs.Enabled=v);
            Toggle("voiceMuted",prefs.Muted,v=>prefs.Muted=v);
            Toggle("voiceDeafened",prefs.Deafened,v=>prefs.Deafened=v);
            Slider("voiceVolume",prefs.Volume,v=>prefs.Volume=v);
            Slider("micGain",prefs.Gain,v=>prefs.Gain=v);
            Slider("voiceThreshold",prefs.Threshold,v=>prefs.Threshold=v);
            var mode=root.Q<DropdownField>("voiceMode"); mode.choices=new(){"Push to talk (hold T)","Open mic (voice activated)"}; mode.index=prefs.PushToTalk ? 0 : 1;
            mode.RegisterValueChangedCallback(e=>prefs.PushToTalk=mode.index==0);
            root.Q<DropdownField>("microphone").RegisterValueChangedCallback(e=>prefs.Device=e.newValue=="System default" ? "" : e.newValue);
            root.Q<Button>("refreshMics").clicked+=RefreshDevices;
            root.Q<Button>("testMic").clicked+=()=>voice.TestMicrophone=!voice.TestMicrophone;
            Slider("voiceRange",voice.hearingDistance,v=>voice.SetHostRules(v,voice.lobbyVoice));
            Toggle("lobbyVoice",voice.lobbyVoice,v=>voice.SetHostRules(voice.hearingDistance,v));
            RefreshDevices();
        }
        void Slider(string id,float value,System.Action<float> change)
        { var field=root.Q<Slider>(id); field.SetValueWithoutNotify(value); field.RegisterValueChangedCallback(e=>change(e.newValue)); }
        void Toggle(string id,bool value,System.Action<bool> change)
        { var field=root.Q<Toggle>(id); field.SetValueWithoutNotify(value); field.RegisterValueChangedCallback(e=>change(e.newValue)); }
        void RefreshDevices()
        {
            var field=root.Q<DropdownField>("microphone"); var choices=new List<string>{"System default"}; choices.AddRange(Microphone.devices);
            if(!string.IsNullOrEmpty(prefs.Device) && !choices.Contains(prefs.Device)) choices.Add(prefs.Device);
            field.choices=choices; field.SetValueWithoutNotify(string.IsNullOrEmpty(prefs.Device) ? "System default" : prefs.Device);
        }
        public void Open() { RefreshDevices(); Refresh(); }
        public void Close() { voice.TestMicrophone=false; profile.Save(); prefs.Save(); }
        public void Refresh()
        {
            root.Q<Label>("voiceStatus").text=voice.Status;
            root.Q<ProgressBar>("micLevel").value=Mathf.Clamp01(voice.InputLevel*5)*100;
            root.Q<Button>("testMic").text=voice.TestMicrophone ? "Stop local mic test" : "Test microphone (local meter)";
            bool host=!flow.Session.manager.IsListening || flow.Session.manager.IsHost;
            var range=root.Q<Slider>("voiceRange"); range.SetEnabled(host); range.SetValueWithoutNotify(voice.hearingDistance); range.label=$"HEARING DISTANCE · {voice.hearingDistance:0} m (HOST)";
            var lobby=root.Q<Toggle>("lobbyVoice"); lobby.SetEnabled(host); lobby.SetValueWithoutNotify(voice.lobbyVoice);
            root.Q<Slider>("voiceThreshold").SetEnabled(!prefs.PushToTalk);
        }
    }
}
