using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CoopPrototype
{
    /// <summary>Owner-only first-person view model: outfit-matched arms, running sway, and the held item shown in the
    /// hands. Purely local presentation: the real (networked) item still follows the server hold point, which is what
    /// other players see. Everything here lives on the view-model layer, drawn by an overlay camera so it never clips
    /// into walls.</summary>
    [DefaultExecutionOrder(1000)] // after the motor's camera update and the item's replicated pose
    public class FirstPersonArms : MonoBehaviour
    {
        [Header("References")]
        public NetworkPlayerMotor motor;
        public PlayerInteractor interactor;
        public PlayerThrow throwing;
        public PlayerAnimation playerAnimation;
        public CharacterAppearance appearance;
        public Transform leftArm, rightArm;
        [Tooltip("Per outfit, same order as CharacterAppearance.variants.")]
        public Renderer[] leftVariants, rightVariants;
        [Tooltip("Arms cut from the player model; their material follows the suit colour (CharacterAppearance.colours order).")]
        public Renderer leftBody, rightBody;
        public Material[] colours;
        [Tooltip("Layer drawn only by the view-model overlay camera.")]
        public int viewModelLayer = 8;

        [Header("Pose (camera space, metres)")]
        public float armLength = .55f;
        public Vector3 shoulder = new(.24f, -.42f, -.12f);
        public Vector3 restHand = new(.25f, -.27f, .44f);
        [Tooltip("Top edge of a carried item, camera space (keeps the item in the lower part of the view).")]
        public float carryTop = -.17f;
        [Tooltip("Distance from the camera to the near face of a carried item.")]
        public float carryDistance = .8f;
        [Tooltip("Degrees the carried item is tipped toward the camera so its top reads.")]
        public float carryTilt = 12;
        [Min(.05f)] public float minGrip = .13f;
        [Tooltip("How far the mitt centres sit outside the item's sides.")]
        public float gripOutset = .06f;
        [Tooltip("Preferred camera-space height of the hands while carrying (kept inside the item's extent).")]
        public float gripHeight = -.36f;
        [Tooltip("Roll (degrees) of the mitts while carrying; turns palms toward the item.")]
        public float carryRoll = 75;

        [Header("Motion")]
        public float strideFrequency = 9f;
        public float runSwing = .085f, runPush = .09f, carryBob = .025f;
        public float lookSway = .0009f, swayReturn = 10f;
        public float pickUpTime = .22f;

        [Header("Inspection")]
        [Tooltip("Seconds for the held item to move between the hands and the inspection pose.")]
        [Min(.01f)] public float inspectTime = .18f;
        [Tooltip("Layer used for a held item that is inspected in third person (drawn by the main camera).")]
        public int thirdPersonInspectLayer = 31;

        readonly List<(Transform t, Vector3 pos, Quaternion rot, Vector3 scale)> heldVisuals = new();
        readonly Dictionary<GameObject, int> savedLayers = new();
        PickupItem shownItem;
        Bounds itemBounds, viewBounds;
        Quaternion viewRotation = Quaternion.identity;
        float viewScale = 1, inspectBlend;
        bool shownInFirstPerson;
        HeldItemInspector inspector;
        Vector3 inspectOffset;
        Vector3 handCentre; Quaternion handRotation = Quaternion.identity; bool hasHandPose;
        Quaternion inspectRotation = Quaternion.identity;
        float phase, heldSince, gripRoll = 12;
        Vector2 sway;
        Quaternion lastCamera;

        void Awake()
        {
            SetLayer(gameObject, viewModelLayer);
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        bool Active => motor != null && motor.IsSpawned && motor.IsOwner && !motor.thirdPerson;

        void LateUpdate()
        {
            bool active = Active;
            if (leftArm.gameObject.activeSelf != active) { leftArm.gameObject.SetActive(active); rightArm.gameObject.SetActive(active); }
            if (inspector == null && motor != null) inspector = motor.GetComponent<HeldItemInspector>();
            bool inspecting = motor != null && motor.IsOwner && inspector != null && inspector.IsInspecting;
            if (inspecting) { inspectOffset = inspector.Offset; inspectRotation = inspector.Rotation; }
            // The item is presented by this view model whenever it is carried in first person, and while it is inspected in third person.
            var held = active || inspecting ? interactor.Held : null;
            if (held != shownItem || (held != null && shownInFirstPerson != active)) ShowItem(held);
            float blendStep = Time.deltaTime / inspectTime;
            inspectBlend = !active ? (inspecting ? 1 : 0) : Mathf.MoveTowards(inspectBlend, inspecting ? 1 : 0, blendStep);
            if (!active)
            {
                if (shownItem != null) PlaceInspected(motor.playerCamera.transform, default, default, 1, false);
                return;
            }

            SelectOutfit();
            var cam = motor.playerCamera.transform;
            float dt = Time.deltaTime;
            float speed = playerAnimation != null ? playerAnimation.NormalizedSpeed : 0;
            phase += dt * strideFrequency * Mathf.Lerp(.35f, 1, speed);

            // Look sway: hands trail slightly behind quick mouse movement.
            var delta = Quaternion.Inverse(lastCamera) * cam.rotation; lastCamera = cam.rotation;
            var e = delta.eulerAngles; float dx = Mathf.DeltaAngle(0, e.y), dy = Mathf.DeltaAngle(0, e.x);
            sway = Vector2.Lerp(sway, Vector2.zero, 1 - Mathf.Exp(-swayReturn * dt)) + new Vector2(-dx, dy) * lookSway;
            sway = Vector2.ClampMagnitude(sway, .06f);
            var swayOffset = new Vector3(sway.x, sway.y, 0);
            float breathe = Mathf.Sin(Time.time * 1.7f) * .006f;

            // Throw timeline from the server clock (same source every peer animates from).
            float t = -1, release = .35f;
            if (throwing != null && throwing.IsBusy)
            {
                t = Mathf.Clamp01((float)((motor.NetworkManager.ServerTime.Time - throwing.StartedAt.Value) / throwing.duration));
                release = throwing.releaseDelay / throwing.duration;
            }

            Vector3 left, right;
            if (shownItem != null)
            {
                // Carry: item centred low in view, hands gripping its sides.
                var ext = viewBounds.extents;
                float grip = Mathf.Max(ext.x + gripOutset, minGrip);
                var anchor = new Vector3(0, carryTop - ext.y + breathe, carryDistance + ext.z) + swayOffset;
                anchor.y += Mathf.Abs(Mathf.Sin(phase)) * carryBob * speed;
                float pick = Mathf.Clamp01((Time.time - heldSince) / pickUpTime);
                anchor.y -= (1 - Ease(pick)) * .35f;
                var tilt = Quaternion.Euler(carryTilt, 0, 0);
                if (t >= 0 && t < release)
                {
                    // Wind-up: heave the item up and back over the shoulders.
                    float w = Ease(t / release);
                    anchor += new Vector3(0, .32f, -.18f) * w;
                    tilt = Quaternion.Euler(carryTilt - 35 * w, 0, 0);
                }
                var itemRotation = cam.rotation * tilt * viewRotation;
                var itemCentre = cam.TransformPoint(anchor);
                handCentre = itemCentre; handRotation = itemRotation; hasHandPose = true;
                // While inspecting, the same visuals glide from the hands to the camera pose (and back).
                if (inspectBlend > 0) PlaceInspected(cam, itemCentre, itemRotation, viewScale, true);
                else PlaceItem(itemCentre, itemRotation, viewScale);
                // Grip the sides at a height that stays on screen, within the item's vertical extent.
                float gripY = Mathf.Clamp(gripHeight, anchor.y - ext.y * .8f, anchor.y + ext.y * .6f) - anchor.y;
                left = anchor + tilt * new Vector3(-grip, gripY, -ext.z * .1f);
                right = anchor + tilt * new Vector3(grip, gripY, -ext.z * .1f);
            }
            else
            {
                float swing = Mathf.Sin(phase) * speed;
                left = new Vector3(-restHand.x, restHand.y + breathe + Mathf.Max(0, -swing) * runSwing * 1.6f - Mathf.Max(0, swing) * runSwing * .4f, restHand.z - swing * runPush);
                right = new Vector3(restHand.x, restHand.y + breathe + Mathf.Max(0, swing) * runSwing * 1.6f - Mathf.Max(0, -swing) * runSwing * .4f, restHand.z + swing * runPush);
                left += swayOffset; right += swayOffset;
                if (t >= 0)
                {
                    // Follow-through after release: both hands shove forward, then settle.
                    float f = t < release ? 0 : Mathf.Sin(Mathf.Clamp01((t - release) / (1 - release)) * Mathf.PI);
                    var push = new Vector3(0, .12f, .3f) * f;
                    left = Vector3.Lerp(left, new Vector3(-.14f, -.12f, .5f), f) + push * .3f;
                    right = Vector3.Lerp(right, new Vector3(.14f, -.12f, .5f), f) + push * .3f;
                }
            }
            // While the item is inspected up close the hands relax to their rest pose instead of gripping empty air.
            if (shownItem != null && inspectBlend > 0)
            {
                float relax = Ease(inspectBlend);
                left = Vector3.Lerp(left, new Vector3(-restHand.x, restHand.y, restHand.z), relax);
                right = Vector3.Lerp(right, restHand, relax);
            }
            // Carrying turns the palms inward against the item; otherwise knuckles face up.
            gripRoll = Mathf.MoveTowards(gripRoll, shownItem != null ? carryRoll : 12, 360 * dt);
            Pose(leftArm, new Vector3(-shoulder.x, shoulder.y, shoulder.z), left, cam, -1);
            Pose(rightArm, shoulder, right, cam, 1);
        }

        /// <summary>Aim an arm from its shoulder at a camera-space hand target; slide it so the mitt lands exactly there.</summary>
        void Pose(Transform arm, Vector3 shoulderLocal, Vector3 handLocal, Transform cam, float side)
        {
            var dir = handLocal - shoulderLocal;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            // Knuckles up, rolled slightly inward like a relaxed fist.
            var rotation = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(0, 0, -side * gripRoll);
            arm.SetPositionAndRotation(cam.TransformPoint(handLocal - dir.normalized * armLength), cam.rotation * rotation);
        }

        /// <summary>World pose the held item's root would have if it stayed exactly where it is seen in the hands (full size).</summary>
        public bool TryGetDropPose(PickupItem item, out Vector3 position, out Quaternion rotation)
        {
            position = default; rotation = default;
            if (item == null || item != shownItem || !hasHandPose || !shownInFirstPerson) return false;
            rotation = handRotation;
            position = handCentre - handRotation * itemBounds.center;
            return true;
        }

        /// <summary>Camera-relative orientation an item has in the hands; inspection starts from it so nothing jumps.</summary>
        public Quaternion CarryRotation(PickupItem item) => Quaternion.Euler(carryTilt, 0, 0) * (item != null ? item.ViewRotation : Quaternion.identity);

        /// <summary>Pose the item's own visuals at the inspection pose, optionally blended from the hand pose.</summary>
        void PlaceInspected(Transform cam, Vector3 handCentre, Quaternion handRotation, float handScale, bool hasHand)
        {
            var size = itemBounds.size;
            float longest = Mathf.Max(.01f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
            var centre = cam.TransformPoint(inspectOffset);
            var rotation = cam.rotation * inspectRotation;
            float scale = (inspector != null ? inspector.targetSize : .42f) / longest;
            if (hasHand)
            {
                float e = Ease(inspectBlend);
                centre = Vector3.Lerp(handCentre, centre, e);
                rotation = Quaternion.Slerp(handRotation, rotation, e);
                scale = Mathf.Lerp(handScale, scale, e);
            }
            PlaceItem(centre, rotation, scale);
        }

        void SelectOutfit()
        {
            int index = appearance != null ? Mathf.Max(0, appearance.Variant.Value) : 0;
            if (leftBody != null && rightBody != null && colours != null && colours.Length > 0)
            {
                var colour = colours[Mathf.Min(index, colours.Length - 1)];
                if (leftBody.sharedMaterial != colour) { leftBody.sharedMaterial = colour; rightBody.sharedMaterial = colour; }
                return;
            }
            for (int i = 0; i < leftVariants.Length; i++)
            {
                if (leftVariants[i] != null) leftVariants[i].enabled = i == index;
                if (i < rightVariants.Length && rightVariants[i] != null) rightVariants[i].enabled = i == index;
            }
        }

        // ---------------------------------------------------------------- held item presentation
        void ShowItem(PickupItem item)
        {
            RestoreItem();
            shownItem = item;
            if (item == null) return;
            heldSince = Time.time;
            inspectBlend = 0;
            shownInFirstPerson = Active;
            viewRotation = item.ViewRotation;
            viewScale = Mathf.Max(.05f, item.ViewScale);
            int layer = shownInFirstPerson ? viewModelLayer : thirdPersonInspectLayer;
            // Only child visuals are moved; the networked root keeps its replicated pose. World-space canvases (document text) count as visuals.
            foreach (Transform child in item.transform)
            {
                // Inactive children are included too, so parts that appear later (e.g. a UV watermark) move with the item.
                if (child.GetComponentInChildren<Renderer>(true) == null && child.GetComponentInChildren<Canvas>(true) == null) continue;
                // Children on layers the player camera never draws (e.g. X-ray-only contents) stay hidden and are not presented.
                if (child.gameObject.layer != viewModelLayer && child.gameObject.layer != thirdPersonInspectLayer && (motor.playerCamera.cullingMask & (1 << child.gameObject.layer)) == 0) continue;
                heldVisuals.Add((child, child.localPosition, child.localRotation, child.localScale));
                SaveAndSetLayer(child.gameObject, layer);
            }
            // Local bounds of the moved visuals, plus the same bounds as seen in the hands (view rotation and scale applied).
            bool any = false;
            var toItem = item.transform.worldToLocalMatrix;
            foreach (var (child, _, _, _) in heldVisuals)
                foreach (var filter in child.GetComponentsInChildren<MeshFilter>())
                {
                    var m = toItem * filter.transform.localToWorldMatrix; var b = filter.sharedMesh.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        var p = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) * 2 - 1, (i >> 1 & 1) * 2 - 1, (i >> 2 & 1) * 2 - 1)));
                        p = Vector3.Scale(p, item.transform.lossyScale);
                        var v = viewRotation * p * viewScale;
                        if (!any) { itemBounds = new Bounds(p, Vector3.zero); viewBounds = new Bounds(v, Vector3.zero); any = true; }
                        else { itemBounds.Encapsulate(p); viewBounds.Encapsulate(v); }
                    }
                }
            if (!any) { itemBounds = new Bounds(Vector3.zero, Vector3.one * .3f); viewBounds = new Bounds(Vector3.zero, Vector3.one * .3f * viewScale); }
        }

        void PlaceItem(Vector3 centre, Quaternion rotation, float scale)
        {
            if (shownItem == null || heldVisuals.Count == 0) return;
            // Pose the visuals as if the item root sat at 'rotation' (uniformly scaled by 'scale') with its bounds centred at 'centre'.
            var root = shownItem.transform;
            var virtualRoot = centre - rotation * (itemBounds.center * scale);
            foreach (var (child, pos, rot, size) in heldVisuals)
            {
                var localOffset = Vector3.Scale(pos, root.lossyScale) * scale;
                child.SetPositionAndRotation(virtualRoot + rotation * localOffset, rotation * rot);
                child.localScale = size * scale;
            }
        }

        void RestoreItem()
        {
            hasHandPose = false;
            inspectBlend = 0;
            foreach (var (child, pos, rot, size) in heldVisuals)
                if (child != null) { child.localPosition = pos; child.localRotation = rot; child.localScale = size; }
            foreach (var pair in savedLayers)
                if (pair.Key != null) pair.Key.layer = pair.Value;
            heldVisuals.Clear();
            savedLayers.Clear();
            shownItem = null;
        }

        void SaveAndSetLayer(GameObject go, int layer)
        {
            savedLayers[go] = go.layer; go.layer = layer;
            foreach (Transform c in go.transform) SaveAndSetLayer(c.gameObject, layer);
        }

        static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayer(c.gameObject, layer);
        }

        static float Ease(float x) => 1 - (1 - x) * (1 - x);
        void OnDisable() => RestoreItem();
    }
}
