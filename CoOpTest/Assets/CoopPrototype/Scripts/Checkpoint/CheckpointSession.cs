using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    public enum VisitPhase { Arriving, Waiting, Reviewing, Approved, Rejected, Cooldown, Closed, Arrested }
    /// <summary>At most one thing is wrong with a visitor's papers. Forged IDs are only visible under a UV lamp.</summary>
    public enum DocumentFlaw { None, Expired, OwnerMismatch, PlateMismatch, PortraitMismatch, Forged, SealMismatch, BadgeMismatch, RaceMismatch }

    /// <summary>One line of a case verdict or day summary, as shown to the crew.</summary>
    public struct ReportLine { public ulong player; public string name, reason; public int amount; }

    /// <summary>Server-owned visit lifecycle. Static case assets are shared; visit state is replicated.</summary>
    public sealed class CheckpointSession : NetworkBehaviour
    {
        public TravellerCase[] cases;
        public DocumentItem documentPrefab;
        [Tooltip("Terminal document slots, in fill order. Requested papers are placed straight into them (and scanned). Add slots for new document types.")]
        public ItemSocket[] documentSlots = Array.Empty<ItemSocket>();
        [Tooltip("Fallback only: where papers appear when there are no free document slots (or none are set up).")]
        public Transform[] documentDeliveryPoints = Array.Empty<Transform>();
        [Tooltip("Cargo vehicles spawn their packages here when they stop. Required if any case has packages.")]
        public CargoPackage packagePrefab;
        public CheckpointVehicleView vehicleView;
        [Tooltip("Cargo spots on the parked truck's bed (one per cargo point). Crates arrive loaded in them; an admitted truck leaves only with every crate back in a spot.")]
        public ItemSocket[] cargoSpots = Array.Empty<ItemSocket>();
        [Tooltip("Optional working day. Without it visitors keep coming forever.")]
        public WorkShift shift;
        [Tooltip("Pick the next visitor at random (never the same one twice, often switching vehicle type). Off = cycle in order.")]
        public bool randomVisitors = true;
        [Tooltip("Reference data (federation seals, vehicle makers, races) for the terminal codex and document seals/badges.")]
        public CheckpointCodex codex;
        [Tooltip("Where arrested visitors and their companions are held until the Galactic Police collect them.")]
        public HoldingCell holdingCell;
        [Tooltip("Fallback date when there is no WorkShift.")]
        public string gameDate = "2219-09-29";
        [Min(1)] public float arrivalSeconds = 6, exitSeconds = 5, rejectionSeconds = 3, intervalSeconds = 3;
        [Header("Document flaws")]
        [Tooltip("Optional obviously incorrect ID photo. Useful while multiple visitors share one model/portrait; otherwise another traveller's photo is used.")]
        public Texture2D mismatchedPortrait;
        [Range(0, 1)] public float flawChance = .4f;
        [Tooltip("Share of flawed visitors whose ID is forged (only a UV lamp reveals it).")]
        [Range(0, 1)] public float forgedShare = .2f;
        [Header("Payouts (credits)")]
        [Tooltip("Seconds after the decision before the crew learns whether it was right.")]
        [Min(0)] public float verdictDelay = 2.5f;
        public int documentCheckFee = 5, packageCheckFee = 5, contrabandFoundBonus = 25, correctApproveBonus = 15, correctRejectBonus = 20;
        public int wrongApproveFine = 30, contrabandApprovedFine = 40, missedContrabandFine = 15, rejectValidFine = 60;

        public NetworkVariable<VisitPhase> Phase = new(VisitPhase.Arriving);
        public NetworkVariable<int> CaseIndex = new();
        public NetworkVariable<int> Visit = new(1);
        public NetworkVariable<double> PhaseStarted = new();
        public NetworkVariable<ulong> Scanned = new();
        public NetworkVariable<int> ApprovedCount = new(), RejectedCount = new(), ArrestedCount = new();
        public NetworkVariable<DocumentFlaw> Flaw = new();
        public NetworkVariable<int> FlawSeed = new();

        /// <summary>Client-side copy of the latest verdict and the latest day summary, for the HUD.</summary>
        public string ReportTitle { get; private set; } = "";
        public List<ReportLine> Report { get; } = new();
        public float ReportReceivedAt { get; private set; } = -100;
        public string SummaryTitle { get; private set; } = "";
        public List<ReportLine> Summary { get; } = new();

        public TravellerCase Current => cases != null && cases.Length > 0 ? cases[Mathf.Clamp(CaseIndex.Value, 0, cases.Length - 1)] : null;
        public bool AtDesk => Phase.Value == VisitPhase.Waiting || Phase.Value == VisitPhase.Reviewing;
        public bool HasVisitor => Phase.Value != VisitPhase.Cooldown && Phase.Value != VisitPhase.Closed;
        public double Elapsed => IsSpawned ? Math.Max(0, NetworkManager.ServerTime.Time - PhaseStarted.Value) : 0;
        public int ScannedCount => Current == null ? 0 : Enumerable.Range(0, Current.documents.Length).Count(IsScanned);
        public bool AllScanned => Current != null && Current.documents.Length > 0 && ScannedCount == Current.documents.Length;
        public bool IsScanned(int index) => index >= 0 && index < 64 && (Scanned.Value & (1UL << index)) != 0;
        public string Date => shift != null ? shift.DateText : gameDate;
        public int Day => shift != null ? shift.Day.Value : 1;

        // Server-only record of who did what during the current visit.
        sealed class Ledger
        {
            public int visit, caseIndex; public DocumentFlaw flaw;
            public readonly Dictionary<int, ulong> documentChecks = new(), packageChecks = new();
            public readonly HashSet<int> contrabandFound = new();
            public ulong decider = ulong.MaxValue; public bool approved, arrested; public double verdictAt = -1;
        }
        Ledger ledger = new();

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            if (cases == null || cases.Length == 0 || documentPrefab == null || ((documentSlots?.Length ?? 0) == 0 && (documentDeliveryPoints?.Length ?? 0) == 0) ||
                cases.Any(c => c == null || c.traveller == null || c.documents == null || c.documents.Length == 0 || c.documents.Length > 64 || c.documents.Any(d => d == null || d.traveller != c.traveller)))
            { Debug.LogError("[Checkpoint] Incomplete case/prefab/delivery configuration.", this); enabled = false; return; }
            if (cases.Any(c => c.PackageCount > 0) && (packagePrefab == null || vehicleView == null || vehicleView.cargoPoints == null || cases.Any(c => c.PackageCount > vehicleView.cargoPoints.Length || c.PackageCount > (cargoSpots?.Length ?? 0))))
            { Debug.LogError("[Checkpoint] A case has packages but the package prefab or the vehicle's cargo points are missing.", this); enabled = false; return; }
            BeginVisit(randomVisitors ? PickNextCase() : 0, false);
        }
        void Update()
        {
            if (!IsSpawned || !IsServer || Current == null) return;
            if (ledger.verdictAt >= 0 && NetworkManager.ServerTime.Time >= ledger.verdictAt) ResolveVerdict();
            // Cargo spots exist only while a truck is parked, one per crate it carries.
            if (cargoSpots != null)
                for (int i = 0; i < cargoSpots.Length; i++)
                {
                    bool available = AtDesk && i < Current.PackageCount;
                    if (cargoSpots[i] != null && cargoSpots[i].IsSpawned && cargoSpots[i].Available.Value != available) cargoSpots[i].Available.Value = available;
                }
            switch (Phase.Value)
            {
                case VisitPhase.Arriving when Elapsed >= arrivalSeconds: SetPhase(VisitPhase.Waiting); SpawnCargo(); break;
                case VisitPhase.Approved when Elapsed >= exitSeconds:
                case VisitPhase.Arrested when Elapsed >= exitSeconds:
                case VisitPhase.Rejected when Elapsed >= rejectionSeconds:
                    ClearDocuments(); SetPhase(VisitPhase.Cooldown); break;
                case VisitPhase.Cooldown when Elapsed >= intervalSeconds:
                    if (shift != null && !shift.IsOpen) CloseDay();
                    else BeginVisit(randomVisitors ? PickNextCase() : (CaseIndex.Value + 1) % cases.Length, true);
                    break;
            }
        }
        void BeginVisit(int caseIndex, bool next)
        {
            if (next) Visit.Value++;
            CaseIndex.Value = caseIndex; Scanned.Value = 0;
            RollFlaw();
            ledger = new Ledger { visit = Visit.Value, caseIndex = caseIndex, flaw = Flaw.Value };
            SetPhase(VisitPhase.Arriving);
        }
        /// <summary>Random visitor that is not the current one; half the time forced onto the other vehicle kind so both get tested.</summary>
        int PickNextCase()
        {
            var candidates = Enumerable.Range(0, cases.Length).Where(i => i != CaseIndex.Value || cases.Length == 1).ToList();
            var otherKind = candidates.Where(i => cases[i].vehicle != Current.vehicle).ToList();
            if (otherKind.Count > 0 && UnityEngine.Random.value < .5f) candidates = otherKind;
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }
        void SetPhase(VisitPhase phase)
        {
            PhaseStarted.Value = NetworkManager.ServerTime.Time; Phase.Value = phase;
            Debug.Log($"[Checkpoint] Visit {Visit.Value}: {phase}");
        }

        // ------------------------------------------------------------------ day
        void CloseDay()
        {
            if (ledger.verdictAt >= 0) ResolveVerdict();
            SetPhase(VisitPhase.Closed);
            var lines = new List<ReportLine>();
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                if (client.PlayerObject == null || !client.PlayerObject.TryGetComponent<PlayerWallet>(out var wallet)) continue;
                int earned = wallet.Payout();
                lines.Add(new ReportLine { player = client.PlayerObject.NetworkObjectId, name = NameOf(client.PlayerObject.NetworkObjectId), amount = earned, reason = $"paid out  ·  balance {wallet.Credits.Value}" });
            }
            shift.CloseDay();
            SummaryRpc($"DAY {shift.Day.Value} COMPLETE  ·  Approved {ApprovedCount.Value}  /  Returned {RejectedCount.Value}", Encode(lines));
        }
        /// <summary>Server. Called by the next-day button.</summary>
        public bool StartNextDay()
        {
            if (!IsServer || shift == null || Phase.Value != VisitPhase.Closed || !shift.StartNextDay()) return false;
            BeginVisit(randomVisitors ? PickNextCase() : (CaseIndex.Value + 1) % cases.Length, true);
            ClearSummaryRpc();
            // The Galactic Police arrive at the start of the day to collect prisoners who are due.
            if (holdingCell != null) holdingCell.PoliceVisit();
            return true;
        }

        // ------------------------------------------------------------------ documents and flaws
        void RollFlaw()
        {
            var flaw = DocumentFlaw.None;
            var current = Current;
            if (UnityEngine.Random.value < flawChance)
            {
                var options = new List<DocumentFlaw>();
                bool identity = current.documents.Any(d => d.kind != null && d.kind.showPortrait);
                bool vehicle = current.documents.Any(d => d.fields.Any(f => f.key == "registration"));
                if (identity) options.Add(DocumentFlaw.Expired);
                if (identity && cases.Length > 1) options.Add(DocumentFlaw.PortraitMismatch);
                if (vehicle && cases.Length > 1 && current.documents.Any(d => d.fields.Any(f => f.key == "owner"))) options.Add(DocumentFlaw.OwnerMismatch);
                if (vehicle) options.Add(DocumentFlaw.PlateMismatch);
                var traveller = current.traveller;
                if (identity && codex != null && traveller.federation != null && codex.federations.Length > 1) options.Add(DocumentFlaw.SealMismatch);
                if (vehicle && codex != null && traveller.vehicle != null && traveller.vehicle.manufacturer != null && codex.manufacturers.Length > 1) options.Add(DocumentFlaw.BadgeMismatch);
                if (identity && codex != null && traveller.raceInfo != null && codex.races.Length > 1 && current.documents.Any(d => d.fields.Any(f => f.key == "race"))) options.Add(DocumentFlaw.RaceMismatch);
                if (identity && UnityEngine.Random.value < forgedShare) flaw = DocumentFlaw.Forged;
                else if (options.Count > 0) flaw = options[UnityEngine.Random.Range(0, options.Count)];
            }
            Flaw.Value = flaw; FlawSeed.Value = UnityEngine.Random.Range(1, 100000);
            if (flaw != DocumentFlaw.None) Debug.Log($"[Checkpoint] Visit {Visit.Value}: papers have a flaw ({flaw})");
        }
        bool FlawApplies(int caseIndex) => caseIndex == CaseIndex.Value && Flaw.Value != DocumentFlaw.None;
        int IdentityIndex(TravellerCase record) => Array.FindIndex(record.documents, d => d.kind != null && d.kind.showPortrait);
        TravellerDefinition OtherTraveller(int caseIndex)
        {
            int n = cases.Length; if (n < 2) return cases[caseIndex].traveller;
            for (int step = 1; step < n; step++)
            {
                var other = cases[(caseIndex + (FlawSeed.Value % (n - 1)) + step) % n].traveller;
                if (other != cases[caseIndex].traveller) return other;
            }
            return cases[caseIndex].traveller;
        }
        /// <summary>The value printed on the document, including this visit's flaw.</summary>
        public string FieldValue(int caseIndex, int documentIndex, DocumentField field)
        {
            if (!FlawApplies(caseIndex)) return field.value;
            if (Flaw.Value == DocumentFlaw.OwnerMismatch && field.key == "owner") return OtherTraveller(caseIndex).displayName;
            if (Flaw.Value == DocumentFlaw.RaceMismatch && field.key == "race" && codex != null) { var other = Other(codex.races, cases[caseIndex].traveller.raceInfo); if (other != null) return other.displayName; }
            if (Flaw.Value == DocumentFlaw.PlateMismatch && field.key == "registration" && field.value.Length > 0)
            {
                var chars = field.value.ToCharArray();
                int last = Array.FindLastIndex(chars, char.IsDigit);
                if (last >= 0) chars[last] = (char)('0' + (chars[last] - '0' + 1 + FlawSeed.Value % 8) % 10);
                else chars[^1] = chars[^1] == 'X' ? 'Y' : 'X';
                return new string(chars);
            }
            return field.value;
        }
        public string ExpiryText(int caseIndex, int documentIndex)
        {
            var document = cases[caseIndex].documents[documentIndex];
            if (FlawApplies(caseIndex) && Flaw.Value == DocumentFlaw.Expired && documentIndex == IdentityIndex(cases[caseIndex]) &&
                DateTime.TryParse(Date, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var today))
                return today.AddDays(-(20 + FlawSeed.Value % 400)).ToString("yyyy-MM-dd");
            return document.expiresOn;
        }
        public Texture2D PortraitFor(int caseIndex, int documentIndex)
        {
            var document = cases[caseIndex].documents[documentIndex];
            if (document.Portrait == null) return null;
            return FlawApplies(caseIndex) && Flaw.Value == DocumentFlaw.PortraitMismatch && documentIndex == IdentityIndex(cases[caseIndex]) ?
                (mismatchedPortrait != null ? mismatchedPortrait : OtherTraveller(caseIndex).portrait) : document.Portrait;
        }
        /// <summary>Another entry of a codex list, chosen by this visit's flaw seed.</summary>
        T Other<T>(T[] list, T current) where T : UnityEngine.Object
        {
            if (list == null) return null;
            var others = list.Where(x => x != null && x != current).ToArray();
            return others.Length == 0 ? null : others[FlawSeed.Value % others.Length];
        }
        /// <summary>Seal on an ID (its issuing federation) or badge on a registration (the vehicle's manufacturer), including this visit's flaw.</summary>
        public Texture2D EmblemFor(int caseIndex, int documentIndex)
        {
            if (cases == null || caseIndex < 0 || caseIndex >= cases.Length) return null;
            var record = cases[caseIndex];
            if (documentIndex < 0 || documentIndex >= record.documents.Length) return null;
            var document = record.documents[documentIndex];
            var traveller = document.traveller != null ? document.traveller : record.traveller;
            bool flawed = FlawApplies(caseIndex);
            if (document.kind != null && document.kind.showPortrait)
            {
                var federation = traveller.federation;
                if (flawed && Flaw.Value == DocumentFlaw.SealMismatch && codex != null) federation = Other(codex.federations, federation) ?? federation;
                return federation != null ? federation.emblem : null;
            }
            var maker = traveller.vehicle != null ? traveller.vehicle.manufacturer : null;
            if (maker == null) return null;
            if (flawed && Flaw.Value == DocumentFlaw.BadgeMismatch && codex != null) maker = Other(codex.manufacturers, maker) ?? maker;
            return maker.emblem;
        }
        /// <summary>Picture of the registered vehicle model, printed on registrations (from the codex).</summary>
        public Texture2D VehiclePictureFor(int caseIndex, int documentIndex)
        {
            if (cases == null || caseIndex < 0 || caseIndex >= cases.Length || documentIndex < 0 || documentIndex >= cases[caseIndex].documents.Length) return null;
            var document = cases[caseIndex].documents[documentIndex];
            if (document.kind != null && document.kind.showPortrait) return null;
            var traveller = document.traveller != null ? document.traveller : cases[caseIndex].traveller;
            return traveller.vehicle != null ? traveller.vehicle.picture : null;
        }
        /// <summary>Genuine IDs carry a UV watermark; a forged one does not.</summary>
        public bool HasWatermark(int caseIndex, int documentIndex)
        {
            var document = cases[caseIndex].documents[documentIndex];
            if (document.kind == null || !document.kind.showPortrait) return false;
            return !(FlawApplies(caseIndex) && Flaw.Value == DocumentFlaw.Forged);
        }
        public static string Describe(DocumentFlaw flaw) => flaw switch
        {
            DocumentFlaw.Expired => "the ID had expired",
            DocumentFlaw.OwnerMismatch => "the vehicle was registered to someone else",
            DocumentFlaw.PlateMismatch => "the registration number did not match the vehicle",
            DocumentFlaw.PortraitMismatch => "the ID photo showed someone else",
            DocumentFlaw.Forged => "the ID was forged (no UV watermark)",
            DocumentFlaw.SealMismatch => "the ID seal did not match its issuing federation",
            DocumentFlaw.BadgeMismatch => "the registration badge did not match the vehicle's maker",
            DocumentFlaw.RaceMismatch => "the ID listed the wrong race",
            _ => "the papers were in order"
        };

        /// <summary>Server. Hands the visitor's papers over: each goes straight into a free terminal document slot, where it is scanned.
        /// <paramref name="requester"/> (the player who asked) is credited with the document checks.</summary>
        public bool IssueDocuments(int visit, ulong requester = ulong.MaxValue)
        {
            if (!IsServer || !enabled || visit != Visit.Value || Phase.Value != VisitPhase.Waiting) return false;
            // Lock before spawning: concurrent requests cannot issue the same papers twice.
            SetPhase(VisitPhase.Reviewing);
            int overflow = 0;
            for (int i = 0; i < Current.documents.Length; i++)
            {
                var slot = documentSlots?.FirstOrDefault(s => s != null && s.IsSpawned && s.Available.Value && s.Occupant.Value == ulong.MaxValue);
                Vector3 position; Quaternion rotation;
                if (slot != null) { position = slot.snapPoint.position; rotation = slot.snapPoint.rotation; }
                else if (documentDeliveryPoints != null && documentDeliveryPoints.Length > 0)
                { var point = documentDeliveryPoints[overflow % documentDeliveryPoints.Length]; position = point.position + Vector3.up * (.05f * (overflow / documentDeliveryPoints.Length)); rotation = point.rotation; overflow++; }
                else { var anchor = documentSlots.First(s => s != null).snapPoint; position = anchor.position + Vector3.up * (.06f * ++overflow); rotation = anchor.rotation; }
                var item = Instantiate(documentPrefab, position, rotation);
                item.CaseIndex.Value = CaseIndex.Value; item.DocumentIndex.Value = i; item.Visit.Value = Visit.Value;
                item.NetworkObject.Spawn(true);
                item.LastHolder = requester;
                if (slot != null) slot.ServerInsert(item); // seating it fires the slot's scanner
            }
            if (overflow > 0) Debug.LogWarning($"[Checkpoint] Visit {Visit.Value}: {overflow} paper(s) had no free terminal slot. Add document slots for more document types.", this);
            return true;
        }
        public bool Scan(DocumentItem document)
        {
            if (!IsServer || document == null || !document.IsSpawned || Phase.Value != VisitPhase.Reviewing ||
                document.Visit.Value != Visit.Value || document.CaseIndex.Value != CaseIndex.Value || document.DocumentIndex.Value < 0 || document.DocumentIndex.Value >= Current.documents.Length) return false;
            Scanned.Value |= 1UL << document.DocumentIndex.Value;
            if (!ledger.documentChecks.ContainsKey(document.DocumentIndex.Value) && document.LastHolder != ulong.MaxValue) ledger.documentChecks[document.DocumentIndex.Value] = document.LastHolder;
            Debug.Log($"[Checkpoint] Visit {Visit.Value}: scanned {document.DocumentIndex.Value}"); return true;
        }
        /// <summary>Server. Called by the X-ray scanner with the player who pressed Scan.</summary>
        public void RecordPackageScan(CargoPackage package, ulong player)
        {
            if (!IsServer || package == null || package.Visit.Value != Visit.Value || package.CaseIndex.Value != CaseIndex.Value || player == ulong.MaxValue) return;
            int index = package.PackageIndex.Value;
            if (!ledger.packageChecks.ContainsKey(index)) ledger.packageChecks[index] = player;
            if (package.IsContraband && !ledger.contrabandFound.Contains(index)) { ledger.contrabandFound.Add(index); ledger.packageChecks[index] = player; }
        }
        /// <summary>Crates of this visit currently seated in a cargo spot on the truck.</summary>
        public int CargoAboard
        {
            get
            {
                int count = 0;
                if (cargoSpots == null || !IsSpawned) return 0;
                foreach (var spot in cargoSpots)
                    if (spot != null && spot.IsSpawned && spot.Occupant.Value != ulong.MaxValue && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(spot.Occupant.Value, out var obj) &&
                        obj.TryGetComponent<CargoPackage>(out var package) && package.Visit.Value == Visit.Value) count++;
                return count;
            }
        }
        /// <summary>An admitted truck may only leave with all of its crates loaded back. A turned-away truck's cargo is confiscated.</summary>
        public bool CargoReadyToLeave => Current == null || Current.PackageCount == 0 || CargoAboard >= Current.PackageCount;
        public bool Decide(int visit, bool approve, ulong decider = ulong.MaxValue) => Decide(visit, approve, decider, out _);
        public bool Decide(int visit, bool approve, ulong decider, out string reason)
        {
            reason = "Wait for the visitor to stop. Another officer may already have closed this case.";
            // A decision is possible as soon as the visitor has stopped: papers need not be requested or scanned.
            if (!IsServer || visit != Visit.Value || !AtDesk) return false;
            if (approve && !CargoReadyToLeave) { reason = $"The truck can't leave without its cargo: {CargoAboard}/{Current.PackageCount} crates are back on the bed."; return false; }
            if (approve) ApprovedCount.Value++; else RejectedCount.Value++;
            ledger.decider = decider; ledger.approved = approve; ledger.verdictAt = NetworkManager.ServerTime.Time + verdictDelay;
            SetPhase(approve ? VisitPhase.Approved : VisitPhase.Rejected);
            // Cargo leaves with the vehicle (the truck shows its moving crates again).
            foreach (var item in FindObjectsByType<CargoPackage>(FindObjectsSortMode.None).Where(p => p.IsSpawned)) Despawn(item);
            return true;
        }
        /// <summary>True when the current visit is really in the wrong: bad papers or contraband aboard.</summary>
        public bool CurrentIsGuilty(out string why)
        {
            bool contraband = Current.packages != null && Current.packages.Any(p => p.contraband);
            why = Flaw.Value != DocumentFlaw.None ? Describe(Flaw.Value) : contraband ? "contraband in the cargo" : "";
            return Flaw.Value != DocumentFlaw.None || contraband;
        }
        /// <summary>Server. Arrests the visitor and everyone travelling with them: they go to the holding cell, the vehicle is impounded and cargo confiscated.</summary>
        public bool Arrest(int visit, ulong decider, out string reason)
        {
            reason = "Wait for the visitor to stop. Another officer may already have closed this case.";
            if (!IsServer || visit != Visit.Value || !AtDesk) return false;
            if (holdingCell == null || !holdingCell.IsSpawned) { reason = "This station has no holding cell."; return false; }
            int people = 1 + Current.CompanionCount;
            if (holdingCell.Free < people) { reason = $"The holding cell is full ({holdingCell.Occupied}/{holdingCell.Capacity}) and this case needs {people} place(s). Set someone free or wait for the Galactic Police."; return false; }
            bool guilty = CurrentIsGuilty(out var why);
            if (!holdingCell.Admit(CaseIndex.Value, Visit.Value, guilty, why, decider, NameOf(decider))) { reason = "The holding cell refused the prisoners."; return false; }
            ArrestedCount.Value++;
            ledger.decider = decider; ledger.approved = false; ledger.arrested = true; ledger.verdictAt = NetworkManager.ServerTime.Time + verdictDelay;
            SetPhase(VisitPhase.Arrested);
            foreach (var item in FindObjectsByType<CargoPackage>(FindObjectsSortMode.None).Where(p => p.IsSpawned)) Despawn(item);
            reason = people > 1 ? $"ARRESTED with {people - 1} companion(s). They are in the holding cell." : "ARRESTED. The visitor is in the holding cell.";
            return true;
        }

        // ------------------------------------------------------------------ verdict and payouts
        void ResolveVerdict()
        {
            var l = ledger; l.verdictAt = -1;
            var record = cases[l.caseIndex];
            bool papersValid = l.flaw == DocumentFlaw.None;
            var contraband = Enumerable.Range(0, record.PackageCount).Where(i => record.packages[i].contraband).ToList();
            bool rightful = papersValid && contraband.Count == 0;
            var lines = new List<ReportLine>();
            void Award(ulong player, int amount, string reason) { if (player != ulong.MaxValue && amount != 0) lines.Add(new ReportLine { player = player, name = NameOf(player), amount = amount, reason = reason }); }
            foreach (var check in l.documentChecks) Award(check.Value, documentCheckFee, "Document check: " + record.documents[check.Key].kind.displayName);
            foreach (var check in l.packageChecks)
            {
                Award(check.Value, packageCheckFee, "Cargo X-ray check");
                if (l.contrabandFound.Contains(check.Key)) Award(check.Value, contrabandFoundBonus, "Contraband found");
            }
            string reasonBad = !papersValid ? Describe(l.flaw) : "contraband in the cargo";
            if (l.arrested)
            {
                // Whether the arrest was right stays secret until the Galactic Police review the case.
                int people = 1 + record.CompanionCount;
                int reviewDay = Day + (holdingCell != null ? holdingCell.policeIntervalDays : 0);
                PayAndReport($"CASE {l.visit:000}  ·  {record.traveller.displayName}{(people > 1 ? $" + {people - 1} companion(s)" : "")} ARRESTED  ·  Galactic Police review on day {reviewDay}", lines);
                return;
            }
            if (l.approved)
            {
                if (rightful) Award(l.decider, correctApproveBonus, "Correctly admitted a valid visitor");
                if (!papersValid) Award(l.decider, -wrongApproveFine, "Admitted bad papers: " + Describe(l.flaw));
                if (contraband.Count > 0)
                {
                    Award(l.decider, -contrabandApprovedFine, "Let contraband through");
                    // Players who X-rayed this cargo but missed the contraband crate(s) share the blame.
                    foreach (var checker in l.packageChecks.Where(c => contraband.All(i => !l.contrabandFound.Contains(i))).Select(c => c.Value).Distinct())
                        Award(checker, -missedContrabandFine, "Checked the cargo but missed contraband");
                }
            }
            else if (rightful) Award(l.decider, -rejectValidFine, "Turned away a valid visitor");
            else Award(l.decider, correctRejectBonus, "Correctly turned away: " + reasonBad);
            string verdict = rightful ? "was a VALID visitor" : papersValid ? "was carrying CONTRABAND" : "had BAD PAPERS: " + Describe(l.flaw);
            PayAndReport($"CASE {l.visit:000}  ·  {record.traveller.displayName} {verdict}  ·  {(l.approved ? "ADMITTED" : "TURNED AWAY")}", lines);
        }
        /// <summary>Server. Adds each line to that player's pending pay and shows the report to the crew.</summary>
        public void PayAndReport(string title, List<ReportLine> lines)
        {
            if (!IsServer) return;
            foreach (var line in lines)
                if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(line.player, out var obj) && obj.TryGetComponent<PlayerWallet>(out var wallet)) wallet.AddPending(line.amount);
            Debug.Log($"[Checkpoint] Report: {title} ({lines.Count} payout line(s))");
            ReportRpc(title, Encode(lines));
        }
        public string NameOf(ulong player) => NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(player, out var obj) && obj.TryGetComponent<PlayerIdentity>(out var identity) ? identity.Name : "Officer";
        static string Encode(List<ReportLine> lines)
        {
            var text = new StringBuilder();
            foreach (var line in lines) text.Append(line.player).Append('\t').Append(line.name.Replace('\t', ' ').Replace('\n', ' ')).Append('\t').Append(line.amount).Append('\t').Append(line.reason.Replace('\t', ' ').Replace('\n', ' ')).Append('\n');
            return text.ToString();
        }
        static void Decode(string text, List<ReportLine> into)
        {
            into.Clear();
            foreach (var row in text.Split('\n'))
            {
                var parts = row.Split('\t');
                if (parts.Length == 4 && ulong.TryParse(parts[0], out var player) && int.TryParse(parts[2], out var amount))
                    into.Add(new ReportLine { player = player, name = parts[1], amount = amount, reason = parts[3] });
            }
        }
        [Rpc(SendTo.Everyone)] void ReportRpc(string title, string lines) { ReportTitle = title; Decode(lines, Report); ReportReceivedAt = Time.unscaledTime; }
        [Rpc(SendTo.Everyone)] void SummaryRpc(string title, string lines) { SummaryTitle = title; Decode(lines, Summary); }
        [Rpc(SendTo.Everyone)] void ClearSummaryRpc() { SummaryTitle = ""; Summary.Clear(); }

        // ------------------------------------------------------------------ testing and cleanup
        /// <summary>Testing hook: ends the current visit at once (papers and packages are cleared) and starts the given case.</summary>
        public void ForceVisit(int caseIndex, DocumentFlaw? flaw = null)
        {
            if (!IsServer || cases == null || caseIndex < 0 || caseIndex >= cases.Length) return;
            ClearDocuments();
            BeginVisit(caseIndex, true);
            if (flaw.HasValue) { Flaw.Value = flaw.Value; ledger.flaw = flaw.Value; }
        }
        // ------------------------------------------------------------------ debug hooks (DebugTools; editor and development builds)
        /// <summary>Debug: the arriving vehicle stops at once (cargo is loaded as usual).</summary>
        public bool DebugFinishArrival()
        {
            if (!IsServer || Phase.Value != VisitPhase.Arriving) return false;
            SetPhase(VisitPhase.Waiting); SpawnCargo(); return true;
        }
        /// <summary>Debug: marks every paper of the current visit as scanned (no check fees are credited).</summary>
        public bool DebugScanAll()
        {
            if (!IsServer || Phase.Value != VisitPhase.Reviewing) return false;
            Scanned.Value = Current.documents.Length >= 64 ? ulong.MaxValue : (1UL << Current.documents.Length) - 1; return true;
        }
        /// <summary>Debug: the visitor leaves without a decision or verdict; the next one follows after the usual interval.</summary>
        public bool DebugSendAway()
        {
            if (!IsServer || !HasVisitor) return false;
            ClearDocuments(); ledger.verdictAt = -1; SetPhase(VisitPhase.Cooldown); return true;
        }
        /// <summary>Debug: ends the working day now, with the normal payout and summary.</summary>
        public bool DebugCloseDay()
        {
            if (!IsServer || shift == null || Phase.Value == VisitPhase.Closed) return false;
            ClearDocuments(); shift.DebugJumpToEnd(); CloseDay(); return true;
        }
        /// <summary>Testing hook: ends the current day at once (no payout) and starts the next one, so police visits can be tested.</summary>
        public void ForceNextDay()
        {
            if (!IsServer || shift == null) return;
            ClearDocuments();
            if (!shift.Closed.Value) { SetPhase(VisitPhase.Closed); shift.CloseDay(); }
            StartNextDay();
        }
        void SpawnCargo()
        {
            var packages = Current.packages;
            if (!IsServer || packagePrefab == null || packages == null) return;
            for (int i = 0; i < packages.Length; i++)
            {
                vehicleView.CargoPose(i, out var position, out var rotation);
                var item = Instantiate(packagePrefab, position + Vector3.up * .02f, rotation);
                item.CaseIndex.Value = CaseIndex.Value; item.PackageIndex.Value = i; item.Visit.Value = Visit.Value;
                item.NetworkObject.Spawn(true);
                // Arrives loaded in its spot on the bed.
                if (cargoSpots != null && i < cargoSpots.Length && cargoSpots[i] != null) { cargoSpots[i].Available.Value = true; cargoSpots[i].ServerInsert(item); }
            }
            if (packages.Length > 0) Debug.Log($"[Checkpoint] Visit {Visit.Value}: {packages.Length} package(s) on the truck bed");
        }
        void Despawn(PickupItem item)
        {
            if (item.Holder.Value != ulong.MaxValue && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(item.Holder.Value, out var holder))
            { var player = holder.GetComponent<PlayerInteractor>(); if (player != null) item.Drop(player); }
            if (item.Socket.Value != ulong.MaxValue && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(item.Socket.Value, out var socket))
            { var slot = socket.GetComponent<ItemSocket>(); if (slot != null) { slot.Occupant.Value = ulong.MaxValue; slot.onOccupancyChanged.Invoke(false); } }
            item.NetworkObject.Despawn(true);
        }
        void ClearDocuments()
        {
            foreach (var item in FindObjectsByType<DocumentItem>(FindObjectsSortMode.None).Where(d => d.IsSpawned && d.Visit.Value == Visit.Value)) Despawn(item);
            foreach (var item in FindObjectsByType<CargoPackage>(FindObjectsSortMode.None).Where(p => p.IsSpawned)) Despawn(item);
        }
    }
}
