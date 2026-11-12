using System;
using System.Collections.Generic;

namespace Via.Minigame.Domain
{
    // Official geometric paradigms, most specific first. Levels pick among matches with their own outcome order.
    public static class ParadigmCatalog
    {
        public static Paradigm DoubleLine { get; private set; }
        public static Paradigm Triangle { get; private set; }
        public static Paradigm Eight { get; private set; }
        public static Paradigm Y { get; private set; }
        public static Paradigm X { get; private set; }
        public static Paradigm S { get; private set; }
        public static Paradigm Wave { get; private set; }
        public static Paradigm Arc { get; private set; }
        public static Paradigm Separated { get; private set; }
        public static Paradigm Polygon { get; private set; }
        public static Paradigm Polyline { get; private set; }
        // Prototype levels still submit a single straight line. It is not one of the eleven official paradigms.
        public static Paradigm Straight { get; private set; }
        public static IReadOnlyList<Paradigm> All { get; private set; }

        static ParadigmCatalog()
        {
            DoubleLine = Define("double_line", "双直线", "||",
                "恰好两个互不连接的部分；两部分均为开放直线；无闭合、无交叉、无明显转折",
                true, new[] { 8 }, new[] { "坚定" }, MatchDoubleLine);
            Triangle = Define("triangle", "三角形", "△",
                "由直线段形成一个闭合区域；恰好有三个明显顶点；无分叉",
                true, new[] { 3, 5, 6 }, new[] { "疑虑", "担忧", "担心" }, MatchTriangle);
            Eight = Define("eight", "8字形", "8",
                "形成两个有面积的闭合区域；仅在一个位置交会（共用节点或路径交点）；无拖尾、无重叠",
                false, new[] { 2, 3, 4, 7, 8 }, new[] { "喜欢", "亲切", "平静", "期待" }, MatchEight);
            Y = Define("y", "Y形", "Y",
                "存在一个有效分叉节点；形成三个末端；无闭合区域",
                false, new[] { 2, 3, 8 }, new[] { "厌恶", "愤怒" }, MatchY);
            X = Define("x", "X形", "X",
                "两条开放路径仅交会一次（路径交点或四向节点）；没有形成闭合区域",
                false, new[] { 6 }, new[] { "恐惧" }, MatchX);
            S = Define("s", "S形", "S",
                "路径开放且平滑；弯曲方向改变一次；无分叉、无交叉",
                true, new[] { 4, 5 }, new[] { "好奇" }, MatchS);
            Wave = Define("wave", "波浪形", "≈",
                "路径开放且平滑；弯曲方向交替改变至少两次；无分叉、无交叉",
                true, new[] { 5 }, new[] { "兴奋" }, MatchWave);
            Arc = Define("arc", "圆弧", "⌒",
                "路径开放且平滑；弯曲方向一致，允许轻微操作误差；无分叉、无交叉（不要求严格等半径）",
                true, new[] { 4 }, new[] { "戒备" }, MatchArc);
            Separated = Define("separated", "分离部分", "··",
                "恰好两个互不连接的部分；每部分均开放、无分叉、无自交；不限制直线或曲线",
                false, new[] { 7 }, new[] { "抗拒" }, MatchSeparated);
            Polygon = Define("polygon", "多边形", "◇",
                "存在一个闭合区域；由直线段组成；不限制顶点数量；无分叉",
                true, new[] { 1, 6 }, new[] { "亲近", "关切" }, MatchPolygon);
            Polyline = Define("polyline", "折线", "/\\",
                "路径开放；至少一个明显转折；无分叉、无交叉",
                true, new[] { 1 }, new[] { "敌意" }, MatchPolyline);
            Straight = Define("straight", "直线", "━━━━",
                "开放单路径；无明显转折，也没有形成平滑弯曲；无分叉、无交叉",
                true, Array.Empty<int>(), Array.Empty<string>(), MatchStraight, false);

            All = new[]
            {
                DoubleLine, Triangle, Eight, Y, X, S, Wave, Arc, Separated, Polygon, Polyline
            };
        }

        public static Paradigm Find(string id)
        {
            if (Straight != null && Straight.Id == id) return Straight;
            if (All == null) return null;
            for (int i = 0; i < All.Count; i++)
                if (All[i].Id == id) return All[i];
            return null;
        }

        public static IReadOnlyList<Paradigm> ForLevel(int levelNumber)
        {
            var found = new List<Paradigm>();
            for (int i = 0; i < All.Count; i++)
            {
                var levels = All[i].Levels;
                for (int n = 0; n < levels.Count; n++)
                    if (levels[n] == levelNumber) { found.Add(All[i]); break; }
            }
            return found;
        }

        private static Paradigm Define(string id, string displayName, string glyph, string judgment,
            bool usesTurnThreshold, int[] levels, string[] emotions, Func<NetworkAnalysis, bool> matches,
            bool official = true)
        {
            return new Paradigm(id, displayName, glyph, judgment, official, usesTurnThreshold, levels, emotions, matches);
        }

        private static bool MatchDoubleLine(NetworkAnalysis analysis) =>
            MatchSeparated(analysis) && analysis.Components[0].IsStraight && analysis.Components[1].IsStraight;

        private static bool MatchTriangle(NetworkAnalysis analysis) =>
            MatchPolygon(analysis) && analysis.Components[0].CornerCount == 3;

        private static bool MatchEight(NetworkAnalysis analysis) => analysis.HasFigureEightRegions;

        private static bool MatchY(NetworkAnalysis analysis) =>
            analysis.ComponentCount == 1 && analysis.CycleCount == 0 && analysis.BranchCount == 1 &&
            analysis.MaxBranchDegree == 3 && analysis.EndpointCount == 3;

        private static bool MatchX(NetworkAnalysis analysis) =>
            analysis.CycleCount == 0 && analysis.EndpointCount == 4 &&
            ((analysis.ComponentCount == 2 && analysis.BranchCount == 0 && analysis.CrossingCount == 1) ||
             (analysis.ComponentCount == 1 && analysis.BranchCount == 1 && analysis.MaxBranchDegree == 4 && analysis.CrossingCount == 0));

        private static bool MatchS(NetworkAnalysis analysis) => IsSmoothOpen(analysis) && analysis.SignChangeCount == 1;

        private static bool MatchWave(NetworkAnalysis analysis) => IsSmoothOpen(analysis) && analysis.SignChangeCount >= 2;

        private static bool MatchArc(NetworkAnalysis analysis) => IsSmoothOpen(analysis) && analysis.SignChangeCount == 0;

        private static bool MatchSeparated(NetworkAnalysis analysis) =>
            analysis.ComponentCount == 2 && analysis.CycleCount == 0 && analysis.BranchCount == 0 &&
            analysis.CrossingCount == 0 && analysis.EndpointCount == 4 && analysis.Components.Count == 2 &&
            analysis.Components[0].IsOpenPath && analysis.Components[1].IsOpenPath;

        // Straight sides: every non-corner joint stays within the straight-joint angle. Edge crossings would split the region, so they are rejected.
        private static bool MatchPolygon(NetworkAnalysis analysis) =>
            analysis.ComponentCount == 1 && analysis.CrossingCount == 0 && analysis.BranchCount == 0 &&
            analysis.Components.Count == 1 && analysis.Components[0].IsPiecewiseStraightCycle;

        private static bool MatchPolyline(NetworkAnalysis analysis) =>
            analysis.IsOpenSimplePath && analysis.CrossingCount == 0 && analysis.CornerCount >= 1;

        private static bool MatchStraight(NetworkAnalysis analysis) =>
            analysis.IsOpenSimplePath && analysis.CrossingCount == 0 && analysis.CornerCount == 0 &&
            analysis.TotalTurn <= ParadigmMetrics.MinimumCurveAngle;

        private static bool IsSmoothOpen(NetworkAnalysis analysis) =>
            analysis.IsOpenSimplePath && analysis.CrossingCount == 0 && analysis.MaxTurn < analysis.CornerAngle &&
            analysis.TotalTurn > ParadigmMetrics.MinimumCurveAngle && analysis.CurveJointCount >= ParadigmMetrics.MinimumCurveJoints;
    }
}
