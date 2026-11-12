using System;

namespace Via.Shared
{
    public interface IMinigameSession
    {
        event Action<LevelResult> OnLevelCompleted;
        // Automatic opponent completion advances the story without recording a player choice.
        event Action<LevelResult> OnOpponentCompleted;
        event Action<CandyUseInfo> OnCandyUseConfirmed;
        void StartLevel(LevelStartRequest request);
        void CloseLevel();
        void PauseLevel();
        void ResumeLevel();
    }

    public readonly struct CandyUseInfo
    {
        public string LevelId { get; }
        public string SessionId { get; }
        public string UseId { get; }
        public string ParadigmId { get; }

        public CandyUseInfo(string levelId, string sessionId, string useId, string paradigmId)
        {
            LevelId = levelId;
            SessionId = sessionId;
            UseId = useId;
            ParadigmId = paradigmId;
        }
    }
}
