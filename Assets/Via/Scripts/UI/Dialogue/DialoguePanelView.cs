using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Via.UI
{
    /// <summary>Dialogue text binding and border-only rendering of the artist's supplied frame.</summary>
    public sealed class DialoguePanelView : IDisposable
    {
        private readonly VisualElement frame;
        private readonly VisualElement speakerPanel;
        private readonly Label speaker;
        private readonly Label body;
        private readonly ScrollView scroll;
        private readonly Texture2D frameTexture;
        private string currentId;
        private string currentText;

        public DialoguePanelView(VisualElement root, Texture2D frameTexture)
        {
            this.frameTexture = frameTexture != null ? frameTexture : throw new ArgumentNullException(nameof(frameTexture));
            frame = Require<VisualElement>(root, "dialogue-frame");
            speakerPanel = Require<VisualElement>(root, "speaker-panel");
            speaker = Require<Label>(root, "speaker");
            body = Require<Label>(root, "dialogue-text");
            scroll = Require<ScrollView>(root, "dialogue-scroll");
            frame.generateVisualContent += DrawFrame;
        }

        public void SetText(string dialogueId, string speakerName, string text)
        {
            speaker.text = string.IsNullOrWhiteSpace(speakerName) ? string.Empty : "【" + speakerName + "】";
            speakerPanel.style.display = string.IsNullOrWhiteSpace(speakerName) ? DisplayStyle.None : DisplayStyle.Flex;
            text = text ?? string.Empty;
            if (currentId == dialogueId && currentText == text) return;
            currentId = dialogueId;
            currentText = text;
            body.text = text;
            scroll.scrollOffset = Vector2.zero;
        }

        // Keep the existing artwork and font size. Both the panel and arrow reveal the rest of a long
        // line before its final activation is forwarded to the story flow.
        public bool TryShowNextPage()
        {
            float bottom = Mathf.Max(0f, scroll.contentContainer.layout.height - scroll.contentViewport.layout.height);
            float offset = scroll.scrollOffset.y;
            if (offset >= bottom - 1f) return false;
            float step = Mathf.Max(1f, scroll.contentViewport.layout.height - body.resolvedStyle.fontSize * 1.5f);
            scroll.scrollOffset = new Vector2(0f, Mathf.Min(bottom, offset + step));
            return true;
        }

        private static T Require<T>(VisualElement root, string name) where T : VisualElement
        {
            T element = root.Q<T>(name);
            return element ?? throw new InvalidOperationException("Dialogue element is missing: " + name);
        }

        private void DrawFrame(MeshGenerationContext context)
        {
            Rect bounds = frame.contentRect;
            if (bounds.width <= 0f || bounds.height <= 0f) return;
            float sourceWidth = frameTexture.width, sourceHeight = frameTexture.height;
            // The PNG has an opaque gradient in its middle. Draw only its actual border strips,
            // keeping the centre transparent so the existing story illustration shows through.
            const float corner = 100f, top = 40f, bottom = 32f, side = 24f;
            float scale = Mathf.Min(1f, Mathf.Min(bounds.width / sourceWidth, bounds.height / sourceHeight));
            float c = corner * scale, t = top * scale, b = bottom * scale, edge = side * scale;
            MeshWriteData mesh = context.Allocate(32, 48, frameTexture);
            int vertex = 0;
            Quad(mesh, ref vertex, new Rect(0,0,c,t), new Rect(0,0,corner,top));
            Quad(mesh, ref vertex, new Rect(c,0,bounds.width-2*c,t), new Rect(corner,0,sourceWidth-2*corner,top));
            Quad(mesh, ref vertex, new Rect(bounds.width-c,0,c,t), new Rect(sourceWidth-corner,0,corner,top));
            Quad(mesh, ref vertex, new Rect(0,t,edge,bounds.height-t-b), new Rect(0,top,side,sourceHeight-top-bottom));
            Quad(mesh, ref vertex, new Rect(bounds.width-edge,t,edge,bounds.height-t-b), new Rect(sourceWidth-side,top,side,sourceHeight-top-bottom));
            Quad(mesh, ref vertex, new Rect(0,bounds.height-b,c,b), new Rect(0,sourceHeight-bottom,corner,bottom));
            Quad(mesh, ref vertex, new Rect(c,bounds.height-b,bounds.width-2*c,b), new Rect(corner,sourceHeight-bottom,sourceWidth-2*corner,bottom));
            Quad(mesh, ref vertex, new Rect(bounds.width-c,bounds.height-b,c,b), new Rect(sourceWidth-corner,sourceHeight-bottom,corner,bottom));
        }

        private void Quad(MeshWriteData mesh, ref int vertex, Rect target, Rect source)
        {
            // Unity 6 remaps texture coordinates into its atlas automatically.
            float u0 = source.xMin / frameTexture.width;
            float u1 = source.xMax / frameTexture.width;
            float v0 = 1f - source.yMin / frameTexture.height;
            float v1 = 1f - source.yMax / frameTexture.height;
            mesh.SetNextVertex(new Vertex { position = new Vector3(target.xMin,target.yMin,Vertex.nearZ), tint = Color.white, uv = new Vector2(u0,v0) });
            mesh.SetNextVertex(new Vertex { position = new Vector3(target.xMax,target.yMin,Vertex.nearZ), tint = Color.white, uv = new Vector2(u1,v0) });
            mesh.SetNextVertex(new Vertex { position = new Vector3(target.xMax,target.yMax,Vertex.nearZ), tint = Color.white, uv = new Vector2(u1,v1) });
            mesh.SetNextVertex(new Vertex { position = new Vector3(target.xMin,target.yMax,Vertex.nearZ), tint = Color.white, uv = new Vector2(u0,v1) });
            mesh.SetNextIndex((ushort)vertex); mesh.SetNextIndex((ushort)(vertex+1)); mesh.SetNextIndex((ushort)(vertex+2));
            mesh.SetNextIndex((ushort)(vertex+2)); mesh.SetNextIndex((ushort)(vertex+3)); mesh.SetNextIndex((ushort)vertex);
            vertex += 4;
        }

        public void Dispose() => frame.generateVisualContent -= DrawFrame;
    }
}
