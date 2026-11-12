using System;
using UnityEngine;

namespace Via.Minigame.Configuration
{
    public enum TutorialGoal { PlaceAttractor, FormParadigm, UseTool, RejoinCutEnds }

    [Serializable]
    public sealed class TutorialStep
    {
        public string instruction;
        public TutorialGoal goal = TutorialGoal.PlaceAttractor;
        public string paradigmId;
        public MinigameTool tool = MinigameTool.Attractor;
        [Min(1)] public int count = 1;
    }

    // A candy sample: a small guided solve for one paradigm. Never completes the real level.
    [CreateAssetMenu(menuName = "Via/Paradigm Sample")]
    public sealed class ParadigmSample : ScriptableObject
    {
        [SerializeField] private string paradigmId = "triangle";
        [SerializeField] private string displayName = "三角形";
        [SerializeField] private string symbol = "△";
        [SerializeField] private InitialStroke[] strokes = Array.Empty<InitialStroke>();
        [SerializeField] private TutorialStep[] steps = Array.Empty<TutorialStep>();

        public string ParadigmId => paradigmId;
        public string DisplayName => displayName;
        public string Symbol => symbol;
        public System.Collections.Generic.IReadOnlyList<InitialStroke> Strokes => strokes;
        public System.Collections.Generic.IReadOnlyList<TutorialStep> Steps => steps;
        public bool IsValid => !string.IsNullOrWhiteSpace(paradigmId) && steps != null && steps.Length > 0;
    }
}
