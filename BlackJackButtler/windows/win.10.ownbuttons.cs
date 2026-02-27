using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private string _newCustomGroupName = string.Empty;
    private string _renameBuffer = string.Empty;
    private int _renamingGroupIndex = -1;

    private void DrawOwnButtonsPage()
    {
        ImGui.TextUnformatted("Own Buttons");
        ImGui.SameLine();
        if (BJBGui.SmallButton("?##varref_own")) _showVarRefPanel = !_showVarRefPanel;
        ImGui.Separator();
        ImGui.TextDisabled("Create your own command groups. They appear as buttons above the dealer row on the main page.");
        ImGui.TextDisabled("Use <t> for the target player name. Works the same way as the built-in command chains.");
        ImGui.Spacing();

        ImGui.SetNextItemWidth(300f);
        ImGui.InputText("##new_custom_group_name", ref _newCustomGroupName, 64);
        ImGui.SameLine();
        bool canAdd = !string.IsNullOrWhiteSpace(_newCustomGroupName)
            && !_config.CustomCommandGroups.Any(g => g.Name.Equals(_newCustomGroupName.Trim(), StringComparison.OrdinalIgnoreCase))
            && !_config.CommandGroups.Any(g => g.Name.Equals(_newCustomGroupName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!canAdd) ImGui.BeginDisabled();
        if (BJBGui.Button("Add Group"))
        {
            _config.CustomCommandGroups.Add(new CommandGroup
            {
                Name = _newCustomGroupName.Trim(),
                Commands = new()
            });
            _newCustomGroupName = string.Empty;
            _save();
        }
        if (!canAdd) ImGui.EndDisabled();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        int toRemoveIndex = -1;

        for (int i = 0; i < _config.CustomCommandGroups.Count; i++)
        {
            var group = _config.CustomCommandGroups[i];
            ImGui.PushID($"custom_group_{i}");

            bool isFirstGroup = i == 0;
            bool isLastGroup = i == _config.CustomCommandGroups.Count - 1;

            float reorderWidth = 52f;
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - reorderWidth);
            bool headerOpen = ImGui.CollapsingHeader($"{group.Name}###custom_grp_{i}", ImGuiTreeNodeFlags.DefaultOpen);

            ImGui.SameLine(ImGui.GetWindowWidth() - reorderWidth - ImGui.GetStyle().WindowPadding.X);
            if (isFirstGroup) ImGui.BeginDisabled();
            if (BJBGui.SmallButton($"^##grp_up_{i}"))
            {
                (_config.CustomCommandGroups[i - 1], _config.CustomCommandGroups[i]) =
                    (_config.CustomCommandGroups[i], _config.CustomCommandGroups[i - 1]);
                if (_renamingGroupIndex == i) _renamingGroupIndex = i - 1;
                else if (_renamingGroupIndex == i - 1) _renamingGroupIndex = i;
                _save();
                ImGui.PopID();
                break;
            }
            if (isFirstGroup) ImGui.EndDisabled();

            ImGui.SameLine();
            if (isLastGroup) ImGui.BeginDisabled();
            if (BJBGui.SmallButton($"v##grp_down_{i}"))
            {
                (_config.CustomCommandGroups[i], _config.CustomCommandGroups[i + 1]) =
                    (_config.CustomCommandGroups[i + 1], _config.CustomCommandGroups[i]);
                if (_renamingGroupIndex == i) _renamingGroupIndex = i + 1;
                else if (_renamingGroupIndex == i + 1) _renamingGroupIndex = i;
                _save();
                ImGui.PopID();
                break;
            }
            if (isLastGroup) ImGui.EndDisabled();

            if (headerOpen)
            {
                if (_renamingGroupIndex == i)
                {
                    ImGui.SetNextItemWidth(300f);
                    ImGui.InputText("##rename_group", ref _renameBuffer, 64);
                    if (ImGui.IsItemDeactivatedAfterEdit())
                    {
                        var trimmed = _renameBuffer.Trim();
                        bool nameValid = !string.IsNullOrWhiteSpace(trimmed)
                            && !_config.CustomCommandGroups.Where((g, idx) => idx != i).Any(g => g.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
                            && !_config.CommandGroups.Any(g => g.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
                        if (nameValid) { group.Name = trimmed; _save(); }
                        _renamingGroupIndex = -1;
                    }
                    ImGui.SameLine();
                    if (BJBGui.SmallButton("Cancel##rename_cancel")) _renamingGroupIndex = -1;
                }
                else
                {
                    if (BJBGui.SmallButton("Rename##rename_start"))
                    {
                        _renamingGroupIndex = i;
                        _renameBuffer = group.Name;
                    }
                }

                if (ImGui.Checkbox("Button Color##btn_col", ref group.UseCustomButtonColor)) _save();
                if (group.UseCustomButtonColor)
                {
                    ImGui.SameLine();
                    if (ImGui.ColorEdit4("##btn_col_pick", ref group.CustomButtonColor,
                        ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel)) _save();
                }

                if (ImGui.Checkbox("Text Color##txt_col", ref group.UseCustomTextColor)) _save();
                if (group.UseCustomTextColor)
                {
                    ImGui.SameLine();
                    if (ImGui.ColorEdit4("##txt_col_pick", ref group.CustomTextColor,
                        ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel)) _save();
                }

                var io = ImGui.GetIO();
                bool ctrlHeld = io.KeyCtrl;

                if (!ctrlHeld) ImGui.BeginDisabled();
                if (ctrlHeld) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0f, 0f, 1f));
                if (BJBGui.SmallButton("Delete Group"))
                {
                    toRemoveIndex = i;
                }
                if (ctrlHeld) ImGui.PopStyleColor();
                if (!ctrlHeld)
                {
                    ImGui.EndDisabled();
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        ImGui.SetTooltip("Hold CTRL to delete this group.");
                }

                DrawCommandGroupTable(group);

                if (BJBGui.Button("+ Add Command Step"))
                {
                    group.Commands.Add(new PluginCommand { Text = "/p New step...", Delay = 1.0f });
                    _save();
                }
            }

            ImGui.PopID();
            ImGui.Spacing();
        }

        if (toRemoveIndex >= 0)
        {
            if (_renamingGroupIndex == toRemoveIndex) _renamingGroupIndex = -1;
            _config.CustomCommandGroups.RemoveAt(toRemoveIndex);
            _save();
        }
    }
}
