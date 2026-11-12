using System;
using System.Collections.Generic;
using Via.Shared;
using Via.Story.Configuration;

namespace Via.Story.Domain
{
    public enum FlowStage { Title, Dialogue, Choice, Menu, Presentation, Minigame, Ending }

    public readonly struct StoryOptionView
    {
        public string OptionId { get; }
        public string Text { get; }
        public StoryOptionView(string optionId, string text) { OptionId = optionId; Text = text; }
    }

    // Story progression only: which node runs next, when it advances, and what gets recorded.
    public sealed class GameFlow : IDisposable
    {
        private static readonly IReadOnlyList<StoryOptionView> NoOptions = Array.Empty<StoryOptionView>();

        private readonly StoryTables tables;
        private readonly IMinigameSession minigame;
        private readonly IPresentationPlayer presentation;
        private readonly HashSet<string> visitedMenu = new HashSet<string>(StringComparer.Ordinal);
        private string activeMenuId;
        private string activeSession;
        private string activePresentationId;
        private bool waitPresentation;
        private bool dialogueConfirmed;
        private bool pendingPresentationAdvance;

        public FlowStage Stage { get; private set; } = FlowStage.Title;
        public FlowRow CurrentNode { get; private set; }
        public DialogueRow CurrentDialogue { get; private set; }
        public IReadOnlyList<StoryOptionView> CurrentOptions { get; private set; } = NoOptions;
        public ChoiceHistory History { get; private set; } = new ChoiceHistory();
        // Last result per level, kept with the checkpoint: a save made after act 2 would otherwise
        // lose the act-2 outcome and the finale condition could not branch.
        private Dictionary<string, string> levelResults = new Dictionary<string, string>(StringComparer.Ordinal);
        public bool IsPaused { get; private set; }
        public event Action Changed;

        public GameFlow(StoryTables tables, IMinigameSession minigame, IPresentationPlayer presentation)
        {
            this.tables = tables ?? throw new ArgumentNullException(nameof(tables));
            this.minigame = minigame ?? throw new ArgumentNullException(nameof(minigame));
            this.presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            minigame.OnLevelCompleted += Completed;
            minigame.OnOpponentCompleted += OpponentCompleted;
            minigame.OnCandyUseConfirmed += CandyConfirmed;
            presentation.Completed += PresentationCompleted;
        }

        public void StartGame()
        {
            ReturnToTitle();
            presentation.Reset();
            History = new ChoiceHistory();
            EnterNode(tables.EntryNodeId);
        }

        // Snapshot of the last result per level, saved with the checkpoint so outcome-driven
        // branches still resolve when the saved history does not reach the level's own record.
        public LevelResultMemory[] CaptureLevelResults()
        {
            var list = new LevelResultMemory[levelResults.Count];
            int i = 0;
            foreach (var pair in levelResults) list[i++] = new LevelResultMemory(pair.Key, pair.Value);
            return list;
        }

        public void Restore(string nodeId, ChoiceRecord[] records)
        {
            Restore(nodeId, records, null);
        }

        public void Restore(string nodeId, ChoiceRecord[] records, LevelResultMemory[] levelMemory)
        {
            if (!tables.TryGetFlow(nodeId, out var restored)) throw new ArgumentException("检查点指向不存在的节点：" + nodeId);
            var history = ChoiceHistory.FromRecords(records);
            ReturnToTitle();
            presentation.Reset();
            History = history;
            levelResults.Clear();
            if (levelMemory != null)
                foreach (var memory in levelMemory)
                    if (memory != null && !string.IsNullOrEmpty(memory.levelId) && !string.IsNullOrEmpty(memory.resultId))
                        levelResults[memory.levelId] = memory.resultId;
            EnterNode(nodeId);
            RebuildStage(restored);
        }

        // A checkpoint only says which node to stand on; the picture must be rebuilt from the beats of
        // the current act. Walks them in original order and keeps each layer's final state.
        private void RebuildStage(FlowRow node)
        {
            // A presentation node is already playing (EnterNode started it); let it build the picture.
            if (node.Type == FlowNodeType.Presentation) return;
            if (!tables.TryGetFlowIndex(node.NodeId, out int index)) return;
            var beats = new List<string>();
            if (!string.IsNullOrEmpty(node.PresentationId)) beats.Add(node.PresentationId);

            int actStart = index;
            while (actStart > 0 && !IsActFirstNode(tables.FlowOrder[actStart - 1].NodeId, node.NodeId)) actStart--;
            for (int i = index; i >= actStart; i--)
            {
                var row = tables.FlowOrder[i];
                if (row.Type != FlowNodeType.Presentation && string.IsNullOrEmpty(row.PresentationId)) continue;
                string id = row.Type == FlowNodeType.Presentation ? row.ContentId : row.PresentationId;
                if (!string.IsNullOrEmpty(id) && tables.HasPresentation(id)) beats.Add(id);
            }

            beats.Reverse();
            var state = new Dictionary<string, PresentationRow>(StringComparer.Ordinal);
            foreach (string id in beats)
            {
                foreach (var row in tables.GetPresentation(id))
                {
                    if (row == null || string.IsNullOrEmpty(row.TargetId)) continue;
                    string action = (row.Action ?? string.Empty).Trim().ToLowerInvariant();
                    if (action != "fadein" && action != "show" && action != "fadeout" &&
                        action != "hide" && action != "setscale" && action != "scale") continue;
                    state[row.TargetId] = new PresentationRow
                    {
                        PresentationId = id,
                        Step = 1,
                        TargetId = row.TargetId,
                        Action = action,
                        Value = row.Value,
                        Duration = 0f,
                        Note = row.Note
                    };
                }
            }

            var visible = new List<PresentationRow>();
            foreach (var row in state.Values)
                if (row.Action == "fadein" || row.Action == "show" || row.Action == "setscale") visible.Add(row);
            presentation.ApplyScene(visible, Array.Empty<string>());
            waitPresentation = false;
        }

        // Act boundaries are the registered level entries and the two prologue/openers; a node belongs to
        // the same act as the first boundary at or before it.
        private static bool IsActFirstNode(string previousNodeId, string currentNodeId)
        {
            return ActIndex(currentNodeId) != ActIndex(previousNodeId);
        }

        private static int ActIndex(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || nodeId[0] != 'F' || !int.TryParse(nodeId.Substring(1), out int n)) return 0;
            if (n >= 1000) return 9;
            if (n >= 800) return 8;
            if (n >= 700) return 7;
            if (n >= 600) return 6;
            if (n >= 400) return 5;
            if (n >= 315) return 4;
            if (n >= 200) return 3;
            if (n >= 100) return 2;
            return 1;
        }

        // Player confirmed the current line; advance only after any simultaneous presentation finished.
        public void ConfirmDialogue()
        {
            if (IsPaused || Stage != FlowStage.Dialogue) return;
            dialogueConfirmed = true;
            AdvanceDialogueIfReady();
        }

        // Story fast-forward: compresses the beat that runs alongside the current line. The player
        // still confirms every node normally, so no branch or record can be lost on the way.
        public void FastForwardPresentation()
        {
            if (IsPaused || !waitPresentation) return;
            presentation.FastForward();
        }

        public void Choose(string optionId)
        {
            if (IsPaused) return;

            if (Stage == FlowStage.Menu)
            {
                LevelResultRow option = null;
                foreach (var candidate in tables.GetOptions(CurrentNode.NodeId))
                    if (candidate.ResultId == optionId) { option = candidate; break; }
                if (option == null) throw new ArgumentException("未知的菜单选项：" + optionId);
                visitedMenu.Add(option.ResultId);
                EnterNode(string.IsNullOrEmpty(option.NextNodeId) ? CurrentNode.NodeId : option.NextNodeId);
                return;
            }

            if (Stage != FlowStage.Choice) return;
            LevelResultRow choice = null;
            foreach (var candidate in tables.GetOptions(CurrentNode.NodeId))
                if (candidate.ResultId == optionId) { choice = candidate; break; }
            if (choice == null) throw new ArgumentException("未知的剧情选项：" + optionId);
            History.Append(new ChoiceRecord
            {
                kind = ChoiceKind.StoryOption,
                nodeId = CurrentNode.NodeId,
                optionId = choice.ResultId,
                resultId = choice.ResultId
            });
            EnterNode(choice.NextNodeId);
        }

        public void SetPaused(bool paused)
        {
            if (Stage == FlowStage.Title || IsPaused == paused) return;
            IsPaused = paused;
            if (Stage == FlowStage.Minigame)
            {
                if (paused) minigame.PauseLevel(); else minigame.ResumeLevel();
            }
            if (!paused && pendingPresentationAdvance)
            {
                pendingPresentationAdvance = false;
                EnterNode(CurrentNode.NextNodeId);
                return;
            }
            if (!paused && Stage == FlowStage.Dialogue && dialogueConfirmed && !waitPresentation)
            {
                AdvanceDialogueIfReady();
                return;
            }
            Changed?.Invoke();
        }

        public void ReturnToTitle()
        {
            activeSession = null;
            activePresentationId = null;
            activeMenuId = null;
            visitedMenu.Clear();
            waitPresentation = false;
            dialogueConfirmed = false;
            pendingPresentationAdvance = false;
            minigame.CloseLevel();
            CurrentNode = null;
            CurrentDialogue = null;
            CurrentOptions = NoOptions;
            Stage = FlowStage.Title;
            IsPaused = false;
            Changed?.Invoke();
        }

        public void Dispose()
        {
            minigame.OnLevelCompleted -= Completed;
            minigame.OnOpponentCompleted -= OpponentCompleted;
            minigame.OnCandyUseConfirmed -= CandyConfirmed;
            presentation.Completed -= PresentationCompleted;
            minigame.CloseLevel();
        }

        private void EnterNode(string nodeId)
        {
            if (!tables.TryGetFlow(nodeId, out var node))
                throw new InvalidOperationException("找不到流程节点：" + nodeId);

            // Visited menu options persist while their content plays; only a different menu resets them.
            if (node.Type == FlowNodeType.Menu && node.NodeId != activeMenuId)
            {
                activeMenuId = node.NodeId;
                visitedMenu.Clear();
            }

            activeSession = null;
            activePresentationId = null;
            waitPresentation = false;
            dialogueConfirmed = false;
            pendingPresentationAdvance = false;
            minigame.CloseLevel();
            CurrentNode = node;
            CurrentDialogue = null;
            CurrentOptions = NoOptions;
            IsPaused = false;

            switch (node.Type)
            {
                case FlowNodeType.Dialogue:
                    CurrentDialogue = tables.GetDialogue(node.ContentId);
                    Stage = FlowStage.Dialogue;
                    if (!string.IsNullOrEmpty(node.PresentationId)) StartPresentation(node.PresentationId);
                    break;
                case FlowNodeType.Choice:
                    CurrentOptions = BuildOptions(node.NodeId);
                    Stage = FlowStage.Choice;
                    break;
                case FlowNodeType.Menu:
                    var remaining = new List<StoryOptionView>();
                    foreach (var row in tables.GetOptions(node.NodeId))
                        if (!visitedMenu.Contains(row.ResultId))
                            remaining.Add(new StoryOptionView(row.ResultId, tables.GetDialogue(row.TextId).Text));
                    if (remaining.Count == 0) { EnterNode(node.NextNodeId); return; }
                    CurrentOptions = remaining;
                    Stage = FlowStage.Menu;
                    break;
                case FlowNodeType.Presentation:
                    Stage = FlowStage.Presentation;
                    StartPresentation(node.ContentId);
                    break;
                case FlowNodeType.Level:
                    Stage = FlowStage.Minigame;
                    activeSession = Guid.NewGuid().ToString("N");
                    minigame.StartLevel(new LevelStartRequest(node.ContentId, activeSession));
                    break;
                case FlowNodeType.End:
                    // The story ends on the final fade; return to the title instead of showing
                    // a separate recap screen.
                    ReturnToTitle();
                    return;
                case FlowNodeType.Condition:
                    EnterNode(node.ConditionType == FlowConditionType.CandyUseCount
                        ? ResolveCandyCountNext(node) : ResolveResultNext(node));
                    return;
            }
            Changed?.Invoke();
        }

        private IReadOnlyList<StoryOptionView> BuildOptions(string nodeId)
        {
            var rows = tables.GetOptions(nodeId);
            var views = new List<StoryOptionView>(rows.Count);
            foreach (var row in rows)
                views.Add(new StoryOptionView(row.ResultId, tables.GetDialogue(row.TextId).Text));
            return views;
        }

        private void StartPresentation(string presentationId)
        {
            activePresentationId = presentationId;
            waitPresentation = true;
            presentation.Play(presentationId, tables.GetPresentation(presentationId));
        }

        private void AdvanceDialogueIfReady()
        {
            if (IsPaused || Stage != FlowStage.Dialogue || !dialogueConfirmed || waitPresentation) return;
            EnterNode(ResolveResultNext(CurrentNode));
        }

        // Result-driven nodes end with a fixed next node or with result branches resolved
        // against recorded level results (the latest matching record wins).
        private string ResolveResultNext(FlowRow node)
        {
            if (!string.IsNullOrEmpty(node.NextNodeId)) return node.NextNodeId;
            var routes = tables.GetRoutes(node.NodeId);
            if (routes.Count > 0)
            {
                LevelResultRow match = null;
                foreach (var record in History.Records)
                {
                    if (record.kind != ChoiceKind.LevelResult) continue;
                    foreach (var route in routes)
                        if (string.Equals(route.ResultId, record.resultId, StringComparison.Ordinal) &&
                            (string.IsNullOrEmpty(route.SourceLevelId) ||
                             string.Equals(route.SourceLevelId, record.levelId, StringComparison.Ordinal))) match = route;
                }
                if (match != null) return match.NextNodeId;
                // The saved history may stop after the level, so consult the checkpoint's level snapshot.
                foreach (var route in routes)
                {
                    if (string.IsNullOrEmpty(route.SourceLevelId)) continue;
                    if (levelResults.TryGetValue(route.SourceLevelId, out string remembered) &&
                        string.Equals(remembered, route.ResultId, StringComparison.Ordinal)) return route.NextNodeId;
                }
                UnityEngine.Debug.LogWarning("缺少分支所需的历史结果，使用首个分支：" + node.NodeId);
                return routes[0].NextNodeId;
            }
            throw new InvalidOperationException("节点未配置下一节点：" + node.NodeId);
        }

        private string ResolveCandyCountNext(FlowRow node)
        {
            string resultId = History.CandyCount > node.Threshold ? "over" : "within";
            var route = tables.GetLevelResult(node.NodeId, resultId);
            if (route == null) throw new InvalidOperationException("传传糖次数分支未配置：" + node.NodeId + " / " + resultId);
            return route.NextNodeId;
        }

        private void PresentationCompleted(string presentationId)
        {
            if (!waitPresentation || presentationId != activePresentationId) return;
            waitPresentation = false;
            if (Stage == FlowStage.Presentation)
            {
                if (IsPaused) pendingPresentationAdvance = true;
                else EnterNode(CurrentNode.NextNodeId);
            }
            else if (Stage == FlowStage.Dialogue) AdvanceDialogueIfReady();
        }

        private void Completed(LevelResult result)
        {
            if (IsPaused || Stage != FlowStage.Minigame || result.SessionId != activeSession ||
                result.LevelId != CurrentNode.ContentId) return;
            var route = tables.GetLevelResult(CurrentNode.NodeId, result.ResultId);
            if (route == null) throw new InvalidOperationException("关卡结果未配置：" + CurrentNode.NodeId + " / " + result.ResultId);
            levelResults[result.LevelId] = result.ResultId;
            if (!History.Append(new ChoiceRecord
            {
                kind = ChoiceKind.LevelResult,
                nodeId = CurrentNode.NodeId,
                levelId = result.LevelId,
                sessionId = result.SessionId,
                resultId = result.ResultId,
                paradigmId = result.ParadigmId
            })) return;
            EnterNode(route.NextNodeId);
        }

        private void OpponentCompleted(LevelResult result)
        {
            if (IsPaused || Stage != FlowStage.Minigame || result.SessionId != activeSession ||
                result.LevelId != CurrentNode.ContentId) return;
            var route = tables.GetLevelResult(CurrentNode.NodeId, result.ResultId);
            if (route == null) throw new InvalidOperationException("对手结果未配置：" + CurrentNode.NodeId + " / " + result.ResultId);
            EnterNode(route.NextNodeId);
        }

        private void CandyConfirmed(CandyUseInfo info)
        {
            if (IsPaused || Stage != FlowStage.Minigame || info.SessionId != activeSession ||
                info.LevelId != CurrentNode.ContentId) return;
            if (History.Append(new ChoiceRecord
            {
                kind = ChoiceKind.CandyUse,
                nodeId = CurrentNode.NodeId,
                levelId = info.LevelId,
                sessionId = info.SessionId,
                useId = info.UseId,
                paradigmId = info.ParadigmId
            })) Changed?.Invoke();
        }
    }
}
