using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Via.Story.Configuration
{
    // Parses the CSV tables, builds indexes and reports configuration errors instead of guessing.
    public static class StoryTableLoader
    {
        public static StoryTables Load(StoryTableSource source)
        {
            if (source == null) throw new InvalidOperationException("未绑定剧情表格来源 StoryTableSource。");
            return Load(source.EntryNodeId,
                source.Flow != null ? source.Flow.text : null,
                source.Dialogue != null ? source.Dialogue.text : null,
                source.Presentation != null ? source.Presentation.text : null,
                source.LevelResults != null ? source.LevelResults.text : null,
                source.Audio != null ? source.Audio.text : null);
        }

        // The same parser and validation can run independently of Unity's TextAsset lifecycle.
        public static StoryTables Load(string entryNodeId, string flowCsv, string dialogueCsv,
            string presentationCsv, string levelResultsCsv, string audioCueCsv)
        {
            var errors = new List<string>();
            var flowOrder = new List<FlowRow>();
            var flow = BuildFlow(flowCsv, errors, flowOrder);
            var dialogue = BuildDialogue(dialogueCsv, errors);
            var presentations = BuildPresentations(presentationCsv, errors);
            var levelResults = new Dictionary<string, LevelResultRow>(StringComparer.Ordinal);
            var options = new Dictionary<string, List<LevelResultRow>>(StringComparer.Ordinal);
            var routes = new Dictionary<string, List<LevelResultRow>>(StringComparer.Ordinal);
            BuildLevelResults(levelResultsCsv, errors, levelResults, options, routes);
            var audioCues = BuildAudioCues(audioCueCsv, errors);

            var optionView = new Dictionary<string, IReadOnlyList<LevelResultRow>>(StringComparer.Ordinal);
            foreach (var pair in options)
            {
                pair.Value.Sort((a, b) => a.Order.CompareTo(b.Order));
                optionView[pair.Key] = pair.Value;
            }
            var routeView = new Dictionary<string, IReadOnlyList<LevelResultRow>>(StringComparer.Ordinal);
            foreach (var pair in routes) routeView[pair.Key] = pair.Value;

            var tables = new StoryTables(entryNodeId, flow, dialogue, presentations, levelResults, optionView, routeView, audioCues, flowOrder);
            Validate(tables, errors);

            if (errors.Count > 0)
                throw new InvalidOperationException("剧情表配置错误：\n- " + string.Join("\n- ", errors));
            return tables;
        }

        private static Dictionary<string, FlowRow> BuildFlow(string asset, List<string> errors, List<FlowRow> order)
        {
            var result = new Dictionary<string, FlowRow>(StringComparer.Ordinal);
            var table = Open("Flow", asset, errors);
            if (table == null) return result;
            for (int i = 0; i < table.Rows.Count; i++)
            {
                string[] row = table.Rows[i];
                string nodeId = table.Get(row, "nodeId");
                if (string.IsNullOrEmpty(nodeId)) { errors.Add(Where(table, i) + "节点ID为空。"); continue; }
                if (result.ContainsKey(nodeId)) { errors.Add(Where(table, i) + "节点ID重复：" + nodeId); continue; }
                string typeText = table.Get(row, "type");
                if (!TryParseType(typeText, out FlowNodeType type)) { errors.Add(Where(table, i) + "未知执行类型：" + typeText); continue; }
                string conditionText = table.Get(row, "conditionType");
                string thresholdText = table.Get(row, "threshold");
                var conditionType = FlowConditionType.LevelResult;
                int threshold = 0;
                if (type == FlowNodeType.Condition)
                {
                    if (conditionText == "candy_use_count") conditionType = FlowConditionType.CandyUseCount;
                    else if (!string.IsNullOrEmpty(conditionText) && conditionText != "level_result")
                        errors.Add(Where(table, i) + "未知条件类型：" + conditionText);
                    if (conditionType == FlowConditionType.CandyUseCount)
                    {
                        if (!int.TryParse(thresholdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out threshold) || threshold < 0)
                            errors.Add(Where(table, i) + "传传糖次数阈值必须为非负整数：" + thresholdText);
                    }
                    else if (!string.IsNullOrEmpty(thresholdText))
                        errors.Add(Where(table, i) + "关卡结果条件不应配置次数阈值。");
                }
                else if (!string.IsNullOrEmpty(conditionText) || !string.IsNullOrEmpty(thresholdText))
                    errors.Add(Where(table, i) + "只有 condition 节点可配置条件类型和阈值。");
                var item = new FlowRow
                {
                    NodeId = nodeId,
                    Type = type,
                    ContentId = table.Get(row, "contentId"),
                    PresentationId = table.Get(row, "presentationId"),
                    NextNodeId = table.Get(row, "nextNodeId"),
                    Note = table.Get(row, "note"),
                    ConditionType = conditionType,
                    Threshold = threshold
                };
                result[nodeId] = item;
                order.Add(item);
            }
            return result;
        }

        private static Dictionary<string, DialogueRow> BuildDialogue(string asset, List<string> errors)
        {
            var result = new Dictionary<string, DialogueRow>(StringComparer.Ordinal);
            var table = Open("Dialogue", asset, errors);
            if (table == null) return result;
            for (int i = 0; i < table.Rows.Count; i++)
            {
                string[] row = table.Rows[i];
                string textId = table.Get(row, "textId");
                if (string.IsNullOrEmpty(textId)) { errors.Add(Where(table, i) + "台词ID为空。"); continue; }
                if (result.ContainsKey(textId)) { errors.Add(Where(table, i) + "台词ID重复：" + textId); continue; }
                result[textId] = new DialogueRow
                {
                    TextId = textId,
                    Speaker = table.Get(row, "speaker"),
                    Text = table.Get(row, "text"),
                    Note = table.Get(row, "note")
                };
            }
            return result;
        }

        private static Dictionary<string, IReadOnlyList<PresentationRow>> BuildPresentations(string asset, List<string> errors)
        {
            var groups = new Dictionary<string, List<PresentationRow>>(StringComparer.Ordinal);
            var table = Open("Presentation", asset, errors);
            if (table == null) return new Dictionary<string, IReadOnlyList<PresentationRow>>();
            for (int i = 0; i < table.Rows.Count; i++)
            {
                string[] row = table.Rows[i];
                string id = table.Get(row, "presentationId");
                if (string.IsNullOrEmpty(id)) { errors.Add(Where(table, i) + "演出ID为空。"); continue; }
                string stepText = table.Get(row, "step");
                if (!int.TryParse(stepText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int step))
                { errors.Add(Where(table, i) + "步骤序号无效：" + stepText); continue; }
                if (!groups.TryGetValue(id, out var list)) { list = new List<PresentationRow>(); groups[id] = list; }
                float.TryParse(table.Get(row, "duration"), NumberStyles.Float, CultureInfo.InvariantCulture, out float duration);
                list.Add(new PresentationRow
                {
                    PresentationId = id,
                    Step = step,
                    TargetId = table.Get(row, "targetId"),
                    Action = table.Get(row, "action"),
                    Value = table.Get(row, "value"),
                    Duration = duration,
                    Note = table.Get(row, "note")
                });
            }
            var result = new Dictionary<string, IReadOnlyList<PresentationRow>>(StringComparer.Ordinal);
            foreach (var pair in groups)
            {
                pair.Value.Sort((a, b) => a.Step.CompareTo(b.Step));
                result[pair.Key] = pair.Value;
            }
            return result;
        }

        private static void BuildLevelResults(string asset, List<string> errors,
            Dictionary<string, LevelResultRow> results, Dictionary<string, List<LevelResultRow>> options,
            Dictionary<string, List<LevelResultRow>> routes)
        {
            var table = Open("LevelResults", asset, errors);
            if (table == null) return;
            for (int i = 0; i < table.Rows.Count; i++)
            {
                string[] row = table.Rows[i];
                string entry = table.Get(row, "entryNodeId");
                string resultId = table.Get(row, "resultId");
                if (string.IsNullOrEmpty(entry) || string.IsNullOrEmpty(resultId))
                { errors.Add(Where(table, i) + "入口节点或结果ID为空。"); continue; }
                int.TryParse(table.Get(row, "order"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int order);
                var item = new LevelResultRow
                {
                    EntryNodeId = entry,
                    ResultId = resultId,
                    NextNodeId = table.Get(row, "nextNodeId"),
                    TextId = table.Get(row, "textId"),
                    Order = order,
                    Note = table.Get(row, "note"),
                    SourceLevelId = table.Get(row, "sourceLevelId")
                };
                string key = StoryTables.LevelResultKey(entry, resultId);
                if (results.ContainsKey(key)) { errors.Add(Where(table, i) + "同一入口的结果/选项ID重复：" + entry + " / " + resultId); continue; }
                results[key] = item;
                if (item.IsOption)
                {
                    if (!options.TryGetValue(entry, out var list)) { list = new List<LevelResultRow>(); options[entry] = list; }
                    list.Add(item);
                }
                else
                {
                    if (!routes.TryGetValue(entry, out var list)) { list = new List<LevelResultRow>(); routes[entry] = list; }
                    list.Add(item);
                }
            }
        }

        private static Dictionary<string, AudioCueRow> BuildAudioCues(string asset, List<string> errors)
        {
            var result = new Dictionary<string, AudioCueRow>(StringComparer.Ordinal);
            var table = Open("AudioCues", asset, errors);
            if (table == null) return result;
            for (int i = 0; i < table.Rows.Count; i++)
            {
                string[] row = table.Rows[i];
                string nodeId = table.Get(row, "nodeId");
                if (string.IsNullOrEmpty(nodeId)) { errors.Add(Where(table, i) + "节点ID为空。"); continue; }
                if (result.ContainsKey(nodeId)) { errors.Add(Where(table, i) + "节点ID重复：" + nodeId); continue; }
                var cue = new AudioCueRow
                {
                    NodeId = nodeId,
                    Music = table.Get(row, "music"),
                    Ambience = table.Get(row, "ambience"),
                    Sfx = table.Get(row, "sfx"),
                    Note = table.Get(row, "note")
                };
                ValidateAudioCue(cue, Where(table, i), errors);
                result[nodeId] = cue;
            }
            return result;
        }

        private static void ValidateAudioCue(AudioCueRow cue, string where, List<string> errors)
        {
            if (!cue.HasMusic && !cue.HasAmbience && AudioCueRow.Split(cue.Sfx).Length == 0)
                errors.Add(where + "音频提示没有配置任何声音：" + cue.NodeId);
            if (cue.HasMusic && !IsKnownMusic(cue.Music))
                errors.Add(where + "music 取值无效：" + cue.Music + "（应为 soft/horror1/horror2/ad/soft_end/stop）");
            foreach (string id in AudioCueRow.Split(cue.Ambience))
            {
                if (id == "none") continue;
                if (!IsLibraryId(id)) errors.Add(where + "ambience 取值无效：" + id);
            }
            foreach (string id in AudioCueRow.Split(cue.Sfx))
                if (!IsLibraryId(id)) errors.Add(where + "sfx 取值无效：" + id);
        }

        private static bool IsKnownMusic(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "soft":
                case "horror1":
                case "horror2":
                case "ad":
                case "soft_end":
                case "stop":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsLibraryId(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok) return false;
            }
            return true;
        }

        private static void Validate(StoryTables tables, List<string> errors)
        {
            if (string.IsNullOrEmpty(tables.EntryNodeId) || !tables.TryGetFlow(tables.EntryNodeId, out var entry))
                errors.Add("入口节点不存在：" + tables.EntryNodeId);
            else if (entry.Type == FlowNodeType.End)
                errors.Add("入口节点不能是 end：" + tables.EntryNodeId);

            foreach (var node in tables.FlowRows)
            {
                switch (node.Type)
                {
                    case FlowNodeType.Dialogue:
                        if (!tables.HasDialogue(node.ContentId)) errors.Add(node.NodeId + " 引用了不存在的台词：" + node.ContentId);
                        if (!string.IsNullOrEmpty(node.PresentationId) && !tables.HasPresentation(node.PresentationId))
                            errors.Add(node.NodeId + " 引用了不存在的演出：" + node.PresentationId);
                        if (tables.HasRoutes(node.NodeId))
                        {
                            if (!string.IsNullOrEmpty(node.NextNodeId))
                                errors.Add(node.NodeId + " 配置了结果分支，nextNodeId 应留空。");
                        }
                        else RequireNext(tables, node, errors);
                        break;
                    case FlowNodeType.Presentation:
                        if (!tables.HasPresentation(node.ContentId)) errors.Add(node.NodeId + " 引用了不存在的演出：" + node.ContentId);
                        RequireNext(tables, node, errors);
                        break;
                    case FlowNodeType.Level:
                        if (string.IsNullOrEmpty(node.ContentId)) errors.Add(node.NodeId + " 缺少关卡ID。");
                        if (!string.IsNullOrEmpty(node.NextNodeId)) errors.Add(node.NodeId + " 为 level 节点，nextNodeId 应留空。");
                        ValidateLevelEntry(tables, node, errors);
                        break;
                    case FlowNodeType.End:
                        if (!string.IsNullOrEmpty(node.NextNodeId)) errors.Add(node.NodeId + " 为 end 节点，nextNodeId 应留空。");
                        if (!string.IsNullOrEmpty(node.ContentId) && !tables.HasDialogue(node.ContentId))
                            errors.Add(node.NodeId + " 的结局台词不存在：" + node.ContentId);
                        break;
                    case FlowNodeType.Choice:
                        if (!string.IsNullOrEmpty(node.ContentId)) errors.Add(node.NodeId + " 为 choice 节点，contentId 应留空。");
                        if (!string.IsNullOrEmpty(node.NextNodeId)) errors.Add(node.NodeId + " 为 choice 节点，nextNodeId 应留空。");
                        ValidateOptionEntry(tables, node, errors);
                        break;
                    case FlowNodeType.Menu:
                        if (!string.IsNullOrEmpty(node.ContentId)) errors.Add(node.NodeId + " 为 menu 节点，contentId 应留空。");
                        if (string.IsNullOrEmpty(node.NextNodeId)) errors.Add(node.NodeId + " 为 menu 节点，必须配置 nextNodeId。");
                        else RequireNext(tables, node, errors);
                        ValidateOptionEntry(tables, node, errors);
                        break;
                    case FlowNodeType.Condition:
                        if (!string.IsNullOrEmpty(node.ContentId)) errors.Add(node.NodeId + " 为 condition 节点，contentId 应留空。");
                        if (!string.IsNullOrEmpty(node.PresentationId)) errors.Add(node.NodeId + " 为 condition 节点，presentationId 应留空。");
                        if (!string.IsNullOrEmpty(node.NextNodeId)) errors.Add(node.NodeId + " 为 condition 节点，nextNodeId 应留空。");
                        if (!tables.HasRoutes(node.NodeId)) errors.Add(node.NodeId + " 为 condition 节点，但未配置结果分支。");
                        if (node.ConditionType == FlowConditionType.CandyUseCount)
                        {
                            var branches = tables.GetRoutes(node.NodeId);
                            if (branches.Count != 2 || tables.GetLevelResult(node.NodeId, "within") == null ||
                                tables.GetLevelResult(node.NodeId, "over") == null)
                                errors.Add(node.NodeId + " 传传糖次数条件必须配置 within 和 over 两个出口。");
                        }
                        break;
                }
            }

            foreach (var option in tables.OptionRows)
            {
                if (string.IsNullOrEmpty(option.TextId)) errors.Add("选项缺少文字ID：" + option.EntryNodeId + " / " + option.ResultId);
                else if (!tables.HasDialogue(option.TextId)) errors.Add("选项引用了不存在的台词：" + option.EntryNodeId + " / " + option.ResultId + " -> " + option.TextId);
                if (option.Order <= 0) errors.Add("选项 order 必须为正整数：" + option.EntryNodeId + " / " + option.ResultId);
                if (!tables.TryGetFlow(option.NextNodeId, out _)) errors.Add("选项跳转节点不存在：" + option.EntryNodeId + " / " + option.ResultId + " -> " + option.NextNodeId);
                if (tables.TryGetFlow(option.EntryNodeId, out var optionNode) && optionNode.Type == FlowNodeType.Dialogue)
                    errors.Add(option.EntryNodeId + " 为 dialogue 节点，不能配置选项：" + option.ResultId);
            }

            var levelResultIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var result in tables.LevelResultRows)
            {
                if (result.IsOption) continue;
                bool entryOk = tables.TryGetFlow(result.EntryNodeId, out var node)
                    && (node.Type == FlowNodeType.Level || node.Type == FlowNodeType.Dialogue || node.Type == FlowNodeType.Condition);
                if (!entryOk) errors.Add("结果行的入口节点必须是 level 或 dialogue 节点：" + result.EntryNodeId);
                if (!tables.TryGetFlow(result.NextNodeId, out _)) errors.Add("结果行跳转节点不存在：" + result.EntryNodeId + " / " + result.ResultId + " -> " + result.NextNodeId);
                if (entryOk && node.Type == FlowNodeType.Level) levelResultIds.Add(result.ResultId);
                if (entryOk && node.Type == FlowNodeType.Condition && node.ConditionType == FlowConditionType.CandyUseCount &&
                    !string.IsNullOrEmpty(result.SourceLevelId))
                    errors.Add("传传糖次数条件不应配置来源关卡：" + result.EntryNodeId);
            }

            // Dialogue result branches resolve against recorded level results; every route must be producible.
            foreach (var route in tables.LevelResultRows)
            {
                if (route.IsOption) continue;
                if (!tables.TryGetFlow(route.EntryNodeId, out var node) || (node.Type != FlowNodeType.Dialogue && node.Type != FlowNodeType.Condition)) continue;
                if (node.Type == FlowNodeType.Condition && node.ConditionType == FlowConditionType.CandyUseCount) continue;
                if (!levelResultIds.Contains(route.ResultId))
                    errors.Add("结果分支无法由任何关卡结果产生：" + route.EntryNodeId + " / " + route.ResultId);
                if (!string.IsNullOrEmpty(route.SourceLevelId))
                {
                    bool sourceFound = false;
                    foreach (var level in tables.FlowRows)
                        if (level.Type == FlowNodeType.Level && level.ContentId == route.SourceLevelId &&
                            tables.GetLevelResult(level.NodeId, route.ResultId) != null) sourceFound = true;
                    if (!sourceFound)
                        errors.Add("结果分支的来源关卡无法产生该结果：" + route.EntryNodeId + " / " + route.SourceLevelId + " / " + route.ResultId);
                }
            }

            // Audio cues hang off flow nodes; stale ids would silently never play.
            foreach (var cue in tables.AudioCueRows)
                if (!tables.TryGetFlow(cue.NodeId, out _))
                    errors.Add("音频提示引用了不存在的节点：" + cue.NodeId);
        }

        private static void ValidateLevelEntry(StoryTables tables, FlowRow node, List<string> errors)
        {
            bool any = false;
            foreach (var row in tables.LevelResultRows)
            {
                if (row.EntryNodeId != node.NodeId) continue;
                if (row.IsOption) errors.Add(node.NodeId + " 为 level 节点，结果行的 textId/order 应留空：" + row.ResultId);
                else any = true;
            }
            if (!any) errors.Add(node.NodeId + " 为 level 节点，但关卡结果表没有配置结果。");
        }

        private static void ValidateOptionEntry(StoryTables tables, FlowRow node, List<string> errors)
        {
            var options = tables.GetOptions(node.NodeId);
            if (options.Count == 0) { errors.Add(node.NodeId + " 为 " + node.Type.ToString().ToLowerInvariant() + " 节点，但未配置任何选项。"); return; }
            var orders = new HashSet<int>();
            foreach (var option in options)
                if (option.Order > 0 && !orders.Add(option.Order))
                    errors.Add(node.NodeId + " 的选项 order 重复：" + option.Order);
            foreach (var row in tables.LevelResultRows)
                if (row.EntryNodeId == node.NodeId && !row.IsOption)
                    errors.Add(node.NodeId + " 为 " + node.Type.ToString().ToLowerInvariant() + " 节点，结果行必须填写 textId/order：" + row.ResultId);
        }

        private static void RequireNext(StoryTables tables, FlowRow node, List<string> errors)
        {
            if (!tables.TryGetFlow(node.NextNodeId, out _)) errors.Add(node.NodeId + " 的下一节点不存在：" + node.NextNodeId);
        }

        private static CsvTable Open(string name, string asset, List<string> errors)
        {
            if (asset == null) { errors.Add("未配置 " + name + " 表。"); return null; }
            return new CsvTable(name, asset);
        }

        private static string Where(CsvTable table, int rowIndex) => table.Name + " 第 " + table.LineOf(rowIndex) + " 行：";

        private static bool TryParseType(string value, out FlowNodeType type)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "dialogue": type = FlowNodeType.Dialogue; return true;
                case "presentation": type = FlowNodeType.Presentation; return true;
                case "level": type = FlowNodeType.Level; return true;
                case "end": type = FlowNodeType.End; return true;
                case "choice": type = FlowNodeType.Choice; return true;
                case "menu": type = FlowNodeType.Menu; return true;
                case "condition": type = FlowNodeType.Condition; return true;
                default: type = default; return false;
            }
        }
    }
}
