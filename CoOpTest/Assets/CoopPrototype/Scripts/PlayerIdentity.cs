using System.Text;
using CoopPrototype.Steam;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CoopPrototype
{
    /// <summary>Replicated display name (Steam persona name, or "Player N" off Steam) plus a simple nameplate
    /// drawn above other players. The owner proposes its name; the server sanitises it and owns the value.</summary>
    public class PlayerIdentity : NetworkBehaviour
    {
        public NetworkVariable<FixedString64Bytes> DisplayName = new();
        [Min(4)] public int maxNameLength = 24;
        [Header("Nameplate")]
        public bool showNameplates = true;
        public float nameplateHeight = 1.15f;
        [Min(1)] public float nameplateRange = 25;

        public string Name => DisplayName.Value.Length > 0 ? DisplayName.Value.ToString() : "Player " + OwnerClientId;
        static GUIStyle style, shadow;

        public override void OnNetworkSpawn()
        {
            if (IsServer && DisplayName.Value.Length == 0) DisplayName.Value = "Player " + OwnerClientId;
            if (IsOwner) SetNameRpc(NetworkManager.TryGetComponent<Frontend.GameFlow>(out var flow) ? flow.Preferences.DisplayName :
                SteamBootstrap.Ready && !string.IsNullOrEmpty(SteamBootstrap.PersonaName) ? SteamBootstrap.PersonaName : "Player " + OwnerClientId);
            DisplayName.OnValueChanged += Changed;
        }

        public override void OnNetworkDespawn() => DisplayName.OnValueChanged -= Changed;

        void Changed(FixedString64Bytes previous, FixedString64Bytes current)
        {
            if (IsServer && previous.Length > 0 && previous != current) Debug.Log($"[Coop] Player {OwnerClientId} is now '{current}'");
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void SetNameRpc(string requested)
        {
            var clean = Sanitize(requested);
            DisplayName.Value = new FixedString64Bytes(clean.Length > 0 ? clean : "Player " + OwnerClientId);
            Debug.Log($"[Coop] Player {OwnerClientId} name: {DisplayName.Value}");
        }

        string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            var builder = new StringBuilder();
            foreach (char c in value.Trim())
            {
                if (char.IsControl(c) || c == '<' || c == '>') continue; // no control chars or rich-text tags
                builder.Append(c);
                if (builder.Length >= maxNameLength) break;
            }
            // FixedString64Bytes holds 61 UTF-8 bytes; trim multi-byte names until they fit.
            var text = builder.ToString();
            while (Encoding.UTF8.GetByteCount(text) > 60 && text.Length > 0) text = text.Substring(0, text.Length - 1);
            return text;
        }

        void OnGUI()
        {
            if (!showNameplates || !IsSpawned || IsOwner || NetworkManager.LocalClient == null || NetworkManager.LocalClient.PlayerObject == null) return;
            var viewer = NetworkManager.LocalClient.PlayerObject.GetComponent<NetworkPlayerMotor>();
            if (viewer == null || viewer.playerCamera == null || !viewer.playerCamera.isActiveAndEnabled) return;
            var camera = viewer.playerCamera;
            var world = transform.position + Vector3.up * nameplateHeight;
            float distance = Vector3.Distance(camera.transform.position, world);
            if (distance > nameplateRange) return;
            var screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0) return;
            style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 16, normal = { textColor = Color.white } };
            shadow ??= new GUIStyle(style) { normal = { textColor = new Color(0, 0, 0, .75f) } };
            var alpha = Mathf.Clamp01((nameplateRange - distance) / 5f);
            var rect = new Rect(screen.x - 120, Screen.height - screen.y - 12, 240, 24);
            var previous = GUI.color; GUI.color = new Color(1, 1, 1, alpha);
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), Name, shadow);
            GUI.Label(rect, Name, style);
            GUI.color = previous;
        }
    }
}
