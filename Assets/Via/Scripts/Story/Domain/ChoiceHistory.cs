using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Via.Story.Domain
{
    public enum ChoiceKind { StoryOption, LevelResult, CandyUse }

    [Serializable]
    public sealed class LevelResultMemory
    {
        public string levelId;
        public string resultId;
        public LevelResultMemory() { }
        public LevelResultMemory(string levelId, string resultId) { this.levelId = levelId; this.resultId = resultId; }
    }

    [Serializable]
    public sealed class ChoiceRecord
    {
        public int sequence;
        public ChoiceKind kind;
        public string nodeId;
        public string optionId;
        public string levelId;
        public string sessionId;
        public string resultId;
        public string paradigmId;
        public string useId;
    }

    public sealed class ChoiceHistory
    {
        private readonly List<ChoiceRecord> records = new List<ChoiceRecord>();
        private readonly HashSet<string> confirmedSessions = new HashSet<string>();
        private readonly HashSet<string> candyUses = new HashSet<string>();
        public ReadOnlyCollection<ChoiceRecord> Records { get; }
        public int CandyCount { get; private set; }
        public ChoiceHistory() { Records = records.AsReadOnly(); }
        public bool HasResult(string id) => records.Exists(record => record.resultId == id);

        public bool Append(ChoiceRecord record)
        {
            if (!AppendCore(record)) return false;
            record.sequence = records.Count;
            return true;
        }
        // Rebuilds the history from a checkpoint: the duplicate guards must be repopulated, otherwise a
        // restored level result or candy use would be accepted a second time.
        public static ChoiceHistory FromRecords(IEnumerable<ChoiceRecord> saved)
        {
            var history = new ChoiceHistory();
            foreach (var source in saved ?? Array.Empty<ChoiceRecord>())
            {
                if (source == null) continue;
                var record = new ChoiceRecord
                {
                    sequence = source.sequence,
                    kind = source.kind,
                    nodeId = source.nodeId,
                    optionId = source.optionId,
                    levelId = source.levelId,
                    sessionId = source.sessionId,
                    resultId = source.resultId,
                    paradigmId = source.paradigmId,
                    useId = source.useId
                };
                if (!history.AppendCore(record)) throw new ArgumentException("检查点包含重复记录。");
                record.sequence = source.sequence > 0 ? source.sequence : history.records.Count;
            }
            return history;
        }

        private bool AppendCore(ChoiceRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.nodeId)) throw new ArgumentException("Record requires a node.");
            if (!Enum.IsDefined(typeof(ChoiceKind), record.kind)) throw new ArgumentException("Unknown record kind.");
            if (record.kind == ChoiceKind.LevelResult)
            {
                if (string.IsNullOrWhiteSpace(record.sessionId) || string.IsNullOrWhiteSpace(record.resultId)) throw new ArgumentException("Invalid level record.");
                if (!confirmedSessions.Add(record.sessionId)) return false;
            }
            if (record.kind == ChoiceKind.CandyUse)
            {
                if (string.IsNullOrWhiteSpace(record.useId)) throw new ArgumentException("Candy record requires a use ID.");
                if (!candyUses.Add(record.useId)) return false;
                CandyCount++;
            }
            records.Add(record);
            return true;
        }
    }
}
