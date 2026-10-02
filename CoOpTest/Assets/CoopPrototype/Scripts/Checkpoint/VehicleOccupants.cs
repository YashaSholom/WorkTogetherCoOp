using System.Collections.Generic;
using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    /// <summary>
    /// Shows the current visitor and companions in their vehicle, each with their race's model. Presentation only: it is
    /// rebuilt from replicated case/visit/phase on every peer, so late joiners see the same occupants.
    /// </summary>
    public sealed class VehicleOccupants : MonoBehaviour
    {
        public CheckpointSession checkpoint;
        [Tooltip("Seat 0: a driver travelling alone. Seats 1..: driver and passengers when there are companions. Children of the moving shuttle hover assembly.")]
        public Transform[] shuttleSeats;
        [Tooltip("Same layout as the shuttle seats, children of the truck assembly.")]
        public Transform[] truckSeats;
        [Tooltip("Original single-race drivers, kept hidden so the seats can show any race.")]
        public GameObject[] legacyDrivers;
        readonly List<GameObject> figures = new();
        int shownVisit = -1, shownCase = -1;
        bool shownVisible;
        void Start() { foreach (var driver in legacyDrivers) if (driver != null) driver.SetActive(false); }
        void Update()
        {
            if (checkpoint == null || !checkpoint.IsSpawned || checkpoint.Current == null) return;
            if (checkpoint.Visit.Value != shownVisit || checkpoint.CaseIndex.Value != shownCase) Rebuild();
            var phase = checkpoint.Phase.Value;
            // Arrested travellers are in the holding cell, not in their (impounded) vehicle.
            bool visible = checkpoint.HasVisitor && phase != VisitPhase.Arrested;
            if (visible != shownVisible) { shownVisible = visible; foreach (var figure in figures) if (figure != null) figure.SetActive(visible); }
        }
        void Rebuild()
        {
            foreach (var figure in figures) if (figure != null) Destroy(figure);
            figures.Clear();
            shownVisit = checkpoint.Visit.Value; shownCase = checkpoint.CaseIndex.Value; shownVisible = true;
            var current = checkpoint.Current;
            var seats = current.vehicle == VehicleKind.Truck ? truckSeats : shuttleSeats;
            if (seats == null) return;
            int people = 1 + current.CompanionCount;
            for (int i = 0; i < people; i++)
            {
                int seat = people == 1 ? 0 : i + 1;
                if (seat >= seats.Length || seats[seat] == null) break;
                var member = current.Member(i);
                var figure = RaceModels.Spawn(RaceModels.RaceOf(member, checkpoint.codex), current.SkinOf(i), seats[seat], member != null ? member.displayName : "Traveller");
                if (figure != null)
                {
                    var presentation = figure.GetComponent<AlienPassengerPresentation>();
                    if (presentation != null) presentation.SetSeated(true);
                    figures.Add(figure);
                }
            }
        }
    }
}
