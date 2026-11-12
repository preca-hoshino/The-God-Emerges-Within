using UnityEngine;
using UnityEngine.UIElements;

namespace Via.UI
{
    /// <summary>
    /// Moves and gently zooms the title viewport toward the pointer without affecting menu input or flow state.
    /// </summary>
    public sealed class TitleMenuMotionController : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private Vector2 maximumViewportOffset = new Vector2(36f, 21f);
        [SerializeField, Range(0f, 0.08f)] private float maximumScaleIncrease = 0.036f;
        [SerializeField, Range(0f, 0.25f)] private float centerDeadZone = 0.04f;
        [SerializeField, Min(0f)] private float safeMargin = 24f;
        [SerializeField, Min(0.1f)] private float followSpeed = 6f;
        [SerializeField, Min(0.1f)] private float returnSpeed = 8f;

        private VisualElement titleScreen;
        private VisualElement titleArtboard;
        private VisualElement titleView;
        private VisualElement titleContent;
        private VisualElement modalLayer;
        private Vector2 pointerDirection;
        private Vector2 viewportOffset;
        private float viewportScale = 1f;
        private bool pointerInside;
        private bool initialized;

        private void Start()
        {
            if (document == null)
            {
                Debug.LogError("Bind the title menu UIDocument in the Inspector.", this);
                enabled = false;
                return;
            }

            VisualElement root = document.rootVisualElement;
            titleScreen = root.Q<VisualElement>("title-screen");
            titleArtboard = root.Q<VisualElement>("title-artboard");
            titleView = root.Q<VisualElement>("title-view-motion");
            titleContent = root.Q<VisualElement>("title-content-motion");
            modalLayer = root.Q<VisualElement>("modal-layer");
            if (titleScreen == null || titleArtboard == null || titleView == null || titleContent == null || modalLayer == null)
            {
                Debug.LogError("Title menu motion elements are missing from the UXML document.", this);
                enabled = false;
                return;
            }

            titleScreen.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            titleScreen.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            initialized = true;
            ApplyTransforms();
        }

        private void Update()
        {
            if (!initialized) return;
            if (titleArtboard == null || titleArtboard.panel == null) return;

            bool responsive = pointerInside && titleScreen.resolvedStyle.display != DisplayStyle.None &&
                modalLayer.resolvedStyle.display == DisplayStyle.None;
            Vector2 targetDirection = responsive ? ApplyDeadZone(pointerDirection) : Vector2.zero;
            float distance = Mathf.Clamp01(targetDirection.magnitude);
            float targetScale = 1f + maximumScaleIncrease * distance;
            Vector2 targetViewportOffset = ConstrainOffset(new Vector2(
                targetDirection.x * maximumViewportOffset.x,
                targetDirection.y * maximumViewportOffset.y), targetScale);
            float speed = responsive ? followSpeed : returnSpeed;
            float blend = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);

            viewportOffset = Vector2.Lerp(viewportOffset, targetViewportOffset, blend);
            viewportScale = Mathf.Lerp(viewportScale, targetScale, blend);
            ApplyTransforms();
        }

        private void OnPointerMove(PointerMoveEvent input)
        {
            Rect viewport = titleArtboard.contentRect;
            if (viewport.width <= 0f || viewport.height <= 0f) return;

            Vector2 panelPosition = new Vector2(input.position.x, input.position.y);
            Vector2 local = titleArtboard.WorldToLocal(panelPosition);
            pointerDirection = new Vector2(
                Mathf.Clamp((local.x / viewport.width - 0.5f) * 2f, -1f, 1f),
                Mathf.Clamp((local.y / viewport.height - 0.5f) * 2f, -1f, 1f));
            pointerInside = true;
        }

        private void OnPointerLeave(PointerLeaveEvent input)
        {
            pointerInside = false;
            pointerDirection = Vector2.zero;
        }

        private Vector2 ApplyDeadZone(Vector2 direction)
        {
            float magnitude = direction.magnitude;
            if (magnitude <= centerDeadZone) return Vector2.zero;
            float scaledMagnitude = Mathf.InverseLerp(centerDeadZone, 1f, Mathf.Min(1f, magnitude));
            return direction.normalized * scaledMagnitude;
        }

        private Vector2 ConstrainOffset(Vector2 desired, float scale)
        {
            Rect viewport = titleArtboard.contentRect;
            Rect content = titleContent.layout;
            if (viewport.width <= 0f || viewport.height <= 0f || content.width <= 0f || content.height <= 0f)
                return desired;

            Vector2 center = viewport.center;
            float scaledLeft = center.x + (content.xMin - center.x) * scale;
            float scaledRight = center.x + (content.xMax - center.x) * scale;
            float scaledTop = center.y + (content.yMin - center.y) * scale;
            float scaledBottom = center.y + (content.yMax - center.y) * scale;
            // Keep both the complete menu and the moving background inside the authored canvas.
            float backgroundX = Mathf.Max(0f, (scale - 1f) * viewport.width * 0.5f);
            float backgroundY = Mathf.Max(0f, (scale - 1f) * viewport.height * 0.5f);
            float minX = Mathf.Max(safeMargin - scaledLeft, -backgroundX);
            float maxX = Mathf.Min(viewport.width - safeMargin - scaledRight, backgroundX);
            float minY = Mathf.Max(safeMargin - scaledTop, -backgroundY);
            float maxY = Mathf.Min(viewport.height - safeMargin - scaledBottom, backgroundY);
            return new Vector2(
                minX <= maxX ? Mathf.Clamp(desired.x, minX, maxX) : 0f,
                minY <= maxY ? Mathf.Clamp(desired.y, minY, maxY) : 0f);
        }

        private void ApplyTransforms()
        {
            titleView.style.translate = new Translate(new Length(viewportOffset.x), new Length(viewportOffset.y));
            titleView.style.scale = new Scale(new Vector2(viewportScale, viewportScale));
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                pointerInside = false;
                pointerDirection = Vector2.zero;
            }
        }

        private void OnDestroy()
        {
            if (!initialized) return;
            if (titleScreen == null || titleScreen.panel == null) return;
            titleScreen.UnregisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            titleScreen.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
        }
    }
}
