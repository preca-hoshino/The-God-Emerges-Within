using UnityEngine;
using Via.Minigame.Runtime;

namespace Via.Minigame.Presentation
{
    // Prototype renderer: all visuals read the same authoritative network and tips.
    public sealed class NeuronCanvasView : MonoBehaviour
    {
        [SerializeField] private MinigameController controller;
        [SerializeField] private NeuronToolController tools;
        [SerializeField, Min(1f)] private float visibleWorldHeight = 12f;
        public Rect CanvasRect => new Rect(24, 112, Mathf.Max(1, Screen.width - 48), Mathf.Max(1, Screen.height - 152));
        private float Scale => CanvasRect.height / visibleWorldHeight;
        public bool Contains(Vector2 point) => CanvasRect.Contains(point);
        public Vector2 ScreenToWorld(Vector2 point) => new Vector2(
            (point.x - CanvasRect.center.x) / Scale, (CanvasRect.center.y - point.y) / Scale);
        private Vector2 WorldToScreen(Vector2 point) => CanvasRect.center + new Vector2(point.x, -point.y) * Scale;

        private void OnGUI()
        {
            if (controller == null || !controller.IsOpen) return;
            GUI.Box(CanvasRect, GUIContent.none);
            bool preview = tools != null && tools.IsDragging && tools.SelectedTool == NeuronToolController.Tool.Pull && tools.PullPreview != null;
            var network = preview ? tools.PullPreview : controller.Network;
            foreach (var edge in network.Edges)
                Line(WorldToScreen(network.Nodes[edge.From].Position),
                    WorldToScreen(network.Nodes[edge.To].Position), new Color(0.3f, 0.9f, 0.9f), 3f);
            foreach (var point in controller.Growth.Attractors)
                Marker(WorldToScreen(point.Position), point.Consumed ? Color.gray : Color.yellow, 7f);
            if (preview)
            {
                foreach (var node in network.Nodes)
                    if ((node.Id != 0 && network.Degree(node.Id) == 1) || (node.Id == 0 && network.Edges.Count == 0))
                        Tip(WorldToScreen(node.Position));
            }
            else foreach (var tip in controller.Growth.Tips)
            {
                Vector2 center = WorldToScreen(tip.Position);
                Line(WorldToScreen(network.Nodes[tip.NodeId].Position), center, Color.cyan, 3f);
                Tip(center);
            }
            Marker(WorldToScreen(network.Nodes[0].Position), new Color(1f, 0.5f, 0.3f), 13f);
            if (tools != null && tools.SelectedTool == NeuronToolController.Tool.Pull)
            {
                foreach (var node in network.Nodes)
                    if (network.CanMove(node.Id)) Marker(WorldToScreen(node.Position), Color.magenta, 8f);
                if (tools.IsDragging) Marker(WorldToScreen(tools.PreviewPosition), Color.yellow, 12f);
            }
        }

        private void Tip(Vector2 center)
        {
            float radius = controller.Configuration.SearchRadius * Scale;
            for (int i = 0; i < 64; i += 2)
            {
                float a = i * Mathf.PI * 2 / 64, b = (i + 1) * Mathf.PI * 2 / 64;
                Line(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, new Color(240f / 255f, 204f / 255f, 116f / 255f), 5f);
            }
            Marker(center, Color.green, 9f);
        }

        private static void Marker(Vector2 center, Color color, float size)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - size / 2, center.y - size / 2, size, size), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void Line(Vector2 start, Vector2 end, Color color, float width)
        {
            Matrix4x4 matrix = GUI.matrix;
            Color previous = GUI.color;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg, start);
            GUI.DrawTexture(new Rect(start.x, start.y - width / 2, Vector2.Distance(start, end), width), Texture2D.whiteTexture);
            GUI.matrix = matrix;
            GUI.color = previous;
        }
    }
}
