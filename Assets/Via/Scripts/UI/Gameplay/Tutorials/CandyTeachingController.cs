using UnityEngine;
using Via.Minigame.Configuration;
using Via.Minigame.Domain;
using Via.Minigame.Runtime;

namespace Via.UI
{
    // Runs candy sample teaching: confirms one candy use, saves the solve, guides the sample, restores.
    public sealed class CandyTeachingController : MonoBehaviour
    {
        [SerializeField] private GameUIController ui;
        [SerializeField] private MinigameController minigame;
        [SerializeField] private NeuronToolController tools;
        [SerializeField] private ParadigmSample[] samples = System.Array.Empty<ParadigmSample>();

        private ParadigmSample current;
        private int step;
        private bool completedShown;
        private NeuronToolController.Tool solveTool;
        private int baseAttractor, basePull, baseCut, baseBranch, baseReshape;
        private bool baseRejoined;

        private void OnEnable()
        {
            if (ui == null || minigame == null || tools == null)
            {
                Debug.LogError("Bind UI, MinigameController and tools for candy teaching.", this);
                enabled = false;
                return;
            }
            ui.SampleRequested += Begin;
            ui.SampleAvailable = CanTeach;
            ui.TeachingExitRequested += Exit;
            ui.TeachingReturnRequested += Return;
        }

        private void OnDisable()
        {
            if (ui == null) return;
            ui.SampleRequested -= Begin;
            if (ui.SampleAvailable == CanTeach) ui.SampleAvailable = null;
            ui.TeachingExitRequested -= Exit;
            ui.TeachingReturnRequested -= Return;
        }

        private void Begin(string paradigmId)
        {
            if (minigame.IsTeaching) return;
            ParadigmSample sample = null;
            foreach (var candidate in samples)
                if (candidate != null && candidate.IsValid && candidate.ParadigmId == paradigmId) { sample = candidate; break; }
            if (sample == null) return;
            if (!minigame.BeginTeaching(sample)) return;

            solveTool = tools.SelectedTool;
            minigame.ConfirmCandyUse(paradigmId);
            current = sample;
            step = 0;
            completedShown = false;
            ShowStep();
        }

        private bool CanTeach(string paradigmId)
        {
            foreach (var sample in samples)
                if (sample != null && sample.IsValid && sample.ParadigmId == paradigmId) return true;
            return false;
        }

        private void ShowStep()
        {
            CaptureBaseline();
            var stepData = current.Steps[step];
            ui.ShowTeaching(step + 1, current.Steps.Count, stepData.instruction, current.Symbol, "按提示亲手完成这一步。", null);
        }

        private void Update()
        {
            if (current == null || !minigame.IsTeaching || !minigame.CanEdit) return;
            var stepData = current.Steps[step];
            if (!IsSatisfied(stepData)) return;
            step++;
            if (step >= current.Steps.Count) Complete();
            else ShowStep();
        }

        private bool IsSatisfied(TutorialStep stepData)
        {
            // Only count actions made after this step started; counters persist across steps.
            if (stepData.goal == TutorialGoal.RejoinCutEnds)
                return minigame.HasRejoinedCuts &&
                    (minigame.HasRejoinedCuts != baseRejoined || UsedSince(MinigameTool.Pull) >= stepData.count);
            if (stepData.goal == TutorialGoal.FormParadigm)
                return ParadigmEvaluator.Contains(minigame.EvaluateParadigms(), stepData.paradigmId);
            if (stepData.goal == TutorialGoal.UseTool)
                return UsedSince(stepData.tool) >= stepData.count;
            return UsedSince(MinigameTool.Attractor) >= stepData.count;
        }

        private void CaptureBaseline()
        {
            baseAttractor = minigame.AttractorPlacedCount;
            basePull = minigame.ToolUses(MinigameTool.Pull);
            baseCut = minigame.ToolUses(MinigameTool.Cut);
            baseBranch = minigame.ToolUses(MinigameTool.Branch);
            baseReshape = minigame.ToolUses(MinigameTool.Reshape);
            baseRejoined = minigame.HasRejoinedCuts;
        }

        private int UsedSince(MinigameTool tool)
        {
            switch (tool)
            {
                case MinigameTool.Attractor: return minigame.AttractorPlacedCount - baseAttractor;
                case MinigameTool.Pull: return minigame.ToolUses(MinigameTool.Pull) - basePull;
                case MinigameTool.Cut: return minigame.ToolUses(MinigameTool.Cut) - baseCut;
                case MinigameTool.Branch: return minigame.ToolUses(MinigameTool.Branch) - baseBranch;
                case MinigameTool.Reshape: return minigame.ToolUses(MinigameTool.Reshape) - baseReshape;
                default: return 0;
            }
        }

        private void Complete()
        {
            minigame.EndTeaching();
            tools.SelectTool(solveTool);
            current = null;
            completedShown = true;
            ui.ShowTeachingComplete();
        }

        private void Exit()
        {
            if (current == null) return;
            minigame.EndTeaching();
            tools.SelectTool(solveTool);
            current = null;
            ui.HideTeaching();
        }

        private void Return()
        {
            if (!completedShown) return;
            completedShown = false;
            ui.HideTeaching();
        }
    }
}
