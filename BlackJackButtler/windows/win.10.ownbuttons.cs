using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private string _newCustomGroupName = string.Empty;

    private void DrawOwnButtonsPage()
    {
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

        CommandGroup? toRemove = null;

        foreach (var group in _config.CustomCommandGroups)
        {
            ImGui.PushID($"custom_group_{group.Name}");

            if (ImGui.CollapsingHeader(group.Name, ImGuiTreeNodeFlags.DefaultOpen))
            {
                var io = ImGui.GetIO();
                bool ctrlHeld = io.KeyCtrl;

                if (!ctrlHeld) ImGui.BeginDisabled();
                if (ctrlHeld) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0f, 0f, 1f));
                if (BJBGui.SmallButton("Delete Group"))
                {
                    toRemove = group;
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

        if (toRemove != null)
        {
            _config.CustomCommandGroups.Remove(toRemove);
            _save();
        }
    }
}
