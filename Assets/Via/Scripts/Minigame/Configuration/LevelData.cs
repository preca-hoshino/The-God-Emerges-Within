using System;
using UnityEngine;

namespace Via.Minigame.Configuration
{
    [Flags]
    public enum MinigameTool
    {
        None = 0,
        Attractor = 1,
        Pull = 2,
        Cut = 4,
        Branch = 8,
        Reshape = 16,
        Candy = 32,
        All = Attractor | Pull | Cut | Branch | Reshape | Candy
    }

    [Serializable]
    public sealed class LevelOutcome
    {
        public string paradigmId;
        public string resultId;
        public string displayName;
        public string description;
        public bool opponentGoal;
    }

    // A pre-existing polyline. The first stroke starts at the origin; later strokes are disconnected parts.
    [Serializable]
    public sealed class InitialStroke
    {
        public Vector2[] points = Array.Empty<Vector2>();
    }

    [CreateAssetMenu(menuName = "Via/Level Data")]
    public sealed class LevelData : ScriptableObject
    {
        [SerializeField] private string levelId = "prototype_01";
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string candyIntroduction;
        [SerializeField] private Vector2 origin = Vector2.zero;
        [SerializeField, Min(0.1f)] private float searchRadius = 3f;
        [SerializeField, Min(0.1f)] private float growthSpeed = 4f;
        [SerializeField, Min(0.001f)] private float joinDistance = 0.5f;
        [SerializeField, Range(1f, 179f)] private float cornerAngle = 20f;
        [SerializeField, Min(0.1f)] private float branchRadius = 1.5f;
        [SerializeField] private MinigameTool tools = MinigameTool.All;
        [SerializeField] private InitialStroke[] initialStrokes = Array.Empty<InitialStroke>();
        [SerializeField] private InitialStroke[] tutorialStrokes = Array.Empty<InitialStroke>();
        [SerializeField] private TutorialStep[] tutorial = Array.Empty<TutorialStep>();
        [SerializeField] private bool hasOpponent;
        [SerializeField] private bool preserveCutPieces = true;
        [SerializeField] private bool allowCurvedPolygon;
        [SerializeField] private LevelOutcome[] outcomes =
        {
            new LevelOutcome { paradigmId = "straight", resultId = "prototype_straight", displayName = "直线" },
            new LevelOutcome { paradigmId = "polyline", resultId = "prototype_polyline", displayName = "折线" }
        };

        public string LevelId => levelId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? levelId : displayName;
        public string CandyIntroduction => candyIntroduction;
        public Vector2 Origin => origin;
        public float SearchRadius => searchRadius;
        public float GrowthSpeed => growthSpeed;
        public float JoinDistance => joinDistance;
        public float CornerAngle => cornerAngle;
        public float BranchRadius => branchRadius;
        public MinigameTool Tools => tools;
        public System.Collections.Generic.IReadOnlyList<InitialStroke> InitialStrokes => initialStrokes;
        public System.Collections.Generic.IReadOnlyList<InitialStroke> TutorialStrokes => tutorialStrokes;
        public System.Collections.Generic.IReadOnlyList<TutorialStep> Tutorial => tutorial;
        public bool HasTutorial => tutorial != null && tutorial.Length > 0;
        public bool HasOpponent => hasOpponent;
        public bool PreserveCutPieces => preserveCutPieces;
        public bool AllowCurvedPolygon => allowCurvedPolygon;
        public bool Allows(MinigameTool tool) => (tools & tool) == tool;
        public LevelOutcome FindOutcome(string paradigmId) =>
            Array.Find(outcomes, outcome => outcome != null && outcome.paradigmId == paradigmId);
        public System.Collections.Generic.IReadOnlyList<LevelOutcome> Outcomes => outcomes;

        public bool IsValid => !string.IsNullOrWhiteSpace(levelId) && searchRadius > joinDistance &&
            joinDistance > 0f && growthSpeed > 0f && branchRadius > 0f && outcomes != null && outcomes.Length > 0;
    }
}
