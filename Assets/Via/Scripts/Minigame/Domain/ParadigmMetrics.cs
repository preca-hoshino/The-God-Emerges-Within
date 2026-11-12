namespace Via.Minigame.Domain
{
    // Turn thresholds shared by network analysis and paradigm rules.
    // A level supplies its own corner angle for "明显转折"; these constants separate a straight joint from a smooth bend.
    public static class ParadigmMetrics
    {
        public const float StraightJointAngle = 8f;
        public const float MinimumCurveAngle = 8f;
        // Small reverse bends caused by hand editing do not constitute a new S/wave lobe.
        public const float DirectionChangeAngle = 8f;
        public const int MinimumCurveJoints = 3;
        public const float CurveSampleAngle = 10f;
    }
}
