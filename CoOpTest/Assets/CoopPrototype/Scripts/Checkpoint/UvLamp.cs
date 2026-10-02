using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    /// <summary>
    /// Handheld UV lamp. While it is in your hands, genuine IDs within range show their violet UV watermark; a forged ID shows
    /// nothing. With the lamp in any of your slots, an ID you are inspecting up close is also shown under UV.
    /// </summary>
    public sealed class UvLamp : PickupItem
    {
        public Light beam;
        [Min(.5f)] public float range = 3.5f;
        protected override void Update()
        {
            base.Update();
            if (beam != null) { bool on = IsSpawned && Holder.Value != ulong.MaxValue && !IsStowed; if (beam.enabled != on) beam.enabled = on; }
        }
        public override string Prompt => "Take UV lamp  /  shows watermarks on genuine IDs";
        static PlayerInteractor LocalPlayer
        {
            get
            {
                var manager = Unity.Netcode.NetworkManager.Singleton;
                return manager != null && manager.LocalClient != null && manager.LocalClient.PlayerObject != null ? manager.LocalClient.PlayerObject.GetComponent<PlayerInteractor>() : null;
            }
        }
        /// <summary>Local presentation: is this document lit by the local player's UV lamp?</summary>
        public static bool Illuminates(DocumentItem document)
        {
            var player = LocalPlayer;
            if (player == null) return false;
            var inspector = player.GetComponent<HeldItemInspector>();
            foreach (var item in player.Carried)
            {
                if (!(item is UvLamp lamp)) continue;
                if (player.Held == lamp && Vector3.Distance(player.Motor.Eye, document.transform.position) <= lamp.range) return true;
                if (player.Held == document && inspector != null && inspector.IsInspecting) return true;
            }
            return false;
        }
    }
}
