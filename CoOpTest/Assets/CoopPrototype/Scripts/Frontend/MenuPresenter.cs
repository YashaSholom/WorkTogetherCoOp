using System.Linq;
using CoopPrototype.Steam;
using UnityEngine;
using UnityEngine.UIElements;

namespace CoopPrototype.Frontend
{
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(-100)]
    public sealed class MenuPresenter : MonoBehaviour
    {
        GameFlow flow;
        IPlayerPreferences profile;
        MenuBackdrop backdrop;
        VisualElement root;
        string page = "home", rosterKey = "";
        float nextRefresh, feedbackUntil;
        string feedback;
        Voice.ProximityVoice voice;
        SettingsPanel settings;
        public void Initialize(GameFlow gameFlow, IPlayerPreferences preferences, MenuBackdrop sceneBackdrop, Voice.ProximityVoice voiceService)
        { flow = gameFlow; profile = preferences; backdrop = sceneBackdrop; voice = voiceService; }
        void Start()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            Bind("startButton", ()=>Show("connect"));
            Bind("customizeButton", ()=>Show("customize"));
            Bind("settingsButton", ()=>Show("settings"));
            Bind("gameSettings", ()=>Show("settings"));
            Bind("partySettings", ()=>Show("settings"));
            Bind("exitButton", ()=> { profile.Save(); Application.Quit(); });
            Bind("connectBack", ()=> { if (flow.Session.manager.IsListening) flow.Leave(); else Show("home"); });
            Bind("customizeBack", ()=>Show(flow.Session.manager.IsConnectedClient ? "party" : "home"));
            Bind("settingsBack", CloseSettings);
            Bind("previousCharacter", ()=>flow.SelectCharacter(profile.Character-1));
            Bind("nextCharacter", ()=>flow.SelectCharacter(profile.Character+1));
            Bind("partyCustomize", ()=>Show("customize"));
            Bind("hostButton", ()=>Connect(true));
            Bind("joinButton", ()=>Connect(false));
            Bind("readyButton", ()=> { var member=flow.LocalMember; if(member!=null) member.SetReadyRpc(!member.Ready.Value); });
            Bind("launchButton", ()=>flow.TryStartGame());
            Bind("leaveButton", ()=>flow.Leave());
            Bind("gameLeave", ()=>flow.Leave());
            Bind("inviteButton", ()=>
            {
                if(flow.Session.steam != null && flow.Session.steam.InLobby) { Show("friends"); RefreshFriends(); }
                else { GUIUtility.systemCopyBuffer=flow.Session.address+":"+flow.Session.port; Feedback("Address copied. 127.0.0.1 is for this PC; LAN friends need the host computer's LAN IP."); }
            });
            Bind("friendsBack", ()=>Show("party"));
            Bind("refreshFriends", RefreshFriends);
            var name = root.Q<TextField>("playerName"); name.value=profile.DisplayName;
            name.RegisterValueChangedCallback(e=> { profile.DisplayName=e.newValue; profile.Save(); });
            var mode=root.Q<DropdownField>("transportMode"); mode.choices=new(){"Direct IP / LAN", "Steam friends"}; mode.index=0;
            mode.RegisterValueChangedCallback(e=>root.Q<Label>("connectHint").text=e.newValue=="Steam friends" ? "Host here, then invite friends. To join, accept an invite or use Steam's Join Game." : "For two editors, use 127.0.0.1 on both. Friends on your LAN use the host computer's IP.");
            root.Q<TextField>("address").value="127.0.0.1";
            root.Q<IntegerField>("port").value=7777;
            root.Q<IntegerField>("capacity").value=4;
            settings=new SettingsPanel(root,flow,profile,voice);
            Show("home");
        }
        void Bind(string id, System.Action action) => root.Q<Button>(id).clicked += action;
        void Feedback(string message) { feedback = message; feedbackUntil = Time.unscaledTime + 6; }
        public void Show(string screen)
        {
            if(page=="settings" && screen!="settings") settings?.Close();
            page=screen;
            flow.SettingsOpen=screen=="settings";
            voice.SettingsOpen=flow.SettingsOpen;
            if(flow.SettingsOpen) { UnityEngine.Cursor.lockState=CursorLockMode.None; settings?.Open(); }
            root.Query<VisualElement>(className:"page").ForEach(e=>e.EnableInClassList("hidden", e.name!=screen));
        }
        void CloseSettings() => Show(flow.InGame ? "game" : flow.Session.manager.IsConnectedClient ? "party" : "home");
        void Connect(bool host)
        {
            int port=root.Q<IntegerField>("port").value;
            string address=root.Q<TextField>("address").value.Trim();
            bool steam=root.Q<DropdownField>("transportMode").index==1;
            if(port<1 || port>65535 || string.IsNullOrEmpty(address)) { Feedback("Enter an address and a port from 1 to 65535."); return; }
            if(steam && !SteamBootstrap.Ready) { Feedback("Steam isn't available. Start Steam or choose Direct IP / LAN."); return; }
            if(steam && !host) { Feedback("Accept your friend's Steam invite to connect."); return; }
            profile.Save();
            flow.Connect(host,address,(ushort)port,root.Q<IntegerField>("capacity").value,steam);
        }
        void Update()
        {
            if(root==null || flow==null) return;
            if(flow.InGame && !flow.Transitioning && UnityEngine.InputSystem.Keyboard.current!=null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            { if(flow.SettingsOpen) CloseSettings(); else Show("settings"); }
            if(flow.Transitioning && flow.SettingsOpen) Show("party");
            if(Time.unscaledTime<nextRefresh) return;
            nextRefresh=Time.unscaledTime+.15f;
            bool gameplay=flow.InGame && !flow.Transitioning;
            root.Q<VisualElement>("menuShell").EnableInClassList("hidden",gameplay && !flow.SettingsOpen);
            root.Q<VisualElement>("gameHud").EnableInClassList("hidden",!gameplay || flow.SettingsOpen);
            root.Q<Label>("voiceHud").text=voice.Status+"\n"+voice.SpeakingNames();
            if(flow.SettingsOpen) settings.Refresh();
            if(gameplay) { root.Q<Label>("status").text="Settings apply immediately. The shared game keeps running."; return; }
            if(backdrop!=null) backdrop.Present(flow,profile);
            bool connected=flow.Session.manager.IsConnectedClient;
            if(connected && (page=="home" || page=="connect")) Show("party");
            if(!connected && page=="party" && !flow.Transitioning) Show("connect");
            root.Q<Label>("characterName").text=flow.characterNames[Mathf.Clamp(profile.Character,0,flow.CharacterCount-1)];
            root.Q<Label>("characterNumber").text=$"CREW MEMBER {profile.Character+1:00} / {flow.CharacterCount:00}";
            root.Q<Label>("status").text=Time.unscaledTime < feedbackUntil ? feedback : flow.Message;
            if (Time.unscaledTime >= feedbackUntil && flow.Session.steam != null &&
                (page == "friends" || root.Q<DropdownField>("transportMode").index == 1) &&
                !string.IsNullOrEmpty(flow.Session.steam.Status))
                root.Q<Label>("status").text += " " + flow.Session.steam.Status;
            root.Q<Label>("partyTitle").text=flow.Transitioning ? "On our way…" : "Your crew";
            root.Q<Button>("launchButton").SetEnabled(flow.CanStart);
            root.Q<Button>("launchButton").text=flow.Session.manager.IsHost ? "Start game" : "Waiting for the host";
            root.Q<Button>("readyButton").SetEnabled(flow.LocalMember!=null && !flow.Transitioning);
            root.Q<Button>("readyButton").text=flow.LocalMember!=null && flow.LocalMember.Ready.Value ? "Not ready" : "I'm ready";
            root.Q<Button>("partyCustomize").SetEnabled(!flow.Transitioning);
            root.Q<Button>("hostButton").SetEnabled(!flow.Session.manager.IsListening);
            root.Q<Button>("joinButton").SetEnabled(!flow.Session.manager.IsListening);
            root.Q<Button>("inviteButton").text=flow.Session.steam!=null && flow.Session.steam.InLobby ? "Invite friends" : "Copy connection address";
            string key=string.Join("|",flow.Members.Where(m=>m!=null).OrderBy(m=>m.OwnerClientId).Select(m=>$"{m.OwnerClientId}:{m.DisplayName.Value}:{m.Character.Value}:{m.Ready.Value}"));
            if(key!=rosterKey) { rosterKey=key; RefreshRoster(); }
        }
        void RefreshRoster()
        {
            var roster=root.Q<VisualElement>("roster"); roster.Clear();
            foreach(var member in flow.Members.Where(m=>m!=null).OrderBy(m=>m.OwnerClientId))
            {
                var card=new VisualElement(); card.AddToClassList("crew-card");
                var title=new Label(member.DisplayName.Value.ToString()+(member.OwnerClientId==0 ? " · HOST" : "")); title.AddToClassList("crew-name"); card.Add(title);
                card.Add(new Label(flow.characterNames[Mathf.Clamp(member.Character.Value,0,flow.CharacterCount-1)]));
                var badge=new Label(member.Ready.Value ? "READY" : "GETTING READY"); badge.AddToClassList(member.Ready.Value ? "ready" : "waiting"); card.Add(badge);
                if(flow.Session.manager.IsHost && member.OwnerClientId!=0) { var id=member.OwnerClientId; var kick=new Button(()=>flow.Session.Kick(id)){text="Remove"}; kick.AddToClassList("small"); card.Add(kick); }
                roster.Add(card);
            }
        }
        void RefreshFriends()
        {
            var list=root.Q<VisualElement>("friendsList"); list.Clear();
            if(flow.Session.steam==null) return;
            foreach(var friend in flow.Session.steam.OnlineFriends())
            {
                var id=friend.id; list.Add(new Button(()=>flow.Session.steam.Invite(id)){text="Invite "+friend.name});
            }
            if(list.childCount==0) list.Add(new Label("No online friends found. Steam must be running on both computers."));
        }
    }
}
