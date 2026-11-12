using System;
using System.Collections.Generic;
using UnityEngine;
using Via.Story.Configuration;
using Via.Story.Domain;

namespace Via.Application
{
    // Temporary player: the presentation spec is not finalized, so it reports completion immediately.
    // Replace with the real presenter (background, portraits, zoom, transitions) when fields are defined.
    public sealed class PlaceholderPresentationPlayer : MonoBehaviour, IPresentationPlayer
    {
        public event Action<string> Completed;

        public void Play(string presentationId, IReadOnlyList<PresentationRow> steps)
        {
            Debug.Log($"演出占位播放：{presentationId}（{steps.Count} 步，尚未实现画面动作）", this);
            Completed?.Invoke(presentationId);
        }

        public void Reset() { }

        public void ApplyScene(IReadOnlyList<PresentationRow> visible, IReadOnlyCollection<string> hidden) { }

        // Completion already fires from Play, so there is nothing left to compress.
        public void FastForward() { }
    }
}
