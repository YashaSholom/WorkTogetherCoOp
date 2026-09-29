using UnityEngine;

namespace CoopPrototype.Frontend
{
    public sealed class CharacterPreview : MonoBehaviour
    {
        public Renderer[] variants;
        public Animator animator;
        public float waveInterval = 12;
        int selected = -1;
        float nextWave;
        public void Show(int index)
        {
            if (selected == index) return;
            selected = index;
            for (int i=0;i<variants.Length;i++) variants[i].enabled = i == index;
        }
        void OnEnable() => nextWave = Time.unscaledTime + Random.Range(4f, 10f);
        void Update()
        {
            if (animator == null || Time.unscaledTime < nextWave) return;
            nextWave = Time.unscaledTime + waveInterval;
            if (animator.HasState(0, Animator.StringToHash("Base Layer.Wave"))) animator.CrossFadeInFixedTime("Wave", .2f);
        }
    }
}
