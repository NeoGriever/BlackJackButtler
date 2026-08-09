using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private string _newCustomGroupName = string.Empty;
    private string _filterOwnButtons = string.Empty;

    private void EnsureButtonOrderMigration()
    {
        if (_config.EnsureCustomButtonEntriesMigration())
            _save();
    }

    private void DrawOwnButtonsPage()
    {
        EnsureButtonOrderMigration();

        ImGui.TextUnformatted("Own Buttons");
        ImGui.Separator();
        BJBGui.DrawFilterBar("ownbtn", ref _filterOwnButtons, "Filter");
        ImGui.Spacing();

        var removeAt = -1;
        for (var index = 0; index < _config.CustomButtonEntries.Count; index++)
        {
            var entry = _config.CustomButtonEntries[index];
            if (!MatchesOwnButtonFilter(entry))
                continue;

            ImGui.PushID($"custom_button_entry_{index}");
            DrawOwnButtonRow(index, entry, ref removeAt);
            ImGui.PopID();
            ImGui.Spacing();
        }

        if (removeAt >= 0)
            RemoveCustomButtonEntry(removeAt);

        ImGui.Separator();
        ImGui.SetNextItemWidth(Math.Max(160f, ImGui.GetContentRegionAvail().X - 190f));
        ImGui.InputText("##new_custom_group_name", ref _newCustomGroupName, 64);
        ImGui.SameLine();
        var canAdd = IsCustomGroupNameAvailable(_newCustomGroupName);
        if (!canAdd) ImGui.BeginDisabled();
        if (BJBGui.Button("Add Group"))
        {
            var group = new CommandGroup
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = _newCustomGroupName.Trim(),
                Commands = new(),
            };
            _config.CustomCommandGroups.Add(group);
            _config.CustomButtonEntries.Add(new CustomButtonEntry { GroupId = group.Id });
            _config.SyncLegacyCustomButtonOrder();
            _newCustomGroupName = string.Empty;
            _save();
        }
        if (!canAdd) ImGui.EndDisabled();
        ImGui.SameLine();
        if (BJBGui.Button("Add Break"))
        {
            _config.CustomButtonEntries.Add(new CustomButtonEntry { IsBreak = true });
            _config.SyncLegacyCustomButtonOrder();
            _save();
        }
    }

    private void DrawOwnButtonRow(int index, CustomButtonEntry entry, ref int removeAt)
    {
        var isFirst = index == 0;
        var isLast = index == _config.CustomButtonEntries.Count - 1;
        if (isFirst) ImGui.BeginDisabled();
        if (BJBGui.SmallButton("^##move_up"))
        {
            (_config.CustomButtonEntries[index - 1], _config.CustomButtonEntries[index]) =
                (_config.CustomButtonEntries[index], _config.CustomButtonEntries[index - 1]);
            _config.SyncLegacyCustomButtonOrder();
            _save();
        }
        if (isFirst) ImGui.EndDisabled();

        ImGui.SameLine();
        if (isLast) ImGui.BeginDisabled();
        if (BJBGui.SmallButton("v##move_down"))
        {
            (_config.CustomButtonEntries[index + 1], _config.CustomButtonEntries[index]) =
                (_config.CustomButtonEntries[index], _config.CustomButtonEntries[index + 1]);
            _config.SyncLegacyCustomButtonOrder();
            _save();
        }
        if (isLast) ImGui.EndDisabled();

        ImGui.SameLine();
        var ctrlHeld = ImGui.GetIO().KeyCtrl;
        if (!ctrlHeld) ImGui.BeginDisabled();
        if (ctrlHeld) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.55f, 0.05f, 0.05f, 1f));
        if (BJBGui.SmallButton("X##delete")) removeAt = index;
        if (ctrlHeld) ImGui.PopStyleColor();
        if (!ctrlHeld)
        {
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("Hold CTRL to delete this entry.");
        }

        ImGui.SameLine();
        var group = FindCustomButtonGroup(entry);
        var visible = entry.IsBreak ? entry.IsVisible : group?.IsVisible ?? entry.IsVisible;
        if (DrawOwnButtonStateButton("O##visible", visible, "Visible in the button bar"))
        {
            if (entry.IsBreak)
                entry.IsVisible = !entry.IsVisible;
            else if (group != null)
                group.IsVisible = !group.IsVisible;
            else
                entry.IsVisible = !entry.IsVisible;
            _save();
        }

        ImGui.SameLine();
        if (entry.IsBreak)
        {
            DrawBreakHeader(entry, index);
            return;
        }

        if (group == null)
        {
            ImGui.TextDisabled(string.IsNullOrWhiteSpace(entry.LegacyGroupName)
                ? "[missing group]"
                : $"[missing] {entry.LegacyGroupName}");
            return;
        }

        var activeAndVisible = group.IsActive && group.IsVisible;
        var headerColor = activeAndVisible
            ? new Vector4(0.22f, 0.34f, 0.49f, 1f)
            : new Vector4(0.13f, 0.13f, 0.13f, 1f);
        var shouldOpen = string.Equals(_pendingOwnButtonGroupName, group.Name, StringComparison.OrdinalIgnoreCase);
        if (shouldOpen || !string.IsNullOrEmpty(_filterOwnButtons))
            ImGui.SetNextItemOpen(true, ImGuiCond.Always);
        ImGui.PushStyleColor(ImGuiCol.Header, headerColor);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(headerColor.X + 0.05f, headerColor.Y + 0.05f, headerColor.Z + 0.05f, 1f));
        var open = ImGui.CollapsingHeader($"{group.Name}###group_header_{index}");
        ImGui.PopStyleColor(2);

        if (shouldOpen && open)
        {
            _pendingOwnButtonGroupName = null;
            _filterOwnButtons = string.Empty;
            ImGui.SetScrollHereY();
        }
        if (open)
            DrawOwnButtonGroupEditor(group, entry, index);
    }

    private void DrawBreakHeader(CustomButtonEntry entry, int index)
    {
        var color = entry.IsVisible
            ? new Vector4(0.22f, 0.34f, 0.49f, 1f)
            : new Vector4(0.13f, 0.13f, 0.13f, 1f);
        ImGui.PushStyleColor(ImGuiCol.Header, color);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, color);
        ImGui.TreeNodeEx($"--Break--###break_{index}",
            ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen);
        ImGui.PopStyleColor(2);
    }

    private void DrawOwnButtonGroupEditor(CommandGroup group, CustomButtonEntry entry, int index)
    {
        var active = group.IsActive;
        if (DrawOwnButtonStateButton("+##active", active, "Active"))
        {
            group.IsActive = !group.IsActive;
            _save();
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(300f);
        var groupName = group.Name;
        if (ImGui.InputText("##group_name", ref groupName, 64) && IsCustomGroupNameAvailable(groupName, group.Id))
        {
            group.Name = groupName.Trim();
            _config.SyncLegacyCustomButtonOrder();
            _save();
        }

        ImGui.TextUnformatted("Label:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(300f);
        if (ImGui.InputTextWithHint("##button_label", group.Name, ref group.ButtonLabel, 64))
            _save();

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
                DrawPaddingFields($"##btn_{index}", ref group.CustomPaddingH, ref group.CustomPaddingV);
                ImGui.Unindent(20f);
            }

            if (ImGui.Checkbox("Font Override##font_ovr", ref group.UseCustomFont)) _save();
            if (group.UseCustomFont)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100f);
                var fontIndex = group.CustomUseMono ? 1 : 0;
                if (BJBGui.Combo("##btn_font", ref fontIndex, "Default\0Mono\0"))
                {
                    group.CustomUseMono = fontIndex == 1;
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
                    group.CustomFontScale = Math.Clamp((float)Math.Round(group.CustomFontScale / 0.05f) * 0.05f, 0.5f, 2f);
                    _save();
                }
            }

            ImGui.TreePop();
        }

        DrawCommandGroupTable(group);
        if (BJBGui.Button("+ Add Command Step"))
        {
            group.Commands.Add(new PluginCommand { Text = "/p New step...", Delay = 1f });
            _save();
        }
    }

    private bool DrawOwnButtonStateButton(string label, bool active, string tooltip)
    {
        if (active)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.22f, 0.42f, 0.70f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.28f, 0.50f, 0.80f, 1f));
        }
        var clicked = BJBGui.SmallButton(label);
        if (active) ImGui.PopStyleColor(2);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);
        return clicked;
    }

    private CommandGroup? FindCustomButtonGroup(CustomButtonEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.GroupId))
            return _config.CustomCommandGroups.FirstOrDefault(group => group.Id == entry.GroupId);
        if (!string.IsNullOrWhiteSpace(entry.LegacyGroupName))
            return _config.CustomCommandGroups.FirstOrDefault(group =>
                group.Name.Equals(entry.LegacyGroupName, StringComparison.OrdinalIgnoreCase));
        return null;
    }

    private bool MatchesOwnButtonFilter(CustomButtonEntry entry)
    {
        if (entry.IsBreak)
            return BJBGui.MatchesFilter(_filterOwnButtons, "--Break--");
        var group = FindCustomButtonGroup(entry);
        return group != null && BJBGui.MatchesFilter(_filterOwnButtons, group.Name, group.Commands.Select(command => command.Text));
    }

    private bool IsCustomGroupNameAvailable(string name, string? currentId = null)
    {
        var trimmed = name.Trim();
        return !string.IsNullOrWhiteSpace(trimmed)
            && !_config.CustomCommandGroups.Any(group => group.Id != currentId
                && group.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
            && !_config.CommandGroups.Any(group => group.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
    }

    private void RemoveCustomButtonEntry(int index)
    {
        if (index < 0 || index >= _config.CustomButtonEntries.Count)
            return;

        var entry = _config.CustomButtonEntries[index];
        _config.CustomButtonEntries.RemoveAt(index);
        if (!entry.IsBreak && !string.IsNullOrWhiteSpace(entry.GroupId))
            _config.CustomCommandGroups.RemoveAll(group => group.Id == entry.GroupId);
        _config.SyncLegacyCustomButtonOrder();
        _save();
    }
}
