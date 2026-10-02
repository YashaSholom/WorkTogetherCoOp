using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    /// <summary>One arrested traveller. Replicated; whether the arrest was justified stays on the server until the police review it.</summary>
    public struct Prisoner : INetworkSerializable, IEquatable<Prisoner>
    {
        public int id, caseIndex, member, visit, arrestedDay, pickupDay, bounty;
        public ulong arrester;
        public FixedString64Bytes arresterName;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref id); serializer.SerializeValue(ref caseIndex); serializer.SerializeValue(ref member);
            serializer.SerializeValue(ref visit); serializer.SerializeValue(ref arrestedDay); serializer.SerializeValue(ref pickupDay);
            serializer.SerializeValue(ref bounty); serializer.SerializeValue(ref arrester); serializer.SerializeValue(ref arresterName);
        }
        public bool Equals(Prisoner other) => id == other.id && caseIndex == other.caseIndex && member == other.member && visit == other.visit &&
            arrestedDay == other.arrestedDay && pickupDay == other.pickupDay && bounty == other.bounty && arrester == other.arrester && arresterName.Equals(other.arresterName);
    }

    /// <summary>
    /// Holding cell for arrested visitors and everyone travelling with them. Prisoners wait here until the Galactic Police
    /// collect them a few days later: a justified arrest pays each prisoner's bounty, a false one costs a large fine per
    /// prisoner. The crew may set prisoners free from the cell tablet before that. Server-written; figures are rebuilt
    /// from the replicated list on every peer (late joiners included).
    /// </summary>
    public sealed class HoldingCell : NetworkBehaviour
    {
        public CheckpointSession checkpoint;
        [Tooltip("Where each prisoner stands inside the cell. The number of spots is the cell's capacity.")]
        public Transform[] prisonerSpots = Array.Empty<Transform>();
        [Tooltip("Days from the arrest until the Galactic Police collect the prisoner (they arrive at the start of that day).")]
        [Min(1)] public int policeIntervalDays = 2;
        [Tooltip("Charged per prisoner the police find falsely imprisoned.")]
        [Min(0)] public int falseArrestFine = 250;
        [Tooltip("Optional world-space status board above the cell entrance.")]
        public TextMesh statusBoard;
        public NetworkList<Prisoner> Prisoners;
        public NetworkVariable<int> NextId = new(1);
        // Server-only: the truth about each arrest, revealed when the police review it.
        readonly Dictionary<int, (bool guilty, string reason)> verdicts = new();
        readonly List<GameObject> figures = new();
        bool dirty = true;
        string boardText;

        public int Capacity => prisonerSpots == null ? 0 : prisonerSpots.Length;
        public int Occupied => Prisoners == null ? 0 : Prisoners.Count;
        public int Free => Capacity - Occupied;
        public int Today => checkpoint != null ? checkpoint.Day : 1;
        void Awake() => Prisoners = new NetworkList<Prisoner>();
        public override void OnNetworkSpawn() { Prisoners.OnListChanged += Changed; dirty = true; }
        public override void OnNetworkDespawn() { Prisoners.OnListChanged -= Changed; Clear(); }
        void Changed(NetworkListEvent<Prisoner> change) => dirty = true;

        public TravellerDefinition TravellerOf(Prisoner p) =>
            checkpoint != null && p.caseIndex >= 0 && p.caseIndex < checkpoint.cases.Length ? checkpoint.cases[p.caseIndex].Member(p.member) : null;
        public int DaysUntilPickup(Prisoner p) => Mathf.Max(0, p.pickupDay - Today);
        public static string PickupText(int days) => days <= 0 ? "police arrive today" : days == 1 ? "police arrive tomorrow" : $"police arrive in {days} days";
        public int NextPoliceDay => Occupied == 0 ? -1 : Enumerable.Range(0, Prisoners.Count).Min(i => Prisoners[i].pickupDay);

        /// <summary>Server. Holds the visitor of a case and every companion.</summary>
        public bool Admit(int caseIndex, int visit, bool guilty, string reason, ulong arrester, string arresterName)
        {
            if (!IsServer || checkpoint == null) return false;
            var record = checkpoint.cases[caseIndex];
            int people = 1 + record.CompanionCount;
            if (Free < people) return false;
            for (int member = 0; member < people; member++)
            {
                var traveller = record.Member(member);
                int id = NextId.Value++;
                verdicts[id] = (guilty, reason);
                Prisoners.Add(new Prisoner { id = id, caseIndex = caseIndex, member = member, visit = visit, arrestedDay = Today, pickupDay = Today + policeIntervalDays,
                    bounty = traveller != null ? traveller.bounty : 0, arrester = arrester, arresterName = new FixedString64Bytes(Trim(arresterName)) });
            }
            Debug.Log($"[Checkpoint] Holding cell: visit {visit} arrested ({people} prisoner(s), justified={guilty}); police due day {Today + policeIntervalDays}");
            return true;
        }
        /// <summary>Server. Sets one prisoner free (no bounty, no fine).</summary>
        public bool Release(int id, out string message)
        {
            message = "That prisoner is no longer in the cell.";
            if (!IsServer) return false;
            for (int i = 0; i < Prisoners.Count; i++)
            {
                if (Prisoners[i].id != id) continue;
                var name = TravellerOf(Prisoners[i])?.displayName ?? "Prisoner";
                Prisoners.RemoveAt(i); verdicts.Remove(id);
                message = $"{name} was set free."; Debug.Log("[Checkpoint] Holding cell: " + message);
                return true;
            }
            return false;
        }
        /// <summary>Server, debug. Releases every prisoner.</summary>
        public int ReleaseAll()
        {
            if (!IsServer) return 0;
            int count = Prisoners.Count; Prisoners.Clear(); verdicts.Clear(); return count;
        }
        /// <summary>Server. Start of a new day: the police collect every prisoner who is due and settle bounties and fines.</summary>
        public void PoliceVisit() => PoliceVisit(false);
        /// <summary>Server. <paramref name="everyone"/> (debug) collects all prisoners, due or not.</summary>
        public void PoliceVisit(bool everyone)
        {
            if (!IsServer || checkpoint == null) return;
            var lines = new List<ReportLine>();
            int collected = 0, wrongful = 0;
            for (int i = Prisoners.Count - 1; i >= 0; i--)
            {
                var p = Prisoners[i];
                if (p.pickupDay > Today && !everyone) continue;
                var name = TravellerOf(p)?.displayName ?? "Prisoner";
                var (guilty, reason) = verdicts.TryGetValue(p.id, out var v) ? v : (false, "");
                if (guilty) Pay(lines, p.arrester, p.bounty, $"Bounty: {name}, arrest upheld");
                else { Pay(lines, p.arrester, -falseArrestFine, $"FALSE IMPRISONMENT: {name} was innocent"); wrongful++; }
                Debug.Log($"[Checkpoint] Police review of {name}: {(guilty ? "upheld (" + reason + ")" : "innocent")}");
                Prisoners.RemoveAt(i); verdicts.Remove(p.id); collected++;
            }
            if (collected == 0) return;
            string title = $"GALACTIC POLICE  ·  {collected} collected  ·  " + (wrongful > 0 ? $"{wrongful} FALSELY IMPRISONED" : "all arrests upheld");
            Debug.Log("[Checkpoint] " + title);
            checkpoint.PayAndReport(title, lines);
        }
        // The arresting officer is paid or fined; if they have left, the crew present shares it.
        void Pay(List<ReportLine> lines, ulong arrester, int amount, string reason)
        {
            if (amount == 0) return;
            if (NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(arrester)) { lines.Add(new ReportLine { player = arrester, name = checkpoint.NameOf(arrester), amount = amount, reason = reason }); return; }
            var crew = NetworkManager.ConnectedClientsList.Where(c => c.PlayerObject != null).Select(c => c.PlayerObject.NetworkObjectId).ToList();
            foreach (var player in crew) lines.Add(new ReportLine { player = player, name = checkpoint.NameOf(player), amount = amount / crew.Count, reason = reason + " (shared)" });
        }
        static string Trim(string value) => string.IsNullOrEmpty(value) ? "Officer" : value.Length > 28 ? value.Substring(0, 28) : value;

        // ------------------------------------------------------------------ presentation
        void Update()
        {
            if (!IsSpawned || checkpoint == null) return;
            if (dirty) Rebuild();
            if (statusBoard != null)
            {
                int next = NextPoliceDay;
                string text = $"HOLDING CELL  {Occupied}/{Capacity}\n" + (next < 0 ? "EMPTY" : "POLICE: DAY " + next);
                if (text != boardText) { boardText = text; statusBoard.text = text; }
            }
        }
        void Rebuild()
        {
            dirty = false; Clear();
            for (int i = 0; i < Prisoners.Count && i < Capacity; i++)
            {
                var p = Prisoners[i];
                var traveller = TravellerOf(p);
                var record = checkpoint.cases[Mathf.Clamp(p.caseIndex, 0, checkpoint.cases.Length - 1)];
                var figure = RaceModels.Spawn(RaceModels.RaceOf(traveller, checkpoint.codex), record.SkinOf(p.member), prisonerSpots[i], "Prisoner " + (traveller != null ? traveller.displayName : p.id.ToString()));
                if (figure != null) figures.Add(figure);
            }
        }
        void Clear() { foreach (var figure in figures) if (figure != null) Destroy(figure); figures.Clear(); }
        public override void OnDestroy() { Prisoners?.Dispose(); base.OnDestroy(); }
    }
}
