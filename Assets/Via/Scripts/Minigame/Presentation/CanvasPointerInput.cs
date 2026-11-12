using System;
using UnityEngine;

namespace Via.Minigame.Presentation
{
    public sealed class CanvasPointerInput : MonoBehaviour
    {
        [SerializeField] private NeuronCanvasView canvasView;
        public bool IsBlocked { get; set; }
        public event Action<Vector2> PointClicked;
        public event Action<Vector2> PointReleased;
        public event Action<Vector2> PointDragged;
        [SerializeField] private bool useLegacyInput = true;
        public void SubmitPress(Vector2 point) { if (!IsBlocked) PointClicked?.Invoke(point); }
        public void SubmitDrag(Vector2 point) { if (!IsBlocked) PointDragged?.Invoke(point); }
        public void SubmitRelease(Vector2 point) { if (!IsBlocked) PointReleased?.Invoke(point); }

        private void OnGUI()
        {
            Event input = Event.current;
            if (!useLegacyInput || IsBlocked || canvasView == null || input.button != 0) return;
            Vector2 position = canvasView.ScreenToWorld(input.mousePosition);
            if (input.type == EventType.MouseDown && canvasView.Contains(input.mousePosition)) PointClicked?.Invoke(position);
            else if (input.type == EventType.MouseDrag) PointDragged?.Invoke(position);
            else if (input.type == EventType.MouseUp) PointReleased?.Invoke(position);
            else return;
            input.Use();
        }
    }
}
