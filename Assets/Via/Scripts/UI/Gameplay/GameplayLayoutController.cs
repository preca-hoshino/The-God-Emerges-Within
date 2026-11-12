using UnityEngine;
using UnityEngine.UIElements;

namespace Via.UI
{
    /// <summary>Fits the authored gameplay board and its input surface with the same transform.</summary>
    public sealed class GameplayLayoutController : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        private VisualElement viewport;
        private VisualElement artboard;

        private void Start()
        {
            if (document == null) { Debug.LogError("Bind the gameplay UIDocument.", this); enabled = false; return; }
            viewport = document.rootVisualElement.Q("level-screen");
            artboard = document.rootVisualElement.Q("gameplay-artboard");
            if (viewport == null || artboard == null) { Debug.LogError("Gameplay layout elements are missing.", this); enabled = false; return; }
            artboard.style.marginLeft = -960f;
            artboard.style.marginTop = -540f;
            viewport.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            Fit();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt) => Fit();
        private void Fit()
        {
            Rect bounds = viewport.contentRect;
            if (bounds.width <= 0f || bounds.height <= 0f) return;
            float scale = Mathf.Min(bounds.width / 1920f, bounds.height / 1080f);
            artboard.style.scale = new Scale(new Vector2(scale, scale));
        }

        private void OnDestroy()
        {
            if (viewport != null) viewport.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }
    }
}
