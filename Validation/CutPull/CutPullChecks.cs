using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Via.Minigame.Configuration;
using Via.Minigame.Domain;

internal static class CutPullChecks
{
    private static bool Has(NeuronNetwork network, string id) => ParadigmEvaluator.Contains(ParadigmEvaluator.Evaluate(network, 20f), id);
    private static int[] Cuts(NeuronNetwork network) => network.Nodes.Where(node => node.IsCutEndpoint).Select(node => node.Id).ToArray();
    private static NeuronGrowthSystem Growth(NeuronNetwork network)
    {
        var growth = new NeuronGrowthSystem(network, 3f, 4f, 0.5f, new System.Random(1));
        growth.ResetTips();
        return growth;
    }
    private static void Grow(NeuronGrowthSystem growth, Vector2 point)
    {
        growth.PlaceAttractor(point);
        for (int i = 0; i < 100; i++) growth.Tick(0.025f);
    }

    public static void Run(Action<string, bool> check)
    {
        var line = SamplePatterns.Create("straight");
        check("default cutting preserves two paths", PathCut.Apply(line, new Vector2(1.2f, -1f), new Vector2(1.2f, 1f), out var cut) && Has(cut, "separated"));
        var ends = Cuts(cut);
        check("cut creates two distinct editable endpoints", ends.Length == 2 && cut.CanMove(ends[0]) && cut.CanMove(ends[1]) && ends[0] != ends[1]);
        check("ordinary terminal and origin stay fixed", !cut.CanMove(0) && !cut.CanMove(1));
        foreach (int end in ends)
        {
            var selected = PathPull.Select(cut, cut.Nodes[end].Position, 0.18f);
            check("nearest overlapping selection chooses cut endpoint " + end, selected.IsValid && selected.NodeId == end);
            Vector2 destination = cut.Nodes[end].Position + new Vector2(0f, end == ends[0] ? 0.8f : -0.8f);
            check("pulling endpoint " + end + " succeeds", PathPull.Apply(cut, selected, destination, 0.5f, out var moved, out int node));
            check("pulling endpoint " + end + " moves exactly one node", moved.Nodes.Count == cut.Nodes.Count && moved.Nodes[node].Position == destination &&
                cut.Nodes.Where(original => original.Id != end).All(original => moved.Nodes[original.Id].Position == original.Position));
            check("pulling endpoint " + end + " preserves both edges", moved.Edges.Count == cut.Edges.Count && Has(moved, "separated"));
            check("preview does not mutate its source " + end, cut.Nodes[end].Position != destination && Cuts(cut).Length == 2);
            var destinationSelection = PathPull.Select(moved, destination, 0.18f);
            int other = ends.First(id => id != end);
            check("dragging endpoint " + end + " back welds the gap", PathPull.Apply(moved, destinationSelection, moved.Nodes[other].Position, 0.5f, out var joined, out int joint));
            check("weld creates one actual shared node " + end, joined.Nodes.Count == moved.Nodes.Count - 1 && joined.Degree(joint) == 2 &&
                joined.Nodes[joint].Position == moved.Nodes[other].Position && !joined.HasCutEndpoints);
            check("weld updates paradigm from separated to straight " + end, Has(joined, "straight") && !Has(joined, "separated") && !Has(joined, "double_line"));
            check("weld preserves fixed origin " + end, joined.Nodes[0].Position == cut.Nodes[0].Position && !joined.CanMove(0));
            var snapshot = moved.Copy(); var savedGrowth = Growth(snapshot);
            var resumed = savedGrowth.CopyFor(joined, true);
            check("weld recomputes valid growth tips " + end, resumed.Tips.Count == 1 && resumed.Tips.All(tip => tip.NodeId < joined.Nodes.Count && tip.Position == joined.Nodes[tip.NodeId].Position));
            check("undo snapshot keeps separation and cut metadata " + end, Has(snapshot, "separated") && Cuts(snapshot).Length == 2 && savedGrowth.Tips.Count == 3);
            Grow(resumed, new Vector2(2.4f, 1.2f));
            check("growth continues after weld " + end, !resumed.IsGrowing && Has(joined, "polyline"));
        }

        int right = ends.OrderBy(id => cut.Nodes[id].Position.x).Last();
        var rightSelection = PathPull.Select(cut, cut.Nodes[right].Position, 0.18f);
        Vector2 away = cut.Nodes[right].Position + new Vector2(0.2f, 0f);
        check("moving away within join radius does not instantly reconnect", PathPull.Apply(cut, rightSelection, away, 0.5f, out var stillCut, out _) && stillCut.HasCutEndpoints && Has(stillCut, "separated"));
        check("invalid positions are rejected", !PathPull.Apply(cut, rightSelection, new Vector2(float.NaN, 0f), 0.5f, out _, out _));
        check("stale out of range selections are rejected", !PathPull.Apply(cut, new PullSelection(999, -1, Vector2.zero), Vector2.one, 0.5f, out _, out _));
        check("zero movement is ignored", !PathPull.Apply(cut, rightSelection, rightSelection.Position, 0.5f, out _, out _));
        check("cut metadata survives copying", Cuts(cut.Copy()).SequenceEqual(ends));

        var multiple = SamplePatterns.Create("straight");
        check("two cuts keep three independent fragments", PathCut.Apply(multiple, new Vector2(0.8f, -1f), new Vector2(0.8f, 1f), out multiple) &&
            PathCut.Apply(multiple, new Vector2(1.6f, -1f), new Vector2(1.6f, 1f), out multiple) && NetworkAnalysis.Analyze(multiple, 20f).ComponentCount == 3);
        int[] firstPair = Cuts(multiple).OrderBy(id => multiple.Nodes[id].Position.x).Take(2).ToArray();
        Vector2[] untouched = Cuts(multiple).Except(firstPair).Select(id => multiple.Nodes[id].Position).ToArray();
        check("welding one pair leaves the other cut open", PathPull.Apply(multiple, PathPull.Select(multiple, multiple.Nodes[firstPair[0]].Position, 0.18f),
            multiple.Nodes[firstPair[1]].Position, 0.5f, out var partialJoin, out _) && Has(partialJoin, "separated") && Cuts(partialJoin).Length == 2);
        check("other cut points retain their positions and editability", untouched.All(position => partialJoin.Nodes.Any(node => node.IsCutEndpoint && node.Position == position && partialJoin.CanMove(node.Id))));
        var state = cut.Copy();
        var stateGrowth = Growth(state);
        stateGrowth.PlaceAttractor(new Vector2(9f, 9f));
        stateGrowth.PlaceBranchZone(new Vector2(8f, 8f), 0.7f);
        Grow(stateGrowth, new Vector2(2.4f, 1.2f));
        int[] stateCuts = Cuts(state);
        PathPull.Apply(state, PathPull.Select(state, state.Nodes[stateCuts[0]].Position, 0.18f), state.Nodes[stateCuts[1]].Position, 0.5f, out var stateJoined, out _);
        var copiedState = stateGrowth.CopyFor(stateJoined, true);
        check("recomputed tips use new IDs after a populated network weld", copiedState.Tips.All(tip => tip.NodeId < stateJoined.Nodes.Count && tip.Position == stateJoined.Nodes[tip.NodeId].Position));
        check("weld preserves attractor consumption and branch configuration", copiedState.Attractors.Count == 2 && !copiedState.Attractors[0].Consumed &&
            copiedState.Attractors[1].Consumed && copiedState.BranchZones.Count == 1 && copiedState.BranchZones[0].Radius == 0.7f);

        var eight = SamplePatterns.Create("eight");
        check("shared crossing can be split", PathCut.Apply(eight, new Vector2(0f, -2f), new Vector2(0f, 2f), out var loops));
        int cutJoint = Cuts(loops).First(id => id != 0);
        check("movable side near fixed origin is selectable", PathPull.Select(loops, loops.Nodes[cutJoint].Position, 0.18f).NodeId == cutJoint && !PathPull.Select(loops, Vector2.zero, 0.18f).IsValid);
        check("shared crossing welds back to origin", PathPull.Apply(loops, PathPull.Select(loops, loops.Nodes[cutJoint].Position, 0.18f), Vector2.zero, 0.5f, out var eightAgain, out int eightJoint));
        check("welded crossing restores four way topology and eight", eightJoint == 0 && eightAgain.Degree(0) == 4 && Has(eightAgain, "eight") && !eightAgain.HasCutEndpoints);
        check("branch junction remains fixed after weld", !eightAgain.CanMove(0));

        var triangle = SamplePatterns.Create("triangle");
        var edge = triangle.Edges[0];
        Vector2 middle = (triangle.Nodes[edge.From].Position + triangle.Nodes[edge.To].Position) * 0.5f;
        Vector2 normal = new Vector2(-(triangle.Nodes[edge.To].Position - triangle.Nodes[edge.From].Position).y,
            (triangle.Nodes[edge.To].Position - triangle.Nodes[edge.From].Position).x).normalized;
        check("polygon opens at a cut", PathCut.Apply(triangle, middle - normal * 0.3f, middle + normal * 0.3f, out var opened) && !Has(opened, "polygon"));
        var polygonEnds = Cuts(opened);
        check("open polygon welds closed again", PathPull.Apply(opened, PathPull.Select(opened, opened.Nodes[polygonEnds[0]].Position, 0.18f),
            opened.Nodes[polygonEnds[1]].Position, 0.5f, out var closed, out _) && Has(closed, "triangle") && Has(closed, "polygon"));
        check("welded polygon has no duplicate or self edges", closed.Edges.All(item => item.From != item.To) &&
            closed.Edges.Select(item => Math.Min(item.From, item.To) + ":" + Math.Max(item.From, item.To)).Distinct().Count() == closed.Edges.Count);

        var bridged = cut.Copy();
        bridged.AddEdge(ends[0], ends[1]);
        check("a short cut bridge can weld into one node", PathPull.Apply(bridged, PathPull.Select(bridged, bridged.Nodes[ends[0]].Position, 0.18f),
            bridged.Nodes[ends[1]].Position, 0.5f, out var weldedBridge, out _) && !weldedBridge.HasCutEndpoints && Has(weldedBridge, "straight"));
        var separateCurve = InitialNetworkBuilder.Create(new[] { new InitialStroke { points = new[] { Vector2.zero, new Vector2(2f, 0f), new Vector2(2f, 2f) } } }, Vector2.zero);
        check("corner path cuts normally", PathCut.Apply(separateCurve, new Vector2(0.8f, -1f), new Vector2(0.8f, 1f), out separateCurve));
        int curveCorner = separateCurve.Nodes.First(node => node.Position == new Vector2(2f, 0f)).Id;
        check("reshape preserves cut point metadata", PathReshape.Apply(separateCurve, curveCorner, 12, out var curvedCut) && Cuts(curvedCut).Length == 2 && Cuts(curvedCut).All(curvedCut.CanMove));

        for (int repeat = 0; repeat < 5; repeat++)
        {
            check("repeated cut " + repeat, PathCut.Apply(line, new Vector2(1.2f, -1f), new Vector2(1.2f, 1f), out line));
            int[] repeatedEnds = Cuts(line);
            check("repeated weld " + repeat, PathPull.Apply(line, PathPull.Select(line, line.Nodes[repeatedEnds[0]].Position, 0.18f),
                line.Nodes[repeatedEnds[1]].Position, 0.5f, out line, out _) && !line.HasCutEndpoints && Has(line, "straight"));
        }
        CheckTutorialAndConfiguration(check);
    }

    private static void CheckTutorialAndConfiguration(Action<string, bool> check)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Assets", "Via"))) root = root.Parent;
        if (root == null) throw new Exception("Repository not found.");
        string configuration = Path.Combine(root.FullName, "Assets", "Via", "Data", "Minigame");
        for (int level = 1; level <= 8; level++)
            check("level " + level + " retains cut fragments", File.ReadAllText(Path.Combine(configuration, "Levels", "Level_0" + level + ".asset")).Contains("preserveCutPieces: 1"));
        check("default level also retains cut fragments", File.ReadAllText(Path.Combine(configuration, "Defaults", "DefaultLevel.asset")).Contains("preserveCutPieces: 1"));
        string data = File.ReadAllText(Path.Combine(configuration, "Levels", "Level_01.asset"));
        string strokeSection = data.Substring(data.IndexOf("  tutorialStrokes:", StringComparison.Ordinal));
        strokeSection = strokeSection.Substring(0, strokeSection.IndexOf("  tutorial:", StringComparison.Ordinal));
        var points = Regex.Matches(strokeSection, @"\{x: ([-\d.]+), y: ([-\d.]+)\}").Select(match => new Vector2(
            float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture))).ToArray();
        var network = InitialNetworkBuilder.Create(new[] { new InitialStroke { points = points } }, Vector2.zero);
        check("tutorial cut preserves two pieces", PathCut.Apply(network, new Vector2(1.5f, -1f), new Vector2(1.5f, 1f), out network) && Has(network, "separated"));
        int[] cutEnds = Cuts(network); int selected = cutEnds[0], other = cutEnds[1];
        Vector2 otherPosition = network.Nodes[other].Position;
        check("tutorial pulls one end aside", PathPull.Apply(network, PathPull.Select(network, network.Nodes[selected].Position, 0.18f),
            network.Nodes[selected].Position + Vector2.up, 0.5f, out network, out selected) && network.Nodes[other].Position == otherPosition);
        check("tutorial rejoins into a single shared node", PathPull.Apply(network, PathPull.Select(network, network.Nodes[selected].Position, 0.18f),
            otherPosition, 0.5f, out network, out _) && !network.HasCutEndpoints && NetworkAnalysis.Analyze(network, 20f).ComponentCount == 1);
        var growth = Growth(network);
        Grow(growth, new Vector2(3f, 1.5f));
        check("tutorial grows a real polyline after reconnecting", !growth.IsGrowing && Has(network, "polyline"));
        int bend = network.Nodes.First(node => node.Position == new Vector2(3f, 0f)).Id;
        check("tutorial still allows pulling a normal corner", PathPull.Apply(network, PathPull.Select(network, network.Nodes[bend].Position, 0.18f),
            new Vector2(2.5f, -0.8f), 0.5f, out network, out _) && Has(network, "polyline"));
        growth = growth.CopyFor(network, true);
        Grow(growth, new Vector2(0f, 1.5f)); Grow(growth, Vector2.zero);
        check("tutorial can close a real polygon", !growth.IsGrowing && Has(network, "polygon"));
        var lessons = Regex.Matches(data, @"- instruction: [^\r\n]*\r?\n\s+goal: (\d+)\s+paradigmId:[ \t]*([^\r\n]*)\r?\n\s+tool: (\d+)\s+count: (\d+)");
        check("tutorial includes independent pull and reconnect steps", lessons.Count == 6 && lessons[2].Groups[1].Value == "3" &&
            lessons[2].Groups[3].Value == "2" && lessons[2].Groups[4].Value == "2" && lessons[4].Groups[4].Value == "3");
    }
}
