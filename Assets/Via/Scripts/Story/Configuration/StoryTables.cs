using System;
using System.Collections.Generic;

namespace Via.Story.Configuration
{
    // Immutable, indexed view of the CSV tables. Built and validated by StoryTableLoader.
    public sealed class StoryTables
    {
        private readonly Dictionary<string, FlowRow> flow;
        private readonly Dictionary<string, DialogueRow> dialogue;
        private readonly Dictionary<string, IReadOnlyList<PresentationRow>> presentations;
        private readonly Dictionary<string, LevelResultRow> levelResults;
        private readonly Dictionary<string, IReadOnlyList<LevelResultRow>> options;
        private readonly Dictionary<string, IReadOnlyList<LevelResultRow>> routes;
        private readonly Dictionary<string, AudioCueRow> audioCues;
        private readonly List<FlowRow> flowOrder;
        private readonly Dictionary<string, int> flowIndex;

        public string EntryNodeId { get; }
        public IEnumerable<FlowRow> FlowRows => flow.Values;
        public IReadOnlyList<FlowRow> FlowOrder => flowOrder;
        public IEnumerable<DialogueRow> DialogueRows => dialogue.Values;
        public IEnumerable<LevelResultRow> LevelResultRows => levelResults.Values;
        public IEnumerable<AudioCueRow> AudioCueRows => audioCues.Values;
        public IEnumerable<LevelResultRow> OptionRows
        {
            get
            {
                foreach (var list in options.Values)
                    foreach (var row in list) yield return row;
            }
        }

        public StoryTables(string entryNodeId,
            Dictionary<string, FlowRow> flow,
            Dictionary<string, DialogueRow> dialogue,
            Dictionary<string, IReadOnlyList<PresentationRow>> presentations,
            Dictionary<string, LevelResultRow> levelResults,
            Dictionary<string, IReadOnlyList<LevelResultRow>> options,
            Dictionary<string, IReadOnlyList<LevelResultRow>> routes,
            Dictionary<string, AudioCueRow> audioCues,
            List<FlowRow> flowOrder = null)
        {
            EntryNodeId = entryNodeId ?? string.Empty;
            this.flow = flow;
            this.dialogue = dialogue;
            this.presentations = presentations;
            this.levelResults = levelResults;
            this.options = options;
            this.routes = routes;
            this.audioCues = audioCues ?? new Dictionary<string, AudioCueRow>(StringComparer.Ordinal);
            this.flowOrder = flowOrder ?? new List<FlowRow>();
            flowIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < this.flowOrder.Count; i++) flowIndex[this.flowOrder[i].NodeId] = i;
        }

        public bool TryGetFlow(string nodeId, out FlowRow row) => flow.TryGetValue(nodeId ?? string.Empty, out row);

        // CSV order; audio state resolution walks it to find the latest cue at or before a node.
        public bool TryGetFlowIndex(string nodeId, out int index) =>
            flowIndex.TryGetValue(nodeId ?? string.Empty, out index);

        public DialogueRow GetDialogue(string textId)
        {
            if (dialogue.TryGetValue(textId ?? string.Empty, out var row)) return row;
            throw new InvalidOperationException("找不到台词：" + textId);
        }

        public bool HasDialogue(string textId) => dialogue.ContainsKey(textId ?? string.Empty);
        public bool HasPresentation(string id) => presentations.ContainsKey(id ?? string.Empty);

        public IReadOnlyList<PresentationRow> GetPresentation(string presentationId) =>
            presentations.TryGetValue(presentationId ?? string.Empty, out var rows) ? rows : Array.Empty<PresentationRow>();

        public LevelResultRow GetLevelResult(string entryNodeId, string resultId) =>
            levelResults.TryGetValue(LevelResultKey(entryNodeId, resultId), out var row) ? row : null;

        // Choice and menu options are both stored in the LevelResults table, keyed by their flow node.
        public IReadOnlyList<LevelResultRow> GetOptions(string entryNodeId) =>
            options.TryGetValue(entryNodeId ?? string.Empty, out var rows) ? rows : Array.Empty<LevelResultRow>();

        public bool HasOptions(string entryNodeId) =>
            options.TryGetValue(entryNodeId ?? string.Empty, out var rows) && rows.Count > 0;

        // Dialogue result branches: level-result rows keyed by a dialogue node, resolved against history.
        public IReadOnlyList<LevelResultRow> GetRoutes(string entryNodeId) =>
            routes.TryGetValue(entryNodeId ?? string.Empty, out var rows) ? rows : Array.Empty<LevelResultRow>();

        public bool HasRoutes(string entryNodeId) =>
            routes.TryGetValue(entryNodeId ?? string.Empty, out var rows) && rows.Count > 0;

        // Story audio cues are keyed by flow node; the director resolves the running state by node order.
        public bool TryGetAudioCue(string nodeId, out AudioCueRow row) =>
            audioCues.TryGetValue(nodeId ?? string.Empty, out row);

        public static string LevelResultKey(string entryNodeId, string resultId) =>
            (entryNodeId ?? string.Empty) + "|" + (resultId ?? string.Empty);
    }
}
