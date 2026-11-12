using System;
using Unity.Profiling;
using UnityEngine;

namespace Via.UI
{
    /// <summary>Owns the shared optical image; it never changes the authored texture or material.</summary>
    internal sealed class MicroscopeLensRenderer : IDisposable
    {
        private static readonly int DispersionScale = Shader.PropertyToID("_DispersionScale");
        private static readonly ProfilerMarker RenderMarker = new ProfilerMarker("Via.UI.MicroscopeLens.Render");
        private readonly Texture2D source;
        private readonly Material material;
        private double nextRenderAt;
        private bool disposed;
        public RenderTexture Texture { get; }

        public MicroscopeLensRenderer(Texture2D source, Material template)
        {
            this.source = source;
            material = new Material(template) { name = "Microscope lens (runtime)", hideFlags = HideFlags.HideAndDontSave };
            Texture = new RenderTexture(Mathf.Min(source.width, 1600), Mathf.Min(source.height, 1600),
                0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "Shared microscope optical field",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false
            };
            if (!Texture.Create())
            {
                Dispose();
                throw new InvalidOperationException("Could not create the microscope optical render target.");
            }
        }

        public void Render(double elapsed, bool force = false)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MicroscopeLensRenderer));
            // Rotation still updates every frame. Only the slow optical breathing needs this pass.
            if (!force && elapsed < nextRenderAt) return;
            nextRenderAt = elapsed + 0.1d;
            using (RenderMarker.Auto())
            {
                float phase = (float)(elapsed % 12d) * (Mathf.PI * 2f / 12f);
                material.SetFloat(DispersionScale, 1f + Mathf.Sin(phase) * 0.12f);
                RenderTexture previousTarget = RenderTexture.active;
                bool previousSrgb = GL.sRGBWrite;
                try
                {
                    GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                    Graphics.Blit(source, Texture, material);
                }
                finally
                {
                    RenderTexture.active = previousTarget;
                    GL.sRGBWrite = previousSrgb;
                }
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (Texture != null)
            {
                Texture.Release();
                UnityEngine.Object.Destroy(Texture);
            }
            if (material != null) UnityEngine.Object.Destroy(material);
        }
    }
}
