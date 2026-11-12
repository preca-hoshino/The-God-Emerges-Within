using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Via.Minigame.Domain
{
    public sealed class NeuronNode
    {
        public int Id { get; }
        public Vector2 Position { get; internal set; }
        public bool IsCutEndpoint { get; internal set; }
        internal NeuronNode(int id, Vector2 position) { Id = id; Position = position; }
    }

    public readonly struct NeuronEdge
    {
        public int From { get; }
        public int To { get; }
        public NeuronEdge(int from, int to) { From = from; To = to; }
    }

    public sealed class NeuronNetwork
    {
        private readonly List<NeuronNode> nodes = new List<NeuronNode>();
        private readonly List<NeuronEdge> edges = new List<NeuronEdge>();
        public ReadOnlyCollection<NeuronNode> Nodes { get; }
        public ReadOnlyCollection<NeuronEdge> Edges { get; }
        public bool HasCutEndpoints => nodes.Exists(node => node.IsCutEndpoint);

        public NeuronNetwork(Vector2 origin)
        {
            Nodes = nodes.AsReadOnly();
            Edges = edges.AsReadOnly();
            nodes.Add(new NeuronNode(0, origin));
        }

        public int Connect(int source, Vector2 position, float joinDistance)
        {
            int destination = -1;
            float best = joinDistance * joinDistance;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (i == source) continue;
                float distance = (nodes[i].Position - position).sqrMagnitude;
                if (distance <= best) { best = distance; destination = i; }
            }
            if (destination < 0) destination = AddNode(position);
            AddEdge(source, destination);
            return destination;
        }

        // Construction API for initial level structures and verification; contains no gameplay rules.
        public int AddNode(Vector2 position)
        {
            int id = nodes.Count;
            nodes.Add(new NeuronNode(id, position));
            return id;
        }

        public bool AddEdge(int from, int to)
        {
            if (from == to || from < 0 || to < 0 || from >= nodes.Count || to >= nodes.Count) return false;
            foreach (NeuronEdge edge in edges)
                if ((edge.From == from && edge.To == to) || (edge.From == to && edge.To == from)) return false;
            edges.Add(new NeuronEdge(from, to));
            return true;
        }

        public int Degree(int node)
        {
            int count = 0;
            foreach (NeuronEdge edge in edges)
                if (edge.From == node || edge.To == node) count++;
            return count;
        }

        public NeuronNetwork Copy()
        {
            var copy = new NeuronNetwork(nodes[0].Position);
            for (int i = 1; i < nodes.Count; i++) copy.nodes.Add(new NeuronNode(i, nodes[i].Position));
            copy.edges.AddRange(edges);
            for (int i = 0; i < nodes.Count; i++) copy.nodes[i].IsCutEndpoint = nodes[i].IsCutEndpoint;
            return copy;
        }

        public bool CanMove(int node) => node > 0 && node < nodes.Count &&
            (Degree(node) == 2 || (Degree(node) == 1 && nodes[node].IsCutEndpoint));

        public bool MoveNode(int node, Vector2 position, float minimumSeparation)
        {
            if (!CanMove(node) || !float.IsFinite(position.x) || !float.IsFinite(position.y)) return false;
            foreach (var edge in edges)
            {
                int other = edge.From == node ? edge.To : edge.To == node ? edge.From : -1;
                if (other >= 0 && Vector2.Distance(nodes[other].Position, position) < minimumSeparation) return false;
            }
            nodes[node].Position = position;
            return true;
        }

        public int SplitEdge(int edgeIndex, Vector2 position)
        {
            var edge = edges[edgeIndex];
            int node = nodes.Count;
            nodes.Add(new NeuronNode(node, position));
            edges[edgeIndex] = new NeuronEdge(edge.From, node);
            edges.Add(new NeuronEdge(node, edge.To));
            return node;
        }
    }
}
