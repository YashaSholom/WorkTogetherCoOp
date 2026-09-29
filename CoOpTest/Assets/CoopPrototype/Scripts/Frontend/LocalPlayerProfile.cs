using UnityEngine;

namespace CoopPrototype.Frontend
{
    public interface IPlayerPreferences
    {
        string DisplayName { get; set; }
        int Character { get; set; }
        float Volume { get; set; }
        float Sensitivity { get; set; }
        void Save();
    }

    /// <summary>Local preferences, separate from the server-owned lobby profile.</summary>
    public sealed class LocalPlayerProfile : IPlayerPreferences
    {
        public string DisplayName { get; set; } = PlayerPrefs.GetString("crew.name", "Crewmate");
        public int Character { get; set; } = PlayerPrefs.GetInt("crew.character", 0);
        public float Volume { get; set; } = PlayerPrefs.GetFloat("crew.volume", .8f);
        public float Sensitivity { get; set; } = PlayerPrefs.GetFloat("crew.sensitivity", .12f);
        public void Save()
        {
            PlayerPrefs.SetString("crew.name", DisplayName);
            PlayerPrefs.SetInt("crew.character", Character);
            PlayerPrefs.SetFloat("crew.volume", Mathf.Clamp01(Volume));
            PlayerPrefs.SetFloat("crew.sensitivity", Mathf.Clamp(Sensitivity, .02f, .4f));
            PlayerPrefs.Save(); AudioListener.volume = Volume;
        }
    }
}
