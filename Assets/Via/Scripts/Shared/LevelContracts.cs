using System;

namespace Via.Shared
{
    public readonly struct LevelStartRequest
    {
        public string LevelId { get; }
        public string SessionId { get; }

        public LevelStartRequest(string levelId, string sessionId)
        {
            if (string.IsNullOrWhiteSpace(levelId) || string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("Level and session identifiers are required.");
            LevelId = levelId;
            SessionId = sessionId;
        }
    }

    public readonly struct LevelResult
    {
        public string LevelId { get; }
        public string SessionId { get; }
        public string ResultId { get; }
        public string ParadigmId { get; }

        public LevelResult(LevelStartRequest request, string resultId, string paradigmId)
        {
            LevelId = request.LevelId;
            SessionId = request.SessionId;
            ResultId = resultId;
            ParadigmId = paradigmId;
        }
    }
}
