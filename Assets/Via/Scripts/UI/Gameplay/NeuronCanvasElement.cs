using UnityEngine;
using UnityEngine.UIElements;
using Via.Minigame.Runtime;
using Via.Minigame.Presentation;

namespace Via.UI
{
    public sealed class NeuronCanvasElement : VisualElement
    {
        private readonly MinigameController controller;
        private readonly NeuronToolController tools;
        private readonly CanvasPointerInput input;
        private const float ArtLineWidth = 8f;
        private const float NodeSize = 54f;
        private const float TipSize = 87f;
        private const float OriginSize = 112f;
        private const float AttractorSize = 15f;

        private readonly Texture2D originIcon;
        private readonly Texture2D pointIcon;
        private readonly Texture2D lineIcon;
        private readonly Texture2D tipIcon;
        private bool HasArtwork => originIcon != null && pointIcon != null;
        private bool pressed;
        private int capturedPointerId = PointerId.invalidPointerId;
        private bool inputEnabled;
        public bool InputEnabled
        {
            get => inputEnabled;
            set
            {
                if (inputEnabled == value) return;
                inputEnabled = value;
                if (!value) CancelInput();
            }
        }
        private float Scale => contentRect.height / 12f;
        private Vector2 ToLocal(Vector2 world) => contentRect.center + new Vector2(world.x, -world.y) * Scale;
        private Vector2 ToWorld(Vector2 local) => new Vector2((local.x - contentRect.center.x) / Scale, (contentRect.center.y - local.y) / Scale);

        public NeuronCanvasElement(MinigameController controller, NeuronToolController tools, CanvasPointerInput input,
            Texture2D originIcon = null, Texture2D pointIcon = null, Texture2D lineIcon = null, Texture2D tipIcon = null)
        {
            this.controller = controller; this.tools = tools; this.input = input;
            this.originIcon = originIcon; this.pointIcon = pointIcon;
            this.lineIcon = lineIcon; this.tipIcon = tipIcon;
            style.flexGrow = 1;
            generateVisualContent += Draw;
            RegisterCallback<PointerDownEvent>(e =>
            {
                if (!InputEnabled || !controller.CanEdit || e.button != 0 || pressed) return;
                pressed = true;
                capturedPointerId = e.pointerId;
                this.CapturePointer(e.pointerId);
                input.SubmitPress(ToWorld(e.localPosition));
                e.StopPropagation();
            });
            RegisterCallback<PointerMoveEvent>(e =>
            {
                if (pressed && InputEnabled && e.pointerId == capturedPointerId)
                    input.SubmitDrag(ToWorld(e.localPosition));
            });
            RegisterCallback<PointerUpEvent>(e =>
            {
                if (!pressed || e.pointerId != capturedPointerId) return;
                try
                {
                    if (InputEnabled && controller.CanEdit) input.SubmitRelease(ToWorld(e.localPosition));
                }
                finally { CancelInput(); }
            });
            RegisterCallback<PointerCaptureOutEvent>(e =>
            {
                if (e.pointerId == capturedPointerId) CancelInput();
            });
            RegisterCallback<PointerCancelEvent>(e =>
            {
                if (e.pointerId == capturedPointerId) CancelInput();
            });
            RegisterCallback<DetachFromPanelEvent>(e => CancelInput());
        }

        public void CancelInput()
        {
            int pointerId = capturedPointerId;
            // A tutorial can finish on PointerDown (e.g. reshape), before PointerUp.
            // Release the capture as well as the press; otherwise that later release
            // is ignored and the completion dialog keeps losing clicks to this canvas.
            pressed = false;
            capturedPointerId = PointerId.invalidPointerId;
            tools.CancelDrag();
            if (pointerId != PointerId.invalidPointerId && this.HasPointerCapture(pointerId))
                this.ReleasePointer(pointerId);
        }

        private void Draw(MeshGenerationContext context)
        {
            if (!controller.IsOpen || controller.Network == null || Scale <= 0) return;
            var painter = context.painter2D;
            bool preview = tools.IsDragging && tools.SelectedTool == NeuronToolController.Tool.Pull && tools.PullPreview != null;
            var network = preview ? tools.PullPreview : controller.Network;
            foreach (var edge in network.Edges)
            {
                Vector2 from = ToLocal(network.Nodes[edge.From].Position);
                Vector2 to = ToLocal(network.Nodes[edge.To].Position);
                if (lineIcon != null) Bar(context, lineIcon, from, to, ArtLineWidth);
                else Line(painter, from, to, HasArtwork ? Color.white : new Color(0.35f, 0.87f, 0.81f), HasArtwork ? 6f : 3f);
            }
            foreach (var point in controller.Growth.Attractors)
            {
                Vector2 center = ToLocal(point.Position);
                if (pointIcon != null)
                    Icon(context, pointIcon, center, AttractorSize,
                        point.Consumed ? new Color(0.62f, 0.68f, 0.71f, 0.7f) : new Color(1f, 0.85f, 0.45f));
                else Circle(painter, center, 6, point.Consumed ? new Color(0.34f, 0.42f, 0.47f) : new Color(1f, 0.78f, 0.3f));
            }

            foreach (var zone in controller.Growth.BranchZones)
            {
                Vector2 center = ToLocal(zone.Position);
                float zoneRadius = zone.Radius * Scale;
                for (int i = 0; i < 72; i += 2)
                {
                    float a = i * Mathf.PI * 2 / 72, b = (i + 1) * Mathf.PI * 2 / 72;
                    Line(painter, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * zoneRadius,
                        center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * zoneRadius, new Color(1f, 0.62f, 0.24f), 2f);
                }
            }
            if (preview)
            {
                foreach (var node in network.Nodes)
                {
                    if ((node.Id != 0 && network.Degree(node.Id) == 1) || (node.Id == 0 && network.Edges.Count == 0))
                        Tip(context, painter, ToLocal(node.Position));
                }
            }
            else foreach (var tip in controller.Growth.Tips)
            {
                Vector2 parent = ToLocal(network.Nodes[tip.NodeId].Position);
                Vector2 center = ToLocal(tip.Position);
                if (lineIcon != null) Bar(context, lineIcon, parent, center, ArtLineWidth);
                else Line(painter, parent, center, Color.cyan, 3);
                Tip(context, painter, center);
            }
            if (HasArtwork)
            {
                // UI_Icon_Point already carries both the hollow ring and the solid core, so one
                // quad per junction is enough. Endpoints are left to Tip(), which draws the
                // authored growth diamond instead of stacking a second marker on the same spot.
                foreach (var node in network.Nodes)
                {
                    if (node.Id == 0 || network.Degree(node.Id) == 1) continue;
                    Icon(context, pointIcon, ToLocal(node.Position), NodeSize);
                }
                Icon(context, originIcon, ToLocal(network.Nodes[0].Position), OriginSize);
            }
            else Circle(painter, ToLocal(network.Nodes[0].Position), 10, new Color(1f, 0.48f, 0.27f));

            if (tools.SelectedTool == NeuronToolController.Tool.Cut)
            {
                if (tools.IsDragging) Line(painter, ToLocal(tools.CutStart), ToLocal(tools.PreviewPosition), new Color(1f, 0.35f, 0.35f), 2f);
                return;
            }
            if (tools.SelectedTool != NeuronToolController.Tool.Pull && tools.SelectedTool != NeuronToolController.Tool.Reshape) return;
            foreach (var node in network.Nodes)
                if (network.CanMove(node.Id) && (tools.SelectedTool != NeuronToolController.Tool.Reshape || network.Degree(node.Id) == 2))
                    Circle(painter, ToLocal(node.Position), 5, new Color(0.85f, 0.5f, 1f));
            if (tools.IsDragging && tools.SelectedTool == NeuronToolController.Tool.Pull)
                Circle(painter, ToLocal(tools.PreviewPosition), 8, Color.yellow);
        }
        private void Tip(MeshGenerationContext context, Painter2D painter, Vector2 center)
        {
            float radius = controller.Configuration.SearchRadius * Scale;
            for (int i = 0; i < 72; i += 2)
            {
                float a = i * Mathf.PI * 2 / 72, b = (i + 1) * Mathf.PI * 2 / 72;
                // Same gold as .sample-symbol in GameUI.uss (rgb(240, 204, 116)) so the ring
                // reads as teaching UI; thick enough to survive the authored artwork behind it.
                Line(painter, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, new Color(240f / 255f, 204f / 255f, 116f / 255f), 5f);
            }
            if (tipIcon != null) Icon(context, tipIcon, center, TipSize);
            else if (pointIcon != null) Icon(context, pointIcon, center, NodeSize);
            else Circle(painter, center, HasArtwork ? 10f : 7f, HasArtwork ? Color.white : new Color(0.4f, 1f, 0.7f));
        }
        // The authored fiber is a tall strip: keep its thickness and stretch it along the
        // segment. The taper baked into the artwork softens both ends.
        private static void Bar(MeshGenerationContext context, Texture2D texture, Vector2 from, Vector2 to, float width)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length <= 0.01f) return;
            Vector2 along = delta / length;
            Vector2 across = new Vector2(-along.y, along.x) * (width * 0.5f);
            float z = Vertex.nearZ;
            var mesh = context.Allocate(4, 6, texture);
            // Same clockwise winding as Icon(); the reversed order was culled and the fiber never showed.
            mesh.SetNextVertex(new Vertex { position = new Vector3(from.x - across.x, from.y - across.y, z), tint = Color.white, uv = new Vector2(0, 0) });
            mesh.SetNextVertex(new Vertex { position = new Vector3(to.x - across.x, to.y - across.y, z), tint = Color.white, uv = new Vector2(0, 1) });
            mesh.SetNextVertex(new Vertex { position = new Vector3(to.x + across.x, to.y + across.y, z), tint = Color.white, uv = new Vector2(1, 1) });
            mesh.SetNextVertex(new Vertex { position = new Vector3(from.x + across.x, from.y + across.y, z), tint = Color.white, uv = new Vector2(1, 0) });
            mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2);
            mesh.SetNextIndex(2); mesh.SetNextIndex(3); mesh.SetNextIndex(0);
        }
        private static void Icon(MeshGenerationContext context, Texture2D texture, Vector2 center, float size, Color? tint = null)
        {
            float width = size, height = size * texture.height / texture.width;
            float x = center.x - width * 0.5f, y = center.y - height * 0.5f;
            Color color = tint ?? Color.white;
            var mesh = context.Allocate(4, 6, texture);
            mesh.SetNextVertex(new Vertex { position = new Vector3(x, y, Vertex.nearZ), tint = color, uv = new Vector2(0, 1) });
            mesh.SetNextVertex(new Vertex { position = new Vector3(x+width, y, Vertex.nearZ), tint = color, uv = new Vector2(1, 1) });
            mesh.SetNextVertex(new Vertex { position = new Vector3(x+width, y+height, Vertex.nearZ), tint = color, uv = new Vector2(1, 0) });
            mesh.SetNextVertex(new Vertex { position = new Vector3(x, y+height, Vertex.nearZ), tint = color, uv = new Vector2(0, 0) });
            mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2);
            mesh.SetNextIndex(2); mesh.SetNextIndex(3); mesh.SetNextIndex(0);
        }
        private static void Line(Painter2D p, Vector2 from, Vector2 to, Color color, float width)
        { p.strokeColor = color; p.lineWidth = width; p.BeginPath(); p.MoveTo(from); p.LineTo(to); p.Stroke(); }
        private static void Circle(Painter2D p, Vector2 position, float radius, Color color)
        { p.fillColor = color; p.BeginPath(); p.Arc(position, radius, 0, 360); p.Fill(); }
    }
}
