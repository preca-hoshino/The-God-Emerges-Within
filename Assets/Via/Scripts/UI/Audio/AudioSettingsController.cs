using UnityEngine;
using Via.Application;

namespace Via.UI
{
    // Loads and saves volume settings, and forwards them to the audio service.
    public sealed class AudioSettingsController : MonoBehaviour
    {
        [SerializeField] private AudioController audioController;
        public float Master { get; private set; }
        public float Music { get; private set; }
        public float Effects { get; private set; }
        private void Awake()
        {
            Master = PlayerPrefs.GetFloat("Via.Volume.Master", 0.8f);
            Music = PlayerPrefs.GetFloat("Via.Volume.Music", 0.8f);
            Effects = PlayerPrefs.GetFloat("Via.Volume.Effects", 0.8f);
            Apply();
        }
        public void SetVolumes(float master, float music, float effects)
        {
            Master = Mathf.Clamp01(master); Music = Mathf.Clamp01(music); Effects = Mathf.Clamp01(effects);
            PlayerPrefs.SetFloat("Via.Volume.Master", Master);
            PlayerPrefs.SetFloat("Via.Volume.Music", Music);
            PlayerPrefs.SetFloat("Via.Volume.Effects", Effects);
            Apply();
        }
        private void Apply()
        {
            AudioListener.volume = Master;
            if (audioController != null) audioController.SetVolumes(Master, Music, Effects);
        }
        public void Flush() => PlayerPrefs.Save();
    }
}
