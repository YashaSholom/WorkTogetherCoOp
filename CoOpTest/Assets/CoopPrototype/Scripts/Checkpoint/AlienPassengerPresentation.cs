using Unity.Netcode;
using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    /// <summary>Local art derived from visitor state. Never moves a network object or changes a case.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class AlienPassengerPresentation : MonoBehaviour
    {
        public Animator animator;
        public Transform head;
        [Min(0)] public float lookRange = 5;
        [Range(0, 85)] public float maxYaw = 65;
        [Range(0, 45)] public float maxPitch = 22;
        [Min(.1f)] public float lookSpeed = 5;
        public bool seated;
        public Transform LookTarget { get; private set; }
        // Last completed presentation frame; editor probes may run between Update and LateUpdate.
        public Quaternion PresentedHeadLocalRotation { get; private set; } = Quaternion.identity;
        public ulong LookPlayerId => targetPlayer != null && LookTarget != null ? targetPlayer.OwnerClientId : ulong.MaxValue;
        static readonly int Seated = Animator.StringToHash("Seated");
        float nextSearch;
        Vector2 look;
        NetworkPlayerMotor targetMotor;
        NetworkObject targetPlayer;
        Transform posedHead;
        Quaternion animatedHeadRotation;

        void OnEnable() { nextSearch = 0; look = Vector2.zero; LookTarget = null; ApplySeat(); }
        void Update() { RestoreHeadPose(); }
        void OnDisable() { RestoreHeadPose(); LookTarget = null; }

        void RestoreHeadPose()
        {
            // Restore before Animator evaluation. Constant animation curves may not write the
            // head every frame; leaving our offset on it would accumulate into continuous spins.
            if (posedHead != null) posedHead.localRotation = animatedHeadRotation;
            posedHead = null;
        }

        public void SetSeated(bool value)
        {
            RestoreHeadPose();
            seated = value; ApplySeat();
            if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
            {
                animator.Play(value ? "Seated Idle" : "Standing Idle", 0, 0);
                animator.Update(0);
            }
        }
        void ApplySeat() { if (animator != null) animator.SetBool(Seated, seated); }

        void LateUpdate()
        {
            if (head == null) return;
            if (Time.unscaledTime >= nextSearch)
            {
                nextSearch = Time.unscaledTime + .2f;
                LookTarget = ClosestPlayer();
                targetMotor = LookTarget != null ? LookTarget.GetComponent<NetworkPlayerMotor>() : null;
                targetPlayer = LookTarget != null ? LookTarget.GetComponent<NetworkObject>() : null;
            }
            Vector2 desired = Vector2.zero;
            if (LookTarget != null && LookTarget.gameObject.activeInHierarchy &&
                (LookTarget.position - transform.position).sqrMagnitude <= lookRange * lookRange)
            {
                // The motor's eye works for remote players too; its origin is controller-centred, not ground-centred.
                var eye = targetMotor != null ? targetMotor.Eye : LookTarget.position + Vector3.up * .65f;
                var direction = transform.InverseTransformDirection(eye - head.position);
                desired.x = Mathf.Clamp(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, -maxYaw, maxYaw);
                desired.y = Mathf.Clamp(-Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg, -maxPitch, maxPitch);
            }
            else LookTarget = null;
            look = Vector2.Lerp(look, desired, 1 - Mathf.Exp(-lookSpeed * Time.deltaTime));
            // Save the unmodified animation pose, then apply one absolute model-frame offset.
            // Update restores this base even when the Animator is culled, disabled or skips constant curves.
            posedHead = head;
            animatedHeadRotation = head.localRotation;
            head.rotation = Quaternion.AngleAxis(look.x, transform.up) * Quaternion.AngleAxis(look.y, transform.right) * head.rotation;
            PresentedHeadLocalRotation = head.localRotation;
        }

        Transform ClosestPlayer()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening || manager.SpawnManager == null) return null;
            float closest = lookRange * lookRange;
            ulong bestId = ulong.MaxValue;
            Transform result = null;
            // The spawn registry exists on clients as well as the host (ConnectedClients does not).
            foreach (var player in manager.SpawnManager.SpawnedObjectsList)
            {
                if (!player.IsPlayerObject || !player.IsSpawned || !player.gameObject.activeInHierarchy) continue;
                float distance = (player.transform.position - transform.position).sqrMagnitude;
                if (distance > closest || (Mathf.Approximately(distance, closest) && player.NetworkObjectId >= bestId)) continue;
                closest = distance; bestId = player.NetworkObjectId; result = player.transform;
            }
            return result;
        }
    }
}
