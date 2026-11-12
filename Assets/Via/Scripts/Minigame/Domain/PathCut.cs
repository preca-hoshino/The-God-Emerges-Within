using System.Collections.Generic;
using UnityEngine;

namespace Via.Minigame.Domain
{
    // Cuts produce independent, movable cut endpoints. Gameplay keeps both pieces by default.
    public static class PathCut
    {
        private const float Epsilon = 1e-4f;

        public static bool Apply(NeuronNetwork source, Vector2 start, Vector2 end, out NeuronNetwork result, bool preservePieces = true)
        {
            result = source;
            if (source == null || !float.IsFinite(start.x) || !float.IsFinite(start.y) ||
                !float.IsFinite(end.x) || !float.IsFinite(end.y) || (end - start).sqrMagnitude < 1e-6f) return false;

            var positions = new List<Vector2>(source.Nodes.Count);
            foreach (var node in source.Nodes) positions.Add(node.Position);
            var cutEndpoints = new HashSet<int>();
            foreach (var node in source.Nodes)
                if (node.IsCutEndpoint) cutEndpoints.Add(node.Id);

            var splitNodes = new Dictionary<int, (int positive, int negative)>();
            Vector2 cutDirection = end - start;
            Vector2 normal = new Vector2(-cutDirection.y, cutDirection.x).normalized;
            if (preservePieces)
                foreach (var node in source.Nodes)
                {
                    float along = Vector2.Dot(node.Position - start, cutDirection) / cutDirection.sqrMagnitude;
                    if (along <= Epsilon || along >= 1f - Epsilon || Mathf.Abs(Vector2.Dot(node.Position - start, normal)) > Epsilon) continue;
                    bool positive = false, negative = false;
                    foreach (var edge in source.Edges)
                    {
                        int other = edge.From == node.Id ? edge.To : edge.To == node.Id ? edge.From : -1;
                        if (other < 0) continue;
                        float side = Vector2.Dot(source.Nodes[other].Position - start, normal);
                        positive |= side > Epsilon;
                        negative |= side < -Epsilon;
                    }
                    if (!positive || !negative) continue; // Merely touching a vertex does not cut it.
                    int duplicate = positions.Count;
                    positions.Add(node.Position - normal * 0.06f);
                    if (node.Id != 0) positions[node.Id] += normal * 0.06f;
                    splitNodes.Add(node.Id, (node.Id, duplicate));
                    cutEndpoints.Add(node.Id);
                    cutEndpoints.Add(duplicate);
                }

            var edges = new List<EdgePair>(source.Edges.Count);
            bool cut = splitNodes.Count > 0;
            foreach (var edge in source.Edges)
            {
                Vector2 p = source.Nodes[edge.From].Position;
                Vector2 q = source.Nodes[edge.To].Position;
                int from = SplitEndpoint(edge.From, q, start, normal, splitNodes);
                int to = SplitEndpoint(edge.To, p, start, normal, splitNodes);
                if (TryIntersect(p, q, start, end, out Vector2 point))
                {
                    // A visible gap keeps separated pieces geometrically distinct as well as topologically distinct.
                    float gap = preservePieces ? Mathf.Min(0.06f, Mathf.Min(Vector2.Distance(p, point), Vector2.Distance(q, point)) * 0.25f) : 0f;
                    Vector2 offset = (q - p).normalized * gap;
                    int nearFrom = positions.Count;
                    positions.Add(point - offset);
                    int nearTo = positions.Count;
                    positions.Add(point + offset);
                    cutEndpoints.Add(nearFrom);
                    cutEndpoints.Add(nearTo);
                    edges.Add(new EdgePair(from, nearFrom));
                    edges.Add(new EdgePair(nearTo, to));
                    cut = true;
                }
                else edges.Add(new EdgePair(from, to));
            }
            if (!cut) return false;

            var reachable = ReachableFromOrigin(positions.Count, edges);
            var removed = new HashSet<int>();
            if (!preservePieces)
                for (int i = 0; i < positions.Count; i++)
                    if (!reachable.Contains(i)) removed.Add(i);

            result = NetworkRebuilder.Rebuild(positions, edges, 0, removed, cutEndpoints);
            return true;
        }

        private static int SplitEndpoint(int node, Vector2 other, Vector2 start, Vector2 normal,
            Dictionary<int, (int positive, int negative)> splitNodes)
        {
            if (!splitNodes.TryGetValue(node, out var split)) return node;
            return Vector2.Dot(other - start, normal) >= 0f ? split.positive : split.negative;
        }

        private static HashSet<int> ReachableFromOrigin(int count, List<EdgePair> edges)
        {
            var adjacency = new List<int>[count];
            for (int i = 0; i < count; i++) adjacency[i] = new List<int>();
            foreach (var edge in edges)
            {
                if (edge.A < 0 || edge.B < 0 || edge.A >= count || edge.B >= count) continue;
                adjacency[edge.A].Add(edge.B);
                adjacency[edge.B].Add(edge.A);
            }
            var visited = new HashSet<int>();
            var stack = new Stack<int>();
            stack.Push(0);
            visited.Add(0);
            while (stack.Count > 0)
            {
                int current = stack.Pop();
                foreach (int next in adjacency[current])
                    if (visited.Add(next)) stack.Push(next);
            }
            return visited;
        }

        private static bool TryIntersect(Vector2 p, Vector2 q, Vector2 a, Vector2 b, out Vector2 point)
        {
            point = Vector2.zero;
            float d = (q.x - p.x) * (b.y - a.y) - (q.y - p.y) * (b.x - a.x);
            if (Mathf.Abs(d) < Epsilon) return false;
            float t = ((a.x - p.x) * (b.y - a.y) - (a.y - p.y) * (b.x - a.x)) / d;
            float u = ((a.x - p.x) * (q.y - p.y) - (a.y - p.y) * (q.x - p.x)) / d;
            if (t <= Epsilon || t >= 1f - Epsilon || u <= Epsilon || u >= 1f - Epsilon) return false;
            point = p + t * (q - p);
            return true;
        }
    }
}
