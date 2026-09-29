using UnityEngine;

namespace CoopPrototype.Voice
{
    /// <summary>Local audio preferences, injected into capture, playback and settings.</summary>
    public sealed class VoicePreferences
    {
        public bool Enabled = PlayerPrefs.GetInt("voice.enabled", 1) != 0;
        public bool Muted = PlayerPrefs.GetInt("voice.muted", 0) != 0;
        public bool Deafened = PlayerPrefs.GetInt("voice.deafened", 0) != 0;
        public bool PushToTalk = PlayerPrefs.GetInt("voice.ptt", 1) != 0;
        public string Device = PlayerPrefs.GetString("voice.device", "");
        public float Volume = Mathf.Clamp01(PlayerPrefs.GetFloat("voice.volume", .8f));
        public float Gain = Mathf.Clamp(PlayerPrefs.GetFloat("voice.gain", 1), 0, 3);
        public float Threshold = Mathf.Clamp(PlayerPrefs.GetFloat("voice.threshold", .015f), .001f, .15f);
        public void Save()
        {
            PlayerPrefs.SetInt("voice.enabled", Enabled ? 1 : 0);
            PlayerPrefs.SetInt("voice.muted", Muted ? 1 : 0);
            PlayerPrefs.SetInt("voice.deafened", Deafened ? 1 : 0);
            PlayerPrefs.SetInt("voice.ptt", PushToTalk ? 1 : 0);
            PlayerPrefs.SetString("voice.device", Device);
            PlayerPrefs.SetFloat("voice.volume", Volume);
            PlayerPrefs.SetFloat("voice.gain", Gain);
            PlayerPrefs.SetFloat("voice.threshold", Threshold);
            PlayerPrefs.Save();
        }
    }
}
