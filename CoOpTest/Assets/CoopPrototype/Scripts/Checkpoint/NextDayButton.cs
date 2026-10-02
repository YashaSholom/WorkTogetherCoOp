namespace CoopPrototype.Checkpoint
{
    /// <summary>Physical "start next day" button on the inspection terminal. Works only after the day has closed.</summary>
    public sealed class NextDayButton : NetworkInteractable
    {
        public CheckpointSession checkpoint;
        public override string Prompt
        {
            get
            {
                var shift = WorkShift.Instance;
                if (shift == null) return "Start next day";
                return shift.Closed.Value ? $"Start day {shift.Day.Value + 1}" : shift.IsOpen ? $"Day {shift.Day.Value} in progress (ends 4 PM)" : "Finish the last visitor first";
            }
        }
        public override bool TryInteract(PlayerInteractor player, out string reason)
        {
            reason = "The day is not over yet";
            if (!IsServer || checkpoint == null || !checkpoint.StartNextDay()) return false;
            reason = "A new day begins"; return true;
        }
    }
}
