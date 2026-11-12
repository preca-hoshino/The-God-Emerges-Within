using UnityEngine;
using UnityEngine.UIElements;

namespace Via.UI
{
    /// <summary>Fits the authored menu canvas as a whole, preserving artwork and button hit positions.</summary>
    public sealed class TitleMenuLayoutController : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

        private VisualElement viewport;
        private VisualElement artboard;

        private void Start()
        {
            if (document == null || referenceResolution.x <= 0f || referenceResolution.y <= 0f)
            {
                Debug.LogError("Bind the title UIDocument and a positive reference resolution in the Inspector.", this);
                enabled = false;
                return;
            }

            var root = document.rootVisualElement;
            viewport = root.Q<VisualElement>("title-screen");
            artboard = root.Q<VisualElement>("title-artboard");
            if (viewport == null || artboard == null)
            {
                Debug.LogError("Title menu layout elements are missing from the UXML document.", this);
                enabled = false;
                return;
            }

            artboard.style.width = referenceResolution.x;
            artboard.style.height = referenceResolution.y;
            artboard.style.marginLeft = -referenceResolution.x * 0.5f;
            artboard.style.marginTop = -referenceResolution.y * 0.5f;
            viewport.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            FitArtboard();
        }

        private void OnGeometryChanged(GeometryChangedEvent change) => FitArtboard();

        private void FitArtboard()
        {
            Rect bounds = viewport.contentRect;
            if (bounds.width <= 0f || bounds.height <= 0f) return;
            float scale = Mathf.Min(bounds.width / referenceResolution.x, bounds.height / referenceResolution.y);
            artboard.style.scale = new Scale(new Vector2(scale, scale));
        }

        private void OnDestroy()
        {
            if (viewport != null) viewport.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }
    }
}
