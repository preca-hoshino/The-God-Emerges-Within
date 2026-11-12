using System;

namespace Via.UI
{
    /// <summary>Computes focusing, a slowing adjustment gesture, then title and menu reveals.</summary>
    public sealed class LoadingIntroSequence
    {
        public double DishRevealStartsAt { get; }
        public double DishRevealEndsAt { get; }
        public double TitleRevealAt { get; }
        public double TitleFadeAt { get; }
        public double MenuReadyAt { get; }
        public double MenuRevealAt { get; }
        public double Duration { get; }

        private readonly double microscopeAppearDuration;
        private readonly double sceneRevealDuration;
        private readonly double menuRevealDuration;
        private readonly double titleRevealDuration;
        private readonly double titleFadeDuration;
        private readonly float viewStartScale;

        public LoadingIntroSequence(double microscopeAppearDuration, double microscopeHoldDuration,
            double sceneRevealDuration, double menuRevealDuration, float viewStartScale,
            double titleRevealDuration = 0.55d, double titleHoldDuration = 1d, double titleFadeDuration = 0.4d)
        {
            RequireDuration(microscopeAppearDuration, nameof(microscopeAppearDuration));
            RequireDuration(microscopeHoldDuration, nameof(microscopeHoldDuration), true);
            RequireDuration(sceneRevealDuration, nameof(sceneRevealDuration));
            RequireDuration(menuRevealDuration, nameof(menuRevealDuration));
            RequireDuration(titleRevealDuration, nameof(titleRevealDuration));
            RequireDuration(titleHoldDuration, nameof(titleHoldDuration), true);
            RequireDuration(titleFadeDuration, nameof(titleFadeDuration));
            if (float.IsNaN(viewStartScale) || float.IsInfinity(viewStartScale) || viewStartScale < 1f)
                throw new ArgumentOutOfRangeException(nameof(viewStartScale), "The view must start at or above the resting scale.");
            this.microscopeAppearDuration = microscopeAppearDuration;
            this.sceneRevealDuration = sceneRevealDuration;
            this.menuRevealDuration = menuRevealDuration;
            this.titleRevealDuration = titleRevealDuration;
            this.titleFadeDuration = titleFadeDuration;
            this.viewStartScale = viewStartScale;
            DishRevealStartsAt = microscopeAppearDuration + microscopeHoldDuration;
            DishRevealEndsAt = DishRevealStartsAt + sceneRevealDuration;
            // A short clear view separates the focus gesture from the small title.
            TitleRevealAt = DishRevealEndsAt + 0.15d;
            TitleFadeAt = TitleRevealAt + titleRevealDuration + titleHoldDuration;
            MenuRevealAt = TitleFadeAt + titleFadeDuration + 0.1d;
            MenuReadyAt = MenuRevealAt + menuRevealDuration;
            if (double.IsNaN(MenuReadyAt) || double.IsInfinity(MenuReadyAt))
                throw new ArgumentOutOfRangeException(nameof(menuRevealDuration), "The dish reveal must finish before the menu is ready.");
            Duration = MenuReadyAt;
        }

        public LoadingIntroFrame Evaluate(double elapsed)
        {
            float microscopeOpacity = Smooth(Progress(elapsed, 0d, microscopeAppearDuration)) *
                (1f - Smooth(Progress(elapsed, DishRevealStartsAt, sceneRevealDuration)));
            float focus = Smooth(Progress(elapsed, 0d, DishRevealEndsAt));
            float viewScale = viewStartScale + (1f - viewStartScale) * focus;
            // Hold the hand below the focus knob until the microscope has finished appearing.
            // Then turn toward the left, easing to a stop before the view comes into focus.
            float remainingTurn = 1f - Progress(elapsed, microscopeAppearDuration,
                DishRevealEndsAt - microscopeAppearDuration);
            float handTurn = 1f - remainingTurn * remainingTurn * remainingTurn;
            float titleOpacity = Smooth(Progress(elapsed, TitleRevealAt, titleRevealDuration)) *
                (1f - Smooth(Progress(elapsed, TitleFadeAt, titleFadeDuration)));
            return new LoadingIntroFrame(microscopeOpacity, 1f, viewScale, focus, handTurn, titleOpacity,
                Smooth(Progress(elapsed, MenuRevealAt, menuRevealDuration)), elapsed >= MenuRevealAt, elapsed >= Duration);
        }

        private static float Smooth(float progress) => progress * progress * (3f - 2f * progress);

        private static float Progress(double elapsed, double startsAt, double duration) =>
            (float)Math.Max(0d, Math.Min(1d, (elapsed - startsAt) / duration));

        private static void RequireDuration(double value, string name, bool allowZero = false)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d || (!allowZero && value == 0d))
                throw new ArgumentOutOfRangeException(name, "Animation duration must be finite and positive.");
        }
    }

    public readonly struct LoadingIntroFrame
    {
        public float MicroscopeOpacity { get; }
        public float DishOpacity { get; }
        public float DishScale { get; }
        public float FocusProgress { get; }
        public float HandTurnProgress { get; }
        public float TitleOpacity { get; }
        public float MenuOpacity { get; }
        public bool IsMenuVisible { get; }
        public bool IsComplete { get; }

        public LoadingIntroFrame(float microscopeOpacity, float dishOpacity, float dishScale, float focusProgress,
            float handTurnProgress, float titleOpacity,
            float menuOpacity, bool isMenuVisible, bool isComplete)
        {
            MicroscopeOpacity = microscopeOpacity;
            DishOpacity = dishOpacity;
            DishScale = dishScale;
            FocusProgress = focusProgress;
            HandTurnProgress = handTurnProgress;
            TitleOpacity = titleOpacity;
            MenuOpacity = menuOpacity;
            IsMenuVisible = isMenuVisible;
            IsComplete = isComplete;
        }
    }
}
