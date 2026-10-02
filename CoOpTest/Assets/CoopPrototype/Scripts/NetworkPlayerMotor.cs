using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoopPrototype
{
    [RequireComponent(typeof(CharacterController))]
    public class NetworkPlayerMotor : NetworkBehaviour
    {
        [Min(0)] public float moveSpeed = 4;
        [Min(0)] public float lookSensitivity = .12f;
        public float gravity = -20;
        [Tooltip("Walking speed multiplier after drinking a coffee (lasts for the rest of the work day).")]
        [Min(1)] public float coffeeSpeed = 1.35f;
        [Tooltip("Speed multiplier while holding Shift (run). Stacks with coffee.")]
        [Min(1)] public float runSpeed = 1.65f;
        /// <summary>True while the player is running (Shift held and moving forward). Server-written; drives the run animation on every peer.</summary>
        public NetworkVariable<bool> Running = new();
        /// <summary>Server, debug panel: run whenever moving, without holding Shift.</summary>
        [System.NonSerialized] public bool debugAlwaysRun;
        /// <summary>Work day on which this player drank a coffee (-1 = none). Server-written.</summary>
        public NetworkVariable<int> CoffeeDay = new(-1);
        public bool Caffeinated => CoffeeDay.Value >= 0 && (Checkpoint.WorkShift.Instance == null || CoffeeDay.Value == Checkpoint.WorkShift.Instance.Day.Value);
        public Camera playerCamera;
        public Transform holdPoint;
        [Header("Camera")]
        [Tooltip("Start in third person. V toggles in play. First person shows the FirstPersonArms view model.")]
        public bool thirdPerson = false;
        [Min(.5f)] public float thirdPersonDistance = 3;
        public float shoulderOffset = .6f;
        Renderer[] visuals;
        readonly RaycastHit[] cameraHits = new RaycastHit[16];
        public float CameraDistance => thirdPerson ? thirdPersonDistance + 1 : 0;
        CharacterController controller;
        Vector2 move;
        bool runHeld;
        float yaw, pitch, vertical, lastInput, nextSend;
        Vector3? injectedSpawn;
        public void InitializeSpawn(Vector3 position) => injectedSpawn = position;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Vector2 probeInput;
        float probeUntil, probeHeading;
        public void DriveForTest(Vector2 input, float heading, float seconds)
        { probeInput = input; probeHeading = heading; probeUntil = Time.unscaledTime + seconds; }
#endif
        public Vector3 Eye => transform.position + Vector3.up * .65f;
        public Vector3 Aim => Quaternion.Euler(pitch, transform.eulerAngles.y, 0) * Vector3.forward;
        /// <summary>Server-authored carrying pose. Items face the holder rather than pointing out into the world.</summary>
        public Vector3 HeldItemPosition => Eye + Aim * .56f - Vector3.up * .16f;
        public Quaternion HeldItemRotation => Quaternion.LookRotation(-Aim, Vector3.up);
        public Quaternion DocumentHoldRotation => Quaternion.LookRotation(transform.up, -Aim);
        public override void OnNetworkSpawn()
        {
            controller = GetComponent<CharacterController>();
            controller.enabled = IsServer;
            playerCamera.gameObject.SetActive(IsOwner && IsClient);
            // Body renderers only; the first-person view model manages its own visibility.
            visuals = System.Array.FindAll(GetComponentsInChildren<Renderer>(true), r => r.GetComponentInParent<FirstPersonArms>(true) == null);
            if (IsOwner && IsClient) ApplyCameraVisibility();
            if (IsServer)
            {
                controller.enabled = false;
                var session = NetworkManager.GetComponent<NetworkSession>();
                var points = session.spawnPoints;
                transform.position = injectedSpawn ?? points[(int)(OwnerClientId % (ulong)points.Length)].position;
                controller.enabled = true;
            }
            yaw = transform.eulerAngles.y;
            pitch = thirdPerson ? 12 : 0;
            if (NetworkManager.TryGetComponent<Frontend.GameFlow>(out var flow)) flow.BindPlayer(this);
        }
        void Update()
        {
            if (!IsSpawned || !IsOwner || !IsClient) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Time.unscaledTime < probeUntil)
            {
                if (Time.unscaledTime >= nextSend) { nextSend = Time.unscaledTime + 1f / 30; InputRpc(probeInput, probeHeading, 0, false); }
                return;
            }
#endif
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame && LocalGameplayModal.ClosedFrame != Time.frameCount) Cursor.lockState = CursorLockMode.None;
            // Only unlock while a blocking panel is actually open. On the frame a panel closes, BlocksMovement is still true
            // (to stop that frame's input), but the panel has already re-locked the cursor and this must not undo it.
            if (NetworkSession.MenuVisible || (LocalGameplayModal.IsOpen && LocalGameplayModal.BlocksMovement)) Cursor.lockState = CursorLockMode.None;
            if (!NetworkSession.MenuVisible && !LocalGameplayModal.BlocksInput && keyboard.vKey.wasPressedThisFrame) { thirdPerson = !thirdPerson; ApplyCameraVisibility(); }
            var cursor = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame && !NetworkSession.MenuVisible && !LocalGameplayModal.BlocksMovement &&
                !NetworkSession.MenuRect.Contains(new Vector2(cursor.x, Screen.height - cursor.y))) Cursor.lockState = CursorLockMode.Locked;
            Vector2 input = Vector2.zero;
            bool run = false;
            if (Cursor.lockState == CursorLockMode.Locked && !LocalGameplayModal.BlocksMovement && !NetworkSession.MenuVisible)
            {
                var inspector = GetComponent<HeldItemInspector>();
                if (!LocalGameplayModal.IsOpen || inspector == null || !inspector.IsRotating)
                {
                    var delta = mouse.delta.ReadValue() * lookSensitivity;
                    yaw += delta.x;
                    pitch = Mathf.Clamp(pitch - delta.y, -70, 70);
                }
                input = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                run = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            }
            playerCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            if (Time.unscaledTime >= nextSend)
            {
                nextSend = Time.unscaledTime + 1f / 30;
                InputRpc(input, yaw, pitch, run);
            }
        }
        void ApplyCameraVisibility()
        {
            foreach (var mesh in visuals) mesh.shadowCastingMode = thirdPerson ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }
        void LateUpdate()
        {
            if (!IsSpawned || !IsOwner || !IsClient) return;
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            var eye = Eye;
            var desired = thirdPerson ? eye + rotation * new Vector3(shoulderOffset, .25f, -thirdPersonDistance) : eye + transform.forward * .1f;
            if (thirdPerson)
            {
                var displacement = desired - eye;
                float distance = displacement.magnitude;
                int count = Physics.SphereCastNonAlloc(eye, .15f, displacement.normalized, cameraHits, distance, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                    if (!cameraHits[i].collider.transform.IsChildOf(transform)) distance = Mathf.Min(distance, Mathf.Max(.1f, cameraHits[i].distance - .05f));
                desired = eye + displacement.normalized * distance;
            }
            playerCamera.transform.SetPositionAndRotation(desired, rotation);
        }
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Owner)]
        void InputRpc(Vector2 input, float heading, float elevation, bool run)
        {
            if (!float.IsFinite(input.x) || !float.IsFinite(input.y) || !float.IsFinite(heading) || !float.IsFinite(elevation)) return;
            move = Vector2.ClampMagnitude(input, 1);
            runHeld = run;
            yaw = heading % 360;
            pitch = Mathf.Clamp(elevation, -70, 70);
            lastInput = Time.unscaledTime;
        }
        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            if (Time.unscaledTime - lastInput > .3f) { move = Vector2.zero; runHeld = false; }
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            // Run only while moving forwards (W, optionally with A/D); backing up or strafing alone stays a walk.
            bool running = (runHeld || debugAlwaysRun) && move.y > .1f;
            if (Running.Value != running) Running.Value = running;
            vertical = controller.isGrounded ? -2 : vertical + gravity * Time.fixedDeltaTime;
            controller.Move((transform.TransformDirection(new Vector3(move.x, 0, move.y)) * (moveSpeed * (Caffeinated ? coffeeSpeed : 1) * (running ? runSpeed : 1)) + Vector3.up * vertical) * Time.fixedDeltaTime);
            if (transform.position.y < -10)
            {
                controller.enabled = false;
                transform.position = new Vector3(0, 1.1f, -5);
                controller.enabled = true;
            }
        }
    }
}
