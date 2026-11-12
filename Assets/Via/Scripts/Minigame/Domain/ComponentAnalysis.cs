namespace Via.Minigame.Domain
{
    // Geometry of one connected piece. Paradigm rules read these instead of guessing from the whole drawing.
    public sealed class ComponentAnalysis
    {
        public int NodeCount { get; }
        public int EdgeCount { get; }
        public int EndpointCount { get; }
        public int BranchCount { get; }
        public int MaxDegree { get; }
        public int CycleCount { get; }
        public int CornerCount { get; }
        public int SignChangeCount { get; }
        public float MaxTurn { get; }
        public float TotalTurn { get; }
        public int CurveJointCount { get; }
        public bool IsOpenPath { get; }
        public bool IsStraight { get; }
        public bool IsPiecewiseStraightCycle { get; }

        public ComponentAnalysis(int nodeCount, int edgeCount, int endpointCount, int branchCount, int maxDegree,
            int cycleCount, int cornerCount, int signChangeCount, float maxTurn, float totalTurn, bool straightSides, int curveJointCount)
        {
            NodeCount = nodeCount;
            EdgeCount = edgeCount;
            EndpointCount = endpointCount;
            BranchCount = branchCount;
            MaxDegree = maxDegree;
            CycleCount = cycleCount;
            CornerCount = cornerCount;
            SignChangeCount = signChangeCount;
            MaxTurn = maxTurn;
            TotalTurn = totalTurn;
            CurveJointCount = curveJointCount;
            IsOpenPath = endpointCount == 2 && branchCount == 0 && cycleCount == 0;
            IsStraight = IsOpenPath && cornerCount == 0 && totalTurn <= ParadigmMetrics.MinimumCurveAngle;
            IsPiecewiseStraightCycle = straightSides && endpointCount == 0 && branchCount == 0 &&
                cycleCount == 1 && cornerCount >= 3;
        }
    }
}
