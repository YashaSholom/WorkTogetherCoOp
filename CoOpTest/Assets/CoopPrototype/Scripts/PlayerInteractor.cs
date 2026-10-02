using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoopPrototype
{
    /// <summary>Three carried-item slots. The active slot is what the player holds in their hands; the others are stowed.</summary>
    public struct InventorySlots : INetworkSerializeByMemcpy
    {
        public const int Count = 3;
        public ulong a, b, c;
        public int active;
        public static InventorySlots Empty => new() { a = ulong.MaxValue, b = ulong.MaxValue, c = ulong.MaxValue };
        public ulong this[int index]
        {
            get => index == 0 ? a : index == 1 ? b : c;
            set { if (index == 0) a = value; else if (index == 1) b = value; else c = value; }
        }
    }

    public class PlayerInteractor : NetworkBehaviour
    {
        [Min(1)] public float reach = 3;
        public LayerMask interactionMask = ~0;
        /// <summary>The item in the active slot (what is in the player's hands). Kept equal to Slots[active] by the server.</summary>
        public NetworkVariable<ulong> HeldItem = new(ulong.MaxValue);
        public NetworkVariable<InventorySlots> Slots = new(InventorySlots.Empty);
        public NetworkPlayerMotor Motor { get; private set; }
        NetworkInteractable target;
        float nextRequest;
        string feedback = "";
        float feedbackUntil;
        PlayerThrow throwing;
        HeldItemInspector inspector;
        FirstPersonArms arms;
        static GUIStyle slotStyle;
        bool cursorWasLocked;
        public override void OnNetworkSpawn()
        {
            Motor = GetComponent<NetworkPlayerMotor>();
            throwing = GetComponent<PlayerThrow>();
            inspector = GetComponent<HeldItemInspector>();
            arms = GetComponentInChildren<FirstPersonArms>(true);
        }
        public PickupItem Held => Find(HeldItem.Value);
        PickupItem Find(ulong id) => id != ulong.MaxValue && NetworkManager != null && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(id, out var obj) ? obj.GetComponent<PickupItem>() : null;
        public PickupItem ItemInSlot(int index) => Find(Slots.Value[index]);
        public int ActiveSlot => Slots.Value.active;
        public bool HasFreeSlot { get { var s = Slots.Value; for (int i = 0; i < InventorySlots.Count; i++) if (s[i] == ulong.MaxValue) return true; return false; } }
        public IEnumerable<PickupItem> Carried { get { for (int i = 0; i < InventorySlots.Count; i++) { var item = ItemInSlot(i); if (item != null) yield return item; } } }
        /// <summary>True when the item is carried by this player but not in the active slot.</summary>
        public bool IsStowed(PickupItem item) => item != null && item.Holder.Value == NetworkObjectId && HeldItem.Value != item.NetworkObjectId;

        // ---------------------------------------------------------------- server inventory
        /// <summary>Server. Puts the item in the active slot if it is empty, otherwise in the first free slot, which becomes active.</summary>
        public bool Store(PickupItem item)
        {
            if (!IsServer) return false;
            var slots = Slots.Value;
            int slot = slots[slots.active] == ulong.MaxValue ? slots.active : -1;
            for (int i = 0; slot < 0 && i < InventorySlots.Count; i++) if (slots[i] == ulong.MaxValue) slot = i;
            if (slot < 0) return false;
            slots[slot] = item.NetworkObjectId; slots.active = slot;
            Slots.Value = slots; HeldItem.Value = item.NetworkObjectId;
            return true;
        }
        /// <summary>Server. Removes the item from whichever slot holds it. The active slot stays selected (now empty).</summary>
        public void Release(PickupItem item)
        {
            if (!IsServer || item == null) return;
            var slots = Slots.Value;
            for (int i = 0; i < InventorySlots.Count; i++) if (slots[i] == item.NetworkObjectId) slots[i] = ulong.MaxValue;
            Slots.Value = slots; HeldItem.Value = slots[slots.active];
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void SelectSlotRpc(int index)
        {
            if (index < 0 || index >= InventorySlots.Count || (throwing != null && throwing.IsBusy)) return;
            var slots = Slots.Value;
            if (slots.active == index) return;
            slots.active = index; Slots.Value = slots; HeldItem.Value = slots[index];
        }

        void Update()
        {
            if (!IsSpawned || !IsOwner || !IsClient) return;
            target = null;
            var camera = Motor.playerCamera;
            float nearest = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(camera.transform.position, camera.transform.forward, reach + Motor.CameraDistance, interactionMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || (Held != null && hit.collider.transform.IsChildOf(Held.transform))) continue;
                if (hit.distance >= nearest) continue;
                nearest = hit.distance;
                target = hit.collider.GetComponentInParent<NetworkInteractable>();
            }
            bool inspecting = inspector != null && inspector.IsInspecting;
            if (Keyboard.current == null || Cursor.lockState != CursorLockMode.Locked || (LocalGameplayModal.BlocksInput && !inspecting) || NetworkSession.MenuVisible) { cursorWasLocked = false; return; }
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            // E and left click are the same primary action. A click that captured the cursor this frame does not count.
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame && cursorWasLocked;
            cursorWasLocked = true;
            if (keyboard.eKey.wasPressedThisFrame || click)
            {
                // Aiming at a socket/tray/NPC/button: interact. Otherwise a usable held item (coffee) is used, and anything else
                // held is dropped (not thrown). With empty hands, aiming at an item picks it up.
                var held = Held;
                if (target != null && !(target is PickupItem && held != null) && !inspecting) RequestInteractionRpc(new NetworkObjectReference(target.NetworkObject));
                else if (held != null && held.CanUse) UseHeldRpc();
                else if (held != null) RequestDrop();
            }
            if (keyboard.qKey.wasPressedThisFrame) RequestDrop();
            // Slot selection: 1-3, or the scroll wheel (the wheel zooms while inspecting).
            if (keyboard.digit1Key.wasPressedThisFrame) SelectSlotRpc(0);
            if (keyboard.digit2Key.wasPressedThisFrame) SelectSlotRpc(1);
            if (keyboard.digit3Key.wasPressedThisFrame) SelectSlotRpc(2);
            if (mouse != null && !inspecting)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > .01f) SelectSlotRpc((ActiveSlot + (scroll < 0 ? 1 : InventorySlots.Count - 1)) % InventorySlots.Count);
            }
        }
        // Ownership is required by default: a client can only invoke requests on its own player.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestInteractionRpc(NetworkObjectReference reference)
        {
            if (throwing != null && throwing.IsBusy) return;
            if (Time.unscaledTime < nextRequest) return;
            nextRequest = Time.unscaledTime + .15f;
            if (!reference.TryGet(out var obj) || !obj.TryGetComponent<NetworkInteractable>(out var interactable)) return;
            Debug.Log($"[Coop] Interaction requested: {OwnerClientId} -> {obj.name}");
            string reason;
            if (!CanReach(interactable)) reason = "Out of reach or obstructed";
            else if (interactable.TryInteract(this, out reason))
            {
                Debug.Log($"[Coop] Interaction accepted: {obj.name}");
                FeedbackRpc(string.IsNullOrEmpty(reason) ? "Accepted" : reason);
                return;
            }
            Debug.Log($"[Coop] Interaction rejected: {reason}");
            FeedbackRpc(reason);
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void UseHeldRpc()
        {
            if ((throwing != null && throwing.IsBusy) || Time.unscaledTime < nextRequest) return;
            nextRequest = Time.unscaledTime + .15f;
            var item = Held;
            if (item == null) return;
            FeedbackRpc(item.TryUse(this, out var reason) ? (string.IsNullOrEmpty(reason) ? "Used " + item.DisplayName : reason) : reason);
        }
        public bool CanReach(NetworkInteractable targetObject)
        {
            var collider = targetObject.GetComponent<Collider>();
            if (collider == null) return false;
            var point = collider.ClosestPoint(Motor.Eye);
            if (Vector3.Distance(Motor.Eye, point) > reach + .25f) return false;
            var direction = collider.bounds.center - Motor.Eye;
            foreach (var hit in Physics.RaycastAll(Motor.Eye, direction.normalized, direction.magnitude, interactionMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (Held != null && hit.collider.transform.IsChildOf(Held.transform)) continue;
                if (hit.collider.GetComponentInParent<NetworkInteractable>() != targetObject) return false;
            }
            return true;
        }
        /// <summary>Drops the held item where the local player sees it in their hands.</summary>
        void RequestDrop()
        {
            var held = Held;
            if (held != null && arms != null && arms.TryGetDropPose(held, out var pos, out var rot)) RequestDropAtRpc(pos, rot);
            else RequestDropRpc();
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void RequestDropAtRpc(Vector3 position, Quaternion rotation)
        {
            if (throwing != null && throwing.IsBusy) return;
            if (Time.unscaledTime < nextRequest) return;
            nextRequest = Time.unscaledTime + .15f;
            var item = Held;
            if (item == null) return;
            // Trust the client's pose only if it is plausibly in front of the player's hands.
            if (float.IsNaN(position.x + position.y + position.z) || (position - Motor.Eye).sqrMagnitude > 2.25f) item.Drop(this);
            else item.Drop(this, position, rotation);
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestDropRpc()
        {
            if (throwing != null && throwing.IsBusy) return;
            if (Time.unscaledTime < nextRequest) return;
            nextRequest = Time.unscaledTime + .15f;
            if (Held != null) Held.Drop(this);
        }
        [Rpc(SendTo.Owner)] void FeedbackRpc(string text) { feedback = text; feedbackUntil = Time.unscaledTime + 4; }
        /// <summary>Server. Shows a short message in this player's HUD.</summary>
        public void Notify(string text) { if (IsServer) FeedbackRpc(text); }
        public override void OnNetworkDespawn()
        {
            if (IsServer) foreach (var item in new List<PickupItem>(Carried)) item.Drop(this);
            if (IsOwner) Cursor.lockState = CursorLockMode.None;
        }
        void OnGUI()
        {
            if (!IsSpawned || !IsOwner || !IsClient || NetworkSession.MenuVisible || LocalGameplayModal.IsOpen) return;
            GUI.Label(new Rect(Screen.width / 2 - 5, Screen.height / 2 - 12, 20, 24), "+");
            DrawHotbar();
            string prompt = target != null && !(target is PickupItem && Held != null) ? "E / click: " + target.Prompt : Held != null ? "E / click: " + (Held.CanUse ? Held.UseVerb + " " : "drop ") + Held.DisplayName : "Look at an object";
            GUI.Box(new Rect(Screen.width / 2 - 320, Screen.height - 100, 640, 85),
                $"WASD move | E or left click: interact / use / drop | 1-3 / wheel switch slot | Hold right mouse: inspect | Q drop | F throw | V camera\n{prompt}\n{(Time.unscaledTime < feedbackUntil ? feedback : "")}");
        }
        void DrawHotbar()
        {
            InspectionGui.Begin();
            slotStyle ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = 15 };
            const float width = 170, height = 44, gap = 8;
            float x = 720 - (width * 3 + gap * 2) / 2, y = 900 - 200;
            for (int i = 0; i < InventorySlots.Count; i++)
            {
                var item = ItemInSlot(i);
                var old = GUI.backgroundColor; GUI.backgroundColor = i == ActiveSlot ? new Color(.45f, 1, .65f) : new Color(.35f, .4f, .5f);
                GUI.Box(new Rect(x + i * (width + gap), y, width, height), $"{i + 1}  {(item != null ? item.DisplayName : "—")}", slotStyle);
                GUI.backgroundColor = old;
            }
            InspectionGui.End();
        }
    }
}
