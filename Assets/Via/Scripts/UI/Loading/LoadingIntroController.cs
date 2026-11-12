using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace Via.UI
{
    /// <summary>Plays the authored startup layers once, then reveals the existing menu.</summary>
    public sealed class LoadingIntroController : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField, Min(0.01f)] private float microscopeAppearDuration = 0.45f;
        [SerializeField, Min(0f)] private float microscopeHoldDuration = 1.35f;
        [FormerlySerializedAs("zoomDuration")]
        [SerializeField, Min(0.01f)] private float sceneRevealDuration = 1.4f;
        [SerializeField, Min(0.01f)] private float titleRevealDuration = 0.55f;
        [SerializeField, Min(0f)] private float titleHoldDuration = 1f;
        [SerializeField, Min(0.01f)] private float titleFadeDuration = 0.4f;
        [SerializeField, Min(0.01f)] private float menuRevealDuration = 1f;
        [SerializeField, Min(1f)] private float viewStartScale = 1.06f;
        [SerializeField, Range(-90f, 0f)] private float handStartAngle = -90f;

        [SerializeField, Min(0f)] private float initialBlur = 18f;

        private VisualElement screen;
        private VisualElement artboard;
        private VisualElement microscopeAssembly;
        private VisualElement hand;
        private VisualElement world;
        private VisualElement menu;
        private VisualElement titleScreen;
        private VisualElement titleBackground;
        private VisualElement titleContent;
        private VisualElement loadingTitleGroup;
        private VisualElement loadingTitleHalo;
        private VisualElement loadingTitleBloom;
        private VisualElement loadingTitle;
        private LoadingIntroSequence sequence;
        private double startedAt;
        private double pausedAt;
        private bool initialized;
        private bool animationStarted;
        private bool paused;
        private int warmupFrames;
        private float appliedBlur = -1f;
        // Unity 6000.3.22f1's compositor requires a non-null filter list, even when empty.
        private static readonly StyleList<FilterFunction> NoFilters =
            new StyleList<FilterFunction>(new List<FilterFunction>());

        public bool IsMenuVisible { get; private set; }
        public bool IsCompleted { get; private set; }
        public double ElapsedSeconds { get; private set; }
        public event Action MenuRevealed;
        public event Action Completed;

        private void Start()
        {
            if (document == null || !PositiveFinite(referenceResolution.x) || !PositiveFinite(referenceResolution.y) ||
                float.IsNaN(initialBlur) || float.IsInfinity(initialBlur) || initialBlur < 0f ||
                float.IsNaN(handStartAngle) || float.IsInfinity(handStartAngle) || handStartAngle < -90f || handStartAngle > 0f)
            {
                Debug.LogError("Bind the intro UIDocument and valid canvas/focus settings in the Inspector.", this);
                enabled = false;
                return;
            }
            try
            {
                sequence = new LoadingIntroSequence(microscopeAppearDuration, microscopeHoldDuration,
                    sceneRevealDuration, menuRevealDuration, viewStartScale,
                    titleRevealDuration, titleHoldDuration, titleFadeDuration);
            }
            catch (ArgumentException exception)
            {
                Debug.LogException(exception, this);
                enabled = false;
                return;
            }
            var root = document.rootVisualElement;
            screen = root.Q<VisualElement>("loading-screen");
            artboard = root.Q<VisualElement>("loading-artboard");
            microscopeAssembly = root.Q<VisualElement>("loading-microscope-assembly");
            hand = root.Q<VisualElement>("loading-hand");
            world = root.Q<VisualElement>("loading-world");
            menu = root.Q<VisualElement>("title-menu");
            titleScreen = root.Q<VisualElement>("title-screen");
            titleBackground = root.Q<VisualElement>("title-background-motion");
            titleContent = root.Q<VisualElement>("title-content-motion");
            loadingTitleGroup = root.Q<VisualElement>("loading-title-group");
            loadingTitleHalo = root.Q<VisualElement>("loading-title-halo");
            loadingTitleBloom = root.Q<VisualElement>("loading-title-bloom");
            loadingTitle = root.Q<VisualElement>("loading-title");
            if (screen == null || artboard == null || microscopeAssembly == null || hand == null || world == null ||
                menu == null || titleScreen == null || titleBackground == null || titleContent == null ||
                loadingTitleGroup == null || loadingTitleHalo == null || loadingTitleBloom == null || loadingTitle == null)
            {
                Debug.LogError("Microscope intro layers or the title menu are missing from the UXML document.", this);
                enabled = false;
                return;
            }

            artboard.style.width = referenceResolution.x;
            artboard.style.height = referenceResolution.y;
            artboard.style.marginLeft = -referenceResolution.x * 0.5f;
            artboard.style.marginTop = -referenceResolution.y * 0.5f;
            screen.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            screen.style.display = DisplayStyle.Flex;
            // The real menu overlays the moving intro; its matching animated backdrop takes over after the intro.
            titleScreen.style.backgroundColor = Color.clear;
            titleBackground.style.display = DisplayStyle.None;
            menu.SetEnabled(false);
            // Each title layer shares the authored logo and keeps its blur surface local to the image.
            loadingTitleHalo.style.filter = CreateBlur(10f);
            loadingTitleBloom.style.filter = CreateBlur(3.5f);
            loadingTitle.style.filter = CreateBlur(0.7f);
            initialized = true;
            FitArtboard();
            ApplyFrame(0d);
        }

        private void Update()
        {
            if (!initialized || IsCompleted) return;
            if (!animationStarted)
            {
                // Let layout, texture uploads and filter surfaces finish their first render.
                // Starting on the first layout includes that startup stall in the gesture.
                if (screen.contentRect.width <= 0f || screen.contentRect.height <= 0f) return;
                if (warmupFrames++ < 2) return;
                startedAt = Time.unscaledTimeAsDouble;
                animationStarted = true;
                return;
            }
            ElapsedSeconds = Math.Max(0d, Time.unscaledTimeAsDouble - startedAt);
            LoadingIntroFrame frame = ApplyFrame(ElapsedSeconds);
            if (!frame.IsComplete) return;
            IsCompleted = true;
            screen.style.display = DisplayStyle.None;
            titleBackground.style.display = DisplayStyle.Flex;
            titleScreen.style.backgroundColor = Color.black;
            menu.SetEnabled(true);
            Completed?.Invoke();
        }

        private LoadingIntroFrame ApplyFrame(double elapsed)
        {
            LoadingIntroFrame frame = sequence.Evaluate(elapsed);
            ApplyFocus(initialBlur * (1f - frame.FocusProgress));
            // Keep the microscope fixed while the hand turns around its authored grip point.
            microscopeAssembly.style.opacity = frame.MicroscopeOpacity;
            hand.style.rotate = new Rotate(new Angle(Mathf.Lerp(handStartAngle, 0f, frame.HandTurnProgress), AngleUnit.Degree));
            world.style.opacity = frame.DishOpacity;
            world.style.scale = new Scale(new Vector2(frame.DishScale, frame.DishScale));
            loadingTitleGroup.style.opacity = frame.TitleOpacity;
            menu.style.opacity = frame.MenuOpacity;
            if (frame.IsMenuVisible && !IsMenuVisible)
            {
                IsMenuVisible = true;
                MenuRevealed?.Invoke();
            }
            return frame;
        }

        private void ApplyFocus(float sigma)
        {
            // Blur changes the rendered pixels. It does not stand in for the existing fades.
            // Small steps avoid rebuilding a filter for imperceptible subpixel changes.
            sigma = Mathf.Round(sigma * 8f) / 8f;
            if (Mathf.Approximately(appliedBlur, sigma)) return;
            StyleList<FilterFunction> filters;
            if (sigma <= 0f)
            {
                // Remove the offscreen filter surface completely once the view is in focus.
                filters = NoFilters;
            }
            else
            {
                filters = CreateBlur(sigma);
            }
            // Only the specimen is out of focus. The foreground gesture and later title stay legible.
            world.style.filter = filters;
            appliedBlur = sigma;
        }

        private static StyleList<FilterFunction> CreateBlur(float sigma)
        {
            var blur = new FilterFunction(FilterFunctionType.Blur);
            blur.AddParameter(new FilterParameter(sigma));
            return new StyleList<FilterFunction>(new List<FilterFunction> { blur });
        }

        private static bool PositiveFinite(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        private void OnGeometryChanged(GeometryChangedEvent change) => FitArtboard();

        private void FitArtboard()
        {
            Rect bounds = screen.contentRect;
            if (bounds.width <= 0f || bounds.height <= 0f) return;
            float fit = Mathf.Min(bounds.width / referenceResolution.x, bounds.height / referenceResolution.y);
            artboard.style.scale = new Scale(new Vector2(fit, fit));
        }

        private void OnDisable()
        {
            if (!initialized || IsCompleted) return;
            pausedAt = Time.unscaledTimeAsDouble;
            paused = true;
        }

        private void OnEnable()
        {
            if (!paused) return;
            if (animationStarted) startedAt += Time.unscaledTimeAsDouble - pausedAt;
            paused = false;
        }

        private void OnDestroy()
        {
            if (screen != null) screen.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            if (world != null) world.style.filter = NoFilters;
            if (loadingTitleHalo != null) loadingTitleHalo.style.filter = NoFilters;
            if (loadingTitleBloom != null) loadingTitleBloom.style.filter = NoFilters;
            if (loadingTitle != null) loadingTitle.style.filter = NoFilters;
            MenuRevealed = null;
            Completed = null;
        }
    }
}
