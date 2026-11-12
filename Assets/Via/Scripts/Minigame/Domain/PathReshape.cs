using System.Collections.Generic;
using UnityEngine;

namespace Via.Minigame.Domain
{
    // Replaces a sharp corner with a sampled quadratic curve while keeping both neighbouring nodes fixed.
    public static class PathReshape
    {
        public static bool Apply(NeuronNetwork source, int corner, int segments, out NeuronNetwork result)
        {
            result = source;
            if (source == null) return false;
            if (corner <= 0 || corner >= source.Nodes.Count || source.Degree(corner) != 2) return false;

            int first = -1, second = -1;
            foreach (var edge in source.Edges)
            {
                int other = edge.From == corner ? edge.To : edge.To == corner ? edge.From : -1;
                if (other < 0) continue;
                if (first < 0) first = other;
                else second = other;
            }
            if (first < 0 || second < 0 || first == second) return false;

            Vector2 pa = source.Nodes[first].Position;
            Vector2 pb = source.Nodes[second].Position;
            Vector2 control = source.Nodes[corner].Position;
            if ((pa - pb).sqrMagnitude < 1e-6f) return false;
            // A fixed six-segment curve could exceed the level's 20-degree corner threshold.
            // Subdivide until the actual displayed segments describe a smooth bend.
            segments = Mathf.Clamp(segments, 12, 256);
            while (MaximumTurn(pa, control, pb, segments) > ParadigmMetrics.CurveSampleAngle && segments < 256)
                segments = Mathf.Min(segments * 2, 256);
            if (MaximumTurn(pa, control, pb, segments) > ParadigmMetrics.CurveSampleAngle) return false;

            var positions = new List<Vector2>(source.Nodes.Count + segments - 1);
            foreach (var node in source.Nodes) positions.Add(node.Position);

            var curve = new List<int>(segments - 1);
            for (int i = 1; i < segments; i++)
            {
                float t = (float)i / segments;
                float oneMinus = 1f - t;
                Vector2 point = oneMinus * oneMinus * pa + 2f * oneMinus * t * control + t * t * pb;
                curve.Add(positions.Count);
                positions.Add(point);
            }

            var edges = new List<EdgePair>(source.Edges.Count + curve.Count);
            foreach (var edge in source.Edges)
            {
                if (edge.From == corner || edge.To == corner) continue;
                edges.Add(new EdgePair(edge.From, edge.To));
            }
            int previous = first;
            foreach (int node in curve)
            {
                edges.Add(new EdgePair(previous, node));
                previous = node;
            }
            edges.Add(new EdgePair(previous, second));

            var removed = new HashSet<int> { corner };
            var cutEndpoints = new HashSet<int>();
            foreach (var node in source.Nodes)
                if (node.IsCutEndpoint) cutEndpoints.Add(node.Id);
            result = NetworkRebuilder.Rebuild(positions, edges, 0, removed, cutEndpoints);
            return true;
        }

        private static float MaximumTurn(Vector2 a, Vector2 control, Vector2 b, int segments)
        {
            float maximum = 0f;
            Vector2 previous = a;
            Vector2 current = Point(a, control, b, 1f / segments);
            for (int i = 2; i <= segments; i++)
            {
                Vector2 next = Point(a, control, b, (float)i / segments);
                maximum = Mathf.Max(maximum, Vector2.Angle(current - previous, next - current));
                previous = current;
                current = next;
            }
            return maximum;
        }

        private static Vector2 Point(Vector2 a, Vector2 control, Vector2 b, float t)
        {
            float inverse = 1f - t;
            return inverse * inverse * a + 2f * inverse * t * control + t * t * b;
        }
    }
}
