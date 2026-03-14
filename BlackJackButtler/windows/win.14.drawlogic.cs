using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private bool _showDrawLogicDoc = false;
    private string _newDrawLogicName = "";

    private void EnsureDrawLogicSeed()
    {
        if (_config.DrawLogicSeeded) return;
        _config.DrawLogicSeeded = true;
        if (_config.DrawLogicEntries.Count == 0)
        {
            _config.DrawLogicEntries.Add(new DrawLogicEntry
            {
                Name = "Player Cross",
                IsIterate = true,
                Script = "// Golden cross at player feet (0.3 yalm radius)\n"
                       + "SetDrawColor(1.0, 0.85, 0.0, 0.7)\n"
                       + "BeginShape(<pos>.x, <pos>.y, <pos>.z)\n"
                       + "BeginPath()\n"
                       + "MoveTo(-0.3, 0, -0.3)\n"
                       + "LineTo(0.3, 0, 0.3)\n"
                       + "EndPath()\n"
                       + "BeginPath()\n"
                       + "MoveTo(0.3, 0, -0.3)\n"
                       + "LineTo(-0.3, 0, 0.3)\n"
                       + "EndPath()\n"
                       + "FinishShape()\n"
                       + "Draw()",
            });
        }
        _save();
    }

    private void DrawDrawLogicPage()
    {
        EnsureDrawLogicSeed();

        ImGui.TextUnformatted("Draw Logic");
        ImGui.SameLine();
        if (BJBGui.SmallButton("?##drawlogic_doc")) _showDrawLogicDoc = !_showDrawLogicDoc;
        ImGui.Separator();
        ImGui.TextDisabled("Scriptable world-drawing system. Define draw commands that execute per frame.");
        ImGui.TextDisabled("When 'Iterate' is on, the script runs once per active player + dealer.");
        ImGui.Spacing();

        var entries = _config.DrawLogicEntries;
        var names = new List<string> { "(None)" };
        names.AddRange(entries.Select(e => e.Name));

        int currentIdx = string.IsNullOrEmpty(_config.DrawLogicStartEntry)
            ? 0
            : names.IndexOf(_config.DrawLogicStartEntry);
        if (currentIdx < 0) currentIdx = 0;

        ImGui.SetNextItemWidth(300f);
        if (BJBGui.Combo("Start Entry##drawlogic_start", ref currentIdx,
            string.Join('\0', names) + '\0'))
        {
            _config.DrawLogicStartEntry = currentIdx == 0 ? "" : names[currentIdx];
            _save();
        }

        ImGui.Spacing();

        ImGui.SetNextItemWidth(300f);
        ImGui.InputText("##new_drawlogic_name", ref _newDrawLogicName, 64);
        ImGui.SameLine();
        bool canAdd = !string.IsNullOrWhiteSpace(_newDrawLogicName)
            && !entries.Any(e => e.Name.Equals(_newDrawLogicName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!canAdd) ImGui.BeginDisabled();
        if (BJBGui.Button("+ New Entry"))
        {
            entries.Add(new DrawLogicEntry { Name = _newDrawLogicName.Trim() });
            _newDrawLogicName = "";
            _save();
        }
        if (!canAdd) ImGui.EndDisabled();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        int toRemove = -1;
        int swapA = -1, swapB = -1;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            ImGui.PushID($"drawlogic_{i}");

            bool open = ImGui.CollapsingHeader($"{entry.Name}###dl_{i}", ImGuiTreeNodeFlags.DefaultOpen);

            if (open)
            {
                var name = entry.Name;
                ImGui.SetNextItemWidth(300f);
                if (ImGui.InputText("Name##dl_name", ref name, 64))
                {
                    var trimmed = name.Trim();
                    bool nameValid = !string.IsNullOrWhiteSpace(trimmed)
                        && !entries.Where((e, idx) => idx != i)
                            .Any(e => e.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
                    if (nameValid)
                    {
                        if (_config.DrawLogicStartEntry == entry.Name)
                            _config.DrawLogicStartEntry = trimmed;
                        entry.Name = trimmed;
                        _save();
                    }
                }

                var iterate = entry.IsIterate;
                if (ImGui.Checkbox("Iterate (run per player)##dl_iter", ref iterate))
                {
                    entry.IsIterate = iterate;
                    _save();
                }

                ImGui.TextUnformatted("Script:");
                var script = entry.Script;
                if (ImGui.InputTextMultiline($"##dl_script_{i}", ref script, 8192,
                    new Vector2(-1, 200), ImGuiInputTextFlags.AllowTabInput))
                {
                    entry.Script = script;
                    _save();
                }

                bool isFirst = i == 0;
                bool isLast = i == entries.Count - 1;

                if (isFirst) ImGui.BeginDisabled();
                if (BJBGui.SmallButton("Up##dl_up")) { swapA = i - 1; swapB = i; }
                if (isFirst) ImGui.EndDisabled();

                ImGui.SameLine();
                if (isLast) ImGui.BeginDisabled();
                if (BJBGui.SmallButton("Down##dl_down")) { swapA = i; swapB = i + 1; }
                if (isLast) ImGui.EndDisabled();

                ImGui.SameLine();
                var io = ImGui.GetIO();
                bool ctrlHeld = io.KeyCtrl;
                if (!ctrlHeld) ImGui.BeginDisabled();
                if (ctrlHeld) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0f, 0f, 1f));
                if (BJBGui.SmallButton("Delete##dl_del")) toRemove = i;
                if (ctrlHeld) ImGui.PopStyleColor();
                if (!ctrlHeld)
                {
                    ImGui.EndDisabled();
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        ImGui.SetTooltip("Hold CTRL to delete.");
                }
            }

            ImGui.PopID();
            ImGui.Spacing();
        }

        if (swapA >= 0 && swapB >= 0 && swapB < entries.Count)
        {
            (entries[swapA], entries[swapB]) = (entries[swapB], entries[swapA]);
            _save();
        }

        if (toRemove >= 0)
        {
            var deletedName = entries[toRemove].Name;
            entries.RemoveAt(toRemove);
            if (_config.DrawLogicStartEntry == deletedName)
                _config.DrawLogicStartEntry = "";
            _save();
        }
    }

    private void ExecuteDrawLogic()
    {
        if (string.IsNullOrEmpty(_config.DrawLogicStartEntry)) return;
        if (_config.DrawLogicEntries.Count == 0) return;

        DrawLogicInterpreter.ExecuteStartEntry(
            _config.DrawLogicEntries,
            _config.DrawLogicStartEntry,
            _players,
            _dealer,
            _config);
    }
}
