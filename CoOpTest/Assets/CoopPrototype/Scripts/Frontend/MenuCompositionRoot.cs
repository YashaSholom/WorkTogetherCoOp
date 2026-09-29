using UnityEngine;

namespace CoopPrototype.Frontend
{
    /// <summary>The only composition root: inspector wiring for Unity objects, constructor injection for plain services.</summary>
    [DefaultExecutionOrder(-900)]
    public sealed class MenuCompositionRoot : MonoBehaviour
    {
        public NetworkSession session;
        public GameFlow flow;
        public MenuPresenter presenter;
        public MenuBackdrop backdrop;
        public Voice.ProximityVoice voice;
        void Awake()
        {
            var preferences = new LocalPlayerProfile();
            AudioListener.volume = preferences.Volume;
            flow.Initialize(session, preferences);
            session.flow = flow;
            session.showPrototypeGUI = false;
            voice.Initialize(flow, new Voice.VoicePreferences());
            presenter.Initialize(flow, preferences, backdrop, voice);
            DontDestroyOnLoad(gameObject);
        }
    }
}
