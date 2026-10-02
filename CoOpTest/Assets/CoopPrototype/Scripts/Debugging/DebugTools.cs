using System;
using System.Collections.Generic;
using System.Linq;
using CoopPrototype.Checkpoint;
using Unity.Netcode;
using UnityEngine;

namespace CoopPrototype.Debugging
{
    /// <summary>
    /// Server-side debug commands for the F1 debug panel (<see cref="DebugMenu"/>). Available only in the Unity Editor and in
    /// Development builds: release builds refuse every request on the server and never show the panel. Any peer may send a
    /// command; the server runs it through the same session/wallet/cell APIs gameplay uses, so the results replicate
    /// normally. Add a case to <see cref="Execute"/> and a button in DebugMenu for each new feature (see FEATURE_IMPLEMENTATION.md).
    /// </summary>
    public sealed class DebugTools : NetworkBehaviour
    {
        public static bool Allowed => Application.isEditor || Debug.isDebugBuild;
        public static DebugTools Instance { get; private set; }
        public CheckpointSession checkpoint;
        public ShopTerminal shop;
        public ContrabandScanner scanner;
        [Tooltip("Named places the debug panel can teleport the player to. Rename/move them freely.")]
        public Transform[] teleportPoints = Array.Empty<Transform>();
        /// <summary>Latest reply from the server for this peer.</summary>
        public string LastReply { get; private set; } = "";
        public float LastReplyAt { get; private set; } = -100;

        void Awake() => Instance = this;
        public override void OnDestroy() { if (Instance == this) Instance = null; base.OnDestroy(); }

        /// <summary>Client entry point. <paramref name="option"/> is command-specific (for visitor commands: the flaw, -1 = roll normally).</summary>
        public void Run(string command, int arg = 0, int option = -1)
        {
            if (!Allowed || !IsSpawned) return;
            RunRpc(command, arg, option);
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void RunRpc(string command, int arg, int option, RpcParams rpcParams = default)
        {
            ulong client = rpcParams.Receive.SenderClientId;
            if (!Allowed) return;
            string result;
            try
            {
                var player = NetworkManager.ConnectedClients.TryGetValue(client, out var connection) && connection.PlayerObject != null ? connection.PlayerObject : null;
                result = Execute(command, arg, option, player);
            }
            catch (Exception e) { result = "Failed: " + e.Message; Debug.LogException(e); }
            Debug.Log($"[Debug] client {client}: {command} {arg} {option} -> {result}");
            ReplyRpc(result, RpcTarget.Single(client, RpcTargetUse.Temp));
        }
        [Rpc(SendTo.SpecifiedInParams)]
        void ReplyRpc(string message, RpcParams rpcParams = default) { LastReply = message; LastReplyAt = Time.unscaledTime; }

        // ------------------------------------------------------------------ server
        string Execute(string command, int arg, int option, NetworkObject player)
        {
            var wallet = player != null ? player.GetComponent<PlayerWallet>() : null;
            var shift = checkpoint != null ? checkpoint.shift : null;
            var cell = checkpoint != null ? checkpoint.holdingCell : null;
            DocumentFlaw? flaw = option >= 0 && Enum.IsDefined(typeof(DocumentFlaw), option) ? (DocumentFlaw)option : null;
            switch (command)
            {
                // ---- economy
                case "credits": wallet.Credits.Value += arg; return $"Credits {(arg >= 0 ? "+" : "")}{arg} → {wallet.Credits.Value} cr";
                case "creditsall":
                    foreach (var w in FindObjectsByType<PlayerWallet>(FindObjectsSortMode.None).Where(w => w.IsSpawned)) w.Credits.Value += arg;
                    return $"Every player {(arg >= 0 ? "+" : "")}{arg} cr";
                case "pending": wallet.AddPending(arg); return $"Today's pay {(arg >= 0 ? "+" : "")}{arg} → {wallet.Pending.Value}";
                case "resetcredits": wallet.Credits.Value = wallet.startingCredits; wallet.Pending.Value = 0; return $"Credits reset to {wallet.startingCredits}";
                // ---- visitors
                case "visitor": return Call(Enumerable.Range(0, checkpoint.cases.Length), flaw, "random visitor");
                case "visitorkind": return Call(Enumerable.Range(0, checkpoint.cases.Length).Where(i => (int)checkpoint.cases[i].vehicle == arg), flaw, ((VehicleKind)arg) + " visitor");
                case "visitorcompanions": return Call(Enumerable.Range(0, checkpoint.cases.Length).Where(i => checkpoint.cases[i].CompanionCount > 0), flaw, "visitor with companions");
                case "visitorcontraband": return Call(Enumerable.Range(0, checkpoint.cases.Length).Where(i => checkpoint.cases[i].packages != null && checkpoint.cases[i].packages.Any(p => p.contraband)), flaw, "visitor with contraband");
                case "visitorcase": return Call(new[] { arg }, flaw, "case " + arg);
                case "arrive": return checkpoint.DebugFinishArrival() ? "Vehicle stopped at the desk" : "No vehicle is arriving";
                case "papers": checkpoint.DebugFinishArrival(); return checkpoint.IssueDocuments(checkpoint.Visit.Value, player.NetworkObjectId) ? "Papers placed in the terminal's document slots" : "Papers were already handed over (or no visitor at the desk)";
                case "scanall": return checkpoint.DebugScanAll() ? "All papers marked scanned" : "Request the papers first";
                case "sendaway": return checkpoint.DebugSendAway() ? "Visitor sent away without a verdict" : "No visitor";
                case "flawchance": checkpoint.flawChance = Mathf.Clamp01(arg / 100f); return $"Flaw chance {arg}% for future visitors";
                // ---- decisions
                case "approve": return checkpoint.Decide(checkpoint.Visit.Value, true, player.NetworkObjectId, out var a) ? "Approved" : a;
                case "reject": return checkpoint.Decide(checkpoint.Visit.Value, false, player.NetworkObjectId, out var r) ? "Rejected" : r;
                case "arrest": checkpoint.Arrest(checkpoint.Visit.Value, player.NetworkObjectId, out var why); return why;
                // ---- day and police
                case "hour": if (shift == null) return "No work shift"; shift.DebugAddMinutes(60); return "Clock → " + shift.TimeText;
                case "endsoon": if (shift == null) return "No work shift"; shift.DebugAddMinutes(Mathf.Max(0, shift.endHour * 60 - 5 - shift.Minutes)); return "Clock → " + shift.TimeText;
                case "closeday": return checkpoint.DebugCloseDay() ? "Day closed and paid out" : "The day is already closed";
                case "nextday": checkpoint.ForceNextDay(); return shift != null ? $"Day {shift.Day.Value} started" : "No work shift";
                case "police": if (cell == null) return "No holding cell"; int before = cell.Occupied; cell.PoliceVisit(true); return $"Police collected {before - cell.Occupied} prisoner(s)";
                case "releaseall": return cell == null ? "No holding cell" : $"Released {cell.ReleaseAll()} prisoner(s)";
                // ---- items and upgrades
                case "spawnitem":
                {
                    if (shop == null || arg < 0 || arg >= shop.offers.Length || shop.offers[arg].prefab == null) return "Not a spawnable item";
                    var item = Instantiate(shop.offers[arg].prefab, player.transform.position + player.transform.forward * 1.2f + Vector3.up * .3f, Quaternion.identity);
                    item.NetworkObject.Spawn(true); return shop.offers[arg].title + " spawned in front of you";
                }
                case "upgradebelt": return scanner != null && scanner.TryUpgrade() ? "X-ray belt second slot unlocked" : "Already unlocked or the scanner is running";
                case "coffee":
                {
                    var motor = player.GetComponent<NetworkPlayerMotor>();
                    int day = shift != null ? shift.Day.Value : 0;
                    motor.CoffeeDay.Value = motor.Caffeinated ? -1 : day; return motor.Caffeinated ? "Coffee speed ON" : "Coffee speed OFF";
                }
                // ---- player
                case "colour":
                {
                    var look = player.GetComponent<CharacterAppearance>();
                    look.RequestNextVariantRpc(); return "Suit colour: " + look.CurrentName;
                }
                case "alwaysrun":
                {
                    var motor = player.GetComponent<NetworkPlayerMotor>();
                    motor.debugAlwaysRun = !motor.debugAlwaysRun; return motor.debugAlwaysRun ? "Always run ON (moving forward runs without Shift)" : "Always run OFF";
                }
                case "teleport":
                {
                    if (arg < 0 || arg >= teleportPoints.Length || teleportPoints[arg] == null) return "Unknown place";
                    var controller = player.GetComponent<CharacterController>();
                    if (controller != null) controller.enabled = false;
                    player.transform.SetPositionAndRotation(teleportPoints[arg].position, teleportPoints[arg].rotation);
                    if (controller != null) controller.enabled = true;
                    return "Teleported to " + teleportPoints[arg].name;
                }
            }
            return "Unknown debug command: " + command;
        }
        string Call(IEnumerable<int> candidates, DocumentFlaw? flaw, string what)
        {
            var list = candidates.Where(i => i >= 0 && i < checkpoint.cases.Length).ToList();
            if (list.Count == 0) return "No case matches: " + what;
            int index = list[UnityEngine.Random.Range(0, list.Count)];
            checkpoint.ForceVisit(index, flaw);
            return $"Called {what}: {checkpoint.cases[index].traveller.displayName} ({checkpoint.cases[index].vehicle}), flaw {checkpoint.Flaw.Value}";
        }
    }
}
