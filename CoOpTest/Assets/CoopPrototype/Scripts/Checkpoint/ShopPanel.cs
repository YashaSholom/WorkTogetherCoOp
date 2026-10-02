using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoopPrototype.Checkpoint
{
    /// <summary>Local UI for the supply terminal. Purchases go through <see cref="ShopTerminal.BuyRpc"/>.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class ShopPanel : MonoBehaviour
    {
        public static ShopPanel Instance { get; private set; }
        ShopTerminal terminal;
        string message = "";
        public bool IsOpen => terminal != null;
        void Awake() => Instance = this;
        void OnDisable() { Close(); if (Instance == this) Instance = null; }
        public void Show(ShopTerminal source)
        {
            if (!LocalGameplayModal.Open(this)) return;
            terminal = source; message = "Spend your banked credits. Today's pay is added at the end of the day.";
        }
        public void Feedback(ShopTerminal source, string value) { if (terminal == source) message = value; }
        public void Close() { if (terminal == null) return; terminal = null; LocalGameplayModal.Close(this); }
        void Update()
        {
            if (!IsOpen) return;
            var manager = NetworkManager.Singleton;
            var player = manager != null && manager.LocalClient?.PlayerObject != null ? manager.LocalClient.PlayerObject.GetComponent<PlayerInteractor>() : null;
            if (player == null || !terminal.IsSpawned || NetworkSession.MenuVisible || !player.CanReach(terminal) ||
                (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)) Close();
        }
        void OnGUI()
        {
            if (!IsOpen || NetworkSession.MenuVisible) return;
            var wallet = PlayerWallet.Local;
            InspectionGui.Begin();
            InspectionGui.Frame("SECTOR 07  /  CREW SUPPLY TERMINAL", "Upgrades & tools");
            InspectionGui.Text(new Rect(900, 160, 330, 40), wallet != null ? $"{wallet.Credits.Value} cr" : "—", title: true);
            InspectionGui.Text(new Rect(900, 200, 330, 24), wallet != null ? $"today so far: {(wallet.Pending.Value >= 0 ? "+" : "")}{wallet.Pending.Value} (paid at 4 PM)" : "", caption: true);
            for (int i = 0; i < terminal.offers.Length; i++)
            {
                var offer = terminal.offers[i];
                var row = new Rect(215, 240 + i * 118, 1010, 106);
                InspectionGui.Fill(row, InspectionGui.Panel);
                InspectionGui.Text(new Rect(row.x + 20, row.y + 12, 640, 32), offer.title, title: false);
                InspectionGui.Text(new Rect(row.x + 20, row.y + 44, 700, 60), offer.description, caption: true);
                bool available = terminal.IsAvailable(i, out var why);
                bool affordable = wallet != null && wallet.Credits.Value >= offer.price;
                GUI.enabled = available && affordable;
                if (InspectionGui.Button(new Rect(row.xMax - 250, row.y + 26, 230, 54), available ? $"BUY  ·  {offer.price} cr" : why, available && affordable ? InspectionGui.Mint : (Color?)null)) terminal.BuyRpc(i);
                GUI.enabled = true;
            }
            InspectionGui.Text(new Rect(215, 700, 760, 80), message, caption: true);
            if (InspectionGui.Button(new Rect(1005, 743, 215, 42), "Close  [Esc]")) Close();
            InspectionGui.End();
        }
    }
}
