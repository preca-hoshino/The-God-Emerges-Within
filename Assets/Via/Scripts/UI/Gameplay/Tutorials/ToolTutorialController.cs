using UnityEngine;
using Via.Minigame.Configuration;
using Via.Minigame.Domain;
using Via.Minigame.Runtime;

namespace Via.UI
{
    // Basic tool tutorial for a level: step-by-step tool prompts, then the level resets for the real solve.
    public sealed class ToolTutorialController : MonoBehaviour
    {
        [SerializeField] private GameUIController ui;
        [SerializeField] private MinigameController minigame;
        [SerializeField] private NeuronToolController tools;

        private bool active;
        private bool pending;
        private int step;
        private int baseAttractor, basePull, baseCut, baseBranch, baseReshape;
        private bool baseRejoined;

        private void OnEnable()
        {
            if (ui == null || minigame == null || tools == null)
            {
                Debug.LogError("Bind UI, MinigameController and NeuronToolController for the tool tutorial.", this);
                enabled = false;
                return;
            }
            minigame.OnLevelStarted += Begin;
            ui.TeachingExitRequested += Exit;
        }

        private void OnDisable()
        {
            if (ui == null || minigame == null) return;
            minigame.OnLevelStarted -= Begin;
            ui.TeachingExitRequested -= Exit;
            active = false;
            pending = false;
        }

        private void Begin()
        {
            active = false;
            pending = false;
            step = 0;
            if (minigame.IsGuided || !minigame.Configuration.HasTutorial) return;
            // The level intro popup pauses the level and disables the tool bar. Starting the
            // tutorial underneath it would leave every tool prompt (and its tooltip) dead, so
            // the tutorial waits until that popup is dismissed.
            pending = true;
        }

        private void ShowStep()
        {
            CaptureBaseline();
            var data = minigame.Configuration.Tutorial[step];
            switch (data.tool)
            {
                case MinigameTool.Attractor: tools.SelectTool(NeuronToolController.Tool.Attractor); break;
                case MinigameTool.Pull: tools.SelectTool(NeuronToolController.Tool.Pull); break;
                case MinigameTool.Cut: tools.SelectTool(NeuronToolController.Tool.Cut); break;
                case MinigameTool.Branch: tools.SelectTool(NeuronToolController.Tool.Branch); break;
                case MinigameTool.Reshape: tools.SelectTool(NeuronToolController.Tool.Reshape); break;
            }
            ui.ShowTeaching(step + 1, minigame.Configuration.Tutorial.Count, data.instruction, "工具教程", "按提示使用左上角工具。", null);
        }

        private void Update()
        {
            if (pending)
            {
                if (ui == null || ui.HasModal || !minigame.IsOpen || minigame.IsPaused) return;
                pending = false;
                if (!minigame.BeginTutorial()) return;
                active = true;
                step = 0;
                ShowStep();
                return;
            }
            if (!active) return;
            if (!minigame.IsOpen || !minigame.IsTutorial)
            {
                active = false;
                ui.HideTeaching();
                return;
            }
            if (minigame.IsPaused || minigame.Growth.IsGrowing) return;
            var data = minigame.Configuration.Tutorial[step];
            if (!IsSatisfied(data)) return;
            step++;
            if (step >= minigame.Configuration.Tutorial.Count) Finish(true);
            else ShowStep();
        }

        private bool IsSatisfied(TutorialStep data)
        {
            // Counters are cumulative across the tutorial, so each step only counts actions made
            // after that step became active. Otherwise a later pull/attractor step is satisfied
            // instantly by the action that completed an earlier step.
            if (data.goal == TutorialGoal.RejoinCutEnds)
                return minigame.HasRejoinedCuts &&
                    (minigame.HasRejoinedCuts != baseRejoined || UsedSince(MinigameTool.Pull) >= data.count);
            if (data.goal == TutorialGoal.FormParadigm)
                return ParadigmEvaluator.Contains(minigame.EvaluateParadigms(), data.paradigmId);
            if (data.goal == TutorialGoal.UseTool)
                return UsedSince(data.tool) >= data.count;
            return UsedSince(MinigameTool.Attractor) >= data.count;
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

        private void Finish(bool completed)
        {
            active = false;
            tools.SelectTool(NeuronToolController.Tool.Attractor);
            minigame.EndTutorial();
            ui.HideTeaching();
            if (completed) ui.ShowToolTutorialComplete();
        }

        private void Exit()
        {
            if (pending) { pending = false; return; }
            if (!active) return;
            Finish(false);
        }
    }
}
