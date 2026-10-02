namespace CoopPrototype.Checkpoint
{
    /// <summary>Consumable: left click to drink. The drinker walks faster until the end of the work day.</summary>
    public sealed class CoffeeCup : PickupItem
    {
        public override bool CanUse => true;
        public override string UseVerb => "drink";
        public override bool TryUse(PlayerInteractor player, out string reason)
        {
            reason = "Already buzzing today";
            if (!IsServer || Holder.Value != player.NetworkObjectId) return false;
            var shift = WorkShift.Instance;
            int day = shift != null ? shift.Day.Value : 0;
            if (player.Motor.CoffeeDay.Value == day) return false;
            player.Motor.CoffeeDay.Value = day;
            Holder.Value = ulong.MaxValue; player.Release(this);
            NetworkObject.Despawn(true);
            reason = "Coffee! You move faster for the rest of the day.";
            return true;
        }
    }
}
