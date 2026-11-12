using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Via.Shared;
using Via.Story.Configuration;
using Via.Story.Domain;

// Verifies the rebuilt production story tables load through the real loader and that the
// whole game can be walked from the shared entry through every registered level and be
// completed, plus that each of the current story ends is reachable.
internal static class StoryIntegrationChecks
{
    public static void Run(Action<string, bool> check)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Assets", "Via"))) root = root.Parent;
        if (root == null) throw new Exception("Project root not found.");
        string assets = Path.Combine(root.FullName, "Assets", "Via");
        string ReadTable(string name) => File.ReadAllText(Path.Combine(assets, "Data", "Story", "Tables", name + ".csv"));

        string source = File.ReadAllText(Path.Combine(assets, "Data", "Story", "MainStoryTables.asset"));
        string entry = Regex.Match(source, @"entryNodeId: (\w+)").Groups[1].Value;
        var tables = StoryTableLoader.Load(entry, ReadTable("Flow"), ReadTable("Dialogue"),
            ReadTable("Presentation"), ReadTable("LevelResults"), ReadTable("AudioCues"));   // throws on any config error
        check("production story tables load and validate", tables.TryGetFlow("F001", out var start)
            && start.Type == FlowNodeType.Presentation);

        var levelNodes = tables.FlowRows.Where(r => r.Type == FlowNodeType.Level).OrderBy(r => r.NodeId).Select(r => r.NodeId).ToArray();
        string[] expected = { "F007", "F161", "F287", "F438", "F501", "F535", "F753", "F868" };
        check("all eight levels registered in order", levelNodes.SequenceEqual(expected));
        check("each level exposes at least one route", expected.All(n => tables.GetOptions(n).Count == 0
            && tables.LevelResultRows.Any(r => r.EntryNodeId == n)));

        // reachability of every level and every stage/chapter end from the shared entry
        var ends = tables.FlowRows.Where(r => r.Type == FlowNodeType.End).Select(r => r.NodeId).ToArray();
        var endIds = ends.OrderBy(x => x).ToArray();
        check("the story ends in the act-9 finale", tables.TryGetFlow("F1044", out var finaleEnd)
            && finaleEnd.Type == FlowNodeType.End && string.IsNullOrEmpty(finaleEnd.ContentId)
            && endIds.Contains("F1044"));
        foreach (string node in expected.Concat(endIds))
            check("story reaches " + node, CanReach(tables, entry, node));

        // walk the whole story, starting each level and completing it with its first result
        var session = new TestSession();
        var player = new TestPresentation();
        using var flow = new GameFlow(tables, session, player);
        flow.StartGame();
        int minigames = 0;
        foreach (string levelNode in expected)
        {
            if (!Advance(flow, player, levelNode, out _)) throw new Exception("could not advance to " + levelNode);
            tables.TryGetFlow(levelNode, out var levelRow);
            check(levelNode + " starts registered level " + levelRow.ContentId,
                flow.Stage == FlowStage.Minigame && session.Request.LevelId == levelRow.ContentId);
            string firstResult = tables.LevelResultRows.First(r => r.EntryNodeId == levelNode && !r.IsOption).ResultId;
            session.Emit(new LevelResult(session.Request, firstResult, "separated"));
            minigames++;
        }
        check("all eight minigame sessions started", minigames == 8);
        check("walking past the final level returns to the title", ReachEnd(flow, player) &&
            flow.Stage == FlowStage.Title && flow.CurrentNode == null);

        // the eighth act branches to the three paradigm stories through level results
        var level8 = tables.LevelResultRows.Where(r => r.EntryNodeId == "F868" && !r.IsOption).ToArray();
        check("level 8 routes to three distinct branch starts", level8.Length == 3 &&
            level8.All(r => tables.TryGetFlow(r.NextNodeId, out _)));

        // Level 7 must route the player's confirmed resistance separately from the doctor's goal.
        var resistanceSession = new TestSession(); var resistancePlayer = new TestPresentation();
        using (var resistance = new GameFlow(tables, resistanceSession, resistancePlayer))
        {
            resistance.Restore("F753", Array.Empty<ChoiceRecord>());
            resistanceSession.Emit(new LevelResult(resistanceSession.Request, "resist", "separated"));
            check("level 7 confirmed resistance enters the success text",
                resistance.CurrentNode.NodeId == "F754" && resistance.CurrentDialogue.TextId == "T749"
                && resistance.History.Records.Count(r => r.kind == ChoiceKind.LevelResult && r.resultId == "resist") == 1);
        }
        var doctorSession = new TestSession(); var doctorPlayer = new TestPresentation();
        using (var doctor = new GameFlow(tables, doctorSession, doctorPlayer))
        {
            doctor.Restore("F753", Array.Empty<ChoiceRecord>());
            doctorSession.Opponent("calm");
            check("level 7 doctor goal enters the failure text without a player record",
                doctor.CurrentNode.NodeId == "F762" && doctor.CurrentDialogue.TextId == "T755"
                && doctor.History.Records.Count == 0);
        }

        foreach (int candyCount in new[] { 3, 4 })
        foreach (string conditionNode in new[] { "F928", "F982" })
        {
            var records = Enumerable.Range(0, candyCount).Select(i => new ChoiceRecord
            {
                kind = ChoiceKind.CandyUse, nodeId = "F501", levelId = "Level_05",
                sessionId = "previous-session", useId = "candy-" + i
            }).ToArray();
            var branchSession = new TestSession(); var branchPlayer = new TestPresentation();
            using var branch = new GameFlow(tables, branchSession, branchPlayer);
            branch.Restore(conditionNode, records);
            string expectedBranch = conditionNode == "F928"
                ? candyCount == 3 ? "F924" : "F921"
                : candyCount == 3 ? "F977" : "F973";
            check(conditionNode + " routes " + candyCount + " confirmed candy uses to " + expectedBranch,
                branch.History.CandyCount == candyCount && branch.CurrentNode.NodeId == expectedBranch);
        }

        // act-4 advertisement chain: the act-3 hospital fades out into the advertisement
        // sequence, which then hands off to the fifth act.
        bool AdStep(string nodeId, string presId, string next) =>
            tables.TryGetFlow(nodeId, out var row) && row.Type == FlowNodeType.Presentation
            && row.ContentId == presId && row.NextNodeId == next && tables.HasPresentation(presId);
        check("act-4 advertisement chain is registered", AdStep("F316", "P301", "F317")
            && AdStep("F318", "P302", "F319") && AdStep("F325", "P303", "F326")
            && AdStep("F328", "P304", "F401")
            && tables.TryGetFlow("F315", out var act3) && act3.NextNodeId == "F316"
            && tables.TryGetFlow("F317", out var adText) && adText.Type == FlowNodeType.Dialogue
            && adText.ContentId == "T316");

        // the act-4 zoom pushes into the fullscreen advertisement(2) layer, which the final fade clears
        check("act-4 advertisement zoom lands on the fullscreen ad",
            tables.GetPresentation("P302").Any(r => r.TargetId == "advertisement" && r.Action == "scale")
            && tables.GetPresentation("P303").Any(r => r.TargetId == "advertisement" && r.Action == "hide")
            && tables.GetPresentation("P303").Any(r => r.TargetId == "advertisement(2)" && r.Action == "fadein")
            && tables.GetPresentation("P304").Any(r => r.TargetId == "advertisement(2)" && r.Action == "fadeout"));

        // audio: cues are keyed by flow nodes; music and ambience follow the act ladder agreed
        // with the composer, beats fire on exact nodes, and every referenced id must resolve
        // through the scene AudioLibrary to a wav that shipped with an import meta.
        var cueNodes = tables.AudioCueRows.Select(r => r.NodeId).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        check("audio cue table covers the designed nodes", cueNodes.SequenceEqual(new[]
        {
            "F001", "F101", "F132", "F201", "F240", "F316", "F401", "F436", "F437", "F454", "F483", "F492",
            "F515", "F555", "F601", "F701", "F710", "F712", "F750", "F755", "F756", "F801", "F874",
            "F903", "F1001", "F1035"
        }.OrderBy(x => x, StringComparer.Ordinal)));
        string CueMusic(string nodeId) => tables.TryGetAudioCue(nodeId, out var cue) ? cue.Music : null;
        string CueAmbience(string nodeId) => tables.TryGetAudioCue(nodeId, out var cue) ? cue.Ambience : null;
        string CueSfx(string nodeId) => tables.TryGetAudioCue(nodeId, out var cue) ? cue.Sfx : null;
        check("act music ladder is registered", CueMusic("F001") == "soft" && CueMusic("F201") == "horror1"
            && CueMusic("F316") == "ad" && CueMusic("F401") == "horror1" && CueMusic("F601") == "horror2"
            && CueMusic("F1001") == "soft" && CueMusic("F1035") == "soft_end");
        check("ambience beds follow the act ladder", CueAmbience("F001") == "amb_lab"
            && CueAmbience("F101") == "amb_cafe"
            && CueAmbience("F201") == "amb_hospital" && CueAmbience("F316") == "amb_tv"
            && CueAmbience("F401") == "amb_rain;amb_tv" && CueAmbience("F601") == "amb_hospital"
            && CueAmbience("F701") == "amb_lab" && CueAmbience("F801") == "none" && CueAmbience("F1001") == "none");
        check("story beats carry their effect cues", CueSfx("F132") == "dishes" && CueSfx("F240") == "drink"
            && CueSfx("F316") == "screen_switch" && CueSfx("F483") == "candy_wrap" && CueSfx("F492") == "door_open"
            && CueSfx("F555") == "breath_fast" && CueSfx("F1001") == "transition3");

        string scene = File.ReadAllText(Path.Combine(assets, "Scenes", "GameFramework.unity"));
        var libraryGuids = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(scene, @"- id: (\w+)\r?\n    clip: \{fileID: 8300000, guid: ([0-9a-f]{32}), type: 3\}"))
            libraryGuids[m.Groups[1].Value] = m.Groups[2].Value;
        var wavGuids = new HashSet<string>(Directory.GetFiles(Path.Combine(assets, "Audio"), "*.wav.meta", SearchOption.AllDirectories)
            .Select(path => Regex.Match(File.ReadAllText(path), @"guid: ([0-9a-f]{32})").Groups[1].Value));
        var usedIds = new List<string>();
        foreach (var cue in tables.AudioCueRows)
        {
            usedIds.AddRange(AudioCueRow.Split(cue.Ambience).Where(id => id != "none"));
            usedIds.AddRange(AudioCueRow.Split(cue.Sfx));
        }
        check("every cue sound resolves in the scene audio library", usedIds.Count > 0
            && usedIds.All(id => libraryGuids.ContainsKey(id)));
        check("every library entry resolves to a delivered wav", libraryGuids.Count > 0
            && libraryGuids.Values.All(guid => wavGuids.Contains(guid)));
        string MusicSlot(string field)
        {
            var match = Regex.Match(scene, field + @": \{fileID: 8300000, guid: ([0-9a-f]{32}), type: 3\}");
            return match.Success ? match.Groups[1].Value : null;
        }
        check("music and growth-bed slots resolve to delivered wavs",
            new[] { "softMusic", "softEnding", "horrorLoop1", "horrorLoop2", "adMusic", "growthBed" }
                .All(field => MusicSlot(field) is string guid && wavGuids.Contains(guid)));

        // act-2 cake chain: the cafe fades out into the cake scene on a dedicated beat right before
        // the level, the eaten-cake close-ups play on their own beat on the like branch, and the
        // return to the cafe clears every cake layer, so the cake can never leak into later acts.
        check("act-2 cake chain is registered", AdStep("F160", "P103", "F161")
            && AdStep("F180", "P104", "F181")
            && tables.TryGetFlow("F159", out var beforeCake) && beforeCake.NextNodeId == "F160"
            && tables.TryGetFlow("F132", out var closingLine) && string.IsNullOrEmpty(closingLine.PresentationId)
            && tables.TryGetFlow("F174", out var likeStart) && string.IsNullOrEmpty(likeStart.PresentationId)
            && tables.TryGetFlow("F179", out var likeEnd) && string.IsNullOrEmpty(likeEnd.PresentationId)
            && likeEnd.NextNodeId == "F180");
        // the eaten cake is hidden by P104 as it is replaced, and the cafe return clears the rest.
        check("act-2 returning to the cafe clears every cake layer",
            ClearsLayers(tables, "P104", "cake001")
            && ClearsLayers(tables, "P105", "cake002", "cake001(3)"));
        // the close-ups are hidden as they hand over, and the finale fade clears the last layer.
        check("act-1 finale clears every remaining mouse layer",
            ClearsLayers(tables, "P005", "mouse004", "mouse004(2)", "mouse004(3)")
            && ClearsLayers(tables, "P006", "mouse004", "mouse004(2)", "mouse004(3)")
            && ClearsLayers(tables, "P007", "mouse001", "mouse004", "mouse005", "mouse006"));

        // act-1 closing chain: the mouse wrap-up hands over to the teacher dialogue, and the
        // final scene fade ends the act right before act 2 begins.
        check("act-1 teacher dialogue chain is registered", tables.TryGetFlow("F010", out var wrapUp)
            && wrapUp.Type == FlowNodeType.Dialogue && wrapUp.ContentId == "T007" && string.IsNullOrEmpty(wrapUp.PresentationId)
            && wrapUp.NextNodeId == "F011"
            && tables.TryGetFlow("F011", out var teacher1) && teacher1.ContentId == "T008" && teacher1.NextNodeId == "F012"
            && tables.TryGetFlow("F022", out var teacher2) && teacher2.ContentId == "T019"
            && AdStep("F023", "P007", "F101"));

        // act-9 links: act-8 branch chains flow silently into the act-9 entry, the closing chain
        // resolves the act-2 cake callback through a condition node, and the callback ends the story.
        bool Continues(string nodeId, string textId) =>
            tables.TryGetFlow(nodeId, out var row) && row.Type == FlowNodeType.Dialogue
            && row.ContentId == textId && row.NextNodeId == "F981";
        check("act-8 branch chains flow into act 9", Continues("F927", "T929") && Continues("F980", "T979")
            && tables.TryGetFlow("F981", out var act8Close) && act8Close.Type == FlowNodeType.Presentation
            && act8Close.ContentId == "P810" && act8Close.NextNodeId == "F1001"
            && tables.HasPresentation("P810"));
        check("act-9 entry presentation is registered", tables.TryGetFlow("F1001", out var act9) &&
            act9.Type == FlowNodeType.Presentation && act9.ContentId == "P1001" && tables.HasPresentation("P1001"));
        check("act-9 ending presentation is registered", tables.TryGetFlow("F1035", out var tailPres) &&
            tailPres.Type == FlowNodeType.Presentation && tailPres.ContentId == "P1002"
            && tailPres.NextNodeId == "F1036" && tables.HasPresentation("P1002"));
        var finaleRoutes = tables.GetRoutes("F1040");
        check("act-9 finale branches on the act-2 level result", finaleRoutes.Count == 2 && finaleRoutes.All(r => !r.IsOption)
            && finaleRoutes.Any(r => r.ResultId == "like" && r.NextNodeId == "F1041")
            && finaleRoutes.Any(r => r.ResultId == "disgust" && r.NextNodeId == "F1042"));
        check("act-9 closing chain reaches the finale end", tables.TryGetFlow("F1040", out var guard)
            && guard.Type == FlowNodeType.Condition && string.IsNullOrEmpty(guard.NextNodeId)
            && tables.TryGetFlow("F1041", out var likeTail) && likeTail.ContentId == "T1034" && likeTail.NextNodeId == "F1043"
            && tables.TryGetFlow("F1042", out var disgustTail) && disgustTail.ContentId == "T1035" && disgustTail.NextNodeId == "F1043"
            && tables.TryGetFlow("F1043", out var closing) && closing.ContentId == "T1036"
            && closing.PresentationId == "P1003" && closing.NextNodeId == "F1044");
        check("act-9 choices expose ordered options", tables.GetOptions("F1004").Count == 2
            && tables.GetOptions("F1008").Count == 2 && tables.GetOptions("F1013").Count == 3
            && tables.GetOptions("F1029").Count == 3);

        // Level 2 and level 3 reuse like/disgust. Later records must not change which
        // level the act-9 closing branch is about. Walk both disagreements through all endings.
        foreach (string remembered in new[] { "like", "disgust" })
        foreach (string ending in new[] { "expect", "resolute", "anger" })
        {
            var s = new TestSession(); var p = new TestPresentation();
            using var f = new GameFlow(tables, s, p);
            f.StartGame();
            foreach (string nodeId in expected)
            {
                if (!Advance(f, p, nodeId, out _) || f.CurrentNode.NodeId != nodeId)
                    throw new Exception("Story skipped level " + nodeId);
                string result = nodeId == "F161" ? remembered : nodeId == "F287" ?
                    (remembered == "like" ? "disgust" : "like") : nodeId == "F868" ? ending :
                    tables.LevelResultRows.First(r => r.EntryNodeId == nodeId && !r.IsOption).ResultId;
                s.Emit(new LevelResult(s.Request, result, "separated"));
            }
            Advance(f, p, "F1039", out _);
            check(remembered + "/" + ending + " reaches act-2 closing branch", f.CurrentNode.NodeId == "F1039");
            f.ConfirmDialogue();
            check(remembered + "/" + ending + " ignores opposite level-3 result",
                f.CurrentNode.NodeId == (remembered == "like" ? "F1041" : "F1042"));
            check(remembered + "/" + ending + " reaches the cake-callback ending and returns to title",
                ReachEnd(f, p) && f.Stage == FlowStage.Title && f.CurrentNode == null);
            check(remembered + "/" + ending + " records exactly eight confirmed levels",
                f.History.Records.Count(r => r.kind == ChoiceKind.LevelResult) == 8);
            var saved = f.History.Records.ToArray();
            f.Restore("F1044", saved);
            check(remembered + "/" + ending + " restores ending and records",
                f.Stage == FlowStage.Title && f.CurrentNode == null && f.History.Records.Count == saved.Length);
        }

        var guards = new TestSession(); var guardedPlayer = new TestPresentation();
        using var guarded = new GameFlow(tables, guards, guardedPlayer);
        guarded.Restore("F287", Array.Empty<ChoiceRecord>());
        guarded.SetPaused(true);
        guards.Emit(new LevelResult(guards.Request, "like", "eight"));
        guards.Candy("paused-use");
        check("new story keeps pause guards on level and candy records", guarded.CurrentNode.NodeId == "F287" && guarded.History.Records.Count == 0);
        guarded.SetPaused(false);
        guards.Candy("one-use"); guards.Candy("one-use");
        check("new story records a candy use once", guarded.History.CandyCount == 1 && guarded.CurrentNode.NodeId == "F287");
        var stale = guards.Request;
        guarded.Restore("F287", guarded.History.Records.ToArray());
        guards.Emit(new LevelResult(stale, "like", "eight"));
        check("restoring a new session rejects an old result", guarded.CurrentNode.NodeId == "F287" && guarded.History.Records.Count == 1);
        var current = guards.Request;
        guards.Emit(new LevelResult(current, "like", "eight"));
        guards.Emit(new LevelResult(current, "like", "eight"));
        check("new story records a level result once", guarded.CurrentNode.NodeId == "F288" && guarded.History.Records.Count == 2);
        guarded.Restore("F753", guarded.History.Records.ToArray());
        int recordsBeforeOpponent = guarded.History.Records.Count;
        guards.Opponent("calm");
        check("new level-7 opponent route does not write a player result", guarded.CurrentNode.NodeId == "F762" && guarded.History.Records.Count == recordsBeforeOpponent);

        bool rejected = false;
        try { StoryTableLoader.Load(entry, ReadTable("Flow"), ReadTable("Dialogue"), ReadTable("Presentation"),
            ReadTable("LevelResults").Replace(",Level_02", ",Unknown_Level"), ReadTable("AudioCues")); }
        catch (InvalidOperationException) { rejected = true; }
        check("unknown history source level is rejected", rejected);

        // Checkpoint restore: layers are reset, the stage is rebuilt from the act's beats, and the
        // duplicate guards come back with the records (a replayed session must not be recorded twice).
        var restoreSession = new TestSession(); var restorePlayer = new TestPresentation();
        using var restored = new GameFlow(tables, restoreSession, restorePlayer);
        restored.StartGame();
        check("a new game clears the presentation layers", restorePlayer.ResetCount == 1);
        restored.Restore("F287", Array.Empty<ChoiceRecord>());
        check("restoring clears the presentation layers", restorePlayer.ResetCount == 2);
        check("restoring rebuilds the stage picture", restorePlayer.LastScene != null && restorePlayer.LastScene.Count > 0);

        restoreSession.Candy("restore-candy");
        var savedRecords = restored.History.Records.ToArray();
        int savedCount = savedRecords.Length;
        restored.Restore("F287", savedRecords);
        check("restored history keeps the candy count", restored.History.CandyCount == 1 && restored.History.Records.Count == savedCount);
        restoreSession.Candy("restore-candy");
        check("restored history keeps candy de-duplication", restored.History.CandyCount == 1 && restored.History.Records.Count == savedCount);
        restoreSession.Emit(new LevelResult(restoreSession.Request, "like", "eight"));
        check("restored history keeps session de-duplication",
            restored.History.Records.Count == savedCount + 1
            && restored.History.Records[savedCount].sequence == savedCount + 1);

        // A checkpoint records the last result of every level, so the act-9 finale still branches when
        // the saved history does not include the act-2 record.
        var memorySession = new TestSession(); var memoryPlayer = new TestPresentation();
        using var memoryFlow = new GameFlow(tables, memorySession, memoryPlayer);
        memoryFlow.Restore("F161", Array.Empty<ChoiceRecord>());
        memorySession.Emit(new LevelResult(memorySession.Request, "like", "eight"));
        var memory = memoryFlow.CaptureLevelResults();

        var resumeSession = new TestSession(); var resumePlayer = new TestPresentation();
        using var resumed = new GameFlow(tables, resumeSession, resumePlayer);
        resumed.Restore("F1040", Array.Empty<ChoiceRecord>(), memory);
        check("checkpoint level memory resolves the finale branch", resumed.CurrentNode.NodeId == "F1041");

        // Story fast-forward compresses presentations instead of cutting them. Every line is still
        // confirmed normally, so no branch, record or level entry can be lost on the way.
        var ffSession = new TestSession(); var ffPlayer = new TestPresentation();
        using var ff = new GameFlow(tables, ffSession, ffPlayer);
        ff.StartGame();
        check("a new game starts on the entry presentation beat",
            ff.Stage == FlowStage.Presentation && ff.CurrentNode.NodeId == "F001" && ffPlayer.Pending == "P001");
        ff.FastForwardPresentation();
        check("fast-forward completes the entry beat and lands on the first line",
            ff.Stage == FlowStage.Dialogue && ff.CurrentNode.NodeId == "F002" && ffPlayer.Pending == null);
        check("fast-forward writes no history records", ff.History.Records.Count == 0);

        // A line that shares its beat with a presentation waits for that beat; fast-forward releases
        // the wait, but the line still advances one node at a time.
        var fxSession = new TestSession(); var fxPlayer = new TestPresentation();
        using var fx = new GameFlow(tables, fxSession, fxPlayer);
        fx.StartGame();
        fx.FastForwardPresentation();
        fx.ConfirmDialogue();
        check("a line starts its simultaneous beat", fx.CurrentNode.NodeId == "F003" && fxPlayer.Pending == "P002");
        fx.ConfirmDialogue();
        check("a line with a simultaneous beat waits for it first",
            fx.Stage == FlowStage.Dialogue && fx.CurrentNode.NodeId == "F003");
        fx.FastForwardPresentation();
        check("fast-forward releases the waiting line and advances exactly one node",
            fx.CurrentNode.NodeId == "F004");

        // Pausing blocks the compression so a paused act cannot be raced through.
        var ffPauseSession = new TestSession(); var ffPausePlayer = new TestPresentation();
        using var ffPause = new GameFlow(tables, ffPauseSession, ffPausePlayer);
        ffPause.StartGame();
        ffPause.FastForwardPresentation();
        ffPause.ConfirmDialogue();
        ffPause.SetPaused(true);
        ffPause.FastForwardPresentation();
        check("fast-forward is refused while paused",
            ffPause.CurrentNode.NodeId == "F003" && ffPausePlayer.Pending == "P002");
        ffPause.SetPaused(false);

        // Fast-forward may never answer a choice on the player's behalf.
        var choiceFfSession = new TestSession(); var choiceFfPlayer = new TestPresentation();
        using var choiceFf = new GameFlow(tables, choiceFfSession, choiceFfPlayer);
        choiceFf.Restore("F1004", Array.Empty<ChoiceRecord>());
        choiceFf.FastForwardPresentation();
        check("fast-forward never answers a choice",
            choiceFf.Stage == FlowStage.Choice && choiceFf.CurrentNode.NodeId == "F1004"
            && choiceFf.History.Records.Count == 0);
    }

    private static bool ClearsLayers(StoryTables tables, string presentationId, params string[] targetIds)
    {
        var rows = tables.GetPresentation(presentationId);
        return targetIds.All(id => rows.Any(r => r.TargetId == id && (r.Action == "fadeout" || r.Action == "hide")));
    }

    private static bool CanReach(StoryTables tables, string startNode, string dest)
    {
        var seen = new HashSet<string>();
        var stack = new Stack<string>();
        stack.Push(startNode);
        while (stack.Count > 0)
        {
            string n = stack.Pop();
            if (n == dest) return true;
            if (!seen.Add(n) || !tables.TryGetFlow(n, out var row)) continue;
            switch (row.Type)
            {
                case FlowNodeType.Dialogue:
                case FlowNodeType.Condition:
                    if (!string.IsNullOrEmpty(row.NextNodeId)) stack.Push(row.NextNodeId);
                    foreach (var r in tables.GetRoutes(n)) stack.Push(r.NextNodeId);
                    break;
                case FlowNodeType.Presentation:
                case FlowNodeType.Menu:
                    if (!string.IsNullOrEmpty(row.NextNodeId)) stack.Push(row.NextNodeId);
                    break;
                case FlowNodeType.Level:
                    foreach (var r in tables.LevelResultRows) if (r.EntryNodeId == n && !r.IsOption) stack.Push(r.NextNodeId);
                    break;
                case FlowNodeType.Choice:
                    foreach (var r in tables.GetOptions(n)) stack.Push(r.NextNodeId);
                    break;
            }
        }
        return false;
    }

    private static bool ReachEnd(GameFlow flow, TestPresentation player)
    {
        for (int i = 0; i < 4000; i++)
        {
            if (flow.Stage == FlowStage.Title) return true;
            if (player.Pending != null) { player.Finish(); continue; }
            switch (flow.Stage)
            {
                case FlowStage.Dialogue: flow.ConfirmDialogue(); break;
                case FlowStage.Choice: flow.Choose(flow.CurrentOptions.First().OptionId); break;
                case FlowStage.Menu: flow.Choose(flow.CurrentOptions.First().OptionId); break;
                default: return false;
            }
        }
        return false;
    }

    private static bool Advance(GameFlow flow, TestPresentation player, string destination, out string saw)
    {
        saw = flow.CurrentNode?.NodeId ?? "";
        for (int i = 0; i < 4000; i++)
        {
            if (flow.CurrentNode?.NodeId == destination) { saw = destination; return true; }
            if (flow.Stage == FlowStage.Ending) { saw = flow.CurrentNode.NodeId; return true; }
            if (player.Pending != null) { player.Finish(); continue; }
            switch (flow.Stage)
            {
                case FlowStage.Dialogue: flow.ConfirmDialogue(); break;
                case FlowStage.Choice: flow.Choose(flow.CurrentOptions.First().OptionId); break;
                case FlowStage.Menu: flow.Choose(flow.CurrentOptions.First().OptionId); break;
                default: return false;
            }
        }
        return false;
    }

    private sealed class TestSession : IMinigameSession
    {
        public event Action<LevelResult> OnLevelCompleted;
        public event Action<LevelResult> OnOpponentCompleted;
        public event Action<CandyUseInfo> OnCandyUseConfirmed;
        public LevelStartRequest Request { get; private set; }
        public int StartCount { get; private set; }
        public void StartLevel(LevelStartRequest request) { Request = request; StartCount++; }
        public void CloseLevel() { }
        public void PauseLevel() { }
        public void ResumeLevel() { }
        public void Emit(LevelResult result) => OnLevelCompleted?.Invoke(result);
        public void Opponent(string result) => OnOpponentCompleted?.Invoke(new LevelResult(Request, result, "eight"));
        public void Candy(string id) => OnCandyUseConfirmed?.Invoke(new CandyUseInfo(Request.LevelId, Request.SessionId, id, "eight"));
    }

    private sealed class TestPresentation : IPresentationPlayer
    {
        public event Action<string> Completed;
        public string Pending { get; private set; }
        public int ResetCount { get; private set; }
        public IReadOnlyList<PresentationRow> LastScene { get; private set; }
        public void Play(string id, IReadOnlyList<PresentationRow> steps) { Pending = id; }
        public void Reset() { Pending = null; ResetCount++; }
        public void ApplyScene(IReadOnlyList<PresentationRow> visible, IReadOnlyCollection<string> hidden) { LastScene = visible; }
        // Verification drives completion explicitly, so a fast-forward request just ends the beat.
        public void FastForward() => Finish();
        public void Finish() { string id = Pending; Pending = null; Completed?.Invoke(id); }
    }
}
