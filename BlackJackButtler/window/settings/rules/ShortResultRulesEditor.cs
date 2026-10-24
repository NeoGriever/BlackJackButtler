using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using BlackJackButtler.Chat;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawShortResultRulesEditor()
    {
        if (!ImGui.CollapsingHeader("Short-Result configuration##short_result_configuration"))
            return;

        ImGui.Indent(18f);
        DrawShortResultRulesEditorBody();
        ImGui.Unindent(18f);
    }

    private void DrawShortResultRulesEditorBody()
    {
        _config.ShortResultRules ??= Configuration.CreateDefaultShortResultRules();
        var rules = _config.ShortResultRules;
        ImGui.TextWrapped("Builds ${results} from top to bottom. <data> is replaced by the selected result data.");

        if (!ImGui.BeginTable("bjb_short_result_editor_layout", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV))
            return;

        ImGui.TableSetupColumn("Rules", ImGuiTableColumnFlags.WidthStretch, 1.7f);
        ImGui.TableSetupColumn("Example output", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        var canUndoShortResult = _shortResultUndoHistory.Count > 0;
        if (!canUndoShortResult) ImGui.BeginDisabled();
        if (BJBGui.SmallButton($"Undo ({_shortResultUndoHistory.Count})##short_result_undo"))
        {
            var last = _shortResultUndoHistory[^1];
            _shortResultUndoHistory.RemoveAt(_shortResultUndoHistory.Count - 1);
            _config.ShortResultRules = CloneShortResultRules(last);
            _save();
        }
        if (!canUndoShortResult) ImGui.EndDisabled();

        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            ImGui.PushID($"short_result_rule_{i}");
            ImGui.Separator();
            var changedOrder = false;
            var beforeEdit = CloneShortResultRules(rules);
            var ruleEdited = false;
            var summary = $"{i + 1}. {rule.Data}: {rule.Template}";

            if (BJBGui.SmallButton("X##delete"))
            {
                PushShortResultUndo(beforeEdit);
                rules.RemoveAt(i);
                changedOrder = true;
            }
            ImGui.SameLine();
            if (i == 0) ImGui.BeginDisabled();
            if (BJBGui.SmallButton("↑##move_up"))
            {
                PushShortResultUndo(beforeEdit);
                (rules[i - 1], rules[i]) = (rules[i], rules[i - 1]);
                changedOrder = true;
            }
            if (i == 0) ImGui.EndDisabled();
            ImGui.SameLine();
            if (i == rules.Count - 1) ImGui.BeginDisabled();
            if (BJBGui.SmallButton("↓##move_down"))
            {
                PushShortResultUndo(beforeEdit);
                (rules[i + 1], rules[i]) = (rules[i], rules[i + 1]);
                changedOrder = true;
            }
            if (i == rules.Count - 1) ImGui.EndDisabled();
            ImGui.SameLine();
            if (BJBGui.SmallButton("⧉##duplicate"))
            {
                PushShortResultUndo(beforeEdit);
                rules.Insert(i + 1, rule.Clone());
                changedOrder = true;
            }
            ImGui.SameLine();

            if (!changedOrder && ImGui.CollapsingHeader($"{summary}###short_result_rule_header"))
            {

            var source = (int)rule.Data;
            ImGui.SetNextItemWidth(180f);
            if (BJBGui.Combo("Data", ref source, "None (no data)\0Winners\0Pushed\0Loosed\0Busted\0"))
            {
                rule.Data = (ShortResultDataSource)source;
                ruleEdited = true;
            }

            if (DrawShortResultRuleToggle("Visible if empty", ref rule.VisibleIfEmpty)) ruleEdited = true;
            if (DrawShortResultRuleToggle("Visible if content before is empty", ref rule.VisibleIfContentBeforeIsEmpty)) ruleEdited = true;
            if (DrawShortResultRuleToggle("Visible if content after is empty", ref rule.VisibleIfContentAfterIsEmpty)) ruleEdited = true;
            if (DrawShortResultRuleToggle("Compress", ref rule.Compress)) ruleEdited = true;

            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("Template", ref rule.Template, 512)) ruleEdited = true;
            }

            ImGui.PopID();
            if (changedOrder)
            {
                _save();
                break;
            }
            if (ruleEdited)
            {
                PushShortResultUndo(beforeEdit);
                _save();
            }
        }

        ImGui.Separator();
        if (BJBGui.SmallButton("Add result rule"))
        {
            PushShortResultUndo(CloneShortResultRules(rules));
            rules.Add(new ShortResultRule());
            _save();
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton("Reset result rules"))
        {
            PushShortResultUndo(CloneShortResultRules(rules));
            _config.ShortResultRules = Configuration.CreateDefaultShortResultRules();
            _save();
        }

        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("Outer result template", ref _config.ResultTemplate, 512))
            _save();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Optional wrapper around the rule output. Use ${results}; default is ${results}.");

        ImGui.Separator();
        ImGui.TextUnformatted("Rule-list import / export");
        if (BJBGui.SmallButton("Export rules"))
        {
            var json = JsonConvert.SerializeObject(_config.ShortResultRules, Formatting.Indented);
            _fileDialogManager.SaveFileDialog(
                "Export Short-Result Rules",
                "JSON Files{.json}",
                "bjb_short_result_rules",
                ".json",
                (ok, path) =>
                {
                    if (!ok || string.IsNullOrWhiteSpace(path))
                        return;
                    try
                    {
                        System.IO.File.WriteAllText(path, json);
                        _shortResultImportStatus = $"Exported rules to {path}";
                    }
                    catch (Exception ex)
                    {
                        _shortResultImportStatus = $"Export failed: {ex.Message}";
                    }
                }
            );
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton("Import rules"))
        {
            _fileDialogManager.OpenFileDialog(
                "Import Short-Result Rules",
                "JSON Files{.json}",
                (ok, path) =>
                {
                    if (!ok || string.IsNullOrWhiteSpace(path))
                        return;
                    try
                    {
                        var json = System.IO.File.ReadAllText(path);
                        var imported = JsonConvert.DeserializeObject<List<ShortResultRule>>(json)
                            ?? throw new JsonException("The JSON file does not contain a rule list.");
                        if (imported.Any(rule => rule == null))
                            throw new JsonException("The rule list contains null entries.");

                        PushShortResultUndo(CloneShortResultRules(_config.ShortResultRules));
                        _config.ShortResultRules = CloneShortResultRules(imported);
                        _config.ShortResultRulesInitialized = true;
                        _shortResultImportStatus = $"Imported {imported.Count} rules from {path}";
                        _save();
                    }
                    catch (Exception ex)
                    {
                        _shortResultImportStatus = $"Import failed: {ex.Message}";
                    }
                }
            );
        }
        if (!string.IsNullOrWhiteSpace(_shortResultImportStatus))
            ImGui.TextWrapped(_shortResultImportStatus);

        ImGui.TableNextColumn();
        ImGui.TextUnformatted("Example output");
        var examples = new[]
        {
            RenderShortResultExample(
                new[] { "Alice Winner", "Bob Winner" },
                new[] { "Cara Push", "Dorian Push" },
                new[] { "Eve Lost", "Finn Lost" },
                new[] { "Gina Bust", "Hugo Bust" }
            ),
            RenderShortResultExample(
                Array.Empty<string>(),
                new[] { "Cara Push", "Dorian Push" },
                Array.Empty<string>(),
                new[] { "Gina Bust", "Hugo Bust" }
            ),
            RenderShortResultExample(
                new[] { "Alice Winner" },
                new[] { "Cara Push" },
                Array.Empty<string>(),
                new[] { "Gina Bust", "Hugo Bust" }
            ),
            RenderShortResultExample(
                new[] { "Gina Bust", "Hugo Bust" },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>()
            ),
        };
        var preview = string.Join("\n---\n", examples);
        ImGui.InputTextMultiline(
            "##short_result_example_output",
            ref preview,
            4096,
            new Vector2(
                -1f,
                260f
            ),
            ImGuiInputTextFlags.ReadOnly
        );

        ImGui.EndTable();
    }

    private string RenderShortResultExample(string[] winners, string[] pushed, string[] loosed, string[] busted)
    {
        var ruleOutput = ShortResultFormatter.Render(_config, winners, pushed, loosed, busted);
        var outerTemplate = string.IsNullOrWhiteSpace(_config.ResultTemplate) ? "${results}" : _config.ResultTemplate;
        return outerTemplate
            .Replace("${results}", ruleOutput)
            .Replace("<results>", ruleOutput)
            .Replace("${winners}", $"Winners: {FormatShortResultPreviewData(winners)}")
            .Replace("${pushed}", $"Pushed: {FormatShortResultPreviewData(pushed)}")
            .Replace("${loosers}", $"Lost: {FormatShortResultPreviewData(loosed)}")
            .Replace("${busted}", $"Busted: {FormatShortResultPreviewData(busted)}");
    }

    private static string FormatShortResultPreviewData(IEnumerable<string> values)
    {
        var joined = string.Join(", ", values);
        return string.IsNullOrEmpty(joined) ? "~" : joined;
    }

    private static List<ShortResultRule> CloneShortResultRules(IEnumerable<ShortResultRule> rules)
        => rules.Select(rule => rule.Clone()).ToList();

    private static bool DrawShortResultRuleToggle(string label, ref bool value)
    {
        ImGui.TextUnformatted(label);
        ImGui.SameLine(260f);
        return BJBOnOffSwitch.Draw($"short_result_{label}", ref value);
    }

    private void PushShortResultUndo(List<ShortResultRule> snapshot)
    {
        _shortResultUndoHistory.Add(snapshot);
        if (_shortResultUndoHistory.Count > 100)
            _shortResultUndoHistory.RemoveAt(0);
    }
}
