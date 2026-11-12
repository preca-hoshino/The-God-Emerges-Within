using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Via.Application;
using Via.Story.Domain;
using Via.Minigame.Configuration;
using Via.Minigame.Domain;
using Via.Minigame.Runtime;
using Via.Minigame.Presentation;

namespace Via.UI
{
    public sealed class GameUIController : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private LoadingIntroController loadingIntro;
        [SerializeField] private Texture2D dialogueFrameTexture;
        [SerializeField] private Texture2D neuronOriginIcon;
        [SerializeField] private Texture2D neuronPointIcon;
        [SerializeField] private Texture2D neuronLineIcon;
        [SerializeField] private Texture2D neuronTipIcon;
        [SerializeField] private GameController game;
        [SerializeField] private MinigameController minigame;
        [SerializeField] private NeuronToolController tools;
        [SerializeField] private CanvasPointerInput pointer;
        [SerializeField] private AudioSettingsController audioSettings;
        [SerializeField] private AudioController audioController;
        [SerializeField] private AudioLibrary audioLibrary;
        [SerializeField, TextArea] private string credits = "";
        // Tap fast-forwards one line; holding repeats. Every line is still confirmed normally, so
        // choices, records and level entries all behave exactly as in a normal read-through.
        [SerializeField, Min(0.02f)] private float skipLineInterval = 0.05f;
        [SerializeField, Min(0f)] private float skipHoldDelay = 0.2f;
        private VisualElement root;
        private DialoguePanelView dialogueView;
        private VisualElement dialoguePanel;
        private NeuronCanvasElement canvas;
        private string modal;
        private string previousModal;
        private bool resumeAfterModal;
        private Action confirmation;
        private IVisualElementScheduledItem refresh;
        private IVisualElementScheduledItem skipTicker;
        private Button skipButton;
        private VisualElement tooltipAnchor;
        private bool skipHeld;
        private bool skipConsumed;
        private float skipHeldSince;
        private string hint = "点击画布放置吸引点，或选择牵引调整已有路径。";
        private bool started;
        private string shownLevelNode;
        
        public event Action<string> SampleRequested;
        public Func<string, bool> SampleAvailable { get; set; }
        public event Action TeachingExitRequested;
        public event Action TeachingReturnRequested;
        // Lets the tutorial wait for the level intro popup: that popup pauses the level, which
        // disables the whole tool bar, so a tutorial started underneath it cannot be played.
        public bool HasModal => modal != null;

        private T Q<T>(string name) where T : VisualElement => root.Q<T>(name);
        private void Show(string name, bool visible) => Q<VisualElement>(name).style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        private void Text(string name, string text) => Q<Label>(name).text = text ?? "";
        private void Click(string name, Action action) => Q<Button>(name).clicked += action;

        private void Start()
        {
            if (game != null) game.EnsureInitialized();
            if (document == null || loadingIntro == null || dialogueFrameTexture == null || game == null || game.Flow == null || minigame == null || tools == null || pointer == null || audioSettings == null || audioLibrary == null)
            {
                Debug.LogError("Game UI 依赖未绑定或剧情表加载失败：" + (game == null ? "missing GameController" : game.LoadError ?? "unknown"), this);
                enabled = false;
                return;
            }
            root = document.rootVisualElement;
            dialoguePanel = Q<VisualElement>("dialogue-panel");
            dialogueView = new DialoguePanelView(dialoguePanel, dialogueFrameTexture);
            dialoguePanel.RegisterCallback<ClickEvent>(OnDialogueClick, TrickleDown.TrickleDown);
            root.RegisterCallback<ClickEvent>(OnUiClick);
            Q<VisualElement>("story-screen").RegisterCallback<ClickEvent>(OnStoryClick);
            canvas = new NeuronCanvasElement(minigame, tools, pointer, neuronOriginIcon, neuronPointIcon, neuronLineIcon, neuronTipIcon);
            Q<VisualElement>("neuron-canvas").Add(canvas);
            var tooltip = Q<VisualElement>("tooltip");
            tooltip.pickingMode = PickingMode.Ignore;
            tooltip.RegisterCallback<GeometryChangedEvent>(OnTooltipGeometryChanged);
            Q<Label>("system-status").pickingMode = PickingMode.Ignore;
            BuildCredits(Q<VisualElement>("credits-list"));
            var creditsPanel = Q<VisualElement>("credits-panel");
            creditsPanel.style.flexDirection = FlexDirection.Column;
            creditsPanel.style.alignItems = Align.Stretch;
            var creditsList = Q<VisualElement>("credits-list");
            creditsList.style.flexDirection = FlexDirection.Column;
            creditsList.style.alignItems = Align.Stretch;
            Click("new-game", () => { CloseModal(); game.StartGame(); });
            Click("continue-game", () => { CloseModal(); game.ContinueGame(); Refresh(); });
            Click("title-settings", () => OpenSettings());
            Click("credits-open", () => OpenModal("credits-panel"));
            Click("credits-close", CloseModal);
            Click("title-quit", AskQuit);
            Click("advance", AdvanceDialogue);
            Click("pause-open", () => OpenModal("pause-panel"));
            skipButton = Q<Button>("skip-story");
            skipButton.clicked += OnSkipClicked;
            skipButton.RegisterCallback<PointerDownEvent>(OnSkipPress, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(OnSkipReleaseUp, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerCancelEvent>(OnSkipReleaseCancel, TrickleDown.TrickleDown);
            skipTicker = root.schedule.Execute(SkipTick).Every(Mathf.Max(20, Mathf.RoundToInt(skipLineInterval * 1000f)));
            skipTicker.Pause();
            Click("pause-resume", CloseModal);
            Click("pause-save", () => { game.SaveCheckpoint(); Text("system-status", game.StatusMessage); });
            Click("pause-title", AskTitle);
            Click("pause-settings", () => OpenSettings());
            Click("pause-quit", AskQuit);
            Click("settings-close", () => { audioSettings.Flush(); if (previousModal == "pause-panel") OpenModal("pause-panel"); else CloseModal(); });
            Click("tool-attractor", () => SelectTool(NeuronToolController.Tool.Attractor));
            Click("tool-pull", () => SelectTool(NeuronToolController.Tool.Pull));
            Click("tool-cut", () => SelectTool(NeuronToolController.Tool.Cut));
            Click("tool-branch", () => SelectTool(NeuronToolController.Tool.Branch));
            Click("tool-reshape", () => SelectTool(NeuronToolController.Tool.Reshape));
            Click("undo", () => { tools.CancelDrag(); if (minigame.UndoOperation()) PlayCue("undo_reset"); hint = "已恢复上一步操作前的现场。"; });
            Click("restart-level", () => Confirm("重新开始本关？", "当前路径和撤销历史将清除。已确认的糖果记录保留。", () => { minigame.ResetLevel(); hint = "已恢复关卡初始结构。"; }));
            Click("submit", CheckResult);
            Click("result-back", CloseModal);
            Click("result-confirm", () => { CloseModal(); minigame.ConfirmResult(); });
            Click("candy-open", OpenCandy);
            Click("candy-back", CloseModal);
            Click("confirm-yes", () => { var action = confirmation; confirmation = null; CloseModal(); action?.Invoke(); Refresh(); });
            Click("confirm-no", () => { confirmation = null; if (previousModal == "pause-panel") OpenModal("pause-panel"); else CloseModal(); });
            Click("notice-close", CloseModal);
            Click("teaching-exit", () => Confirm("退出教学？", minigame.IsTutorial ?
                "退出后恢复本关初始路径，开始正式解题。" : "退出后应恢复原解题现场，已确认服用记录保留。",
                () => TeachingExitRequested?.Invoke()));
            Click("teaching-return", () => { CloseModal(); TeachingReturnRequested?.Invoke(); });
            Volume("master-volume", audioSettings.Master); Volume("music-volume", audioSettings.Music); Volume("effects-volume", audioSettings.Effects);
            Tooltip("tool-attractor", "吸引点\n1.点击左侧工具栏中光亮的【吸引点】工具。\n2.【吸引点】会形成一个新端点，直线会自动寻找路径后，延伸并链接上端点。");
            Tooltip("tool-pull", "牵引\n1.用鼠标拖拽紫色光点，可改变该段直线的长度和方向。\n\n1.若想将两条分开的直线重新汇合为一条直线。\n2.点击左侧工具栏中光亮的【牵引】工具。\n3.当两个紫色光点靠近时，【牵引】工具将自动将其整合为一条直线。");
            Tooltip("tool-cut", "切断\n1.点击左侧工具栏中光亮的【切断】。\n2.将鼠标移动至橙色光点与绿色光点间的直线上，长按鼠标拖拽出一根红色直线。\n3.当红色直线穿过原有绿色直线时，会将该线在交接处被切割成两条新直线。\n4.两条新直线的端点为紫色光点，可单独拖拽紫色光点的位置。");
            Tooltip("tool-branch", "分叉\n放置范围圈，生长端进入后沿 ±45° 分成两支，原吸引点失效。");
            Tooltip("tool-reshape", "重塑\n点击已有折角，将折线平滑为曲线，保留两侧连接位置。");
            Tooltip("candy-open", "选择一个范式，跟随提示学习样本。确认服用才写入记录，样本不会直接通关。");
            Tooltip("submit", "闭合\n1.若想完成判定，则需在最后将直线连接回橙色起点完成闭环。\n\n提交\n1.形成闭环后可进一步检查直线所连图形是否符合所想图案。\n2.依据右侧的图案判定，会影响“你”的感受，进而影响游戏剧情走向。\n3.因此，谨慎对待所连图案，可选中底部的【重新开始】/【撤销】进行修改。\n4.在确认无误后，即可点击底部的【提交图案】，剧情会根据最终提交的结果而有所不同。");
            Show("teaching-hud", false); Show("teaching-marker", false); Show("tooltip", false);
            Show("modal-layer", false);
            loadingIntro.MenuRevealed += Refresh;
            loadingIntro.Completed += Refresh;
            game.Flow.Changed += Refresh;
            started = true;
            refresh = root.schedule.Execute(TickUI).Every(50);
            Refresh();
        }

        private void Volume(string name, float value)
        {
            var slider = Q<Slider>(name); slider.SetValueWithoutNotify(value);
            slider.RegisterValueChangedCallback(e => audioSettings.SetVolumes(Q<Slider>("master-volume").value,
                Q<Slider>("music-volume").value, Q<Slider>("effects-volume").value));
        }

        private void OnUiClick(ClickEvent evt)
        {
            if (!(evt.target is Button button)) return;
            // Buttons with a dedicated cue keep only that cue.
            if (button.name == "advance" || button.name == "choice-option" || button.name == "undo") return;
            PlayCue("ui_click");
        }

        private void PlayCue(string id)
        {
            if (audioController == null || audioLibrary == null) return;
            var clip = audioLibrary.Get(id);
            if (clip != null) audioController.PlaySfx(clip);
            else Debug.LogWarning("音频库缺少音效：" + id, this);
        }

        // Act 9 plays as a film: subtitles sit centered on screen and any click advances.
        private static bool IsFinaleFilm(string nodeId)
        {
            return !string.IsNullOrEmpty(nodeId) && nodeId[0] == 'F'
                && int.TryParse(nodeId.Substring(1), out var index) && index >= 1000;
        }

        private void AdvanceDialogue()
        {
            if (modal != null || game.Flow.IsPaused || game.Flow.Stage != FlowStage.Dialogue) return;
            if (!dialogueView.TryShowNextPage()) game.Flow.ConfirmDialogue();
            PlayCue("dialogue_tick");
        }

        // Story fast-forward: a tap advances one line, holding repeats. Every node is confirmed
        // through the normal flow path, so branches, records and level entries are never skipped.
        private bool CanSkipStory
        {
            get
            {
                var flow = game.Flow;
                return flow != null && modal == null && !flow.IsPaused &&
                    (flow.Stage == FlowStage.Dialogue || flow.Stage == FlowStage.Presentation);
            }
        }

        // Pointer tracking only drives the hold timer; the single step comes from the button's own
        // click, so this keeps the same activation path as every other button.
        private void OnSkipPress(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            skipHeld = true;
            skipConsumed = false;
            skipHeldSince = Time.unscaledTime;
            skipTicker?.Resume();
        }

        private void OnSkipReleaseUp(PointerUpEvent evt) => StopSkipping();
        private void OnSkipReleaseCancel(PointerCancelEvent evt) => StopSkipping();

        private void OnSkipClicked()
        {
            // A completed hold already advanced the story; drop the click that closes it.
            if (skipConsumed) { skipConsumed = false; return; }
            SkipOnce();
        }

        private void SkipTick()
        {
            if (!skipHeld) { skipTicker?.Pause(); return; }
            if (!CanSkipStory) { StopSkipping(); return; }
            // Only start repeating once the press has been held past the tap window.
            if (Time.unscaledTime - skipHeldSince < skipHoldDelay) return;
            skipConsumed = true;
            SkipOnce();
        }

        private void SkipOnce()
        {
            if (!CanSkipStory) { StopSkipping(); return; }
            var flow = game.Flow;
            // Compress the beat running alongside this line; otherwise the confirm waits it out.
            flow.FastForwardPresentation();
            if (flow.Stage != FlowStage.Dialogue) return;
            // Reveal the rest of a long line first; only the last page confirms the node.
            while (dialogueView.TryShowNextPage()) { }
            flow.ConfirmDialogue();
        }

        private void StopSkipping()
        {
            skipHeld = false;
            skipTicker?.Pause();
        }

        private void OnDialogueClick(ClickEvent evt)
        {
            if (evt.button != 0 || modal != null || game.Flow.Stage != FlowStage.Dialogue) return;
            // Buttons keep their own activation, including the arrow's keyboard support.
            // Ignore scrollbar controls so scrolling cannot also confirm a line.
            for (var element = evt.target as VisualElement; element != null && element != dialoguePanel; element = element.parent)
                if (element is Button || element is Scroller) return;
            AdvanceDialogue();
            // The next line can enter the finale; do not let this same click advance its film handler too.
            evt.StopPropagation();
        }

        private void OnStoryClick(ClickEvent evt)
        {
            if (modal != null || evt.target is Button) return;
            var flow = game.Flow;
            if (flow.Stage != FlowStage.Dialogue || !IsFinaleFilm(flow.CurrentNode?.NodeId)) return;
            flow.ConfirmDialogue();
            PlayCue("dialogue_tick");
        }

        private void SelectTool(NeuronToolController.Tool tool) { tools.SelectTool(tool); TickUI(); }

        private void Refresh()
        {
            var flow = game.Flow;
            Show("title-screen", flow.Stage == FlowStage.Title && loadingIntro.IsMenuVisible);
            Show("story-screen", flow.Stage == FlowStage.Dialogue || flow.Stage == FlowStage.Choice || flow.Stage == FlowStage.Menu);
            bool film = flow.Stage != FlowStage.Title && IsFinaleFilm(flow.CurrentNode?.NodeId);
            Show("dialogue-panel", (flow.Stage == FlowStage.Dialogue || flow.Stage == FlowStage.Choice || flow.Stage == FlowStage.Menu) && !film);
            bool subtitled = film && flow.Stage == FlowStage.Dialogue && flow.CurrentDialogue != null;
            Show("story-subtitle", subtitled);
            if (subtitled)
            {
                Text("story-subtitle", flow.CurrentDialogue.Text);
                // The act-9 line "它即【涌现】" is the story's thesis; it lands larger and bolder.
                Q<Label>("story-subtitle").EnableInClassList("hero-line", flow.CurrentNode?.ContentId == "T1029");
            }
            else Q<Label>("story-subtitle").EnableInClassList("hero-line", false);
            Show("level-screen", flow.Stage == FlowStage.Minigame);
            Show("pause-open", flow.Stage != FlowStage.Title && modal == null);
            // Skip shares the pause button's lifetime during story, and leaves the gameplay screen
            // alone because there is no dialogue to skip there.
            Show("skip-story", flow.Stage != FlowStage.Title && flow.Stage != FlowStage.Minigame && modal == null);
            bool hasCheckpoint = game.HasCheckpoint;
            var continueButton = Q<Button>("continue-game");
            continueButton.SetEnabled(hasCheckpoint);
            continueButton.EnableInClassList("unavailable", !hasCheckpoint);
            Q<VisualElement>("choice-list").Clear();
            if (flow.CurrentDialogue != null)
            {
                dialogueView.SetText(flow.CurrentNode.NodeId, flow.CurrentDialogue.Speaker, flow.CurrentDialogue.Text);
            }
            Show("advance", flow.Stage == FlowStage.Dialogue);
            if (flow.Stage == FlowStage.Choice || flow.Stage == FlowStage.Menu)
                foreach (var option in flow.CurrentOptions)
                {
                    var captured = option.OptionId;
                    var button = new Button(() => { PlayCue("ui_confirm"); flow.Choose(captured); }) { text = option.Text, name = "choice-option" };
                    button.AddToClassList("story-option-button");
                    button.AddToClassList(flow.Stage == FlowStage.Choice ? "story-choice-option" : "story-menu-option");
                    Q<VisualElement>("choice-list").Add(button);
                }
            if (flow.Stage == FlowStage.Minigame)
            {
                Text("level-name", minigame.Configuration.LevelId.ToUpperInvariant());
                Q<Label>("level-name").tooltip = minigame.Configuration.DisplayName;
                BuildParadigms();
                if (shownLevelNode != flow.CurrentNode.NodeId)
                {
                    shownLevelNode = flow.CurrentNode.NodeId;
                    hint = LevelLesson.Hint(minigame.Configuration.LevelId) ?? "点击画布放置吸引点，或选择牵引调整已有路径。";
                    string feelingPrompt = LevelFeelingCopy.Before(minigame.Configuration.LevelId);
                    string introduction = minigame.Configuration.CandyIntroduction;
                    if (!string.IsNullOrWhiteSpace(introduction) && !string.IsNullOrWhiteSpace(feelingPrompt))
                        Notice(minigame.Configuration.DisplayName, introduction + "\n\n" + feelingPrompt);
                    else if (!string.IsNullOrWhiteSpace(introduction))
                        Notice(minigame.Configuration.DisplayName, introduction);
                    else if (!string.IsNullOrWhiteSpace(feelingPrompt))
                        Notice(minigame.Configuration.DisplayName, feelingPrompt);
                }
            }
            else { shownLevelNode = null; HideTeaching(); }
            TickUI();
        }

        private void BuildParadigms()
        {
            var list = Q<ScrollView>("paradigm-list"); list.Clear();
            list.EnableInClassList("three-targets", minigame.Configuration.Outcomes.Count > 2);
            foreach (var outcome in minigame.Configuration.Outcomes)
            {
                var paradigm = ParadigmCatalog.Find(outcome.paradigmId);
                var card = new Button { name = "target-" + outcome.paradigmId }; card.AddToClassList("paradigm-card");
                card.EnableInClassList("opponent-target", outcome.opponentGoal);
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("gameplay-paradigm-icon");
                if (outcome.paradigmId == "y" || outcome.paradigmId == "eight"
                    || outcome.paradigmId == "polyline" || outcome.paradigmId == "polygon")
                    icon.AddToClassList("paradigm-" + outcome.paradigmId);
                else
                {
                    icon.AddToClassList("paradigm-generated");
                    var diagram = new ParadigmDiagramElement(outcome.paradigmId, true);
                    diagram.style.width = Length.Percent(80); diagram.style.height = Length.Percent(70);
                    icon.Add(diagram);
                }
                card.Add(icon);
                var caption = new Label((outcome.opponentGoal ? "医生目标 · " : "") + outcome.displayName) { pickingMode = PickingMode.Ignore };
                caption.AddToClassList("gameplay-paradigm-caption"); card.Add(caption);
                string judgment = paradigm == null ? "按本关结果配置识别。" : paradigm.Judgment;
                if (outcome.paradigmId == "polygon" && minigame.Configuration.AllowCurvedPolygon)
                    judgment = "形成一个有面积的闭合区域；允许直线边或曲线；无分叉、无交叉。";
                if (paradigm != null && paradigm.UsesTurnThreshold)
                    judgment += "\n本关明显转折阈值：" + minigame.Configuration.CornerAngle.ToString("0.#") + "°。";
                string copy = outcome.displayName + "\n判定：" + judgment;
                if (!string.IsNullOrWhiteSpace(outcome.description)) copy += "\n" + outcome.description;
                if (outcome.opponentGoal) copy += "\n医生达成后进入对应剧情分支，不记录为你的正式选择。";
                // Cards explain themselves in the click-through popup only; the hover bubble was
                // dropped because it duplicated the same copy and covered the board.
                card.clicked += () => Notice(outcome.displayName, copy);
                list.Add(card);
            }
        }

        private void CheckResult()
        {
            tools.CancelDrag();
            var result = minigame.CheckResult();
            if (result == null) PlayCue("submit_fail");
            bool opponentGoal = result != null && result.opponentGoal;
            Text("result-title", result == null ? "尚未达成" : (opponentGoal ? "医生已达成 · " : "已达成 · ") + result.displayName);
            string feelingLine = result != null && !opponentGoal
                ? LevelFeelingCopy.After(minigame.Configuration.LevelId, result.displayName)
                : null;
            Text("result-text", result == null ? "当前图案未符合本关目标，或仍在生长。返回画布继续修改。" :
                opponentGoal ? "连接已趋于平静，本轮抵抗失败。继续后进入对应剧情，不记录为你的正式选择。" :
                feelingLine ?? "你可以继续修改，也可以确认这次回应。确认后将写入正式选择并继续剧情。");
            Q<Button>("result-confirm").text = opponentGoal ? "继续" : "确认结果";
            Show("result-confirm", result != null); OpenModal("result-panel");
        }

        private void OpenCandy()
        {
            var list = Q<ScrollView>("sample-list"); list.Clear();
            foreach (var outcome in minigame.Configuration.Outcomes)
            {
                string id = outcome.paradigmId;
                bool available = SampleRequested != null && SampleAvailable != null && SampleAvailable(id);
                var button = new Button(() => Confirm("服用糖果并学习样本？",
                    "确认后记录一次服用，并暂存当前路径与撤销历史。教学完成或退出后恢复现场，再由你提交正式回应。",
                    () => SampleRequested?.Invoke(id)))
                    { text = outcome.displayName + (available ? " · 学习样本" : "") };
                button.SetEnabled(available); list.Add(button);
            }
            OpenModal("candy-panel");
        }

        public void ShowTeaching(int current, int total, string instruction, string sample, string feedback, Vector2? marker)
        {
            Text("teaching-progress", current + "/" + total); Text("teaching-instruction", instruction);
            Text("teaching-sample", sample); Text("teaching-feedback", feedback); Show("teaching-hud", true);
            Show("teaching-marker", marker.HasValue);
            if (marker.HasValue) { Q<VisualElement>("teaching-marker").style.left = marker.Value.x; Q<VisualElement>("teaching-marker").style.top = marker.Value.y; }
        }
        public void HideTeaching() { Show("teaching-hud", false); Show("teaching-marker", false); }
        public void ShowTeachingComplete() => OpenModal("teaching-complete-panel");
        public void ShowToolTutorialComplete() => Notice("工具教程完成",
            "已恢复本关的正式初始路径。请完成右侧目标并提交确认；教程中的图形不计作剧情选择。");

        private void OpenSettings() { previousModal = modal; OpenModal("settings-panel"); }
        private void AskTitle() => Confirm("返回标题？", "未保存的进度将丢失。可以先在暂停面板保存检查点。", () => game.Flow.ReturnToTitle());
        private void AskQuit() => Confirm("退出游戏？", "未保存的进度将丢失。", () =>
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        });
        private void Confirm(string title, string text, Action action)
        { previousModal = modal; confirmation = action; Text("confirm-title", title); Text("confirm-text", text); OpenModal("confirm-panel"); }
        private void Notice(string title, string text)
        { PlayCue("system_prompt"); Text("notice-title", title); Text("notice-text", text); OpenModal("notice-panel"); }

        // Credits are authored as "角色-成员" lines; splitting them keeps the roles in a fixed
        // column so the name lists line up no matter how long they are.
        private void BuildCredits(VisualElement list)
        {
            list.Clear();
            var roles = new List<string>();
            var names = new List<string>();
            foreach (string raw in (credits ?? string.Empty).Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                int dash = line.IndexOf('-');
                roles.Add(dash > 0 ? line.Substring(0, dash).Trim() : string.Empty);
                names.Add(dash > 0 ? line.Substring(dash + 1).Trim() : line);
            }
            int widest = 0;
            foreach (string role in roles) widest = Math.Max(widest, role.Length);

            int roleColumn = Math.Max(88, widest * 24 + 16);
            for (int i = 0; i < roles.Count; i++)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.minHeight = 34;
                row.style.marginBottom = 10;
                row.style.flexGrow = 0;
                row.style.flexShrink = 0;

                var roleLabel = new Label(roles[i]);
                roleLabel.style.width = roleColumn;
                roleLabel.style.minWidth = roleColumn;
                roleLabel.style.flexGrow = 0;
                roleLabel.style.flexShrink = 0;
                roleLabel.style.marginLeft = 0;
                roleLabel.style.marginRight = 0;
                roleLabel.style.fontSize = 22;
                roleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                roleLabel.style.color = Color.black;
                roleLabel.style.whiteSpace = WhiteSpace.NoWrap;
                roleLabel.pickingMode = PickingMode.Ignore;

                var namesLabel = new Label(names[i]);
                namesLabel.style.flexGrow = 1;
                namesLabel.style.flexShrink = 1;
                namesLabel.style.maxWidth = 400;
                namesLabel.style.fontSize = 22;
                namesLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                namesLabel.style.whiteSpace = WhiteSpace.Normal;
                namesLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                namesLabel.style.marginLeft = 0;
                namesLabel.pickingMode = PickingMode.Ignore;

                row.Add(roleLabel);
                row.Add(namesLabel);
                list.Add(row);
            }
        }

        private void OpenModal(string name)
        {
            if (modal == null)
            {
                resumeAfterModal = game.Flow.Stage != FlowStage.Title && !game.Flow.IsPaused;
                tools.CancelDrag(); canvas.CancelInput();
            }
            modal = name;
            Q<VisualElement>("modal-layer").EnableInClassList("pause-overlay", name == "pause-panel");
            if (resumeAfterModal) game.Flow.SetPaused(true);
            foreach (var child in Q<VisualElement>("modal-layer").Children()) child.style.display = child.name == name ? DisplayStyle.Flex : DisplayStyle.None;
            Show("modal-layer", true); Show("tooltip", false); TickUI();
        }
        private void CloseModal()
        {
            modal = null; Show("modal-layer", false);
            Q<VisualElement>("modal-layer").RemoveFromClassList("pause-overlay");
            if (resumeAfterModal) { resumeAfterModal = false; game.Flow.SetPaused(false); }
            TickUI();
        }

        private void TickUI()
        {
            if (root == null || canvas == null) return;
            bool edit = game.Flow.Stage == FlowStage.Minigame && minigame.CanEdit && modal == null;
            pointer.IsBlocked = !edit; canvas.InputEnabled = edit;
            canvas.MarkDirtyRepaint();
            bool solving = edit && !minigame.IsGuided;
            Q<Button>("undo").SetEnabled(solving && minigame.CanUndo);
            Q<Button>("submit").SetEnabled(solving); Q<Button>("restart-level").SetEnabled(solving);
            Q<Button>("candy-open").SetEnabled(edit && !minigame.IsGuided &&
                (minigame.Configuration.Tools & MinigameTool.Candy) != 0);
            Q<Button>("tool-attractor").EnableInClassList("selected", tools.SelectedTool == NeuronToolController.Tool.Attractor);
            Q<Button>("tool-pull").EnableInClassList("selected", tools.SelectedTool == NeuronToolController.Tool.Pull);
            Q<Button>("tool-cut").EnableInClassList("selected", tools.SelectedTool == NeuronToolController.Tool.Cut);
            Q<Button>("tool-branch").EnableInClassList("selected", tools.SelectedTool == NeuronToolController.Tool.Branch);
            Q<Button>("tool-reshape").EnableInClassList("selected", tools.SelectedTool == NeuronToolController.Tool.Reshape);
            var allowed = minigame.Configuration.Tools;
            Q<Button>("tool-attractor").SetEnabled(edit && (allowed & MinigameTool.Attractor) != 0);
            Q<Button>("tool-pull").SetEnabled(edit && (allowed & MinigameTool.Pull) != 0);
            Q<Button>("tool-cut").SetEnabled(edit && (allowed & MinigameTool.Cut) != 0);
            Q<Button>("tool-branch").SetEnabled(edit && (allowed & MinigameTool.Branch) != 0);
            Q<Button>("tool-reshape").SetEnabled(edit && (allowed & MinigameTool.Reshape) != 0);
            Text("operation-hint", tools.Feedback ?? hint); Text("system-status", game.StatusMessage);
            Show("pause-open", game.Flow.Stage != FlowStage.Title && modal == null);
            Show("skip-story", game.Flow.Stage != FlowStage.Title && game.Flow.Stage != FlowStage.Minigame && modal == null);
            if (skipHeld && !CanSkipStory) StopSkipping();
            Show("system-status", game.Flow.Stage != FlowStage.Title);
        }
        private void Tooltip(string name, string text) => AttachTooltip(Q<VisualElement>(name), text);
        private void AttachTooltip(VisualElement element, string text)
        {
            element.RegisterCallback<PointerEnterEvent>(e =>
            {
                if (modal != null) return;
                Text("tooltip-text", text);
                tooltipAnchor = element;
                Show("tooltip", true);
                PositionTooltip();
            });
            element.RegisterCallback<PointerLeaveEvent>(e =>
            {
                if (tooltipAnchor != element) return;
                tooltipAnchor = null;
                Show("tooltip", false);
            });
        }

        private void OnTooltipGeometryChanged(GeometryChangedEvent evt)
        {
            if (tooltipAnchor != null) PositionTooltip();
        }

        private void PositionTooltip()
        {
            var tip = Q<VisualElement>("tooltip");
            var parent = tip.parent;
            var bounds = tooltipAnchor.worldBound;
            var topRight = parent.WorldToLocal(new Vector2(bounds.xMax, bounds.yMin));
            float left = Mathf.Clamp(topRight.x + 14f, 10f,
                Mathf.Max(10f, parent.resolvedStyle.width - tip.resolvedStyle.width - 10f));
            float top = Mathf.Clamp(topRight.y, 10f,
                Mathf.Max(10f, parent.resolvedStyle.height - tip.resolvedStyle.height - 10f));
            tip.style.left = left;
            tip.style.top = top;
        }
        private void OnDestroy()
        {
            refresh?.Pause();
            skipTicker?.Pause();
            if (skipButton != null)
            {
                skipButton.clicked -= OnSkipClicked;
                skipButton.UnregisterCallback<PointerDownEvent>(OnSkipPress, TrickleDown.TrickleDown);
            }
            dialoguePanel?.UnregisterCallback<ClickEvent>(OnDialogueClick, TrickleDown.TrickleDown);
            if (root != null)
            {
                root.Q<VisualElement>("tooltip")?.UnregisterCallback<GeometryChangedEvent>(OnTooltipGeometryChanged);
                root.UnregisterCallback<ClickEvent>(OnUiClick);
                root.UnregisterCallback<PointerUpEvent>(OnSkipReleaseUp, TrickleDown.TrickleDown);
                root.UnregisterCallback<PointerCancelEvent>(OnSkipReleaseCancel, TrickleDown.TrickleDown);
                root.Q<VisualElement>("story-screen")?.UnregisterCallback<ClickEvent>(OnStoryClick);
            }
            dialogueView?.Dispose();
            if (loadingIntro != null)
            {
                loadingIntro.MenuRevealed -= Refresh;
                loadingIntro.Completed -= Refresh;
            }
            if (started && game != null && game.Flow != null) game.Flow.Changed -= Refresh;
        }
    }
}

