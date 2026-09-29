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
        PlayerInteractor interactor;
        PlayerThrow throwing;
        Vector3 lastPosition;
        float speed;
        double lastThrow = -100;
        static readonly int Speed = Animator.StringToHash("Speed");
        static readonly int Carrying = Animator.StringToHash("Carrying");
        static readonly int ThrowState = Animator.StringToHash("Base Layer.Throw");
        public override void OnNetworkSpawn()
        {
            interactor = GetComponent<PlayerInteractor>();
            throwing = GetComponent<PlayerThrow>();
            lastPosition = transform.position;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        void Update()
        {
            if (!IsSpawned || animator == null) return;
            var delta = transform.position - lastPosition; delta.y = 0;
            lastPosition = transform.position;
            float measured = Mathf.Clamp01(delta.magnitude / Mathf.Max(Time.deltaTime * walkReferenceSpeed, .001f));
            speed = Mathf.Lerp(speed, measured, 1 - Mathf.Exp(-Time.deltaTime / movementDamping));
            animator.SetFloat(Speed, speed);
            animator.SetBool(Carrying, interactor.HeldItem.Value != ulong.MaxValue);
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
