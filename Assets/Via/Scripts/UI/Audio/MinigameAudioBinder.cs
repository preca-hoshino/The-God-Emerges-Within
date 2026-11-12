using UnityEngine;
using Via.Application;
using Via.Minigame.Runtime;
using Via.Shared;

namespace Via.UI
{
    // Bridges minigame events to the audio library: level results, undo/reset, candy confirmation
    // and the tool operations that actually changed the network.
    public sealed class MinigameAudioBinder : MonoBehaviour
    {
        [SerializeField] private MinigameController minigame;
        [SerializeField] private AudioController audioController;
        [SerializeField] private AudioLibrary library;

        private void OnEnable()
        {
            if (minigame == null || audioController == null || library == null)
            {
                Debug.LogError("MinigameAudioBinder 需要绑定 MinigameController、AudioController 与 AudioLibrary。", this);
                enabled = false;
                return;
            }
            minigame.OnLevelCompleted += LevelCompleted;
            minigame.OnOpponentCompleted += LevelCompleted;
            minigame.OnLevelReset += LevelReset;
            minigame.OnCandyUseConfirmed += CandyUsed;
            minigame.OperationApplied += OperationApplied;
        }

        private void OnDisable()
        {
            if (minigame == null) return;
            minigame.OnLevelCompleted -= LevelCompleted;
            minigame.OnOpponentCompleted -= LevelCompleted;
            minigame.OnLevelReset -= LevelReset;
            minigame.OnCandyUseConfirmed -= CandyUsed;
            minigame.OperationApplied -= OperationApplied;
        }

        private void LevelCompleted(LevelResult result) => Play("level_advance");
        private void CandyUsed(CandyUseInfo info) => Play("candy_use");

        // A deliberate rewind of the open level.
        private void LevelReset() => Play("undo_reset");

        private void OperationApplied(string operation)
        {
            switch (operation)
            {
                case "attractor": Play("attractor_place"); break;
                case "branch": Play("branch_place"); break;
                case "pull": Play("adjust_done"); break;
                case "cut": Play("tool_cut"); break;
                case "reshape": Play("corner_done"); break;
            }
        }

        private void Play(string id)
        {
            if (library.TryGet(id, out var entry) && entry.clip != null) audioController.PlaySfx(entry.clip);
            else Debug.LogWarning("音频库缺少音效：" + id, this);
        }
    }
}
