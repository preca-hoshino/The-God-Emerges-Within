using System.Collections.Generic;
using UnityEngine;

namespace Via.Minigame.Domain
{
    // A derived planar graph of the actual path segments. It never edits gameplay topology.
    // This also recognises a closed bow-tie drawn through an edge interior as two touching regions.
    public static class ClosedRegionGeometry
    {
        public static bool HasSingleRegion(NeuronNetwork network)
        {
            var adjacency = new List<int>[network.Nodes.Count];
            for (int i = 0; i < adjacency.Length; i++) adjacency[i] = new List<int>();
            foreach (var edge in network.Edges)
            {
                adjacency[edge.From].Add(edge.To);
                adjacency[edge.To].Add(edge.From);
            }
            int start = -1, active = 0;
            for (int i = 0; i < adjacency.Length; i++)
            {
                if (adjacency[i].Count == 0) continue;
                if (adjacency[i].Count != 2) return false;
                if (start < 0) start = i;
                active++;
            }
            if (start < 0) return false;
            var loop = new List<Vector2>();
            var visited = new HashSet<int>();
            int previous = -1, current = start;
            do
            {
                if (!visited.Add(current)) return false;
                loop.Add(network.Nodes[current].Position);
                int next = adjacency[current][0] == previous ? adjacency[current][1] : adjacency[current][0];
                previous = current;
                current = next;
            } while (current != start);
            return visited.Count == active && HasArea(loop);
        }

        public static bool IsFigureEight(NeuronNetwork network)
        {
            if (network == null || network.Edges.Count < 4) return false;
            var points = new List<Vector2>();
            var splits = new List<List<int>>();
            Vector2 min = network.Nodes[0].Position, max = min;
            foreach (var node in network.Nodes)
            {
                min = Vector2.Min(min, node.Position);
                max = Vector2.Max(max, node.Position);
            }
            float tolerance = Mathf.Max(0.00001f, (max - min).magnitude * 0.00001f);

            foreach (var edge in network.Edges)
            {
                int a = FindOrAdd(points, network.Nodes[edge.From].Position, tolerance);
                int b = FindOrAdd(points, network.Nodes[edge.To].Position, tolerance);
                if (a == b) return false;
                splits.Add(new List<int> { a, b });
            }

            for (int i = 0; i < splits.Count; i++)
                for (int j = i + 1; j < splits.Count; j++)
                {
                    Vector2 a = points[splits[i][0]], b = points[splits[i][1]];
                    Vector2 c = points[splits[j][0]], d = points[splits[j][1]];
                    Vector2 ab = b - a, cd = d - c;
                    float denominator = Cross(ab, cd);
                    if (Mathf.Abs(denominator) <= tolerance * Mathf.Max(ab.magnitude, cd.magnitude))
                    {
                        if (Mathf.Abs(Cross(c - a, ab)) > tolerance * ab.magnitude) continue;
                        float first = Vector2.Dot(c - a, ab) / ab.sqrMagnitude;
                        float last = Vector2.Dot(d - a, ab) / ab.sqrMagnitude;
                        float overlap = Mathf.Min(1f, Mathf.Max(first, last)) - Mathf.Max(0f, Mathf.Min(first, last));
                        if (overlap * ab.magnitude > tolerance) return false; // Retracing is not an enclosed region.
                        continue;
                    }
                    float t = Cross(c - a, cd) / denominator;
                    float u = Cross(c - a, ab) / denominator;
                    if (t < -tolerance / ab.magnitude || t > 1f + tolerance / ab.magnitude ||
                        u < -tolerance / cd.magnitude || u > 1f + tolerance / cd.magnitude) continue;
                    int meeting = FindOrAdd(points, a + ab * Mathf.Clamp01(t), tolerance);
                    if (!splits[i].Contains(meeting)) splits[i].Add(meeting);
                    if (!splits[j].Contains(meeting)) splits[j].Add(meeting);
                }

            var adjacency = new List<int>[points.Count];
            for (int i = 0; i < adjacency.Length; i++) adjacency[i] = new List<int>();
            foreach (var split in splits)
            {
                Vector2 start = points[split[0]], direction = points[split[1]] - start;
                split.Sort((a, b) => Vector2.Dot(points[a] - start, direction).CompareTo(Vector2.Dot(points[b] - start, direction)));
                for (int i = 1; i < split.Count; i++)
                {
                    int a = split[i - 1], b = split[i];
                    if (adjacency[a].Contains(b)) return false;
                    adjacency[a].Add(b);
                    adjacency[b].Add(a);
                }
            }

            int junction = -1;
            for (int i = 0; i < adjacency.Length; i++)
            {
                if (adjacency[i].Count == 4 && junction < 0) junction = i;
                else if (adjacency[i].Count != 2) return false;
            }
            if (junction < 0) return false;

            var visited = new HashSet<int> { junction };
            var loops = new List<List<Vector2>>();
            foreach (int neighbour in adjacency[junction])
            {
                if (visited.Contains(neighbour)) continue;
                var loop = new List<Vector2> { points[junction] };
                int previous = junction, current = neighbour;
                while (current != junction)
                {
                    if (!visited.Add(current) || adjacency[current].Count != 2) return false;
                    loop.Add(points[current]);
                    int next = adjacency[current][0] == previous ? adjacency[current][1] : adjacency[current][0];
                    previous = current;
                    current = next;
                }
                if (!HasArea(loop)) return false;
                loops.Add(loop);
            }
            if (loops.Count != 2 || visited.Count != points.Count) return false;
            // Nested rings share a vertex but are not the two lobes of an eight.
            for (int i = 1; i < loops[0].Count; i++) if (Inside(loops[0][i], loops[1])) return false;
            for (int i = 1; i < loops[1].Count; i++) if (Inside(loops[1][i], loops[0])) return false;
            return true;
        }

        private static int FindOrAdd(List<Vector2> points, Vector2 point, float tolerance)
        {
            for (int i = 0; i < points.Count; i++)
                if ((points[i] - point).sqrMagnitude <= tolerance * tolerance) return i;
            points.Add(point);
            return points.Count - 1;
        }

        private static bool HasArea(List<Vector2> loop)
        {
            if (loop.Count < 3) return false;
            float twiceArea = 0f, perimeter = 0f;
            for (int i = 0; i < loop.Count; i++)
            {
                Vector2 a = loop[i] - loop[0], b = loop[(i + 1) % loop.Count] - loop[0];
                twiceArea += Cross(a, b);
                perimeter += Vector2.Distance(a, b);
            }
            return Mathf.Abs(twiceArea) > perimeter * perimeter * 0.002f;
        }

        private static bool Inside(Vector2 point, List<Vector2> polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                Vector2 a = polygon[i], b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
