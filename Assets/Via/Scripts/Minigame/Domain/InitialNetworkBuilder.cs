using System.Collections.Generic;
using UnityEngine;
using Via.Minigame.Configuration;

namespace Via.Minigame.Domain
{
    public static class InitialNetworkBuilder
    {
        public static NeuronNetwork Create(IReadOnlyList<InitialStroke> strokes, Vector2 fallbackOrigin)
        {
            Vector2 origin = strokes != null && strokes.Count > 0 && strokes[0] != null &&
                strokes[0].points != null && strokes[0].points.Length > 0 ? strokes[0].points[0] : fallbackOrigin;
            var network = new NeuronNetwork(origin);
            if (strokes == null) return network;
            foreach (var stroke in strokes)
            {
                if (stroke == null || stroke.points == null || stroke.points.Length == 0) continue;
                int previous = -1;
                for (int i = 0; i < stroke.points.Length; i++)
                {
                    Vector2 point = stroke.points[i];
                    int id = i == 0 && (point - origin).sqrMagnitude < 0.0001f ? 0 : network.AddNode(point);
                    if (previous >= 0) network.AddEdge(previous, id);
                    previous = id;
                }
            }
            return network;
        }
    }
}
