namespace Via.Story.Configuration
{
    public enum FlowNodeType { Dialogue, Presentation, Level, End, Choice, Menu, Condition }
    public enum FlowConditionType { LevelResult, CandyUseCount }

    // One row of the Flow table. contentId means dialogueId, presentationId or levelId by type.
    public sealed class FlowRow
    {
        public string NodeId { get; internal set; }
        public FlowNodeType Type { get; internal set; }
        public string ContentId { get; internal set; }
        public string PresentationId { get; internal set; }
        public string NextNodeId { get; internal set; }
        public string Note { get; internal set; }
        public FlowConditionType ConditionType { get; internal set; }
        public int Threshold { get; internal set; }
    }
}
