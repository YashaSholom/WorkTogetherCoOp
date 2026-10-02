using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    public sealed class CheckpointVehicleView : MonoBehaviour
    {
        public CheckpointSession checkpoint;
        public Transform vehicle, hoverVisual, boot, entry, stop, exit;
        [Header("Authored checkpoint routes (optional)")]
        public Transform[] arrivalRoute, departureRoute;
        // Routes are scene-authored, and time remains driven by replicated server phase.
        public void RoutePose(Transform[] route, float progress, out Vector3 position, out Quaternion rotation)
        {
            float length = 0;
            for (int i = 1; i < route.Length; i++) length += Vector3.Distance(route[i - 1].position, route[i].position);
            float distance = Mathf.Clamp01(progress) * length;
            for (int i = 1; i < route.Length; i++)
            {
                Vector3 delta = route[i].position - route[i - 1].position;
                float segment = delta.magnitude;
                if (distance <= segment || i == route.Length - 1)
                {
                    position = Vector3.Lerp(route[i - 1].position, route[i].position, segment > .001f ? distance / segment : 0);
                    Vector3 heading = Vector3.ProjectOnPlane(delta, Vector3.up);
                    rotation = heading.sqrMagnitude > .001f ? Quaternion.LookRotation(heading) : route[i].rotation;
                    return;
                }
                distance -= segment;
            }
            position = stop.position; rotation = stop.rotation;
        }
        public Renderer[] alienSkin;
        public TextMesh registration;
        [Header("Passenger shuttle (default vehicle)")]
        public Vector3 shuttleColliderCenter = new(0, 1.6f, 0), shuttleColliderSize = new(3.6f, 2.8f, 3.4f);
        [Header("Cargo truck (optional)")]
        public Transform truckAssembly;
        public TextMesh truckRegistration;
        public Vector3 truckColliderCenter = new(0, 1.7f, 1.5f), truckColliderSize = new(2.6f, 2.2f, 2.3f);
        [Tooltip("Where the packages sit on the truck bed (children of the truck). Real packages spawn here when the truck stops.")]
        public Transform[] cargoPoints;
        [Tooltip("Cosmetic packages shown while the truck drives in and out; one per cargo point.")]
        public GameObject[] cargoDummies;
        [Tooltip("Solid bed floor and lip so real packages rest on the truck while it is stopped.")]
        public GameObject bedCollision;
        MaterialPropertyBlock properties, dummyProperties;
        Vector3 hoverOrigin;
        Renderer[] vehicleRenderers;
        BoxCollider vehicleCollider;
        void Awake()
        {
            if (hoverVisual != null) hoverOrigin = hoverVisual.localPosition;
            if (vehicle != null) { vehicleRenderers = vehicle.GetComponentsInChildren<Renderer>(true); vehicleCollider = vehicle.GetComponent<BoxCollider>(); }
        }
        /// <summary>World pose of a cargo slot as it is when the vehicle is parked at the inspection stop.</summary>
        public void CargoPose(int index, out Vector3 position, out Quaternion rotation)
        {
            var point = cargoPoints[index];
            position = stop.TransformPoint(vehicle.InverseTransformPoint(point.position));
            rotation = stop.rotation * (Quaternion.Inverse(vehicle.rotation) * point.rotation);
        }
        void Update()
        {
            if (checkpoint == null || !checkpoint.IsSpawned) return;
            float time = (float)checkpoint.Elapsed;
            var phase = checkpoint.Phase.Value;
            var current = checkpoint.Current;
            bool truck = current != null && current.vehicle == VehicleKind.Truck && truckAssembly != null;
            if (hoverVisual != null && hoverVisual.gameObject.activeSelf == truck) hoverVisual.gameObject.SetActive(!truck);
            if (truckAssembly != null && truckAssembly.gameObject.activeSelf != truck) truckAssembly.gameObject.SetActive(truck);
            vehicleCollider.center = truck ? truckColliderCenter : shuttleColliderCenter;
            vehicleCollider.size = truck ? truckColliderSize : shuttleColliderSize;
            foreach (var renderer in vehicleRenderers) renderer.enabled = checkpoint.HasVisitor;
            vehicleCollider.enabled = checkpoint.AtDesk;
            if (bedCollision != null) bedCollision.SetActive(truck && checkpoint.AtDesk);
            boot.gameObject.SetActive(phase == VisitPhase.Rejected);
            Vector3 position = stop.position; Quaternion rotation = stop.rotation;
            if (phase == VisitPhase.Arriving) position = Vector3.Lerp(entry.position, stop.position, Mathf.SmoothStep(0, 1, time / checkpoint.arrivalSeconds));
            // An arrested visitor's vehicle is towed away empty along the departure route (to the impound).
            bool leaving = phase == VisitPhase.Approved || phase == VisitPhase.Arrested;
            if (leaving) position = Vector3.Lerp(stop.position, exit.position, Mathf.SmoothStep(0, 1, time / checkpoint.exitSeconds));
            if (phase == VisitPhase.Arriving && arrivalRoute != null && arrivalRoute.Length > 1)
                RoutePose(arrivalRoute, Mathf.SmoothStep(0, 1, time / checkpoint.arrivalSeconds), out position, out rotation);
            if (leaving && departureRoute != null && departureRoute.Length > 1)
                RoutePose(departureRoute, Mathf.SmoothStep(0, 1, time / checkpoint.exitSeconds), out position, out rotation);
            if (phase == VisitPhase.Rejected)
            {
                float kick = Mathf.Clamp01((time - .65f) / Mathf.Max(.1f, checkpoint.rejectionSeconds - .65f));
                position = Vector3.Lerp(stop.position, entry.position, kick) + Vector3.up * (Mathf.Sin(kick * Mathf.PI) * 3);
                rotation *= Quaternion.Euler(kick * 620, 0, kick * 80);
                boot.position = stop.TransformPoint(new Vector3(0, 2.8f, 4 - Mathf.Sin(Mathf.Clamp01(time / 1.3f) * Mathf.PI) * 4));
                boot.rotation = stop.rotation * Quaternion.Euler(-Mathf.Sin(Mathf.Clamp01(time / 1.3f) * Mathf.PI) * 32, 0, 0);
            }
            vehicle.SetPositionAndRotation(position, rotation);
            // The shuttle bobs; the truck stays level so real packages resting on its bed never look detached.
            if (hoverVisual != null) hoverVisual.localPosition = hoverOrigin + Vector3.up * (Mathf.Sin((float)checkpoint.NetworkManager.ServerTime.Time * 2) * .09f);
            PresentCargo(current, truck, phase);
            if (current != null)
            {
                properties ??= new MaterialPropertyBlock(); properties.SetColor("_BaseColor", current.skinColor);
                foreach (var renderer in alienSkin) renderer.SetPropertyBlock(properties);
                if (registration != null) registration.text = current.traveller.vehicleRegistration;
                if (truckRegistration != null) truckRegistration.text = current.traveller.vehicleRegistration;
            }
        }
        // Dummies stand in for the packages while the truck is moving. When it stops the real, pick-up-able packages take their place.
        void PresentCargo(TravellerCase current, bool truck, VisitPhase phase)
        {
            if (cargoDummies == null) return;
            bool moving = phase != VisitPhase.Waiting && phase != VisitPhase.Reviewing;
            for (int i = 0; i < cargoDummies.Length; i++)
            {
                if (cargoDummies[i] == null) continue;
                bool show = truck && moving && phase != VisitPhase.Arrested && checkpoint.HasVisitor && current != null && i < current.PackageCount;
                if (cargoDummies[i].activeSelf != show) cargoDummies[i].SetActive(show);
                if (!show) continue;
                dummyProperties ??= new MaterialPropertyBlock();
                dummyProperties.SetColor("_BaseColor", current.packages[i].colour);
                var body = cargoDummies[i].GetComponentInChildren<Renderer>();
                if (body != null) body.SetPropertyBlock(dummyProperties);
            }
        }
    }
}
