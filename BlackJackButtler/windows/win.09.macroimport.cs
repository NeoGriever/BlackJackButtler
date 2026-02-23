using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using RRX = System.Text.RegularExpressions;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private readonly Dictionary<string, int> _macroImportSelections = new();
    private readonly Dictionary<string, List<PluginCommand>> _macroPreviewCache = new();

    private static readonly RRX.Regex InlineWaitRegex =
        new(@"<wait\.(\d+(?:\.\d+)?)>\s*$", RRX.RegexOptions.Compiled);

    private static readonly RRX.Regex StandaloneWaitRegex =
        new(@"^/(wait|pause|warte)\s+(\d+(?:\.\d+)?)\s*$",
            RRX.RegexOptions.Compiled | RRX.RegexOptions.IgnoreCase);

    private static unsafe List<PluginCommand> ParseMacroToCommands(RaptureMacroModule.Macro* macro)
    {
        var commands = new List<PluginCommand>();

        for (int line = 0; line < 15; line++)
        {
            var raw = macro->Lines[line].ToString().Trim();
            if (raw.Length == 0) continue;

            var standaloneMatch = StandaloneWaitRegex.Match(raw);
            if (standaloneMatch.Success)
            {
                var delay = float.TryParse(standaloneMatch.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0.5f;
                if (commands.Count > 0)
                    commands[^1].Delay = delay;
                else
                    commands.Add(new PluginCommand { Text = string.Empty, Delay = delay, Enabled = true });
                continue;
            }

            var text = raw;
            float cmdDelay = 0.5f;

            var inlineMatch = InlineWaitRegex.Match(raw);
            if (inlineMatch.Success)
            {
                text = raw[..inlineMatch.Index].TrimEnd();
                cmdDelay = float.TryParse(inlineMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0.5f;
            }

            commands.Add(new PluginCommand { Text = text, Delay = cmdDelay, Enabled = true });
        }

        return commands;
    }

    private void DrawMacroPreview(string macroId, List<PluginCommand> commands)
    {
        if (commands.Count == 0) return;

        ImGui.Indent(20f);
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.7f);

        if (ImGui.BeginTable($"preview_{macroId}", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Act", ImGuiTableColumnFlags.WidthFixed, 30);
            ImGui.TableSetupColumn("Command / Chat Message", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Wait (s)", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableHeadersRow();

            for (int i = 0; i < commands.Count; i++)
            {
                var cmd = commands[i];
                ImGui.TableNextRow();
                ImGui.PushID($"prev_{macroId}_{i}");

                ImGui.TableNextColumn();
                ImGui.BeginDisabled();
                var enabled = cmd.Enabled;
                ImGui.Checkbox("##act", ref enabled);
                ImGui.EndDisabled();

                ImGui.TableNextColumn();
                if (string.IsNullOrEmpty(cmd.Text))
                    ImGui.TextDisabled("(wait only)");
                else
                    ImGui.TextUnformatted(cmd.Text);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{cmd.Delay:F1}s");

                ImGui.PopID();
            }

            ImGui.EndTable();
        }

        ImGui.PopStyleVar();
        ImGui.Unindent(20f);
    }

    private unsafe void DrawMacroImportPage()
    {
        ImGui.TextUnformatted("Macro Import (Debug)");
        ImGui.Separator();

        var groupNames = new List<string> { "No import" };
        groupNames.AddRange(_config.CommandGroups.Select(g => g.Name));
        var standardCount = groupNames.Count;
        groupNames.AddRange(_config.CustomCommandGroups.Select(g => $"[USER] {g.Name}"));
        var groupArray = groupNames.ToArray();

        ImGui.BeginChild("macro_list", new Vector2(0, -35), true);

        var module = RaptureMacroModule.Instance();

        for (int set = 0; set <= 1; set++)
        {
            var prefix = set == 0 ? "C" : "G";

            for (int index = 0; index < 100; index++)
            {
                var macro = module->GetMacro((uint)set, (uint)index);

                bool hasContent = false;
                for (int line = 0; line < 15; line++)
                {
                    if (macro->Lines[line].ToString().Length > 0)
                    {
                        hasContent = true;
                        break;
                    }
                }
                if (!hasContent) continue;

                var id = $"{prefix}{index:D2}";
                var name = macro->Name.ToString();

                if (!_macroImportSelections.ContainsKey(id))
                    _macroImportSelections[id] = 0;

                var tagColor = set == 0
                    ? new Vector4(0.4f, 0.7f, 1.0f, 1.0f)
                    : new Vector4(1.0f, 0.6f, 0.2f, 1.0f);

                var displayName = string.IsNullOrEmpty(name)
                    ? $"Macro #{index:D2}"
                    : $"\"{name}\"";

                ImGui.PushStyleColor(ImGuiCol.Text, tagColor);
                if (BJBGui.SmallButton($"{id}##{id}_btn"))
                {
                    var agent = AgentMacro.Instance();
                    agent->OpenMacro((uint)set, (uint)index);
                }
                ImGui.PopStyleColor();

                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Text, tagColor);
                ImGui.TextUnformatted(displayName);
                ImGui.PopStyleColor();

                if (ImGui.IsItemHovered())
                {
                    var tooltipLines = new List<string>();
                    for (int line = 0; line < 15; line++)
                    {
                        var text = macro->Lines[line].ToString();
                        if (text.Length > 0)
                            tooltipLines.Add(text);
                    }
                    ImGui.SetTooltip(string.Join("\n", tooltipLines));
                }

                ImGui.SameLine(300f);

                var sel = _macroImportSelections[id];
                ImGui.SetNextItemWidth(300f);
                if (BJBGui.Combo($"##{id}", ref sel, groupArray, groupArray.Length))
                {
                    _macroImportSelections[id] = sel;
                    _macroPreviewCache.Remove(id);
                }

                if (sel > 0)
                {
                    if (!_macroPreviewCache.TryGetValue(id, out var cmds))
                    {
                        cmds = ParseMacroToCommands(macro);
                        _macroPreviewCache[id] = cmds;
                    }
                    DrawMacroPreview(id, cmds);
                }
                else
                {
                    _macroPreviewCache.Remove(id);
                }
            }
        }

        ImGui.EndChild();

        if (BJBGui.Button("Try to import"))
        {
            foreach (var kvp in _macroImportSelections)
            {
                if (kvp.Value <= 0) continue;
                if (!_macroPreviewCache.TryGetValue(kvp.Key, out var cmds) || cmds.Count == 0) continue;

                CommandGroup? target;
                if (kvp.Value < standardCount)
                {
                    var name = groupArray[kvp.Value];
                    target = _config.CommandGroups.FirstOrDefault(g => g.Name == name);
                }
                else
                {
                    var idx = kvp.Value - standardCount;
                    target = idx >= 0 && idx < _config.CustomCommandGroups.Count
                        ? _config.CustomCommandGroups[idx]
                        : null;
                }

                if (target != null)
                {
                    target.Commands.Clear();
                    target.Commands.AddRange(cmds.Select(c => new PluginCommand
                    {
                        Text = c.Text,
                        Delay = c.Delay,
                        Enabled = c.Enabled
                    }));
                }
            }
            _save();
        }
    }
}
