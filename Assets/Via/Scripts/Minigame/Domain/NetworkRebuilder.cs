using System.Collections.Generic;
using UnityEngine;

namespace Via.Minigame.Domain
{
    public readonly struct EdgePair
    {
        public int A { get; }
        public int B { get; }
        public EdgePair(int a, int b) { A = a; B = b; }
    }

    // Rebuilds a network from a node/edge description, dropping removed nodes and renumbering so the origin stays node 0.
    public static class NetworkRebuilder
    {
        public static NeuronNetwork Rebuild(IReadOnlyList<Vector2> positions, IReadOnlyList<EdgePair> edges,
            int originIndex, ICollection<int> removed, ICollection<int> cutEndpoints = null)
        {
            var map = new int[positions.Count];
            for (int i = 0; i < map.Length; i++) map[i] = -1;

            var order = new List<int>();
            if (removed == null || !removed.Contains(originIndex)) order.Add(originIndex);
            for (int i = 0; i < positions.Count; i++)
                if (i != originIndex && (removed == null || !removed.Contains(i))) order.Add(i);

            if (order.Count == 0) return new NeuronNetwork(Vector2.zero);
            var network = new NeuronNetwork(positions[order[0]]);
            map[order[0]] = 0;
            for (int i = 1; i < order.Count; i++)
            {
                network.AddNode(positions[order[i]]);
                map[order[i]] = i;
            }
            if (cutEndpoints != null)
                foreach (int original in cutEndpoints)
                    if (original >= 0 && original < map.Length && map[original] >= 0)
                        network.Nodes[map[original]].IsCutEndpoint = true;

            for (int i = 0; i < edges.Count; i++)
            {
                int a = map[edges[i].A];
                int b = map[edges[i].B];
                if (a < 0 || b < 0) continue;
                network.AddEdge(a, b);
            }
            return network;
        }
    }
}
