using System;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CoopPrototype.Editor
{
    /// <summary>Local editor-only validation mailbox. Inactive unless Temp/WorkshopRequest.json exists.
    /// Each MPPM editor uses its own project Temp folder. No runtime/network debug endpoint is shipped.</summary>
    [InitializeOnLoad]
    public static class WorkshopEditorValidation
    {
        [Serializable] public class Request
        {
            public string command;
            public string target;
            public ulong player;
            public Vector3 position;
            public float heading;
            public float seconds = 1;
        }
        static double next;
        [Serializable] class Snapshot
        {
            public bool connected, server;
            public PlayerSample[] players;
            public ItemSample[] items;
            public StateSample[] states;
        }
        [Serializable] class PlayerSample
        {
            public ulong owner, objectId, held;
            public Vector3 position;
            public string animation;
            public float speed;
            public bool carrying, throwing;
            public double throwStarted;
        }
        [Serializable] class ItemSample
        {
            public string name;
            public ulong id, holder, socket;
            public Vector3 position;
        }
        [Serializable] class StateSample { public string name; public bool value; }
        static WorkshopEditorValidation() { EditorApplication.update += Update; }
        static void Update()
        {
            if (EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            next = EditorApplication.timeSinceStartup + .25;
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp"));
            string path = Path.Combine(folder, "WorkshopRequest.json");
            if (!File.Exists(path)) return;
            string result;
            try
            {
                var request = JsonUtility.FromJson<Request>(File.ReadAllText(path)); File.Delete(path);
                result = Execute(request);
            }
            catch (Exception e) { result = "ERROR: " + e; Debug.LogException(e); }
            File.WriteAllText(Path.Combine(folder, "WorkshopResponse.txt"), result);
        }
        static string Execute(Request r)
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp"));
            var manager = NetworkManager.Singleton;
            var local = manager != null && manager.LocalClient != null && manager.LocalClient.PlayerObject != null ? manager.LocalClient.PlayerObject.GetComponent<PlayerInteractor>() : null;
            switch (r.command)
            {
                case "refresh": AssetDatabase.Refresh(); return "Asset refresh requested";
                case "menusetup": MainMenuSetup.Create(); return "Menu scenes created";
                case "voicesetup": VoiceSetup.Configure(); return "Voice configured";
                case "voicecodec": VoiceValidation.Run(); return "Codec validation passed";
                case "voicescreen":
                    Directory.CreateDirectory("TestResults/Voice");
                    ScreenCapture.CaptureScreenshot("TestResults/Voice/Settings.png"); return "Game view capture requested";
                case "settingsscroll":
                    Object.FindFirstObjectByType<UIDocument>().rootVisualElement.Q<ScrollView>(className:"settings-scroll").scrollOffset=new Vector2(0,r.position.y); return "Settings scrolled";
                case "settingsvalue":
                    Object.FindFirstObjectByType<UIDocument>().rootVisualElement.Q<Slider>(r.target).value=r.position.x; return "Settings slider changed";
                case "voiceoutputs":
                    return string.Join("\n",manager.GetComponentsInChildren<Voice.VoicePlayback>().Select(p=>$"{p.name} spatial={p.SpatialBlend} volume={p.PlaybackVolume} played={p.PlayedSamples}"));
                case "voicetone": manager.GetComponent<Voice.ProximityVoice>().ProbeTone(r.seconds); return "Synthetic tone requested (no mic)";
                case "voicerange": manager.GetComponent<Voice.ProximityVoice>().SetHostRules(r.position.x, true); return "Voice range requested";
                case "voicedeafen": manager.GetComponent<Voice.ProximityVoice>().Preferences.Deafened=r.target!="false"; return "Deafen set";
                case "voicemute": manager.GetComponent<Voice.ProximityVoice>().Preferences.Muted=r.target!="false"; return "Mute set";
                case "voicestatus":
                    var voice=manager.GetComponent<Voice.ProximityVoice>();
                    return $"sent={voice.SentFrames} received={voice.ReceivedFrames} relayed={voice.RelayedFrames} rejected={voice.RejectedFrames} outputSamples={voice.OutputSamples} range={voice.hearingDistance} settings={voice.SettingsOpen} capturing={voice.Capturing} status={voice.Status}";
                case "batterycategory": MainMenuSetup.RepairBatteryCategory(); return "Battery category repaired";
                case "menuopen": UnityEditor.SceneManagement.EditorSceneManager.OpenScene(MainMenuSetup.MenuPath); return "Menu opened";
                case "ready":
                    var member = manager != null ? manager.GetComponent<Frontend.GameFlow>()?.LocalMember : null;
                    if (member == null) return "Lobby player is not connected yet";
                    member.SetReadyRpc(r.target != "false"); return "Ready requested";
                case "select": manager.GetComponent<Frontend.GameFlow>().SelectCharacter((int)r.player); return "Character selected";
                case "startgame": return "Started: " + manager.GetComponent<Frontend.GameFlow>().TryStartGame();
                case "leaveparty": manager.GetComponent<Frontend.GameFlow>().Leave(); return "Leave requested";
                case "menupage": Object.FindFirstObjectByType<Frontend.MenuPresenter>().Show(r.target); return "Page shown";
                case "lobbystatus":
                    var flow=manager.GetComponent<Frontend.GameFlow>();
                    return "scene="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name+" connected="+manager.IsConnectedClient+" canStart="+flow.CanStart+" loading="+flow.Transitioning+" inGame="+flow.InGame+"\n"+
                        string.Join("\n",flow.Members.Select(m=>$"member={m.OwnerClientId} character={m.Character.Value} ready={m.Ready.Value} name={m.DisplayName.Value}"))+"\n"+
                        string.Join("\n",Object.FindObjectsByType<CharacterAppearance>(FindObjectsSortMode.None).Where(a=>a.IsSpawned).Select(a=>$"player={a.OwnerClientId} character={a.Variant.Value}"));
                case "apply": WorkshopArtSetup.Apply(); return "Art applied";
                case "preview": WorkshopArtSetup.RenderPreview(r.target); return "Preview saved";
                case "clips": WorkshopArtSetup.RefreshClips(); AssetDatabase.SaveAssets(); return "Clips recreated";
                case "play": EditorApplication.isPlaying = true; return "Play requested";
                case "stop": EditorApplication.isPlaying = false; return "Stop requested";
                case "host": Object.FindFirstObjectByType<NetworkSession>().Launch("Host"); return "Host started: " + manager.IsHost;
                case "client": Object.FindFirstObjectByType<NetworkSession>().Launch("Client"); return "Client started: " + manager.IsClient;
                case "disconnect": manager.Shutdown(); return "Disconnected";
                case "move": local.Motor.DriveForTest(new Vector2(r.position.x,r.position.z), r.heading, r.seconds); return "Movement input sent";
                case "pickup":
                    var item = Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None).First(p=>p.name.StartsWith(r.target));
                    local.RequestInteractionRpc(new NetworkObjectReference(item.NetworkObject)); return "Pickup requested";
                case "interact":
                    var target = Object.FindObjectsByType<NetworkInteractable>(FindObjectsSortMode.None).First(p=>p.name.StartsWith(r.target));
                    local.RequestInteractionRpc(new NetworkObjectReference(target.NetworkObject)); return "Interaction requested";
                case "throw": local.GetComponent<PlayerThrow>().RequestThrowRpc(); return "Throw requested";
                case "drop": local.RequestDropRpc(); return "Drop requested";
                case "arrange":
                    if (!manager.IsServer) throw new InvalidOperationException("Arrange only on server");
                    var motor = manager.ConnectedClients[r.player].PlayerObject.GetComponent<NetworkPlayerMotor>();
                    var cc = motor.GetComponent<CharacterController>(); cc.enabled=false; motor.transform.position=r.position; cc.enabled=true;
                    return "Player arranged";
                case "capture":
                    var cam = local.Motor.playerCamera;
                    var rt = new RenderTexture(1280,800,24); var old=cam.targetTexture; cam.targetTexture=rt; cam.Render();
                    var active=RenderTexture.active; RenderTexture.active=rt;
                    var image=new Texture2D(1280,800,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1280,800),0,0); image.Apply();
                    Directory.CreateDirectory(folder); File.WriteAllBytes(Path.Combine(folder,"WorkshopGame.png"),image.EncodeToPNG());
                    cam.targetTexture=old; RenderTexture.active=active; Object.DestroyImmediate(image); Object.DestroyImmediate(rt);
                    return "Game captured";
                case "status":
                    return "Play="+EditorApplication.isPlaying+" Connected="+(manager!=null && manager.IsConnectedClient)+" Server="+(manager!=null && manager.IsServer)+"\n"+
                        string.Join("\n",Object.FindObjectsByType<PlayerInteractor>(FindObjectsSortMode.None).Select(p=>
                        {
                            var anim=p.GetComponent<PlayerAnimation>();
                            return $"Player {p.OwnerClientId} position={p.transform.position:F3} held={p.HeldItem.Value} speed={(anim!=null ? anim.animator.GetFloat("Speed") : -1):F2} state={(anim!=null ? anim.animator.GetCurrentAnimatorStateInfo(0).shortNameHash : 0)} throwing={p.GetComponent<PlayerThrow>()?.IsBusy}";
                        }))+"\n"+string.Join("\n",Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None).Select(p=>$"Item {p.name} id={p.NetworkObjectId} pos={p.transform.position:F3} holder={p.Holder.Value} socket={p.Socket.Value} velocity={p.GetComponent<Rigidbody>().linearVelocity:F2}"));
                case "snapshot":
                    return JsonUtility.ToJson(new Snapshot
                    {
                        connected = manager != null && manager.IsConnectedClient, server = manager != null && manager.IsServer,
                        players = Object.FindObjectsByType<PlayerInteractor>(FindObjectsSortMode.None).Select(p =>
                        {
                            var animator = p.GetComponent<PlayerAnimation>().animator;
                            var info = animator.GetCurrentAnimatorStateInfo(0);
                            var throwing = p.GetComponent<PlayerThrow>();
                            return new PlayerSample { owner=p.OwnerClientId, objectId=p.NetworkObjectId, held=p.HeldItem.Value, position=p.transform.position,
                                animation=new[]{"Idle","Walk","Carry","Carry Walk","Throw","Pick Up","Wave"}.FirstOrDefault(info.IsName) ?? "Transition",
                                speed=animator.GetFloat("Speed"), carrying=animator.GetBool("Carrying"), throwing=throwing.IsBusy, throwStarted=throwing.StartedAt.Value };
                        }).ToArray(),
                        items = Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None).Select(p=> new ItemSample { name=p.name,id=p.NetworkObjectId,holder=p.Holder.Value,socket=p.Socket.Value,position=p.transform.position }).ToArray(),
                        states = Object.FindObjectsByType<NetworkState>(FindObjectsSortMode.None).Select(p=>new StateSample {name=p.name,value=p.Value.Value}).ToArray()
                    }, true);
            }
            throw new InvalidOperationException("Unknown validation command: " + r.command);
        }
    }
}
