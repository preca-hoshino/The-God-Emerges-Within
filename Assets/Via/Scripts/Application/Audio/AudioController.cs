using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace Via.Application
{
    // Audio service: owns the pre-attached sources and mixes music, ambience, the growth bed and
    // one-shot effects. Clip selection lives in AudioLibrary; this class only plays what it is given.
    public sealed class AudioController : MonoBehaviour
    {
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource musicSourceB;
        [SerializeField] private AudioSource ambienceSource;
        [SerializeField] private AudioSource bedSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioMixer mixer;

        private AudioSource activeMusic;
        private Coroutine musicRoutine;
        private AudioClip musicPending;
        private bool musicPendingLoop;
        private Coroutine ambienceRoutine;
        private AudioClip ambiencePending;
        private Coroutine bedRoutine;
        private float musicFactorA = 1f;
        private float musicFactorB;
        private float ambienceFactor = 1f;
        private float bedFactor;

        public float Master { get; private set; } = 0.8f;
        public float Music { get; private set; } = 0.8f;
        public float Effects { get; private set; } = 0.8f;

        public AudioClip CurrentMusic => activeMusic != null ? activeMusic.clip : null;
        public bool IsGrowthBedActive { get; private set; }

        private void Awake()
        {
            activeMusic = musicSource != null ? musicSource : musicSourceB;
            if (musicSource != null) musicSource.loop = true;
            if (musicSourceB != null) musicSourceB.loop = true;
            if (ambienceSource != null) ambienceSource.loop = true;
            if (bedSource != null) bedSource.loop = true;
            if (sfxSource != null) sfxSource.loop = false;
            ApplyVolumes();
        }

        public void SetVolumes(float master, float music, float effects)
        {
            Master = Mathf.Clamp01(master);
            Music = Mathf.Clamp01(music);
            Effects = Mathf.Clamp01(effects);
            ApplyVolumes();
            if (mixer != null)
            {
                mixer.SetFloat("MasterVolume", Mathf.Log10(Mathf.Max(Master, 0.0001f)) * 20f);
                mixer.SetFloat("MusicVolume", Mathf.Log10(Mathf.Max(Music, 0.0001f)) * 20f);
                mixer.SetFloat("EffectsVolume", Mathf.Log10(Mathf.Max(Effects, 0.0001f)) * 20f);
            }
        }

        // Instant switch, used for the title loop where there is nothing to fade from.
        public void PlayMusic(AudioClip clip) => PlayMusicNow(clip, true);

        public void PlayMusicNow(AudioClip clip, bool loop)
        {
            StopMusicRoutine();
            if (musicSource != null) musicSource.Stop();
            if (musicSourceB != null) musicSourceB.Stop();
            musicFactorA = 0f;
            musicFactorB = 0f;
            activeMusic = musicSource != null ? musicSource : musicSourceB;
            if (clip == null || activeMusic == null) { ApplyVolumes(); return; }
            activeMusic.clip = clip;
            activeMusic.loop = loop;
            activeMusic.time = 0f;
            activeMusic.Play();
            SetFactor(activeMusic, 1f);
            ApplyVolumes();
        }

        // Crossfade into a new track; delay postpones the fade-in after the old track has left.
        public void PlayStoryMusic(AudioClip clip, bool loop, float fadeOut, float delay, float fadeIn)
        {
            if (clip == null) { FadeOutMusic(fadeOut > 0f ? fadeOut : 1f); return; }
            if (musicRoutine == null && activeMusic != null && activeMusic.clip == clip && activeMusic.isPlaying && activeMusic.loop == loop) return;
            // A fade towards this same track is already running; let it finish instead of restarting.
            if (musicPending == clip && musicPendingLoop == loop) return;
            StopMusicRoutine();
            musicPending = clip;
            musicPendingLoop = loop;
            musicRoutine = StartCoroutine(CrossfadeRoutine(clip, loop, fadeOut, delay, fadeIn));
        }

        public void FadeOutMusic(float duration)
        {
            if (musicRoutine == null && (activeMusic == null || !activeMusic.isPlaying || FactorOf(activeMusic) <= 0f)) return;
            StopMusicRoutine();
            musicRoutine = StartCoroutine(FadeOutMusicRoutine(duration));
        }

        public void StopMusic()
        {
            StopMusicRoutine();
            if (musicSource != null) musicSource.Stop();
            if (musicSourceB != null) musicSourceB.Stop();
            musicFactorA = 1f;
            musicFactorB = 0f;
            ApplyVolumes();
        }

        public void SetAmbience(AudioClip clip)
        {
            if (clip == null) { StopAmbience(1f); return; }
            if (ambienceSource == null) return;
            // Same bed already running steadily; a pending fade/switch still needs to be replaced.
            if (ambienceSource.clip == clip && ambienceSource.isPlaying && ambienceRoutine == null) return;
            if (ambiencePending == clip) return;
            StopAmbienceRoutine();
            ambiencePending = clip;
            ambienceRoutine = StartCoroutine(SwitchAmbienceRoutine(clip));
        }

        public void StopAmbience(float fade)
        {
            if (ambienceSource == null) return;
            if (ambienceRoutine == null && !ambienceSource.isPlaying) return;
            StopAmbienceRoutine();
            ambienceRoutine = StartCoroutine(FadeAmbienceRoutine(fade, true));
        }

        public void SetGrowthBed(AudioClip clip, bool active, float fade)
        {
            if (clip == null || bedSource == null) return;
            if (IsGrowthBedActive == active) return;
            IsGrowthBedActive = active;
            StopBedRoutine();
            bedRoutine = StartCoroutine(GrowthBedRoutine(clip, active, fade));
        }

        public void PlaySfx(AudioClip clip)
        {
            if (clip == null || sfxSource == null) return;
            sfxSource.PlayOneShot(clip);
        }

        private IEnumerator CrossfadeRoutine(AudioClip clip, bool loop, float fadeOut, float delay, float fadeIn)
        {
            var from = activeMusic;
            var to = from == musicSource ? musicSourceB : musicSource;
            if (from != null && from.isPlaying && fadeOut > 0f)
            {
                float start = FactorOf(from);
                for (float t = 0f; t < fadeOut; t += Time.unscaledDeltaTime)
                {
                    SetFactor(from, Mathf.Lerp(start, 0f, t / fadeOut));
                    ApplyVolumes();
                    yield return null;
                }
            }
            if (from != null) { from.Stop(); SetFactor(from, 0f); }
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (to == null) { musicPending = null; musicRoutine = null; yield break; }
            to.clip = clip;
            to.loop = loop;
            to.time = 0f;
            SetFactor(to, 0f);
            to.Play();
            activeMusic = to;
            if (fadeIn > 0f)
            {
                for (float t = 0f; t < fadeIn; t += Time.unscaledDeltaTime)
                {
                    SetFactor(to, Mathf.Clamp01(t / fadeIn));
                    ApplyVolumes();
                    yield return null;
                }
            }
            SetFactor(to, 1f);
            ApplyVolumes();
            musicPending = null;
            musicRoutine = null;
        }

        private IEnumerator FadeOutMusicRoutine(float duration)
        {
            var from = activeMusic;
            if (from != null && from.isPlaying)
            {
                float start = FactorOf(from);
                for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
                {
                    SetFactor(from, Mathf.Lerp(start, 0f, t / duration));
                    ApplyVolumes();
                    yield return null;
                }
                from.Stop();
                SetFactor(from, 0f);
                ApplyVolumes();
            }
            musicRoutine = null;
        }

        private IEnumerator SwitchAmbienceRoutine(AudioClip clip)
        {
            if (ambienceSource.isPlaying)
            {
                float start = ambienceFactor;
                for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
                {
                    ambienceFactor = Mathf.Lerp(start, 0f, t / 0.5f);
                    ApplyVolumes();
                    yield return null;
                }
                ambienceSource.Stop();
            }
            ambienceSource.clip = clip;
            ambienceSource.loop = true;
            ambienceSource.time = 0f;
            ambienceSource.Play();
            ambienceFactor = 0f;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
            {
                ambienceFactor = Mathf.Clamp01(t / 1f);
                ApplyVolumes();
                yield return null;
            }
            ambienceFactor = 1f;
            ApplyVolumes();
            ambiencePending = null;
            ambienceRoutine = null;
        }

        private IEnumerator FadeAmbienceRoutine(float duration, bool stop)
        {
            if (ambienceSource.isPlaying)
            {
                float start = ambienceFactor;
                for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
                {
                    ambienceFactor = Mathf.Lerp(start, 0f, t / duration);
                    ApplyVolumes();
                    yield return null;
                }
                if (stop) ambienceSource.Stop();
            }
            ambienceFactor = stop ? 1f : 0f;
            ApplyVolumes();
            ambienceRoutine = null;
        }

        private IEnumerator GrowthBedRoutine(AudioClip clip, bool active, float fade)
        {
            if (active)
            {
                bedSource.clip = clip;
                bedSource.loop = true;
                bedSource.time = 0f;
                bedSource.Play();
                bedFactor = 0f;
                for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
                {
                    bedFactor = Mathf.Clamp01(t / fade);
                    ApplyVolumes();
                    yield return null;
                }
                bedFactor = 1f;
            }
            else
            {
                float start = bedFactor;
                for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
                {
                    bedFactor = Mathf.Lerp(start, 0f, t / fade);
                    ApplyVolumes();
                    yield return null;
                }
                bedSource.Stop();
                bedFactor = 0f;
            }
            ApplyVolumes();
            bedRoutine = null;
        }

        private float FactorOf(AudioSource source) => source == musicSource ? musicFactorA : musicFactorB;

        private void SetFactor(AudioSource source, float value)
        {
            if (source == musicSource) musicFactorA = value;
            else if (source == musicSourceB) musicFactorB = value;
        }

        private void ApplyVolumes()
        {
            float musicVolume = Master * Music;
            float effectsVolume = Master * Effects;
            if (musicSource != null) musicSource.volume = musicVolume * musicFactorA;
            if (musicSourceB != null) musicSourceB.volume = musicVolume * musicFactorB;
            if (ambienceSource != null) ambienceSource.volume = effectsVolume * ambienceFactor;
            if (bedSource != null) bedSource.volume = effectsVolume * bedFactor;
            if (sfxSource != null) sfxSource.volume = effectsVolume;
        }

        private void StopMusicRoutine()
        {
            musicPending = null;
            if (musicRoutine != null) { StopCoroutine(musicRoutine); musicRoutine = null; }
        }

        private void StopAmbienceRoutine()
        {
            ambiencePending = null;
            if (ambienceRoutine != null) { StopCoroutine(ambienceRoutine); ambienceRoutine = null; }
        }

        private void StopBedRoutine()
        {
            if (bedRoutine != null) { StopCoroutine(bedRoutine); bedRoutine = null; }
        }
    }
}
