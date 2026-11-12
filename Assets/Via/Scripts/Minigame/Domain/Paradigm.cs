using System;
using System.Collections.Generic;

namespace Via.Minigame.Domain
{
    // One geometric paradigm a level can accept. Id stays stable across story results and candy samples.
    public sealed class Paradigm
    {
        private readonly Func<NetworkAnalysis, bool> matches;

        public string Id { get; }
        public string DisplayName { get; }
        public string Glyph { get; }
        public string Judgment { get; }
        public bool IsOfficial { get; }
        public bool UsesTurnThreshold { get; }
        public IReadOnlyList<int> Levels { get; }
        public IReadOnlyList<string> Emotions { get; }

        internal Paradigm(string id, string displayName, string glyph, string judgment,
            bool official, bool usesTurnThreshold, int[] levels, string[] emotions,
            Func<NetworkAnalysis, bool> matches)
        {
            Id = id;
            DisplayName = displayName;
            Glyph = glyph;
            Judgment = judgment;
            IsOfficial = official;
            UsesTurnThreshold = usesTurnThreshold;
            Levels = levels ?? Array.Empty<int>();
            Emotions = emotions ?? Array.Empty<string>();
            this.matches = matches ?? (_ => false);
        }

        public bool Matches(NetworkAnalysis analysis) => analysis != null && matches(analysis);
    }
}
