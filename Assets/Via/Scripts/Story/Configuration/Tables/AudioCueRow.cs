using System;

namespace Via.Story.Configuration
{
    // One audio cue line. Entering the node starts the listed music, ambient beds and one-shot effects;
    // empty columns keep whatever the previous lines left running.
    public sealed class AudioCueRow
    {
        // Music ids: soft / horror1 / horror2 / ad / soft_end / stop.
        public string Music { get; set; }

        // Semicolon-separated ambience on top of parameters: library ids, or "none" to stop the current bed.
        public string Ambience { get; set; }

        // Semicolon-separated one-shot library ids played when this exact node is entered.
        public string Sfx { get; set; }

        public string NodeId { get; set; }
        public string Note { get; set; }

        public bool HasMusic => !string.IsNullOrEmpty(Music);
        public bool HasAmbience => !string.IsNullOrEmpty(Ambience);

        public static string[] Split(string list) =>
            string.IsNullOrEmpty(list)
                ? Array.Empty<string>()
                : list.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
    }
}
