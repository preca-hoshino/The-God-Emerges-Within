using UnityEngine;
using UnityEngine.UIElements;
using Via.Minigame.Domain;

namespace Via.UI
{
    // Uses the same canonical network that is verified by the gameplay evaluator.
    public sealed class ParadigmDiagramElement : VisualElement
    {
        private static readonly Color Line = new Color(0.56f, 0.87f, 0.82f);
        private static readonly Color Junction = new Color(1f, 0.65f, 0.3f);
        private readonly NeuronNetwork sample;
        private readonly Color lineColor;
        private readonly Color junctionColor;

        public ParadigmDiagramElement(string paradigmId, bool gameplayPalette = false)
        {
            lineColor = gameplayPalette ? Color.white : Line;
            junctionColor = gameplayPalette ? new Color(1f, 0.12f, 0.06f) : Junction;
            sample = SamplePatterns.Create(paradigmId);
            pickingMode = PickingMode.Ignore;
            AddToClassList("diagram");
            style.width = 132;
            style.height = 76;
            style.flexShrink = 0;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            if (sample == null || contentRect.width < 1f || contentRect.height < 1f) return;
            Vector2 min = sample.Nodes[0].Position, max = min;
            foreach (var node in sample.Nodes)
            {
                min = Vector2.Min(min, node.Position);
                max = Vector2.Max(max, node.Position);
            }
            Vector2 extent = max - min;
            float scale = Mathf.Min((contentRect.width - 14f) / Mathf.Max(extent.x, 0.01f),
                (contentRect.height - 14f) / Mathf.Max(extent.y, 0.01f));
            Vector2 center = (min + max) * 0.5f;
            var painter = context.painter2D;
            painter.strokeColor = lineColor;
            painter.lineWidth = 2.5f;
            foreach (var edge in sample.Edges)
            {
                painter.BeginPath();
                painter.MoveTo(ToLocal(sample.Nodes[edge.From].Position, center, scale));
                painter.LineTo(ToLocal(sample.Nodes[edge.To].Position, center, scale));
                painter.Stroke();
            }
            foreach (var node in sample.Nodes)
            {
                int degree = sample.Degree(node.Id);
                if (degree != 1 && degree < 3) continue;
                painter.fillColor = degree >= 3 ? junctionColor : lineColor;
                painter.BeginPath();
                painter.Arc(ToLocal(node.Position, center, scale), degree >= 3 ? 4f : 3f, 0f, 360f);
                painter.Fill();
            }
        }

        private Vector2 ToLocal(Vector2 point, Vector2 center, float scale) =>
            contentRect.center + new Vector2(point.x - center.x, center.y - point.y) * scale;
    }
}
