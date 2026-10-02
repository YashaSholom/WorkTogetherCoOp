using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    /// <summary>Giant station clock. Presentation only: reads the replicated <see cref="WorkShift"/>.</summary>
    public sealed class WorldClock : MonoBehaviour
    {
        public WorkShift shift;
        [Tooltip("Rotate around their local Z axis; 0 degrees = 12 o'clock.")]
        public Transform hourHand, minuteHand;
        public TextMesh digital, dayLabel;
        public Renderer lamp;
        public Color openColour = new(.4f, 1, .6f), closedColour = new(1, .45f, .35f);
        MaterialPropertyBlock block;
        void Update()
        {
            if (shift == null) shift = WorkShift.Instance;
            if (shift == null || !shift.IsSpawned) return;
            float minutes = shift.Minutes;
            if (hourHand != null) hourHand.localRotation = Quaternion.Euler(0, 0, -(minutes / 60f % 12) * 30);
            if (minuteHand != null) minuteHand.localRotation = Quaternion.Euler(0, 0, -(minutes % 60) * 6);
            bool open = shift.IsOpen;
            if (digital != null) { digital.text = shift.TimeText; digital.color = open ? openColour : closedColour; }
            if (dayLabel != null) dayLabel.text = $"DAY {shift.Day.Value}  ·  {shift.DateText}  ·  {(open ? "OPEN 8 AM - 4 PM" : shift.Closed.Value ? "CLOSED" : "LAST VISITOR")}";
            if (lamp != null)
            {
                block ??= new MaterialPropertyBlock(); var c = open ? openColour : closedColour;
                block.SetColor("_BaseColor", c * .6f); block.SetColor("_EmissionColor", c * 2); lamp.SetPropertyBlock(block);
            }
        }
    }
}
