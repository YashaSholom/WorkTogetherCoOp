using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoopPrototype
{
    /// <summary>Local-only close inspection of the held item. It never clones anything: it only owns the input and the
    /// camera-space inspection pose (<see cref="Offset"/>, <see cref="Rotation"/>). <see cref="FirstPersonArms"/> applies that
    /// pose to the item's own visuals, so the object in the hands is the object being inspected.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class HeldItemInspector : NetworkBehaviour
    {
        [Tooltip("Longest side of the inspected item, in metres, as seen from the camera.")]
        [Min(.05f)] public float targetSize = .42f;
        [Tooltip("Nearest and farthest distance of the inspected item from the camera (scroll zooms).")]
        public Vector2 distanceRange = new(.24f, .78f);
        PlayerInteractor player;
        FirstPersonArms arms;
        PickupItem inspected;
        bool open, pinned;
        public bool IsInspecting => open;
        public bool IsRotating => open && !pinned && Mouse.current != null && Mouse.current.rightButton.isPressed;
        /// <summary>Camera-space position of the inspected item's centre.</summary>
        public Vector3 Offset { get; private set; }
        /// <summary>Orientation of the inspected item relative to the camera; drag to change it.</summary>
        public Quaternion Rotation { get; private set; } = Quaternion.identity;

        public override void OnNetworkSpawn()
        {
            player = GetComponent<PlayerInteractor>();
            arms = GetComponentInChildren<FirstPersonArms>(true);
        }
        void Update()
        {
            if (!IsOwner || !IsClient || !IsSpawned) return;
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            // Inspection lasts only while the right mouse button is held; releasing it returns the item to the hands.
            if (open && (inspected == null || player.Held != inspected || NetworkSession.MenuVisible ||
                (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || (!pinned && (mouse == null || !mouse.rightButton.isPressed))))
            { Close(); return; }
            if (!open && !NetworkSession.MenuVisible && !LocalGameplayModal.BlocksInput && mouse != null && mouse.rightButton.wasPressedThisFrame && player.Held != null)
                OpenHeld();
            if (!open || mouse == null) return;
            // While inspecting, moving the mouse turns the item instead of the view.
            if (IsRotating)
            {
                var delta = mouse.delta.ReadValue();
                Rotation = Quaternion.AngleAxis(-delta.x * .4f, Vector3.up) * Rotation;
                Rotation = Quaternion.AngleAxis(delta.y * .4f, Vector3.right) * Rotation;
            }
            var offset = Offset;
            offset.z = Mathf.Clamp(offset.z - mouse.scroll.ReadValue().y * .001f, distanceRange.x, distanceRange.y);
            Offset = offset;
        }

        /// <param name="pin">Keep it open without the mouse button (test probes only).</param>
        public void OpenHeld(bool pin = false)
        {
            if (open || player == null || player.Held == null) return;
            if (arms == null) arms = GetComponentInChildren<FirstPersonArms>(true);
            if (arms == null) { Debug.LogWarning("[Coop] Inspection needs a FirstPersonArms on the player."); return; }
            if (!LocalGameplayModal.Open(this)) return;
            if (!NetworkSession.MenuVisible) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            inspected = player.Held;
            open = true; pinned = pin;
            Offset = new Vector3(0, -.08f, .48f);
            // Start exactly where the item already is in the hands, so it simply moves toward the camera.
            Rotation = arms.CarryRotation(inspected);
        }

        public void Close()
        {
            open = false; pinned = false;
            inspected = null;
            LocalGameplayModal.Close(this);
        }
        public override void OnNetworkDespawn() => Close();
        void OnDisable() => Close();
    }
}
