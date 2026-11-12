using UnityEngine;
using Via.Story.Domain;

namespace Via.Application
{
    /// <summary>Adapts flow pause to Unity's simulation clock and gameplay audio.</summary>
    public sealed class GamePauseController : MonoBehaviour
    {
        [SerializeField] private GameController controller;
        private GameFlow flow;
        private bool started;
        private bool ownsPause;
        private float previousTimeScale;
        private bool previousAudioPause;

        private void Start()
        {
            started = true;
            Bind();
        }

        private void OnEnable() { if (started) Bind(); }

        private void Bind()
        {
            if (flow != null) return;
            if (controller == null || controller.Flow == null)
            {
                Debug.LogError("Bind an initialized GameController for game pause.", this);
                enabled = false;
                return;
            }
            flow = controller.Flow;
            flow.Changed += ApplyPause;
            ApplyPause();
        }

        private void ApplyPause()
        {
            if (!flow.IsPaused) { ReleasePause(); return; }
            if (ownsPause) return;
            // One application adapter owns these engine settings. Nested menus must not
            // overwrite the values to restore, including a pre-existing slow-motion state.
            previousTimeScale = Time.timeScale;
            previousAudioPause = AudioListener.pause;
            ownsPause = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        private void ReleasePause()
        {
            if (!ownsPause) return;
            Time.timeScale = previousTimeScale;
            AudioListener.pause = previousAudioPause;
            ownsPause = false;
        }

        private void OnDisable()
        {
            if (flow != null) flow.Changed -= ApplyPause;
            flow = null;
            ReleasePause();
        }
    }
}
