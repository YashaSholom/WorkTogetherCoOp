#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace CoopPrototype
{
    /// <summary>Opt-in development-only multi-process integration harness. Inactive without -probe PATH.</summary>
    public class PrototypeProbe : MonoBehaviour
    {
        string path;
        float next;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-probe");
            if (index >= 0 && index + 1 < args.Length) new GameObject("Development Probe").AddComponent<PrototypeProbe>().path = args[index + 1];
        }
        void Update()
        {
            if (string.IsNullOrEmpty(path) || Time.unscaledTime < next) return;
            next = Time.unscaledTime + .25f;
            var net = NetworkManager.Singleton;
            if (net == null || !net.IsListening) return;
            try
            {
                if (File.Exists(path + ".command"))
                {
                    var command = File.ReadAllText(path + ".command").Trim().Split('|'); File.Delete(path + ".command");
                    var local = net.LocalClient?.PlayerObject?.GetComponent<PlayerInteractor>();
                    if (command[0] == "interact" && local != null)
                    {
                        var target = FindObjectsByType<NetworkInteractable>(FindObjectsSortMode.None).First(x => x.name == command[1]);
                        local.RequestInteractionRpc(new NetworkObjectReference(target.NetworkObject));
                    }
                    if (command[0] == "drop" && local != null) local.RequestDropRpc();
                    if (command[0] == "move" && local != null) local.Motor.DriveForTest(new Vector2(float.Parse(command[1]), float.Parse(command[2])), float.Parse(command[3]), float.Parse(command[4]));
                    if (command[0] == "arrange" && net.IsServer)
                    {
                        var player = net.ConnectedClients[ulong.Parse(command[1])].PlayerObject;
                        var controller = player.GetComponent<CharacterController>(); controller.enabled = false;
                        player.transform.position = new Vector3(float.Parse(command[2]), 1.05f, float.Parse(command[3])); controller.enabled = true;
                    }
                    if (command[0] == "quit") Application.Quit();
                }
                var lines = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None).Where(x => x.IsSpawned).OrderBy(x => x.NetworkObjectId).Select(x =>
                {
                    var item = x.GetComponent<PickupItem>(); var slot = x.GetComponent<ItemSocket>(); var state = x.GetComponent<NetworkState>();
                    return $"{x.NetworkObjectId}|{x.name}|owner={x.OwnerClientId}|pos={x.transform.position.ToString("F2")}" +
                        (item != null ? $"|holder={item.Holder.Value}|socket={item.Socket.Value}" : "") +
                        (slot != null ? $"|occupant={slot.Occupant.Value}" : "") + (state != null ? $"|state={state.Value.Value}" : "");
                });
                File.WriteAllLines(path + ".state", lines.Concat(new[] { "lamps=" + FindObjectsByType<Light>(FindObjectsSortMode.None).Count(x => x.name.StartsWith("Generator lamp") && x.enabled), "door=" + GameObject.Find("Door").transform.position.ToString("F2") }));
            }
            catch (Exception e) { Debug.LogError("[Probe] " + e); }
        }
    }
}
#endif
