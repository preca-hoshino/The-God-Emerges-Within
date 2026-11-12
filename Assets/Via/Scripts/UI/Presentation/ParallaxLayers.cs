using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Via.UI
{
    [Serializable]
    public sealed class ParallaxLayer
    {
        public string name;
        public Texture2D[] frames = Array.Empty<Texture2D>();
        [Range(0f, 1f)] public float depth = 0.5f;
    }

    // Multi-layer static-illustration parallax. Layers are added bottom-first (list order = z-order);
    // depth 0 = far (small movement), 1 = near (large movement). Click advances every layer's frames.
    // Falls back to the legacy near/far fields when no layers are configured.
    [ExecuteAlways]
    public sealed class ParallaxLayers : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private ParallaxLayer[] layers = Array.Empty<ParallaxLayer>();

        [Header("Legacy two-layer")]
        [SerializeField] private Texture2D nearTexture;
        [SerializeField] private Texture2D[] farFrames;

        [Header("Motion")]
        [SerializeField, Min(0f)] private float maxAmplitude = 46f;
        [SerializeField, Min(0f)] private float baseFrequency = 0.55f;
        [SerializeField, Range(0f, 1f)] private float driftAmount = 0.35f;
        [SerializeField, Min(0f)] private float oversize = 120f;
        [SerializeField] private bool fitWholeImage;

        private VisualElement[] elements;
        private int[] frameIndex;
        private int[] frameCount;

        private void OnEnable() { Build(); }
        private void OnValidate() { if (isActiveAndEnabled) { elements = null; Build(); } }

        private void Build()
        {
            if (document == null) return;
            var root = document.rootVisualElement;
            if (root == null) return;

            var configs = CollectConfigs();
            if (configs == null || configs.Length == 0) return;
            if (elements != null && elements.Length == configs.Length && elements[0] != null &&
                elements[0].parent == root) return;

            for (int i = root.childCount - 1; i >= 0; i--)
                if (root[i].name != null && root[i].name.StartsWith("pl-")) root.RemoveAt(i);

            elements = new VisualElement[configs.Length];
            frameIndex = new int[configs.Length];
            frameCount = new int[configs.Length];
            for (int i = 0; i < configs.Length; i++)
            {
                var e = new VisualElement { name = "pl-" + i, pickingMode = PickingMode.Ignore };
                e.style.position = Position.Absolute;
                e.style.left = -oversize; e.style.right = -oversize;
                e.style.top = -oversize; e.style.bottom = -oversize;
                e.style.backgroundSize = new BackgroundSize(fitWholeImage ? BackgroundSizeType.Contain : BackgroundSizeType.Cover);
                var frames = configs[i].frames;
                if (frames != null && frames.Length > 0) e.style.backgroundImage = new StyleBackground(frames[0]);
                root.Add(e);
                elements[i] = e;
                frameIndex[i] = 0;
                frameCount[i] = frames == null ? 0 : frames.Length;
            }
        }

        private ParallaxLayer[] CollectConfigs()
        {
            if (layers != null && layers.Length > 0) return layers;
            if (farFrames == null && nearTexture == null) return null;
            return new[]
            {
                new ParallaxLayer { name = "far", frames = farFrames, depth = 0.15f },
                new ParallaxLayer { name = "near", frames = new[] { nearTexture }, depth = 1f }
            };
        }

        private void Update()
        {
            if (!UnityEngine.Application.isPlaying || elements == null) return;
            var configs = CollectConfigs();
            if (configs == null) return;

            float px = 0f, py = 0f;
            if (Screen.width > 0 && Screen.height > 0)
            {
                px = (Input.mousePosition.x / Screen.width - 0.5f) * 2f;
                py = (Input.mousePosition.y / Screen.height - 0.5f) * 2f;
            }
            float t = Time.time;
            for (int i = 0; i < elements.Length && i < configs.Length; i++)
            {
                if (elements[i] == null) continue;
                float d = Mathf.Clamp01(configs[i].depth);
                float amp = maxAmplitude * d;
                float freq = baseFrequency * (0.5f + d);
                elements[i].style.translate = new Translate(
                    px * amp + Mathf.Sin(t * freq) * amp * driftAmount,
                    py * amp + Mathf.Cos(t * freq * 0.8f) * amp * driftAmount);
            }
            if (Input.GetMouseButtonDown(0)) AdvanceFrame();
        }

        public void AdvanceFrame()
        {
            if (elements == null) return;
            var configs = CollectConfigs();
            for (int i = 0; i < elements.Length; i++)
            {
                if (frameCount[i] < 2 || configs[i].frames == null) continue;
                frameIndex[i] = (frameIndex[i] + 1) % configs[i].frames.Length;
                elements[i].style.backgroundImage = new StyleBackground(configs[i].frames[frameIndex[i]]);
            }
        }
    }
}
