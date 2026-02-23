using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawCommandsPage()
    {
        ImGui.TextUnformatted("Command Chains");
        ImGui.SameLine();

        var io = ImGui.GetIO();
        bool keysDown = io.KeyCtrl && io.KeyShift;

        if (!keysDown) ImGui.BeginDisabled();
        if (keysDown) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0f, 0f, 1f));
        if (BJBGui.Button("Reset Commands to Default##res_cmds"))
        {
            _config.ForceResetCommandGroups();
            _save();
        }
        if (keysDown) ImGui.PopStyleColor();
        if (!keysDown)
        {
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("Hold CTRL + SHIFT to reset all command chains.");
        }

        ImGui.Separator();
        ImGui.TextDisabled("Define what happens when an action is triggered. Use <t> for the player name.");
        ImGui.Spacing();

        foreach (var group in _config.CommandGroups)
        {
            ImGui.PushID($"group_{group.Name}");

            string displayName = group.Name switch {
                "Initial" => "Player Start",
                "Hit" => "Player Hit",
                "Stand" => "Player Stand",
                "DD" => "Player Double Down",
                "Split" => "Player Split",
                "PlayerBJ" => "Player has Natural Blackjack",
                "PlayerDirtyBJ" => "Player has Dirty Blackjack",
                "PlayerBust" => "Player Busted",
                "DealStart" => "Dealer Start",
                "DealHit" => "Dealer Hit",
                "DealStand" => "Dealer Stand",
                "DealerBJ" => "Dealer Has Blackjack",
                "DealerBust" => "Dealer Busted",
                "BankTell" => "Bank Tell (Individual)",
                _ => group.Name
            };

            if (ImGui.CollapsingHeader($"{displayName} (Internal: {group.Name})"))
            {
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
    }

    /// <summary>
    /// Shared table renderer for any CommandGroup. Used by both the Commands page and Own Buttons page.
    /// Handles GroupId=0 (ungrouped) and GroupId>0 (iterative/random line groups).
    /// </summary>
    private void DrawCommandGroupTable(CommandGroup group)
    {
        if (!ImGui.BeginTable($"table_{group.Name}", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            return;

        ImGui.TableSetupColumn("Act",                    ImGuiTableColumnFlags.WidthFixed,   30);
        ImGui.TableSetupColumn("Grp",                    ImGuiTableColumnFlags.WidthFixed,   45);
        ImGui.TableSetupColumn("Command / Chat Message", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Wait (s)",               ImGuiTableColumnFlags.WidthFixed,  100);
        ImGui.TableSetupColumn("",                       ImGuiTableColumnFlags.WidthFixed,   50);
        ImGui.TableSetupColumn("X",                      ImGuiTableColumnFlags.WidthFixed,   30);
        ImGui.TableHeadersRow();

        int  prevGroupId = 0;
        bool groupOpen   = false;

        for (int i = 0; i < group.Commands.Count; i++)
        {
            var cmd = group.Commands[i];

            // ── group boundary transitions ────────────────────────────────
            if (cmd.GroupId != prevGroupId)
            {
                // close the previous group with a thin footer row
                if (groupOpen)
                {
                    ImGui.TableNextRow(ImGuiTableRowFlags.None, 4f);
                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0,
                        ImGui.GetColorU32(new Vector4(0.25f, 0.25f, 0.45f, 0.5f)));
                    groupOpen = false;
                }

                // open the new group with a header row
                if (cmd.GroupId != 0)
                {
                    if (!group.LineGroups.ContainsKey(cmd.GroupId))
                    {
                        group.LineGroups[cmd.GroupId] = new CommandLineGroup();
                        _save();
                    }
                    var lg = group.LineGroups[cmd.GroupId];

                    ImGui.TableNextRow(ImGuiTableRowFlags.None, 22f);
                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0,
                        ImGui.GetColorU32(new Vector4(0.15f, 0.18f, 0.38f, 0.9f)));

                    ImGui.TableSetColumnIndex(2);
                    ImGui.TextColored(new Vector4(0.65f, 0.82f, 1f, 1f), $"  Gruppe {cmd.GroupId}");
                    ImGui.SameLine();
                    string modeLabel = lg.Mode == SelectionMode.Iterative ? "Iterativ" : "Zufällig";
                    if (ImGui.SmallButton($"{modeLabel}##grphdr_{group.Name}_{cmd.GroupId}"))
                    {
                        lg.Mode = lg.Mode == SelectionMode.Iterative
                            ? SelectionMode.Random
                            : SelectionMode.Iterative;
                        _save();
                    }

                    groupOpen = true;
                }

                prevGroupId = cmd.GroupId;
            }

            // ── regular command row ───────────────────────────────────────
            ImGui.TableNextRow();
            ImGui.PushID(i);

            // Col 0 — enabled checkbox
            ImGui.TableNextColumn();
            if (ImGui.Checkbox("##active", ref cmd.Enabled)) _save();

            // Col 1 — group id (drag to set, 0 = no group)
            ImGui.TableNextColumn();
            {
                int gid = cmd.GroupId;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.DragInt("##grp", ref gid, 0.15f, 0, 9, gid == 0 ? "-" : "%d"))
                {
                    cmd.GroupId = Math.Clamp(gid, 0, 9);
                    _save();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Gruppe (0 = keine Gruppe).\nGleiche Nummer = ein Befehl pro Auslösung (iterativ/zufällig).");
            }

            // Col 2 — command text (indented when in a group)
            ImGui.TableNextColumn();
            if (cmd.GroupId != 0) ImGui.Indent(10f);
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("##text", ref cmd.Text, 256)) _save();
            if (cmd.GroupId != 0) ImGui.Unindent(10f);

            // Col 3 — delay slider
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            float maxDelay = _config.UnlockWaitTimer ? 30f : 12f;
            if (ImGui.SliderFloat("##delay", ref cmd.Delay, 0.5f, maxDelay, "%.1fs"))
            {
                cmd.Delay = Math.Clamp(cmd.Delay, 0.5f, maxDelay);
                _save();
            }

            // Col 4 — up / down
            ImGui.TableNextColumn();
            bool isFirst = i == 0;
            bool isLast  = i == group.Commands.Count - 1;

            if (isFirst) ImGui.BeginDisabled();
            if (BJBGui.SmallButton("^##up"))
            {
                (group.Commands[i - 1], group.Commands[i]) = (group.Commands[i], group.Commands[i - 1]);
                _save();
                ImGui.PopID();
                break;
            }
            if (isFirst) ImGui.EndDisabled();

            ImGui.SameLine();

            if (isLast) ImGui.BeginDisabled();
            if (BJBGui.SmallButton("v##down"))
            {
                (group.Commands[i], group.Commands[i + 1]) = (group.Commands[i + 1], group.Commands[i]);
                _save();
                ImGui.PopID();
                break;
            }
            if (isLast) ImGui.EndDisabled();

            // Col 5 — delete
            ImGui.TableNextColumn();
            if (BJBGui.Button("X##del"))
            {
                group.Commands.RemoveAt(i);
                _save();
                ImGui.PopID();
                break;
            }

            ImGui.PopID();
        }

        // close last open group with a thin footer row
        if (groupOpen)
        {
            ImGui.TableNextRow(ImGuiTableRowFlags.None, 4f);
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0,
                ImGui.GetColorU32(new Vector4(0.25f, 0.25f, 0.45f, 0.5f)));
        }

        ImGui.EndTable();
    }
}
