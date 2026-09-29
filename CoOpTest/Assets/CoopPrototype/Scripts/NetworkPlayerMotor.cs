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
                if (Time.unscaledTime >= nextSend) { nextSend = Time.unscaledTime + 1f / 30; InputRpc(probeInput, probeHeading, 0); }
                return;
            }
#endif
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;
            if (NetworkSession.MenuVisible) Cursor.lockState = CursorLockMode.None;
            if (!NetworkSession.MenuVisible && keyboard.vKey.wasPressedThisFrame) { thirdPerson = !thirdPerson; ApplyCameraVisibility(); }
            var cursor = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame && !NetworkSession.MenuVisible &&
                !NetworkSession.MenuRect.Contains(new Vector2(cursor.x, Screen.height - cursor.y))) Cursor.lockState = CursorLockMode.Locked;
            Vector2 input = Vector2.zero;
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                var delta = mouse.delta.ReadValue() * lookSensitivity;
                yaw += delta.x;
                pitch = Mathf.Clamp(pitch - delta.y, -70, 70);
                input = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
            }
            playerCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            if (Time.unscaledTime >= nextSend)
            {
                nextSend = Time.unscaledTime + 1f / 30;
                InputRpc(input, yaw, pitch);
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
        void InputRpc(Vector2 input, float heading, float elevation)
        {
            if (!float.IsFinite(input.x) || !float.IsFinite(input.y) || !float.IsFinite(heading) || !float.IsFinite(elevation)) return;
            move = Vector2.ClampMagnitude(input, 1);
            yaw = heading % 360;
            pitch = Mathf.Clamp(elevation, -70, 70);
            lastInput = Time.unscaledTime;
        }
        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            if (Time.unscaledTime - lastInput > .3f) move = Vector2.zero;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            vertical = controller.isGrounded ? -2 : vertical + gravity * Time.fixedDeltaTime;
            controller.Move((transform.TransformDirection(new Vector3(move.x, 0, move.y)) * moveSpeed + Vector3.up * vertical) * Time.fixedDeltaTime);
            if (transform.position.y < -10)
            {
                controller.enabled = false;
                transform.position = new Vector3(0, 1.1f, -5);
                controller.enabled = true;
            }
        }
    }
}


