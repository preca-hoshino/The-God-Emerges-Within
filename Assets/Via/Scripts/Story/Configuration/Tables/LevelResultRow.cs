namespace Via.Story.Configuration
{
    // One row of LevelResults. Serves both level exits and choice options:
    // - level node: TextId/Order empty, ResultId is the in-level result id.
    // - choice node: TextId/Order filled, ResultId is the option id.
    public sealed class LevelResultRow
    {
        public string EntryNodeId { get; internal set; }
        public string ResultId { get; internal set; }
        public string NextNodeId { get; internal set; }
        public string TextId { get; internal set; }
        public int Order { get; internal set; }
        public string Note { get; internal set; }
        // Optional on dialogue-history routes; selects the level whose result is recalled.
        public string SourceLevelId { get; internal set; }

        public bool IsOption => !string.IsNullOrEmpty(TextId) || Order > 0;
    }
}
