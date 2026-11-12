using UnityEngine;
using UnityEngine.UIElements;

namespace Via.UI
{
    /// <summary>Adapts the filmed microscope menu transition to the existing pause actions.</summary>
    public sealed class MicroscopeMenuController : MonoBehaviour
    {
        private static readonly string[] ActionNames =
        {
            "pause-resume", "pause-save", "pause-settings", "pause-title", "pause-quit"
        };
        // Physical objectives are independent of the number of menu actions. An even ring
        // keeps the filmed light/dark alternation at every menu entry, including wraparound.
        private const int ObjectiveCount = 6;
        private static readonly Vector2 LowerLens = new Vector2(259.630f, 408.256f);
        private static readonly Vector2 MiddleLens = new Vector2(349.848f, 351.727f);
        private static readonly Vector2 UpperLens = new Vector2(352.924f, 252.682f);
        private static readonly Vector2 DiscCentre = new Vector2(265f, 297f);
        private static readonly Vector2 ActionAnchor = new Vector2(478f, 225f);
        private static readonly ActionPose RestPose = new ActionPose(ActionAnchor, 0f, 1f);
        private static readonly ActionPose EntryPose = new ActionPose(ActionAnchor + new Vector2(-76f, -170f), -30f, 0f);
        private static readonly ActionPose ExitPose = new ActionPose(ActionAnchor + new Vector2(-200f, 112f), 90f, 0f);
        [SerializeField] private UIDocument document;
        // Finish upright and clickable in one short transition, without a spring tail.
        [SerializeField, Min(0.05f)] private float turnDuration = 0.25f;

        private VisualElement viewport;
        private VisualElement modalLayer;
        private VisualElement artboard;
        private VisualElement[] lenses;
        private VisualElement[] lightObjectives;
        private VisualElement[] darkObjectives;
        private Button[] actions;
        private ActionPose[] actionPoses;
        private Button turret;
        private Button turnHint;
        private int activeIndex;
        private int outgoingIndex;
        private int incomingIndex;
        private float elapsed;
        private float phase;
        private float startPhase;
        private float targetPhase;
        private ActionPose outgoingStart;
        private ActionPose incomingStart;
        private bool reverse;
        private bool turning;
        private bool wasVisible;
        private bool initialized;

        private readonly struct ActionPose
        {
            public readonly Vector2 Centre;
            public readonly float Rotation;
            public readonly float Opacity;

            public ActionPose(Vector2 centre, float rotation, float opacity)
            {
                Centre = centre;
                Rotation = rotation;
                Opacity = opacity;
            }

            public static ActionPose Interpolate(ActionPose from, ActionPose to, float progress)
            {
                return new ActionPose(Vector2.LerpUnclamped(from.Centre, to.Centre, progress),
                    Mathf.LerpUnclamped(from.Rotation, to.Rotation, progress),
                    Mathf.Clamp01(Mathf.LerpUnclamped(from.Opacity, to.Opacity, progress)));
            }
        }

        private void Start() => Initialize();

        private void Initialize()
        {
            ReleaseBindings();
            if (document == null)
            {
                Debug.LogError("Bind the pause microscope UIDocument in the Inspector.", this);
                enabled = false;
                return;
            }

            VisualElement root = document.rootVisualElement;
            viewport = root.Q("pause-panel");
            modalLayer = root.Q("modal-layer");
            artboard = root.Q("pause-artboard");
            turret = root.Q<Button>("pause-turret");
            turnHint = root.Q<Button>("pause-click-arrow");
            lenses = new VisualElement[ObjectiveCount];
            lightObjectives = new VisualElement[ObjectiveCount];
            darkObjectives = new VisualElement[ObjectiveCount];
            actions = new Button[ActionNames.Length];
            actionPoses = new ActionPose[ActionNames.Length];
            bool valid = viewport != null && modalLayer != null && artboard != null
                && turret != null && turnHint != null;
            for (int i = 0; i < ObjectiveCount; i++)
            {
                lenses[i] = root.Q("pause-lens-" + i);
                lightObjectives[i] = lenses[i]?.Q(className: "pause-objective-light");
                darkObjectives[i] = lenses[i]?.Q(className: "pause-objective-dark");
                valid &= lenses[i] != null && lightObjectives[i] != null
                    && darkObjectives[i] != null;
            }
            for (int i = 0; i < ActionNames.Length; i++)
            {
                actions[i] = root.Q<Button>(ActionNames[i]);
                valid &= actions[i] != null;
            }
            if (!valid)
            {
                Debug.LogError("Pause microscope elements are missing from the UXML document.", this);
                enabled = false;
                return;
            }

            Settle();
            turret.clicked += Advance;
            turnHint.clicked += Advance;
            viewport.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            initialized = true;
            FitArtboard();
        }

        private void Update()
        {
            if (!initialized || viewport == null || modalLayer == null || artboard == null
                || turret == null || turnHint == null || actions == null || lenses == null
                || (viewport.panel == null && document.rootVisualElement.Q("pause-panel") != viewport))
            {
                Initialize();
                if (!initialized) return;
            }
            bool visible = modalLayer.resolvedStyle.display != DisplayStyle.None
                && viewport.resolvedStyle.display != DisplayStyle.None;
            if (!visible)
            {
                if (wasVisible) Settle();
                wasVisible = false;
                return;
            }
            wasVisible = true;
            if (!turning) return;

            // Pause stops the game clock, not the menu animation.
            elapsed += Time.unscaledDeltaTime;
            ApplyTransition(EvaluateProgress(elapsed));
            if (elapsed >= turnDuration) Settle();
        }

        private void Advance()
        {
            if (!initialized) return;
            // Keep the current displayed pose if another click interrupts the transition.
            if (turning) activeIndex = incomingIndex;
            outgoingIndex = activeIndex;
            incomingIndex = (activeIndex + 1) % actions.Length;
            reverse = actions.Length == 2 && incomingIndex == 0;
            outgoingStart = actionPoses[outgoingIndex];
            incomingStart = reverse ? ExitPose : EntryPose;
            startPhase = phase;
            targetPhase = reverse ? 0f : (turning ? targetPhase + 1f : phase + 1f);
            elapsed = 0f;
            turning = true;
            for (int i = 0; i < actions.Length; i++)
            {
                actions[i].style.display = i == outgoingIndex || i == incomingIndex
                    ? DisplayStyle.Flex : DisplayStyle.None;
                actions[i].SetEnabled(false);
            }
            ApplyTransition(0f);
        }

        private float EvaluateProgress(float time)
        {
            float remaining = 1f - Mathf.Clamp01(time / turnDuration);
            return 1f - remaining * remaining * remaining;
        }

        private void ApplyTransition(float progress)
        {
            phase = Mathf.LerpUnclamped(startPhase, targetPhase, progress);
            ApplyLenses();
            SetActionPose(outgoingIndex, ActionPose.Interpolate(outgoingStart,
                reverse ? EntryPose : ExitPose, progress));
            SetActionPose(incomingIndex, ActionPose.Interpolate(incomingStart, RestPose, progress));
        }

        private void Settle()
        {
            if (turning)
            {
                activeIndex = incomingIndex;
                phase = Mathf.Repeat(targetPhase, lenses.Length);
            }
            turning = false;
            for (int i = 0; i < actions.Length; i++)
            {
                actions[i].style.display = i == activeIndex ? DisplayStyle.Flex : DisplayStyle.None;
                actions[i].SetEnabled(i == activeIndex);
            }
            SetActionPose(activeIndex, RestPose);
            ApplyLenses();
        }

        private void SetActionPose(int index, ActionPose pose)
        {
            actionPoses[index] = pose;
            actions[index].style.left = pose.Centre.x - 80f;
            actions[index].style.top = pose.Centre.y - 30f;
            actions[index].style.rotate = new Rotate(Angle.Degrees(pose.Rotation));
            actions[index].style.opacity = pose.Opacity;
        }

        private void ApplyLenses()
        {
            for (int i = 0; i < lenses.Length; i++)
            {
                float slot = Mathf.Repeat(i - phase + 1f, lenses.Length) - 1f;
                bool exposed = slot > -1f && slot < 2f;
                lenses[i].style.display = exposed ? DisplayStyle.Flex : DisplayStyle.None;
                if (!exposed) continue;

                // Keep the front poses from the artwork. The outgoing objective stays on
                // the disc and rotates behind the body instead of flying out on a straight line.
                Vector2 centre = slot <= 1f
                    ? Vector2.LerpUnclamped(LowerLens, MiddleLens, slot)
                    : Vector2.LerpUnclamped(MiddleLens, UpperLens, slot - 1f);
                if (slot < 0f)
                {
                    Vector2 offset = LowerLens - DiscCentre;
                    float radians = -45f * slot * Mathf.Deg2Rad;
                    float sin = Mathf.Sin(radians);
                    float cos = Mathf.Cos(radians);
                    centre = DiscCentre + new Vector2(offset.x * cos - offset.y * sin,
                        offset.x * sin + offset.y * cos);
                }
                bool dark = (i & 1) != 0;
                lenses[i].style.left = centre.x;
                lenses[i].style.top = centre.y;
                lenses[i].style.rotate = new Rotate(Angle.Degrees(-45f * slot + (dark ? 45f : 0f)));
                lenses[i].style.opacity = Mathf.Clamp01((slot + 1f) * 2f)
                    * Mathf.Clamp01((2f - slot) * 2f);
                lightObjectives[i].style.opacity = dark ? 0f : 1f;
                darkObjectives[i].style.opacity = dark ? 1f : 0f;
            }
        }

        private void OnGeometryChanged(GeometryChangedEvent change) => FitArtboard();

        private void FitArtboard()
        {
            Rect bounds = viewport.contentRect;
            float width = artboard.resolvedStyle.width;
            float height = artboard.resolvedStyle.height;
            if (bounds.width <= 0f || bounds.height <= 0f || !(width > 0f) || !(height > 0f)) return;
            float scale = Mathf.Min(bounds.width / width, bounds.height / height);
            artboard.style.scale = new Scale(new Vector2(scale, scale));
        }

        private void OnDisable() => ReleaseBindings();
        private void OnDestroy() => ReleaseBindings();

        private void ReleaseBindings()
        {
            if (turret != null) turret.clicked -= Advance;
            if (turnHint != null) turnHint.clicked -= Advance;
            if (viewport != null) viewport.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            initialized = false;
        }
    }
}
