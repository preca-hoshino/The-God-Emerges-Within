using System.Collections.Generic;

namespace Via.Minigame.Domain
{
    // Returns every matching paradigm id, most specific first. A level submits the first id listed in its outcomes.
    public static class ParadigmEvaluator
    {
        public static IReadOnlyList<string> Evaluate(NeuronNetwork network, float cornerAngle, bool allowCurvedPolygon = false)
        {
            var matches = new List<string>();
            if (network == null || network.Edges.Count == 0) return matches;

            var analysis = NetworkAnalysis.Analyze(network, cornerAngle);
            var catalog = ParadigmCatalog.All;
            for (int i = 0; i < catalog.Count; i++)
                if (catalog[i].Id == "polygon" && allowCurvedPolygon ? analysis.HasSingleClosedRegion : catalog[i].Matches(analysis))
                    matches.Add(catalog[i].Id);
            if (ParadigmCatalog.Straight.Matches(analysis)) matches.Add(ParadigmCatalog.Straight.Id);
            return matches;
        }

        public static bool Contains(IReadOnlyList<string> matches, string paradigmId)
        {
            if (matches == null) return false;
            for (int i = 0; i < matches.Count; i++)
                if (matches[i] == paradigmId) return true;
            return false;
        }
    }
}
