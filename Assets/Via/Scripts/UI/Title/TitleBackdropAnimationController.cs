using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Via.UI
{
    /// <summary>Shares the optical six-pose disc between the opening and the title menu.</summary>
    public sealed class TitleBackdropAnimationController : MonoBehaviour
    {
        private const int PoseCount = 6;
        private const float DegreesPerPose = 360f / PoseCount;

        [SerializeField] private UIDocument document;
        // This is the complete authored disc: mice, neurons and slit marks turn together.
        [SerializeField] private Texture2D rotatingImage;
        [SerializeField] private Material lensMaterial;
        [SerializeField, Min(0.01f)] private float frameDuration = 0.14f;

        private VisualElement introScreen, titleScreen, introDish, titleDish;
        private MicroscopeLensRenderer lens;
        private double elapsed;
        private double previousFrameAt;
        private float currentAngle;
        private long lastStep = -1;
        private bool clockRunning;
        private bool applicationPaused;
        private bool initialized;
        private int warmupFrames;

        private void Start() => Initialize();

        private void Initialize()
        {
            ReleaseBindings();
            if (document == null || rotatingImage == null || lensMaterial == null ||
                !lensMaterial.HasProperty("_DispersionScale") ||
                float.IsNaN(frameDuration) || float.IsInfinity(frameDuration) || frameDuration <= 0f)
            {
                Debug.LogError("Bind the title UIDocument, complete six-pose disc, optical material and positive frame duration.", this);
                enabled = false;
                return;
            }

            var root = document.rootVisualElement;
            if (root == null) return;
            introScreen = root.Q("loading-screen");
            titleScreen = root.Q("title-screen");
            introDish = root.Q("loading-dish");
            titleDish = root.Q("title-dish");
            if (introScreen == null || titleScreen == null || introDish == null || titleDish == null)
            {
                Debug.LogError("Author both optical discs in GameUI.uxml.", this);
                enabled = false;
                return;
            }

            if (lens == null) lens = new MicroscopeLensRenderer(rotatingImage, lensMaterial);
            lens.Render(elapsed, true);
            var opticalImage = new StyleBackground(Background.FromRenderTexture(lens.Texture));
            introDish.style.backgroundImage = opticalImage;
            titleDish.style.backgroundImage = opticalImage;
            initialized = true;
            warmupFrames = 0;
            // A rebuilt UI document resumes at the same disc pose as the clock.
            ApplyFrame();
        }

        private void Update()
        {
            // UI Toolkit elements can be rebuilt while Play Mode recompiles scripts.
            if (!initialized || document == null ||
                (introScreen.panel == null && document.rootVisualElement?.Q("loading-screen") != introScreen))
            {
                Initialize();
                if (!initialized) return;
            }
            if (applicationPaused || (!IsVisible(introScreen) && !IsVisible(titleScreen)))
            {
                clockRunning = false;
                return;
            }
            // Let the panel and optical target settle before the first timed pose.
            if (warmupFrames < 2)
            {
                warmupFrames++;
                return;
            }
            double now = Time.unscaledTimeAsDouble;
            if (!clockRunning)
            {
                previousFrameAt = now;
                clockRunning = true;
                ApplyFrame();
                return;
            }
            double delta = Math.Max(0d, now - previousFrameAt);
            previousFrameAt = now;
            elapsed += delta;
            ApplyFrame();
        }

        private void ApplyFrame()
        {
            long step = (long)(elapsed / frameDuration);
            if (step != lastStep)
            {
                // A 60-degree jump advances the pose at each fixed viewing position.
                // No interpolation occurs between the six authored disc poses.
                currentAngle = -(step % PoseCount) * DegreesPerPose;
                var rotation = new Rotate(new Angle(currentAngle, AngleUnit.Degree));
                introDish.style.rotate = rotation;
                titleDish.style.rotate = rotation;
                lastStep = step;
            }
            lens.Render(elapsed);
        }

        private static bool IsVisible(VisualElement screen)
        {
            return screen.panel != null && screen.resolvedStyle.display != DisplayStyle.None &&
                screen.resolvedStyle.visibility == Visibility.Visible &&
                screen.contentRect.width > 0f && screen.contentRect.height > 0f;
        }

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            clockRunning = false;
        }

        private void ReleaseBindings()
        {
            initialized = false;
            clockRunning = false;
            lastStep = -1;
            introScreen = titleScreen = introDish = titleDish = null;
        }

        // Domain reload calls OnDisable without OnDestroy: release the non-serialized GPU owner here.
        private void OnDisable() => ReleaseLens();
        private void OnDestroy() => ReleaseLens();

        private void ReleaseLens()
        {
            var original = new StyleBackground(rotatingImage);
            if (introDish != null) introDish.style.backgroundImage = original;
            if (titleDish != null) titleDish.style.backgroundImage = original;
            lens?.Dispose();
            lens = null;
            ReleaseBindings();
        }
    }
}
