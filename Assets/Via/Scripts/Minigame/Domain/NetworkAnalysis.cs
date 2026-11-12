using System.Collections.Generic;
using UnityEngine;

namespace Via.Minigame.Domain
{
    // Structural + geometric features of a network. No Unity objects, so it can be verified directly.
    public sealed class NetworkAnalysis
    {
        public int NodeCount { get; private set; }
        public int EdgeCount { get; private set; }
        public int ComponentCount { get; private set; }
        public int CycleCount { get; private set; }
        public int EndpointCount { get; private set; }
        public int BranchCount { get; private set; }
        public int MaxBranchDegree { get; private set; }
        public int CrossingCount { get; private set; }
        public int CornerCount { get; private set; }
        public int SignChangeCount { get; private set; }
        public float MaxTurn { get; private set; }
        public float TotalTurn { get; private set; }
        public float CornerAngle { get; private set; }
        public bool IsOpenSimplePath { get; private set; }
        public bool HasFigureEightRegions { get; private set; }
        public bool HasSingleClosedRegion { get; private set; }
        public int CurveJointCount { get; private set; }
        public IReadOnlyList<ComponentAnalysis> Components { get; private set; }

        private const float Epsilon = 1e-4f;
        private const float TurnEpsilon = 0.01f;

        public static NetworkAnalysis Analyze(NeuronNetwork network, float cornerAngle)
        {
            var result = new NetworkAnalysis();
            int n = network.Nodes.Count;
            result.NodeCount = n;
            result.EdgeCount = network.Edges.Count;
            result.CornerAngle = cornerAngle;

            var degree = new int[n];
            var adjacency = new List<int>[n];
            for (int i = 0; i < n; i++) adjacency[i] = new List<int>();
            foreach (var edge in network.Edges)
            {
                if (edge.From < 0 || edge.To < 0 || edge.From >= n || edge.To >= n) continue;
                degree[edge.From]++;
                degree[edge.To]++;
                adjacency[edge.From].Add(edge.To);
                adjacency[edge.To].Add(edge.From);
            }

            var components = BuildComponents(network, degree, adjacency, cornerAngle);
            result.Components = components;
            result.ComponentCount = components.Count;
            foreach (var component in components)
            {
                result.EndpointCount += component.EndpointCount;
                result.BranchCount += component.BranchCount;
                result.CycleCount += component.CycleCount;
                result.CornerCount += component.CornerCount;
                if (component.MaxDegree > result.MaxBranchDegree && component.MaxDegree >= 3)
                    result.MaxBranchDegree = component.MaxDegree;
            }

            result.CrossingCount = CountCrossings(network);
            result.HasFigureEightRegions = ClosedRegionGeometry.IsFigureEight(network);
            result.HasSingleClosedRegion = result.ComponentCount == 1 && result.CycleCount == 1 &&
                result.CrossingCount == 0 && ClosedRegionGeometry.HasSingleRegion(network);
            result.IsOpenSimplePath = components.Count == 1 && components[0].IsOpenPath;
            if (result.IsOpenSimplePath)
            {
                result.MaxTurn = components[0].MaxTurn;
                result.TotalTurn = components[0].TotalTurn;
                result.SignChangeCount = components[0].SignChangeCount;
                result.CurveJointCount = components[0].CurveJointCount;
            }
            return result;
        }

        private static List<ComponentAnalysis> BuildComponents(NeuronNetwork network, int[] degree,
            List<int>[] adjacency, float cornerAngle)
        {
            int n = degree.Length;
            var visited = new bool[n];
            var components = new List<ComponentAnalysis>();
            for (int i = 0; i < n; i++)
            {
                if (degree[i] == 0 || visited[i]) continue;
                var members = new List<int>();
                var stack = new Stack<int>();
                stack.Push(i);
                visited[i] = true;
                while (stack.Count > 0)
                {
                    int current = stack.Pop();
                    members.Add(current);
                    foreach (int next in adjacency[current])
                        if (!visited[next]) { visited[next] = true; stack.Push(next); }
                }
                components.Add(Describe(network, members, degree, adjacency, cornerAngle));
            }
            return components;
        }

        private static ComponentAnalysis Describe(NeuronNetwork network, List<int> members, int[] degree,
            List<int>[] adjacency, float cornerAngle)
        {
            var memberSet = new HashSet<int>(members);
            int edgeCount = 0;
            foreach (var edge in network.Edges)
                if (memberSet.Contains(edge.From) && memberSet.Contains(edge.To)) edgeCount++;

            int endpoints = 0;
            int branches = 0;
            int maxDegree = 0;
            foreach (int node in members)
            {
                int nodeDegree = degree[node];
                if (nodeDegree > maxDegree) maxDegree = nodeDegree;
                if (nodeDegree == 1) endpoints++;
                if (nodeDegree >= 3) branches++;
            }

            int cycles = edgeCount - members.Count + 1;
            bool openPath = endpoints == 2 && branches == 0 && cycles == 0;
            bool simpleCycle = endpoints == 0 && branches == 0 && cycles == 1;
            int corners = 0;
            int signChanges = 0;
            float maxTurn = 0f;
            float totalTurn = 0f;
            bool straightSides = false;
            int curveJoints = 0;

            if (openPath) WalkOpen(network, members, degree, adjacency, cornerAngle,
                ref corners, ref signChanges, ref maxTurn, ref totalTurn, ref curveJoints);
            else if (simpleCycle) WalkCycle(network, members, adjacency, cornerAngle,
                ref corners, ref straightSides);
            else corners = CountCorners(network, members, degree, adjacency, cornerAngle);

            return new ComponentAnalysis(members.Count, edgeCount, endpoints, branches, maxDegree, cycles,
                corners, signChanges, maxTurn, totalTurn, straightSides, curveJoints);
        }

        private static void WalkOpen(NeuronNetwork network, List<int> members, int[] degree, List<int>[] adjacency,
            float cornerAngle, ref int corners, ref int signChanges, ref float maxTurn, ref float totalTurn, ref int curveJoints)
        {
            int start = -1;
            foreach (int node in members)
                if (degree[node] == 1) { start = node; break; }
            if (start < 0) return;

            int previous = -1;
            int current = start;
            int direction = 0;
            float firstBend = 0f, reverseBend = 0f;
            int guard = 0;
            while (guard++ <= members.Count)
            {
                int next = -1;
                foreach (int candidate in adjacency[current])
                    if (candidate != previous) { next = candidate; break; }
                if (next < 0) break;
                if (degree[current] == 2)
                {
                    float turn = TurnAt(network, previous, current, next, out int sign);
                    if (turn >= cornerAngle) corners++;
                    maxTurn = Mathf.Max(maxTurn, turn);
                    totalTurn += turn;
                    if (turn >= TurnEpsilon)
                    {
                        curveJoints++;
                        if (direction == 0)
                        {
                            firstBend += sign * turn;
                            if (Mathf.Abs(firstBend) >= ParadigmMetrics.DirectionChangeAngle)
                                direction = firstBend > 0f ? 1 : -1;
                        }
                        else
                        {
                            reverseBend = Mathf.Max(0f, reverseBend - direction * sign * turn);
                            if (reverseBend >= ParadigmMetrics.DirectionChangeAngle)
                            {
                                signChanges++;
                                direction = -direction;
                                reverseBend = 0f;
                            }
                        }
                    }
                }
                previous = current;
                current = next;
            }
        }

        private static void WalkCycle(NeuronNetwork network, List<int> members, List<int>[] adjacency,
            float cornerAngle, ref int corners, ref bool straightSides)
        {
            int start = members[0];
            if (adjacency[start].Count < 2) return;
            int incoming = adjacency[start][0];
            int current = start;
            bool straight = true;
            int guard = 0;
            do
            {
                int outgoing = adjacency[current][0] == incoming ? adjacency[current][1] : adjacency[current][0];
                float turn = TurnAt(network, incoming, current, outgoing, out _);
                if (turn >= cornerAngle) corners++;
                else if (turn > ParadigmMetrics.StraightJointAngle) straight = false;
                incoming = current;
                current = outgoing;
                guard++;
            }
            while (current != start && guard <= members.Count);
            straightSides = straight && current == start;
        }

        private static int CountCorners(NeuronNetwork network, List<int> members, int[] degree,
            List<int>[] adjacency, float cornerAngle)
        {
            int corners = 0;
            foreach (int node in members)
            {
                if (degree[node] != 2 || adjacency[node].Count < 2) continue;
                float turn = TurnAt(network, adjacency[node][0], node, adjacency[node][1], out _);
                if (turn >= cornerAngle) corners++;
            }
            return corners;
        }

        private static int CountCrossings(NeuronNetwork network)
        {
            int crossings = 0;
            var edges = network.Edges;
            for (int i = 0; i < edges.Count; i++)
                for (int j = i + 1; j < edges.Count; j++)
                {
                    var a = edges[i];
                    var b = edges[j];
                    if (SharesVertex(a, b)) continue;
                    if (ProperIntersect(network.Nodes[a.From].Position, network.Nodes[a.To].Position,
                            network.Nodes[b.From].Position, network.Nodes[b.To].Position)) crossings++;
                }

            // A sample point can sit on another path without the segments crossing in their open interiors.
            var nodes = network.Nodes;
            for (int node = 0; node < nodes.Count; node++)
                foreach (var edge in edges)
                {
                    if (edge.From == node || edge.To == node) continue;
                    if (PointOnOpenSegment(nodes[node].Position, nodes[edge.From].Position, nodes[edge.To].Position))
                        crossings++;
                }
            return crossings;
        }

        private static bool SharesVertex(NeuronEdge a, NeuronEdge b) =>
            a.From == b.From || a.From == b.To || a.To == b.From || a.To == b.To;

        private static bool PointOnOpenSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            float abx = end.x - start.x;
            float aby = end.y - start.y;
            float length = abx * abx + aby * aby;
            if (length < Epsilon) return false;
            float t = ((point.x - start.x) * abx + (point.y - start.y) * aby) / length;
            if (t <= Epsilon || t >= 1f - Epsilon) return false;
            float dx = point.x - (start.x + abx * t);
            float dy = point.y - (start.y + aby * t);
            return dx * dx + dy * dy <= Epsilon * Epsilon;
        }

        private static bool ProperIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
        {
            float d = (p2.x - p1.x) * (p4.y - p3.y) - (p2.y - p1.y) * (p4.x - p3.x);
            if (Mathf.Abs(d) < Epsilon) return false;
            float t = ((p3.x - p1.x) * (p4.y - p3.y) - (p3.y - p1.y) * (p4.x - p3.x)) / d;
            float u = ((p3.x - p1.x) * (p2.y - p1.y) - (p3.y - p1.y) * (p2.x - p1.x)) / d;
            return t > Epsilon && t < 1f - Epsilon && u > Epsilon && u < 1f - Epsilon;
        }

        private static float TurnAt(NeuronNetwork network, int from, int at, int to, out int sign)
        {
            Vector2 incoming = network.Nodes[at].Position - network.Nodes[from].Position;
            Vector2 outgoing = network.Nodes[to].Position - network.Nodes[at].Position;
            float cross = incoming.x * outgoing.y - incoming.y * outgoing.x;
            float angle = Mathf.Abs(Mathf.Atan2(cross, Vector2.Dot(incoming, outgoing)) * Mathf.Rad2Deg);
            sign = cross >= 0f ? 1 : -1;
            return angle;
        }
    }
}
