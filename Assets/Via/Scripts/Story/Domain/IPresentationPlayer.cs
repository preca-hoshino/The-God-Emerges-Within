using System;
using System.Collections.Generic;
using Via.Story.Configuration;

namespace Via.Story.Domain
{
    // The flow drives presentation playback and waits for completion; the implementation owns the visuals.
    public interface IPresentationPlayer
    {
        event Action<string> Completed;
        void Play(string presentationId, IReadOnlyList<PresentationRow> steps);

        // Clears every layer so a new game or a checkpoint restore cannot inherit leftovers.
        void Reset();

        // Snaps the stage to a restored picture: show the given layers, everything else stays hidden.
        void ApplyScene(IReadOnlyList<PresentationRow> visible, IReadOnlyCollection<string> hidden);

        // Compresses the rest of the running presentation instead of cutting it, so every step still
        // lands and the completion event fires once as usual. No-op when nothing is playing.
        void FastForward();
    }
}
