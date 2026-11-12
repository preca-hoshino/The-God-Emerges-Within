using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using IOPath = System.IO.Path;
using System.Text.RegularExpressions;
using UnityEngine;
using Via.Minigame.Domain;

internal static class Program
{
    private static int passed;

    private static void Main()
    {
        foreach (string id in new[] { "y", "eight", "triangle", "polygon", "polyline", "straight", "x", "separated", "double_line", "arc", "s", "wave" })
            Check("canonical " + id, Has(SamplePatterns.Create(id), id));

        var eight = SamplePatterns.Create("eight");
        Check("eight unchanged by evaluation", eight.Edges.Count == 8 && eight.Nodes.Count == 7);
        foreach (float scale in new[] { 0.1f, 1f, 10f })
            Check("eight rotated / scaled " + scale, Has(Transform(eight, scale, 0.73f, new Vector2(4f, -3f)), "eight"));
        Check("crossing inside a closed path forms two regions", Has(Closed(new Vector2(-2f, -1f), new Vector2(2f, 1f), new Vector2(-2f, 1f), new Vector2(2f, -1f)), "eight"));

        var touching = Path(new Vector2(0f, 0f), new Vector2(2f, 1f), new Vector2(2f, -1f), Vector2.zero,
            new Vector2(-2f, -1f), new Vector2(-2f, 1f), Vector2.zero);
        Check("coincident meeting points recognised", Has(touching, "eight"));
        var tail = eight.Copy();
        tail.AddEdge(1, tail.AddNode(new Vector2(1.2f, 2f)));
        Check("eight with tail rejected", !Has(tail, "eight"));
        Check("single loop rejected as eight", !Has(SamplePatterns.Create("polygon"), "eight"));
        Check("X without closed regions rejected as eight", !Has(SamplePatterns.Create("x"), "eight"));
        var joinedX = SamplePatterns.Create("y");
        joinedX.AddEdge(0, joinedX.AddNode(new Vector2(0f, -2f)));
        Check("X can meet at a four-way network node", Has(joinedX, "x"));
        Check("joined X still has no two closed regions", !Has(joinedX, "eight"));

        var bridge = Closed(new Vector2(-3f, 0f), new Vector2(-2f, 1f), new Vector2(-2f, -1f));
        int right = bridge.AddNode(new Vector2(2f, 0f));
        int ra = bridge.AddNode(new Vector2(3f, 1f)), rb = bridge.AddNode(new Vector2(3f, -1f));
        bridge.AddEdge(right, ra); bridge.AddEdge(ra, rb); bridge.AddEdge(rb, right); bridge.AddEdge(1, right);
        Check("loops connected by bridge rejected", !Has(bridge, "eight"));
        var nested = Closed(Vector2.zero, new Vector2(4f, -3f), new Vector2(4f, 3f));
        int na = nested.AddNode(new Vector2(2f, -0.5f)), nb = nested.AddNode(new Vector2(2f, 0.5f));
        nested.AddEdge(0, na); nested.AddEdge(na, nb); nested.AddEdge(nb, 0);
        Check("nested loops rejected", !Has(nested, "eight"));
        var flat = Closed(Vector2.zero, new Vector2(1f, 0f), new Vector2(2f, 0f));
        int fa = flat.AddNode(new Vector2(-1f, 0f)), fb = flat.AddNode(new Vector2(-2f, 0f));
        flat.AddEdge(0, fa); flat.AddEdge(fa, fb); flat.AddEdge(fb, 0);
        Check("zero-area / retraced rings rejected", !Has(flat, "eight"));
        Check("two intersections are not one meeting", !Has(OverlappingSquares(), "eight"));

        foreach (float angle in new[] { 35f, 60f, 90f, 120f, 150f })
        {
            float radians = angle * Mathf.Deg2Rad;
            var corner = Path(Vector2.zero, new Vector2(2f, 0f), new Vector2(2f + 2f * Mathf.Cos(radians), 2f * Mathf.Sin(radians)));
            Check("reshape succeeds " + angle, PathReshape.Apply(corner, 1, 6, out var arc));
            Check("reshaped arc accepted " + angle, Has(arc, "arc"));
            Check("original remains polyline " + angle, Has(corner, "polyline"));
            foreach (float scale in new[] { 0.25f, 3f })
                Check("arc rotated / scaled " + angle + "/" + scale, Has(Transform(arc, scale, 1.3f, new Vector2(-5f, 2f)), "arc"));
        }
        Check("sharp corner is not arc", !Has(SamplePatterns.ArcDrill(), "arc"));
        Check("straight is not arc", !Has(SamplePatterns.Create("straight"), "arc"));
        Check("one shallow corner is not smooth arc", !Has(Path(Vector2.zero, new Vector2(1f, 0f), new Vector2(2f, 0.2f)), "arc"));
        Check("S is not arc", !Has(SamplePatterns.Create("s"), "arc"));
        Check("wave is not arc", !Has(SamplePatterns.Create("wave"), "arc"));
        Check("minor reversed bend tolerated", Has(FromTurns(5f, 5f, -2f, 5f, 5f, 5f), "arc"));
        Check("substantial reversed bend becomes S", Has(FromTurns(5f, 5f, 5f, -5f, -5f, -5f), "s"));
        var denseArc = new Vector2[257];
        for (int i = 0; i < denseArc.Length; i++)
        {
            float radians = i / 256f * Mathf.PI * 0.5f;
            denseArc[i] = new Vector2(2f * Mathf.Cos(radians), 2f * Mathf.Sin(radians));
        }
        Check("dense curve sampling still recognised as arc", Has(Path(denseArc), "arc"));
        Check("reverse traversal preserves arc", Has(Path(denseArc.Reverse().ToArray()), "arc"));
        var circlePoints = new Vector2[128];
        for (int i = 0; i < circlePoints.Length; i++)
        {
            float radians = i / 128f * Mathf.PI * 2f;
            circlePoints[i] = new Vector2(2f * Mathf.Cos(radians), 2f * Mathf.Sin(radians));
        }
        var circle = Closed(circlePoints);
        Check("level 6 accepts a curved closed region", ParadigmEvaluator.Contains(ParadigmEvaluator.Evaluate(circle, 20f, true), "polygon"));
        Check("strict polygon rule retained elsewhere", !Has(circle, "polygon"));
        Check("open arc is not a closed region", !ParadigmEvaluator.Contains(ParadigmEvaluator.Evaluate(SamplePatterns.Create("arc"), 20f, true), "polygon"));

        var line = SamplePatterns.Create("straight");
        Check("preserving cut succeeds", PathCut.Apply(line, new Vector2(1.2f, -1f), new Vector2(1.2f, 1f), out var separated, true));
        Check("cut makes two open pieces", Has(separated, "separated") && Has(separated, "double_line"));
        Check("cut gap is visible", Vector2.Distance(separated.Nodes[2].Position, separated.Nodes[3].Position) > 0.01f);
        Check("default cut succeeds", PathCut.Apply(line, new Vector2(1.2f, -1f), new Vector2(1.2f, 1f), out var remaining));
        Check("default cut retains both pieces", NetworkAnalysis.Analyze(remaining, 20f).ComponentCount == 2);
        Check("source remains intact for undo", line.Edges.Count == 1 && line.Nodes.Count == 2);
        var growth = new NeuronGrowthSystem(separated, 3f, 4f, 0.5f, new System.Random(1));
        growth.ResetTips();
        Check("cut ends resume growth", growth.Tips.Count == 3);

        var grown = new NeuronNetwork(Vector2.zero);
        var growing = new NeuronGrowthSystem(grown, 3f, 4f, 0.5f, new System.Random(1));
        foreach (var target in new[] { new Vector2(1.2f, 0.9f), new Vector2(2.4f, 0f), new Vector2(1.2f, -0.9f), Vector2.zero,
            new Vector2(-1.2f, 0.9f), new Vector2(-2.4f, 0f), new Vector2(-1.2f, -0.9f), Vector2.zero })
        {
            growing.PlaceAttractor(target);
            growing.Tick(1f);
        }
        Check("level 2 eight attainable with attractors only", Has(grown, "eight"));
        Check("cut shared junction", PathCut.Apply(eight, new Vector2(0f, -2f), new Vector2(0f, 2f), out var splitLoops, true));
        Check("shared junction cut separates the loops", NetworkAnalysis.Analyze(splitLoops, 20f).ComponentCount == 2);
        Check("origin stays fixed after junction cut", splitLoops.Nodes[0].Position == eight.Nodes[0].Position);
        Check("open left loop at its vertex", PathCut.Apply(splitLoops, new Vector2(-2.5f, 0f), new Vector2(-2.3f, 0f), out var openedLeft, true));
        Check("open right loop at its vertex", PathCut.Apply(openedLeft, new Vector2(2.3f, 0f), new Vector2(2.5f, 0f), out var openedBoth, true));
        Check("level 7 resistance attainable by cutting a complete eight", Has(openedBoth, "separated"));
        var doctor = new ReverseOpponent(new[] { new Vector2(2f, 0f) }, 2.2f, 20f);
        Check("level 7 doctor waits for confirmation of a settled resistance shape",
            doctor.Tick(3f, openedBoth) == null);
        var sixthInitial = Path(Vector2.zero, new Vector2(2.2f, 0f), new Vector2(2.4f, 1.8f), new Vector2(0.3f, 2.1f), new Vector2(-0.2f, 0.7f));
        Check("level 6 initial path can be split", PathCut.Apply(sixthInitial, new Vector2(1.8f, 0.9f), new Vector2(2.8f, 0.9f), out var sixthSplit, true));
        Check("level 6 pulling a split piece succeeds", sixthSplit.MoveNode(1, new Vector2(-1f, 2f), 0.5f));
        Check("level 6 X attainable by cut and pull", Has(sixthSplit, "x"));
        Check("level 6 pull first triangle side", sixthInitial.MoveNode(1, new Vector2(1.2f, 0.9f), 0.5f));
        Check("level 6 pull second triangle side", sixthInitial.MoveNode(3, new Vector2(1.1f, 1.25f), 0.5f));
        var sixthGrowth = new NeuronGrowthSystem(sixthInitial, 3f, 4f, 0.5f, new System.Random(1));
        sixthGrowth.ResetTips(); sixthGrowth.PlaceAttractor(Vector2.zero); sixthGrowth.Tick(1f);
        Check("level 6 triangle attainable by pull and close", Has(sixthInitial, "triangle"));
        ValidateLevels();
#if CUT_PULL_VALIDATION
        CutPullChecks.Run(Check);
        Console.WriteLine("PASS: " + passed + " geometry / cut / pull checks; Via sources compiled against the Unity engine assemblies supplied through UnityManaged.");
#else
        StoryIntegrationChecks.Run(Check);
        Console.WriteLine("PASS: " + passed + " geometry / tool / story integration checks; Via sources compiled against the Unity engine assemblies supplied through UnityManaged.");
#endif
    }

    private static bool Has(NeuronNetwork network, string id) => ParadigmEvaluator.Contains(ParadigmEvaluator.Evaluate(network, 20f), id);
    private static void ValidateLevels()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !Directory.Exists(IOPath.Combine(root.FullName, "Assets", "Via"))) root = root.Parent;
        if (root == null) throw new Exception("Project root not found.");
        string directory = IOPath.Combine(root.FullName, "Assets", "Via", "Data", "Minigame", "Levels");
        int[] tools = { 15, 47, 63, 63, 63, 55 };
        string[][] outcomes = { new[] { "y", "eight" }, new[] { "y", "eight", "triangle" }, new[] { "s", "eight", "arc" },
            new[] { "triangle", "s", "wave" }, new[] { "triangle", "polygon", "x" }, new[] { "eight", "separated" } };
        for (int level = 2; level <= 7; level++)
        {
            string data = File.ReadAllText(IOPath.Combine(directory, "Level_0" + level + ".asset"));
            Check("level " + level + " tools match design", int.Parse(Regex.Match(data, @"\btools: (\d+)").Groups[1].Value) == tools[level - 2]);
            var ids = Regex.Matches(data, @"- paradigmId: (\w+)").Select(match => match.Groups[1].Value).ToArray();
            Check("level " + level + " targets match design", ids.SequenceEqual(outcomes[level - 2]));
            if (level >= 6) Check("level " + level + " preserves cut pieces", data.Contains("preserveCutPieces: 1"));
        }
        string sixth = File.ReadAllText(IOPath.Combine(directory, "Level_06.asset"));
        Check("level 6 permits curved closed region", sixth.Contains("allowCurvedPolygon: 1"));
        var triangleMatches = ParadigmEvaluator.Evaluate(SamplePatterns.Create("triangle"), 20f, true);
        var routes = Regex.Matches(sixth, @"- paradigmId: (\w+)\s+resultId: (\w+)");
        string selected = routes.First(route => triangleMatches.Contains(route.Groups[1].Value)).Groups[2].Value;
        Check("level 6 triangle submits worry instead of concern", selected == "worry");
    }
    private static void Check(string name, bool condition)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        passed++;
        Console.WriteLine("PASS " + name);
    }
    private static NeuronNetwork Path(params Vector2[] points)
    {
        var network = new NeuronNetwork(points[0]);
        for (int i = 1; i < points.Length; i++) network.AddEdge(i - 1, network.AddNode(points[i]));
        return network;
    }
    private static NeuronNetwork Closed(params Vector2[] points)
    {
        var network = Path(points);
        network.AddEdge(points.Length - 1, 0);
        return network;
    }
    private static NeuronNetwork Transform(NeuronNetwork source, float scale, float angle, Vector2 offset)
    {
        Vector2 Map(Vector2 p) => new Vector2(p.x * Mathf.Cos(angle) - p.y * Mathf.Sin(angle), p.x * Mathf.Sin(angle) + p.y * Mathf.Cos(angle)) * scale + offset;
        var network = new NeuronNetwork(Map(source.Nodes[0].Position));
        for (int i = 1; i < source.Nodes.Count; i++) network.AddNode(Map(source.Nodes[i].Position));
        foreach (var edge in source.Edges) network.AddEdge(edge.From, edge.To);
        return network;
    }
    private static NeuronNetwork FromTurns(params float[] turns)
    {
        var points = new List<Vector2> { Vector2.zero, new Vector2(1f, 0f) };
        float angle = 0f;
        foreach (float turn in turns)
        {
            angle += turn * Mathf.Deg2Rad;
            points.Add(points[points.Count - 1] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
        }
        return Path(points.ToArray());
    }
    private static NeuronNetwork OverlappingSquares()
    {
        var network = Closed(new Vector2(-2f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-2f, 1f));
        int a = network.AddNode(new Vector2(-1f, -2f)), b = network.AddNode(new Vector2(2f, -2f));
        int c = network.AddNode(new Vector2(2f, 0f)), d = network.AddNode(new Vector2(-1f, 0f));
        network.AddEdge(a, b); network.AddEdge(b, c); network.AddEdge(c, d); network.AddEdge(d, a);
        return network;
    }
}
