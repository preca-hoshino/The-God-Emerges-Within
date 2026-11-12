using System;
using System.Collections.Generic;
using UnityEngine;
using Via.Shared;
using Via.Minigame.Configuration;
using Via.Minigame.Domain;

namespace Via.Minigame.Runtime
{
    public sealed class MinigameController : MonoBehaviour, IMinigameSession
    {
        [SerializeField] private LevelData levelData;
        [SerializeField] private LevelData[] additionalLevels = Array.Empty<LevelData>();
        private LevelData activeLevel;
        private LevelStartRequest request;
        private readonly Stack<(NeuronNetwork network, NeuronGrowthSystem growth)> undo =
            new Stack<(NeuronNetwork, NeuronGrowthSystem)>();
        public bool CanUndo => CanEdit && !IsGuided && undo.Count > 0;
        public NeuronNetwork Network { get; private set; }
        public NeuronGrowthSystem Growth { get; private set; }
        public LevelData Configuration => activeLevel != null ? activeLevel : levelData;
        public bool IsOpen { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsCompleted { get; private set; }
        public bool CanEdit => IsOpen && !IsPaused && !IsCompleted && Network != null && Growth != null;
        public event Action<LevelResult> OnLevelCompleted;
        public event Action<LevelResult> OnOpponentCompleted;
        // The candy tutorial will raise this only after explicit use confirmation.
        public event Action<CandyUseInfo> OnCandyUseConfirmed;
        public event Action OnLevelStarted;
        public event Action OnLevelReset;
        // Raised after an edit lands on the network: attractor / branch / pull / cut / reshape.
        public event Action<string> OperationApplied;
        private int pullUses, cutUses, branchUses, reshapeUses;
        public bool IsTutorial { get; private set; }
        public bool IsGuided => IsTeaching || IsTutorial;
        public bool HasRejoinedCuts => IsOpen && Growth != null && Network != null && !Growth.IsGrowing && cutUses > 0 && !Network.HasCutEndpoints &&
            NetworkAnalysis.Analyze(Network, Configuration.CornerAngle).ComponentCount == 1;

        public void StartLevel(LevelStartRequest startRequest)
        {
            var selectedLevel = levelData != null && SameLevelId(levelData.LevelId, startRequest.LevelId) ? levelData :
                Array.Find(additionalLevels, level => level != null && SameLevelId(level.LevelId, startRequest.LevelId));
            if (selectedLevel == null || !selectedLevel.IsValid)
                throw new InvalidOperationException("找不到可用关卡配置：" + startRequest.LevelId);
            if (string.IsNullOrEmpty(startRequest.SessionId))
                throw new ArgumentException("Invalid level start request.");
            CloseLevel();
            activeLevel = selectedLevel;
            request = startRequest;
            IsOpen = true;
            IsCompleted = false;
            undo.Clear();
            ResetLevelInternal();
            OnLevelStarted?.Invoke();
        }

        private static bool SameLevelId(string configured, string requested) =>
            string.Equals(configured, requested, System.StringComparison.OrdinalIgnoreCase);

        // A deliberate rewind of an open level; building the level at start does not raise this.
        public void ResetLevel()
        {
            if (ResetLevelInternal()) OnLevelReset?.Invoke();
        }

        private bool ResetLevelInternal()
        {
            if (!IsOpen || IsCompleted || IsTeaching) return false;
            undo.Clear();
            IsTutorial = false;
            pullUses = cutUses = branchUses = reshapeUses = 0;
            AttractorPlacedCount = 0;
            Network = InitialNetworkBuilder.Create(Configuration.InitialStrokes, Configuration.Origin);
            Growth = new NeuronGrowthSystem(Network, Configuration.SearchRadius,
                Configuration.GrowthSpeed, Configuration.JoinDistance, new System.Random());
            Growth.ResetTips();
            IsPaused = false;
            IsCompleted = false;
            return true;
        }

        public int ToolUses(MinigameTool tool)
        {
            switch (tool)
            {
                case MinigameTool.Attractor: return AttractorPlacedCount;
                case MinigameTool.Pull: return pullUses;
                case MinigameTool.Cut: return cutUses;
                case MinigameTool.Branch: return branchUses;
                case MinigameTool.Reshape: return reshapeUses;
                default: return 0;
            }
        }

        // Basic tool tutorial: builds the tutorial scene; on finish the level resets to its initial structure.
        public bool BeginTutorial()
        {
            if (!IsOpen || IsTeaching || IsTutorial || !Configuration.HasTutorial) return false;
            undo.Clear();
            IsTutorial = true;
            pullUses = cutUses = branchUses = reshapeUses = 0;
            AttractorPlacedCount = 0;
            Network = InitialNetworkBuilder.Create(Configuration.TutorialStrokes, Configuration.Origin);
            Growth = new NeuronGrowthSystem(Network, Configuration.SearchRadius,
                Configuration.GrowthSpeed, Configuration.JoinDistance, new System.Random());
            Growth.ResetTips();
            return true;
        }

        public void EndTutorial() { if (IsTutorial) ResetLevel(); }

        public bool PlaceAttractor(Vector2 position)
        {
            if (CanEdit && Configuration.Allows(MinigameTool.Attractor) &&
                float.IsFinite(position.x) && float.IsFinite(position.y))
            {
                SaveOperation();
                Growth.PlaceAttractor(position);
                AttractorPlacedCount++;
                OperationApplied?.Invoke("attractor");
                return true;
            }
            return false;
        }

        public bool PlaceBranchZone(Vector2 position)
        {
            if (!CanEdit || !Configuration.Allows(MinigameTool.Branch) ||
                !float.IsFinite(position.x) || !float.IsFinite(position.y)) return false;
            SaveOperation();
            Growth.PlaceBranchZone(position, Configuration.BranchRadius);
            branchUses++;
            OperationApplied?.Invoke("branch");
            return true;
        }

        private void SaveOperation()
        {
            if (IsGuided) return;
            var network = Network.Copy();
            undo.Push((network, Growth.CopyFor(network)));
        }

        public bool PullPath(PullSelection selection, Vector2 destination)
        {
            if (!CanEdit || !Configuration.Allows(MinigameTool.Pull) || Growth.IsGrowing || !selection.IsValid || !float.IsFinite(destination.x) ||
                !float.IsFinite(destination.y)) return false;
            if (!PathPull.Apply(Network, selection, destination, Configuration.JoinDistance, out var trial, out _)) return false;
            SaveOperation();
            Network = trial;
            Growth = Growth.CopyFor(Network, true);
            pullUses++;
            OperationApplied?.Invoke("pull");
            return true;
        }

        public bool CutPath(Vector2 start, Vector2 end)
        {
            if (!CanEdit || !Configuration.Allows(MinigameTool.Cut) || Growth.IsGrowing) return false;
            if (!PathCut.Apply(Network, start, end, out NeuronNetwork next, Configuration.PreserveCutPieces)) return false;
            SaveOperation();
            Network = next;
            Growth = Growth.CopyFor(Network, true);
            cutUses++;
            OperationApplied?.Invoke("cut");
            return true;
        }

        public bool ReshapeCorner(int corner, int segments)
        {
            if (!CanEdit || !Configuration.Allows(MinigameTool.Reshape) || Growth.IsGrowing) return false;
            if (!PathReshape.Apply(Network, corner, segments, out NeuronNetwork next)) return false;
            SaveOperation();
            Network = next;
            Growth = Growth.CopyFor(Network, true);
            reshapeUses++;
            OperationApplied?.Invoke("reshape");
            return true;
        }

        public bool UndoOperation()
        {
            if (!CanUndo) return false;
            var state = undo.Pop();
            Network = state.network;
            Growth = state.growth;
            return true;
        }

        public IReadOnlyList<string> EvaluateParadigms() => !IsOpen || Growth.IsGrowing ?
            Array.Empty<string>() : ParadigmEvaluator.Evaluate(Network, Configuration.CornerAngle, Configuration.AllowCurvedPolygon);

        // Opponent placement: no undo entry, ignores tool gating, still respects pause/guided/complete.
        public bool OpponentAttract(Vector2 position)
        {
            if (!CanEdit || IsGuided || !float.IsFinite(position.x) || !float.IsFinite(position.y)) return false;
            Growth.PlaceAttractor(position);
            return true;
        }

        public LevelOutcome CheckResult()
        {
            if (!IsOpen || Growth.IsGrowing) return null;
            var matches = ParadigmEvaluator.Evaluate(Network, Configuration.CornerAngle, Configuration.AllowCurvedPolygon);
            foreach (var outcome in Configuration.Outcomes)
                if (outcome != null && ParadigmEvaluator.Contains(matches, outcome.paradigmId))
                    return outcome;
            return null;
        }

        public bool ConfirmResult()
        {
            if (!CanEdit || IsGuided) return false;
            LevelOutcome outcome = CheckResult();
            if (outcome == null) return false;
            if (outcome.opponentGoal) return TryCompleteOpponent();
            IsCompleted = true;
            OnLevelCompleted?.Invoke(new LevelResult(request, outcome.resultId, outcome.paradigmId));
            return true;
        }

        public bool TryCompleteOpponent()
        {
            if (!CanEdit || IsGuided || !Configuration.HasOpponent) return false;
            LevelOutcome outcome = CheckResult();
            if (outcome == null || !outcome.opponentGoal) return false;
            IsCompleted = true;
            OnOpponentCompleted?.Invoke(new LevelResult(request, outcome.resultId, outcome.paradigmId));
            return true;
        }

        // Candy sample teaching: saves the real solve, runs a guided sample, then restores it.
        public bool IsTeaching { get; private set; }
        public int AttractorPlacedCount { get; private set; }
        private NeuronNetwork solveNetwork;
        private NeuronGrowthSystem solveGrowth;
        private (NeuronNetwork network, NeuronGrowthSystem growth)[] solveUndo;
        private (int attractor, int pull, int cut, int branch, int reshape) solveToolUses;
        private string teachingParadigm;
        private bool teachingUseConfirmed;

        public void ConfirmCandyUse(string paradigmId)
        {
            if (!CanEdit || !IsTeaching || teachingUseConfirmed || paradigmId != teachingParadigm) return;
            teachingUseConfirmed = true;
            OnCandyUseConfirmed?.Invoke(new CandyUseInfo(Configuration.LevelId, request.SessionId,
                Guid.NewGuid().ToString("N"), paradigmId));
        }

        public bool BeginTeaching(ParadigmSample sample)
        {
            if (!CanEdit || IsGuided || !Configuration.Allows(MinigameTool.Candy) || sample == null ||
                !sample.IsValid || Configuration.FindOutcome(sample.ParadigmId) == null) return false;
            solveNetwork = Network;
            solveGrowth = Growth;
            solveUndo = undo.ToArray();
            solveToolUses = (AttractorPlacedCount, pullUses, cutUses, branchUses, reshapeUses);
            undo.Clear();
            IsTeaching = true;
            teachingParadigm = sample.ParadigmId;
            teachingUseConfirmed = false;
            pullUses = cutUses = branchUses = reshapeUses = 0;
            AttractorPlacedCount = 0;
            Network = InitialNetworkBuilder.Create(sample.Strokes, Configuration.Origin);
            Growth = new NeuronGrowthSystem(Network, Configuration.SearchRadius,
                Configuration.GrowthSpeed, Configuration.JoinDistance, new System.Random());
            Growth.ResetTips();
            return true;
        }

        public void EndTeaching()
        {
            if (!IsTeaching) return;
            Network = solveNetwork;
            Growth = solveGrowth;
            solveNetwork = null;
            solveGrowth = null;
            undo.Clear();
            for (int i = solveUndo.Length - 1; i >= 0; i--) undo.Push(solveUndo[i]);
            solveUndo = null;
            (AttractorPlacedCount, pullUses, cutUses, branchUses, reshapeUses) = solveToolUses;
            IsTeaching = false;
            teachingParadigm = null;
            teachingUseConfirmed = false;
        }

        public void CloseLevel()
        {
            IsOpen = false; IsPaused = false; Network = null; Growth = null; undo.Clear();
            IsTeaching = false; IsTutorial = false; solveNetwork = null; solveGrowth = null;
            solveUndo = null; teachingParadigm = null; teachingUseConfirmed = false;
        }
        public void PauseLevel() { if (IsOpen) IsPaused = true; }
        public void ResumeLevel() { if (IsOpen) IsPaused = false; }
        private void Update() { if (CanEdit) Growth.Tick(Time.deltaTime); }
    }
}
