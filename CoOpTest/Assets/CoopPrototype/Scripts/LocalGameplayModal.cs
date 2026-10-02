using UnityEngine;

namespace CoopPrototype
{
    /// <summary>Local UI focus shared by interaction panels, inspection and gameplay input.</summary>
    public static class LocalGameplayModal
    {
        static Object owner;
        public static bool IsOpen => owner != null;
        public static int ClosedFrame { get; private set; } = -1;
        public static bool BlocksInput => IsOpen || ClosedFrame == Time.frameCount;
        /// <summary>Inspect is visual-only: movement remains available while other modals own gameplay input.</summary>
        public static bool BlocksMovement => (IsOpen && !(owner is HeldItemInspector)) || ClosedFrame == Time.frameCount;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { owner = null; ClosedFrame = -1; }
        public static bool Open(Object panel)
        {
            if (owner != null && owner != panel) return false;
            owner = panel;
            if (!(panel is HeldItemInspector)) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            return true;
        }
        public static void Close(Object panel)
        {
            if (owner != panel) return;
            owner = null; ClosedFrame = Time.frameCount;
            if (!NetworkSession.MenuVisible) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }
    }
}
