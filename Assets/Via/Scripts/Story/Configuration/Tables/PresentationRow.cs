namespace Via.Story.Configuration
{
    // One action of a presentation. Rows of the same presentationId with the same Step run in parallel;
    // steps run in ascending order.
    public sealed class PresentationRow
    {
        public string PresentationId { get; internal set; }
        public int Step { get; internal set; }
        public string TargetId { get; internal set; }
        public string Action { get; internal set; }
        public string Value { get; internal set; }
        public float Duration { get; internal set; }
        public string Note { get; internal set; }
    }
}
