using System.Collections.Generic;
using UnityEngine;

namespace Via.Minigame.Domain
{
    public readonly struct PullSelection
    {
        public int NodeId { get; }
        public int EdgeIndex { get; }
        public Vector2 Position { get; }
        public bool IsValid => NodeId >= 0 || EdgeIndex >= 0;
        public PullSelection(int node, int edge, Vector2 position)
        { NodeId = node; EdgeIndex = edge; Position = position; }
    }

    public static class PathPull
    {
        public static PullSelection Select(NeuronNetwork network, Vector2 position, float tolerance)
        {
            if (network == null || !float.IsFinite(position.x) || !float.IsFinite(position.y) || tolerance < 0f)
                return new PullSelection(-1, -1, position);
            int nearestNode = -1;
            float nearestDistance = tolerance;
            foreach (var node in network.Nodes)
            {
                float distance = Vector2.Distance(node.Position, position);
                if (distance <= nearestDistance && (nearestNode < 0 || distance < nearestDistance))
                { nearestNode = node.Id; nearestDistance = distance; }
            }
            if (nearestNode >= 0)
                return network.CanMove(nearestNode) ? new PullSelection(nearestNode, -1, network.Nodes[nearestNode].Position) :
                    new PullSelection(-1, -1, position);
            float nearest = tolerance;
            var selection = new PullSelection(-1, -1, position);
            for (int i = 0; i < network.Edges.Count; i++)
            {
                var edge = network.Edges[i];
                Vector2 start = network.Nodes[edge.From].Position;
                Vector2 end = network.Nodes[edge.To].Position;
                Vector2 delta = end - start;
                if (delta.sqrMagnitude < 0.0001f) continue;
                float t = Vector2.Dot(position - start, delta) / delta.sqrMagnitude;
                if (t <= 0f || t >= 1f) continue;
                Vector2 projected = start + t * delta;
                if (Vector2.Distance(start, projected) < tolerance || Vector2.Distance(end, projected) < tolerance) continue;
                float distance = Vector2.Distance(projected, position);
                if (distance <= nearest) { nearest = distance; selection = new PullSelection(-1, i, projected); }
            }
            return selection;
        }

        // Produces the same edit for previews and commits; the source remains available for undo.
        public static bool Apply(NeuronNetwork source, PullSelection selection, Vector2 destination, float joinDistance,
            out NeuronNetwork result, out int movedNode)
        {
            result = source;
            movedNode = -1;
            if (source == null || !selection.IsValid || !float.IsFinite(destination.x) || !float.IsFinite(destination.y) ||
                !float.IsFinite(selection.Position.x) || !float.IsFinite(selection.Position.y) ||
                !float.IsFinite(joinDistance) || joinDistance <= 0f)
                return false;
            var trial = source.Copy();
            int node = selection.NodeId;
            if (node < 0)
            {
                if (selection.EdgeIndex < 0 || selection.EdgeIndex >= trial.Edges.Count) return false;
                node = trial.SplitEdge(selection.EdgeIndex, selection.Position);
            }
            if (!trial.CanMove(node)) return false;
            int target = FindJoinTarget(trial, node, destination, joinDistance);
            if (target >= 0)
            {
                var positions = new List<Vector2>();
                var cutEndpoints = new HashSet<int>();
                foreach (var item in trial.Nodes)
                {
                    positions.Add(item.Position);
                    if (item.IsCutEndpoint && item.Id != node && item.Id != target) cutEndpoints.Add(item.Id);
                }
                var edges = new List<EdgePair>();
                foreach (var edge in trial.Edges)
                    edges.Add(new EdgePair(edge.From == node ? target : edge.From, edge.To == node ? target : edge.To));
                result = NetworkRebuilder.Rebuild(positions, edges, 0, new HashSet<int> { node }, cutEndpoints);
                movedNode = target > node ? target - 1 : target;
                return true;
            }
            if (Vector2.Distance(selection.Position, destination) < 0.01f || !trial.MoveNode(node, destination, joinDistance)) return false;
            result = trial;
            movedNode = node;
            return true;
        }

        private static int FindJoinTarget(NeuronNetwork network, int node, Vector2 destination, float radius)
        {
            if (!network.Nodes[node].IsCutEndpoint) return -1;
            int target = -1;
            float nearest = radius * radius;
            foreach (var other in network.Nodes)
            {
                if (other.Id == node || (other.Id != 0 && network.Degree(other.Id) == 0)) continue;
                bool adjacent = false;
                foreach (var edge in network.Edges)
                    if ((edge.From == node && edge.To == other.Id) || (edge.To == node && edge.From == other.Id))
                    { adjacent = true; break; }
                // A short bridge between two cut points can also be welded into one real node.
                if (adjacent && !other.IsCutEndpoint) continue;
                float distance = (other.Position - destination).sqrMagnitude;
                float previous = (other.Position - network.Nodes[node].Position).sqrMagnitude;
                // Fresh cuts are already within the join radius. Moving away must not weld them back.
                if (distance > nearest || (previous <= radius * radius && distance >= previous)) continue;
                if (target < 0 || distance < nearest) { nearest = distance; target = other.Id; }
            }
            return target;
        }
    }
}
