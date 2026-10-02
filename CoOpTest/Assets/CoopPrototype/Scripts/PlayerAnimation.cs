using Unity.Netcode;
using UnityEngine;

namespace CoopPrototype
{
    /// <summary>Presentation derived from replicated movement, held-item state and the server throw clock.</summary>
    [RequireComponent(typeof(PlayerInteractor), typeof(PlayerThrow))]
    public class PlayerAnimation : NetworkBehaviour
    {
        public Animator animator;
        [Min(.01f)] public float movementDamping = .12f;
        public float walkReferenceSpeed = 4;
        [Tooltip("Walk-cycle playback rate at a crawl and at full speed (drives the optional MoveRate parameter).")]
        public Vector2 walkPlaybackRate = new(.9f, 2f);
        [Tooltip("Run-cycle playback rate at the start of a run and at full running speed.")]
        public Vector2 runPlaybackRate = new(1.5f, 2.1f);
        PlayerInteractor interactor;
        PlayerThrow throwing;
        Vector3 lastPosition;
        float speed;
        double lastThrow = -100;
        ulong lastHeld = ulong.MaxValue;
        /// <summary>Smoothed horizontal speed, 0 (still) to 1 (full walk). Also drives the first-person arms.</summary>
        public float NormalizedSpeed => Mathf.Clamp01(speed);
        bool hasMoveRate, hasPickUp, hasRunning;
        NetworkPlayerMotor motor;
        static readonly int Speed = Animator.StringToHash("Speed");
        static readonly int Carrying = Animator.StringToHash("Carrying");
        static readonly int MoveRate = Animator.StringToHash("MoveRate");
        static readonly int Running = Animator.StringToHash("Running");
        static readonly int ThrowState = Animator.StringToHash("Base Layer.Throw");
        static readonly int PickUpState = Animator.StringToHash("Base Layer.Pick Up");
        public override void OnNetworkSpawn()
        {
            interactor = GetComponent<PlayerInteractor>();
            throwing = GetComponent<PlayerThrow>();
            lastPosition = transform.position;
            // Late joiners start in the current carry pose instead of replaying an old pick-up.
            lastHeld = interactor.HeldItem.Value;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            // Optional features, so older controllers (e.g. the legacy worker) keep working.
            motor = GetComponent<NetworkPlayerMotor>();
            foreach (var parameter in animator.parameters)
            {
                if (parameter.nameHash == MoveRate) hasMoveRate = true;
                if (parameter.nameHash == Running) hasRunning = true;
            }
            hasPickUp = animator.HasState(0, PickUpState);
        }
        void Update()
        {
            if (!IsSpawned || animator == null) return;
            var delta = transform.position - lastPosition; delta.y = 0;
            lastPosition = transform.position;
            // Physics/replication move the body in steps, so many rendered frames see no motion. Smooth the raw
            // rate first and clamp afterwards; clamping per frame would under-report speed.
            float measured = delta.magnitude / Mathf.Max(Time.deltaTime * walkReferenceSpeed, .001f);
            if (Time.deltaTime > 0) speed = Mathf.Lerp(speed, Mathf.Min(measured, 3), 1 - Mathf.Exp(-Time.deltaTime / movementDamping));
            float normalized = Mathf.Clamp01(speed);
            animator.SetFloat(Speed, normalized);
            // Running is replicated from the server, so every peer (and late joiners) shows the same gait.
            bool running = hasRunning && motor != null && motor.Running.Value;
            if (hasRunning) animator.SetBool(Running, running);
            if (hasMoveRate)
                animator.SetFloat(MoveRate, running
                    ? Mathf.Lerp(runPlaybackRate.x, runPlaybackRate.y, Mathf.InverseLerp(1f, motor.runSpeed, speed))
                    : Mathf.Lerp(walkPlaybackRate.x, walkPlaybackRate.y, normalized));
            ulong held = interactor.HeldItem.Value;
            animator.SetBool(Carrying, held != ulong.MaxValue);
            if (held != lastHeld)
            {
                // Picking something up (from the floor or a slot) plays a short heave on every peer.
                if (hasPickUp && lastHeld == ulong.MaxValue && held != ulong.MaxValue && !throwing.IsBusy)
                    animator.CrossFadeInFixedTime(PickUpState, .05f, 0);
                lastHeld = held;
            }
            double start = throwing.StartedAt.Value;
            if (start == lastThrow) return;
            lastThrow = start;
            double elapsed = NetworkManager.ServerTime.Time - start;
            // Late joiners reconstruct a throw only while it is still taking place.
            if (elapsed < throwing.duration)
                animator.Play(ThrowState, 0, Mathf.Clamp01((float)(elapsed / throwing.duration)));
        }
    }
}
