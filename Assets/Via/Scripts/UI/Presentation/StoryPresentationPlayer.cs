using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Via.Story.Configuration;
using Via.Story.Domain;

namespace Via.UI
{
    [Serializable]
    public sealed class StoryLayer
    {
        public string id;
        public Texture2D image;
        [Range(0f, 1f)] public float depth = 0.5f;
        // Full-canvas artwork shares an aspect-preserving crop with its companion layers,
        // including enough image outside the viewport for parallax on wider screens.
        public bool coverViewport;
        // Portrait props (doctors, characters) keep their aspect, anchor to the bottom-right corner and
        // can be scaled down so they sit inside the room instead of covering it.
        public bool fitHeight;
        public bool alignRight;
        [Min(0.05f)] public float scale = 1f;
    }

    // Plays Presentation.csv on named layers. Rows of the same step run in parallel; steps run in order.
    // Actions: show / hide / fadein / fadeout / scale (targetId = layer), fadeblack / fadewhite (full screen).
    // Layers are drawn in list order (first = back) and drift with mouse + time for parallax by depth.
    public sealed class StoryPresentationPlayer : MonoBehaviour, IPresentationPlayer
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private Texture2D staticNoise;
        [SerializeField] private StoryLayer[] layers = Array.Empty<StoryLayer>();
        [SerializeField] private bool fitWholeImage = true;

        [Header("Parallax")]
        [SerializeField, Min(0f)] private float parallaxAmplitude = 34f;
        [SerializeField, Min(0f)] private float parallaxFrequency = 0.5f;
        [SerializeField, Range(0f, 1f)] private float parallaxDrift = 0.35f;
        [SerializeField, Min(0f)] private float oversize = 90f;

        public event Action<string> Completed;

        // Story fast-forward compresses the running beat instead of cutting it: every step still
        // lands in its authored final state and the completion callback stays single-shot.
        private const float FastForwardRate = 60f;
        private bool fastForward;
        private float Rate => fastForward ? FastForwardRate : 1f;

        private readonly Dictionary<string, VisualElement> map = new Dictionary<string, VisualElement>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> depthMap = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> baseScaleMap = new Dictionary<string, float>(StringComparer.Ordinal);
        private VisualElement blackOverlay;
        private VisualElement glitchOverlay;
        private VisualElement whiteOverlay;
        private Coroutine running;
        private string currentId;

        private void OnEnable() { Build(); }
        private void OnValidate() { if (isActiveAndEnabled) { map.Clear(); Build(); } }

        private void Build()
        {
            if (document == null) return;
            var root = document.rootVisualElement;
            if (root == null || layers == null || layers.Length == 0) return;
            if (map.Count == layers.Length) return;

            // Layers sit below the story UI, drawn in list order (first = back, last = front).
            var container = root.Q("screen") ?? root;
            map.Clear();
            depthMap.Clear();
            baseScaleMap.Clear();
            int slot = 0;
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (layer == null || string.IsNullOrEmpty(layer.id)) continue;
                var e = new VisualElement { name = "layer-" + layer.id, pickingMode = PickingMode.Ignore };
                Stretch(e, oversize);
                e.style.backgroundSize = new BackgroundSize(layer.coverViewport || !fitWholeImage
                    ? BackgroundSizeType.Cover
                    : BackgroundSizeType.Contain);
                if (layer.coverViewport)
                {
                    e.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
                    e.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
                }
                if (layer.fitHeight)
                {
                    // Keep the artwork's aspect and pin it to the corner so the room stays readable.
                    // The element must stay full-screen: an auto width collapses to zero in UI Toolkit
                    // and the background image then has no surface to paint on.
                    Stretch(e, 0f);
                    e.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                    e.style.backgroundPositionX = new BackgroundPosition(layer.alignRight
                        ? BackgroundPositionKeyword.Right
                        : BackgroundPositionKeyword.Center);
                    e.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Bottom);
                }
                if (layer.image != null) e.style.backgroundImage = new StyleBackground(layer.image);
                float layerScale = Mathf.Clamp(layer.scale, 0.05f, 4f);
                if (!Mathf.Approximately(layerScale, 1f))
                {
                    // Scale around the bottom-right so a smaller prop stays anchored in the corner.
                    if (layer.alignRight) e.style.transformOrigin = new TransformOrigin(Length.Percent(100f), Length.Percent(100f));
                    e.style.scale = new Scale(new Vector3(layerScale, layerScale, 1f));
                }
                e.style.display = DisplayStyle.None;
                e.style.opacity = 0f;
                container.Insert(slot++, e);
                map[layer.id] = e;
                depthMap[layer.id] = Mathf.Clamp01(layer.depth);
                baseScaleMap[layer.id] = layerScale;
            }
            glitchOverlay = Overlay(container, "glitch-static", Color.white);
            if (staticNoise != null) glitchOverlay.style.backgroundImage = new StyleBackground(staticNoise);
            glitchOverlay.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            blackOverlay = Overlay(container, "fade-black", Color.black);
            whiteOverlay = Overlay(container, "fade-white", Color.white);
        }

        private void Update()
        {
            if (!UnityEngine.Application.isPlaying || map.Count == 0) return;
            float px = 0f, py = 0f;
            if (Screen.width > 0 && Screen.height > 0)
            {
                px = (Mathf.Clamp01(Input.mousePosition.x / Screen.width) - 0.5f) * 2f;
                py = (Mathf.Clamp01(Input.mousePosition.y / Screen.height) - 0.5f) * 2f;
            }
            float t = Time.time;
            foreach (var pair in map)
            {
                var e = pair.Value;
                if (e == null) continue;
                float d = depthMap.TryGetValue(pair.Key, out float depth) ? depth : 0.5f;
                float amp = parallaxAmplitude * d;
                float freq = parallaxFrequency * (0.5f + d);
                e.style.translate = new Translate(
                    px * amp + Mathf.Sin(t * freq) * amp * parallaxDrift,
                    py * amp + Mathf.Cos(t * freq * 0.8f) * amp * parallaxDrift);
            }
        }

        // Static/noise burst that frays into black: a scrambled full-screen grain, then a short
        // dissolve so the operator's switch reads as a malfunction rather than a clean cut.
        private IEnumerator Glitch(float duration)
        {
            if (glitchOverlay == null) yield break;
            duration = Mathf.Max(0.5f, duration);
            float burst = duration * 0.62f;
            float dissolve = duration - burst;

            glitchOverlay.style.display = DisplayStyle.Flex;
            if (blackOverlay != null) blackOverlay.style.display = DisplayStyle.None;
            float t = 0f;
            int seed = 7919;
            while (t < burst)
            {
                float falloff = 1f - t / burst;
                seed = unchecked(seed * 1103515245 + 12345);
                float r = ((seed >> 8) & 0xFFFF) / 65535f;
                glitchOverlay.style.opacity = (0.18f + r * 0.55f) * falloff;
                glitchOverlay.style.translate = new Translate((r - 0.5f) * 56f, (r - 0.5f) * 14f);
                t += 0.045f;
                yield return WaitFor(0.045f);
            }

            if (blackOverlay != null)
            {
                blackOverlay.style.display = DisplayStyle.Flex;
                blackOverlay.style.opacity = 0f;
            }
            float f = 0f;
            while (f < dissolve)
            {
                float k = f / dissolve;
                glitchOverlay.style.opacity = 0.32f * (1f - k);
                if (blackOverlay != null) blackOverlay.style.opacity = k;
                f += Time.deltaTime * Rate;
                yield return null;
            }

            glitchOverlay.style.display = DisplayStyle.None;
            glitchOverlay.style.opacity = 0f;
            glitchOverlay.style.translate = new Translate(0f, 0f);
            // The scene keeps its own dark backdrop; clear the black so the next beat can fade in.
            if (blackOverlay != null) { blackOverlay.style.display = DisplayStyle.None; blackOverlay.style.opacity = 0f; }
        }

        private static VisualElement Overlay(VisualElement root, string name, Color color)
        {
            var o = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            Stretch(o, 0f);
            o.style.backgroundColor = color;
            o.style.display = DisplayStyle.None;
            o.style.opacity = 0f;
            root.Add(o);
            return o;
        }

        private static void Stretch(VisualElement e, float inset)
        {
            e.style.position = Position.Absolute;
            e.style.left = -inset; e.style.right = -inset; e.style.top = -inset; e.style.bottom = -inset;
        }

        public void Play(string presentationId, IReadOnlyList<PresentationRow> steps)
        {
            if (document == null) { Completed?.Invoke(presentationId); return; }
            if (running != null) StopCoroutine(running);
            fastForward = false;
            currentId = presentationId;
            running = StartCoroutine(Run(steps));
        }

        // Clears every layer and overlay so a new game or a checkpoint restore cannot inherit leftovers.
        public void Reset()
        {
            if (running != null) { StopCoroutine(running); running = null; }
            fastForward = false;
            currentId = null;
            HideAll();
        }

        // Snaps the stage to a checkpoint picture: no animation, no completion callback.
        public void ApplyScene(IReadOnlyList<PresentationRow> visible, IReadOnlyCollection<string> hidden)
        {
            if (running != null) { StopCoroutine(running); running = null; }
            fastForward = false;
            currentId = null;
            HideAll();
            if (visible == null) return;
            foreach (var row in visible)
            {
                if (row == null || string.IsNullOrEmpty(row.TargetId) || !map.TryGetValue(row.TargetId, out var e)) continue;
                string action = (row.Action ?? string.Empty).Trim().ToLowerInvariant();
                if (action == "fadeout" || action == "hide") continue;
                e.style.display = DisplayStyle.Flex;
                e.style.opacity = 1f;
                if (action == "setscale" || action == "scale")
                {
                    float value = 1f;
                    float.TryParse(row.Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out value);
                    e.style.scale = new Scale(new Vector3(value, value, 1f));
                }
            }
        }

        // Story fast-forward: shortens the running beat without dropping any step, so the stage still
        // reaches its authored final state and the completion event still arrives exactly once.
        public void FastForward()
        {
            if (running != null) fastForward = true;
        }

        // Static per-layer scale authored in the scene; animated scale actions multiply into it.
        private float BaseScale(string id) => id != null && baseScaleMap.TryGetValue(id, out float value) ? value : 1f;

        private void HideAll()
        {
            foreach (var pair in map)
            {
                var e = pair.Value;
                if (e == null) continue;
                e.style.display = DisplayStyle.None;
                e.style.opacity = 0f;
                float baseScale = BaseScale(pair.Key);
                e.style.scale = new Scale(new Vector3(baseScale, baseScale, 1f));
            }
            if (glitchOverlay != null) { glitchOverlay.style.display = DisplayStyle.None; glitchOverlay.style.opacity = 0f; glitchOverlay.style.translate = new Translate(0f, 0f); }
            if (blackOverlay != null) { blackOverlay.style.display = DisplayStyle.None; blackOverlay.style.opacity = 0f; }
            if (whiteOverlay != null) { whiteOverlay.style.display = DisplayStyle.None; whiteOverlay.style.opacity = 0f; }
        }

        private IEnumerator Run(IReadOnlyList<PresentationRow> steps)
        {
            if (steps == null || steps.Count == 0) { yield return null; Finish(); yield break; }
            // Steps must run in numeric order; the CSV does not guarantee row order within a beat.
            var ordered = new List<PresentationRow>(steps);
            ordered.Sort((a, b) => a.Step.CompareTo(b.Step));
            int i = 0;
            while (i < ordered.Count)
            {
                int step = ordered[i].Step;
                float maxDuration = 0f;
                while (i < ordered.Count && ordered[i].Step == step)
                {
                    var row = ordered[i];
                    StartCoroutine(Apply(row));
                    if (row.Duration > maxDuration) maxDuration = row.Duration;
                    i++;
                }
                if (maxDuration > 0f) yield return WaitFor(maxDuration);
                else yield return null;
            }
            Finish();
        }

        // Re-reads the rate every frame, so a fast-forward requested mid-beat takes effect at once
        // instead of waiting out the original duration.
        private IEnumerator WaitFor(float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime * Rate;
                yield return null;
            }
        }

        private void Finish()
        {
            string id = currentId;
            running = null;
            fastForward = false;
            Completed?.Invoke(id);
        }

        private IEnumerator Apply(PresentationRow row)
        {
            string action = (row.Action ?? string.Empty).Trim().ToLowerInvariant();
            switch (action)
            {
                case "glitch": yield return Glitch(row.Duration > 0f ? row.Duration : 1.2f); break;
                case "fadeblack": yield return Fade(blackOverlay, 0f, 1f, row.Duration, false); break;
                case "fadewhite": yield return Fade(whiteOverlay, 0f, 1f, row.Duration, false); break;
                default:
                    if (!map.TryGetValue(row.TargetId ?? string.Empty, out var e)) break;
                    switch (action)
                    {
                        case "show": e.style.display = DisplayStyle.Flex; e.style.opacity = 1f; break;
                        case "hide": e.style.display = DisplayStyle.None; break;
                        case "fadein": yield return Fade(e, 0f, 1f, row.Duration, true); break;
                        case "fadeout":
                            if (e.style.display.value == DisplayStyle.None) break;
                            yield return Fade(e, 1f, 0f, row.Duration, true);
                            e.style.display = DisplayStyle.None;
                            break;
                        case "scale":
                            float target = 1f;
                            float.TryParse(row.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out target);
                            yield return Scale(e, target * BaseScale(row.TargetId), row.Duration);
                            break;
                        case "setscale":
                            float set = 1f;
                            float.TryParse(row.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out set);
                            float authored = set * BaseScale(row.TargetId);
                            e.style.scale = new Scale(new Vector3(authored, authored, 1f));
                            break;
                    }
                    break;
            }
        }

        private IEnumerator Fade(VisualElement e, float from, float to, float duration, bool showDuring)
        {
            if (e == null) yield break;
            if (showDuring) e.style.display = DisplayStyle.Flex;
            if (duration <= 0f) { e.style.opacity = to; yield break; }
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime * Rate;
                e.style.opacity = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
            e.style.opacity = to;
        }

        private IEnumerator Scale(VisualElement e, float target, float duration)
        {
            if (e == null) yield break;
            Vector3 from = e.resolvedStyle.scale.value;
            if (from == Vector3.zero) from = Vector3.one;
            if (duration <= 0f) { e.style.scale = new Scale(new Vector3(target, target, 1f)); yield break; }
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime * Rate;
                float k = Mathf.Clamp01(t / duration);
                float s = Mathf.Lerp(from.x, target, k);
                e.style.scale = new Scale(new Vector3(s, s, 1f));
                yield return null;
            }
            e.style.scale = new Scale(new Vector3(target, target, 1f));
        }
    }
}
