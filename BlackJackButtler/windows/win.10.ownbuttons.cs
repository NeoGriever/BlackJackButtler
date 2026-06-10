using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private string _newCustomGroupName = string.Empty;
    private string _renameBuffer = string.Empty;
    private int _renamingGroupIndex = -1;
    private string _filterOwnButtons = string.Empty;

    private void EnsureButtonOrderMigration()
    {
        if (_config.CustomButtonOrder.Count == 0 && _config.CustomCommandGroups.Count > 0)
        {
            _config.CustomButtonOrder = _config.CustomCommandGroups.Select(g => g.Name).ToList();
            _save();
        }
    }

    private void DrawOwnButtonsPage()
    {
        EnsureButtonOrderMigration();

        ImGui.TextUnformatted("Own Buttons");
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
            var name = _newCustomGroupName.Trim();
            _config.CustomCommandGroups.Add(new CommandGroup
            {
                Name = name,
                Commands = new()
            });
            _config.CustomButtonOrder.Add(name);
            _newCustomGroupName = string.Empty;
            _save();
        }
        if (!canAdd) ImGui.EndDisabled();

        ImGui.Spacing();
        ImGui.Separator();
        BJBGui.DrawFilterBar("ownbtn", ref _filterOwnButtons, "Search button name or command...");
        ImGui.Spacing();

        int toRemoveIndex = -1;

        for (int i = 0; i < _config.CustomCommandGroups.Count; i++)
        {
            var group = _config.CustomCommandGroups[i];
            var shouldOpen = string.Equals(
                _pendingOwnButtonGroupName,
                group.Name,
                StringComparison.OrdinalIgnoreCase);

            if (!shouldOpen && !BJBGui.MatchesFilter(_filterOwnButtons, group.Name, group.Commands.Select(c => c.Text))) continue;

            ImGui.PushID($"custom_group_{i}");

            if (shouldOpen || !string.IsNullOrEmpty(_filterOwnButtons))
                ImGui.SetNextItemOpen(true, ImGuiCond.Always);

            bool headerOpen = ImGui.CollapsingHeader($"{group.Name}###custom_grp_{i}");
            if (shouldOpen && headerOpen)
            {
                _pendingOwnButtonGroupName = null;
                _filterOwnButtons = string.Empty;
                ImGui.SetScrollHereY();
            }

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
                        if (nameValid)
                        {
                            var oldName = group.Name;
                            group.Name = trimmed;
                            for (int o = 0; o < _config.CustomButtonOrder.Count; o++)
                                if (_config.CustomButtonOrder[o] == oldName)
                                    _config.CustomButtonOrder[o] = trimmed;
                            _save();
                        }
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

                if (ImGui.Checkbox("Active##active_toggle", ref group.IsActive)) _save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("When disabled, the button is hidden and the group cannot be executed.");
                ImGui.SameLine();
                if (!group.IsActive) ImGui.BeginDisabled();
                if (ImGui.Checkbox("Visible##visible_toggle", ref group.IsVisible)) _save();
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip("When hidden, the button won't show in the bar\nbut can still be called via Command Reference or Regex.");
                if (!group.IsActive) ImGui.EndDisabled();

                ImGui.SetNextItemWidth(300f);
                if (ImGui.InputText("Button Label##btn_label", ref group.ButtonLabel, 64)) _save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Custom text displayed on the button.\nLeave empty to use the group name.");

                if (ImGui.TreeNode("Style Overrides##style_overrides"))
                {
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

                    if (ImGui.Checkbox("Padding Override##pad_ovr", ref group.UseCustomPadding)) _save();
                    if (group.UseCustomPadding)
                    {
                        ImGui.Indent(20f);
                        DrawPaddingFields($"##btn_{i}", ref group.CustomPaddingH, ref group.CustomPaddingV);
                        ImGui.Unindent(20f);
                    }

                    if (ImGui.Checkbox("Font Override##font_ovr", ref group.UseCustomFont)) _save();
                    if (group.UseCustomFont)
                    {
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(100f);
                        int fIdx = group.CustomUseMono ? 1 : 0;
                        if (BJBGui.Combo("##btn_font", ref fIdx, "Default\0Mono\0"))
                        {
                            group.CustomUseMono = fIdx == 1;
                            _save();
                        }
                    }

                    if (ImGui.Checkbox("Font Size Override##fscale_ovr", ref group.UseCustomFontScale)) _save();
                    if (group.UseCustomFontScale)
                    {
                        ImGui.SameLine();
                        ImGui.SetNextItemWidth(150f);
                        if (BJBGui.SliderFloat("##btn_fscale", ref group.CustomFontScale, 0.5f, 2.0f, "%.2fx"))
                        {
                            group.CustomFontScale = (float)(Math.Round(group.CustomFontScale / 0.05) * 0.05);
                            group.CustomFontScale = Math.Clamp(group.CustomFontScale, 0.5f, 2.0f);
                            _save();
                        }
                    }

                    ImGui.TreePop();
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
            var deletedName = _config.CustomCommandGroups[toRemoveIndex].Name;
            _config.CustomCommandGroups.RemoveAt(toRemoveIndex);
            _config.CustomButtonOrder.RemoveAll(e => e == deletedName);
            _save();
        }

        DrawButtonOrderSection();
    }

    private void DrawButtonOrderSection()
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("Button Order");
        ImGui.SameLine();
        if (BJBGui.SmallButton("+ Break##add_break"))
        {
            _config.CustomButtonOrder.Add("---");
            _save();
        }

        if (_config.CustomButtonOrder.Count == 0)
        {
            ImGui.TextDisabled("No buttons configured.");
        }
        else
        {
            int removeAt = -1;
            int swapA = -1, swapB = -1;

            for (int i = 0; i < _config.CustomButtonOrder.Count; i++)
            {
                var entry = _config.CustomButtonOrder[i];
                bool isBreak = entry == "---";
                ImGui.PushID($"btn_order_{i}");

                bool isFirst = i == 0;
                bool isLast = i == _config.CustomButtonOrder.Count - 1;

                if (isFirst) ImGui.BeginDisabled();
                if (BJBGui.SmallButton("^##order_up"))
                {
                    swapA = i - 1;
                    swapB = i;
                }
                if (isFirst) ImGui.EndDisabled();

                ImGui.SameLine();
                if (isLast) ImGui.BeginDisabled();
                if (BJBGui.SmallButton("v##order_down"))
                {
                    swapA = i;
                    swapB = i + 1;
                }
                if (isLast) ImGui.EndDisabled();

                ImGui.SameLine();
                if (BJBGui.SmallButton("X##order_remove"))
                {
                    removeAt = i;
                }

                ImGui.SameLine();

                if (isBreak)
                {
                    ImGui.TextDisabled("--- (Break)");
                }
                else
                {
                    var group = _config.CustomCommandGroups.FirstOrDefault(g => g.Name == entry);
                    if (group != null)
                    {
                        string previewLabel = !string.IsNullOrEmpty(group.ButtonLabel) ? group.ButtonLabel : entry;

                        if (!group.IsActive)
                        {
                            ImGui.TextDisabled($"[inactive] {previewLabel}");
                        }
                        else if (!group.IsVisible)
                        {
                            ImGui.TextDisabled($"[hidden] {previewLabel}");
                        }
                        else
                        {
                            int colorPushCount = 0;
                            if (group.UseCustomButtonColor)
                            {
                                ImGui.PushStyleColor(ImGuiCol.Button, group.CustomButtonColor);
                                colorPushCount++;
                            }
                            if (group.UseCustomTextColor)
                            {
                                ImGui.PushStyleColor(ImGuiCol.Text, group.CustomTextColor);
                                colorPushCount++;
                            }

                            ImGui.BeginDisabled();
                            if (colorPushCount > 0 && group.UseCustomTextColor)
                                ImGui.SmallButton(previewLabel);
                            else
                                BJBGui.SmallButton(previewLabel);
                            ImGui.EndDisabled();

                            if (colorPushCount > 0) ImGui.PopStyleColor(colorPushCount);
                        }
                    }
                    else
                    {
                        ImGui.TextDisabled(entry);
                        ImGui.SameLine();
                        ImGui.TextDisabled("(missing)");
                    }
                }

                ImGui.PopID();
            }

            if (swapA >= 0 && swapB >= 0)
            {
                (_config.CustomButtonOrder[swapA], _config.CustomButtonOrder[swapB]) =
                    (_config.CustomButtonOrder[swapB], _config.CustomButtonOrder[swapA]);
                _save();
            }

            if (removeAt >= 0)
            {
                _config.CustomButtonOrder.RemoveAt(removeAt);
                _save();
            }
        }

        var unassigned = _config.CustomCommandGroups
            .Where(g => !_config.CustomButtonOrder.Contains(g.Name))
            .ToList();

        if (unassigned.Count > 0)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("Unassigned:");
            foreach (var g in unassigned)
            {
                if (BJBGui.SmallButton($"+##{g.Name}_assign"))
                {
                    _config.CustomButtonOrder.Add(g.Name);
                    _save();
                }
                ImGui.SameLine();
                ImGui.TextUnformatted(g.Name);
            }
        }
    }
}
