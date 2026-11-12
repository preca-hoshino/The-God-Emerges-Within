using UnityEngine;

namespace Via.Minigame.Domain
{
    // Canonical drawings for candy samples. Each one is built so the paradigm rules accept it.
    public static class SamplePatterns
    {
        public static NeuronNetwork Create(string paradigmId)
        {
            switch (paradigmId)
            {
                case "y": return Y();
                case "eight": return Eight();
                case "triangle": return Loop(new Vector2(0f, 0f), new Vector2(2.2f, 0f), new Vector2(1.1f, 1.9f));
                case "polygon": return Loop(new Vector2(0f, 0f), new Vector2(2f, 0f), new Vector2(2.4f, 1.3f), new Vector2(1f, 2.1f), new Vector2(-0.4f, 1.3f));
                case "polyline": return Path(new Vector2(0f, 0f), new Vector2(1f, 1.5f), new Vector2(2.4f, 0.2f));
                case "straight": return Path(Vector2.zero, new Vector2(2.4f, 0f));
                case "x": return TwoParts(new Vector2(-1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f));
                case "separated": return TwoParts(new Vector2(-2f, 0.6f), new Vector2(-0.5f, -0.6f), new Vector2(0.5f, 0.6f), new Vector2(2f, -0.6f));
                case "double_line": return TwoParts(new Vector2(-1.5f, -0.5f), new Vector2(1.5f, -0.5f), new Vector2(-1.5f, 0.5f), new Vector2(1.5f, 0.5f));
                case "arc": return Arc();
                case "s": return Curve(0.2f, 2f * Mathf.PI - 0.2f, 48);
                case "wave": return Curve(0.2f, 3f * Mathf.PI - 0.2f, 72);
                default: return null;
            }
        }

        public static NeuronNetwork ArcDrill() =>
            Path(new Vector2(0f, 0f), new Vector2(1.6f, 0f), new Vector2(1.6f, 1.6f));

        private static NeuronNetwork Y()
        {
            var network = new NeuronNetwork(Vector2.zero);
            network.AddEdge(0, network.AddNode(new Vector2(0f, 1.7f)));
            network.AddEdge(0, network.AddNode(new Vector2(1.4f, -1.1f)));
            network.AddEdge(0, network.AddNode(new Vector2(-1.4f, -1.1f)));
            return network;
        }

        private static NeuronNetwork Eight()
        {
            var network = new NeuronNetwork(Vector2.zero);
            int rightA = network.AddNode(new Vector2(1.2f, 0.9f));
            int rightEnd = network.AddNode(new Vector2(2.4f, 0f));
            int rightB = network.AddNode(new Vector2(1.2f, -0.9f));
            int leftA = network.AddNode(new Vector2(-1.2f, 0.9f));
            int leftEnd = network.AddNode(new Vector2(-2.4f, 0f));
            int leftB = network.AddNode(new Vector2(-1.2f, -0.9f));
            network.AddEdge(0, rightA);
            network.AddEdge(rightA, rightEnd);
            network.AddEdge(rightEnd, rightB);
            network.AddEdge(rightB, 0);
            network.AddEdge(0, leftA);
            network.AddEdge(leftA, leftEnd);
            network.AddEdge(leftEnd, leftB);
            network.AddEdge(leftB, 0);
            return network;
        }

        private static NeuronNetwork Arc()
        {
            PathReshape.Apply(ArcDrill(), 1, 6, out var network);
            return network;
        }

        private static NeuronNetwork TwoParts(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            var network = Path(a, b);
            int first = network.AddNode(c);
            network.AddEdge(first, network.AddNode(d));
            return network;
        }

        private static NeuronNetwork Curve(float start, float end, int steps)
        {
            var network = new NeuronNetwork(new Vector2(start, Mathf.Sin(start)));
            int previous = 0;
            for (int i = 1; i <= steps; i++)
            {
                float x = start + (end - start) * i / steps;
                int node = network.AddNode(new Vector2(x, Mathf.Sin(x)));
                network.AddEdge(previous, node);
                previous = node;
            }
            return network;
        }

        private static NeuronNetwork Path(params Vector2[] points)
        {
            var network = new NeuronNetwork(points[0]);
            int previous = 0;
            for (int i = 1; i < points.Length; i++)
            {
                int node = network.AddNode(points[i]);
                network.AddEdge(previous, node);
                previous = node;
            }
            return network;
        }

        private static NeuronNetwork Loop(params Vector2[] points)
        {
            var network = Path(points);
            network.AddEdge(points.Length - 1, 0);
            return network;
        }
    }
}
