using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoopPrototype.Checkpoint
{
    /// <summary>
    /// The working day. The clock runs from <see cref="startHour"/> to <see cref="endHour"/> on the server's clock, so every
    /// peer shows the same time. When it reaches the end no new visitors are admitted; the visitor being processed stays
    /// until cleared. Then the day is closed and paid out, and a crew member starts the next day from the terminal button.
    /// Debug: the F1 debug panel (DebugTools) moves the clock forward or to the end of the day (editor and development builds).
    /// </summary>
    public sealed class WorkShift : NetworkBehaviour
    {
        public static WorkShift Instance { get; private set; }
        [Range(0, 23)] public int startHour = 8;
        [Range(1, 24)] public int endHour = 16;
        [Tooltip("Real seconds per in-game minute. 1 = an 8-hour day lasts 8 minutes.")]
        [Min(.05f)] public float secondsPerGameMinute = 1;
        [Tooltip("Calendar date of day 1 (YYYY-MM-DD). Each day adds one day; documents expire against this date.")]
        public string firstDate = "2219-09-29";
        public NetworkVariable<int> Day = new(1);
        public NetworkVariable<double> DayStarted = new();
        public NetworkVariable<float> SkippedMinutes = new();
        /// <summary>True once the day has ended and been paid out; the crew can start the next day.</summary>
        public NetworkVariable<bool> Closed = new();

        void Awake() => Instance = this;
        public override void OnDestroy() { if (Instance == this) Instance = null; base.OnDestroy(); }
        public override void OnNetworkSpawn() { if (IsServer) { DayStarted.Value = NetworkManager.ServerTime.Time; SkippedMinutes.Value = 0; Closed.Value = false; } }

        /// <summary>Minutes since midnight, clamped to the end of the working day.</summary>
        public float Minutes
        {
            get
            {
                if (!IsSpawned) return startHour * 60;
                float elapsed = (float)((NetworkManager.ServerTime.Time - DayStarted.Value) / secondsPerGameMinute);
                return Mathf.Min(startHour * 60 + elapsed + SkippedMinutes.Value, endHour * 60);
            }
        }
        /// <summary>New visitors may arrive only while the shift is open.</summary>
        public bool IsOpen => !Closed.Value && Minutes < endHour * 60;
        public string TimeText
        {
            get
            {
                int total = Mathf.FloorToInt(Minutes), hour = total / 60, minute = total % 60;
                return $"{(hour % 12 == 0 ? 12 : hour % 12)}:{minute:00} {(hour < 12 ? "AM" : "PM")}";
            }
        }
        public string DateText
        {
            get
            {
                if (!DateTime.TryParse(firstDate, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date)) return firstDate;
                return date.AddDays(Mathf.Max(0, Day.Value - 1)).ToString("yyyy-MM-dd");
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SkipHourRpc(RpcParams rpcParams = default)
        {
            if (!(Application.isEditor || Debug.isDebugBuild) || Closed.Value) return;
            SkippedMinutes.Value += 60;
            Debug.Log($"[Checkpoint] Debug: clock moved forward one hour to {TimeText} by client {rpcParams.Receive.SenderClientId}");
        }
        /// <summary>Server, debug. Moves the clock forward by the given number of minutes (clamped to the end of the day).</summary>
        public void DebugAddMinutes(float minutes) { if (IsServer && !Closed.Value) SkippedMinutes.Value += Mathf.Min(minutes, endHour * 60 - Minutes); }
        /// <summary>Server, debug. Sets the clock to the end of the working day.</summary>
        public void DebugJumpToEnd() { if (IsServer) SkippedMinutes.Value += endHour * 60 - Minutes; }
        /// <summary>Server. Called by the checkpoint once the day is over and the last visitor has been cleared.</summary>
        public void CloseDay() { if (IsServer && !Closed.Value) { Closed.Value = true; Debug.Log($"[Checkpoint] Day {Day.Value} closed at {TimeText}"); } }
        /// <summary>Server.</summary>
        public bool StartNextDay()
        {
            if (!IsServer || !Closed.Value) return false;
            Day.Value++; SkippedMinutes.Value = 0; DayStarted.Value = NetworkManager.ServerTime.Time; Closed.Value = false;
            Debug.Log($"[Checkpoint] Day {Day.Value} started");
            return true;
        }
    }
}
