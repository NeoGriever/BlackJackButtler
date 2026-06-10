using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private string _filterCommands = string.Empty;

    private void DrawCommandsPage()
    {
        if (ImGui.BeginTabBar("##commands_tabs"))
        {
            var commandsFlags = _pendingCommandsTab == "Commands"
                ? ImGuiTabItemFlags.SetSelected
                : ImGuiTabItemFlags.None;
            if (ImGui.BeginTabItem("Commands", commandsFlags))
            {
                if (_pendingCommandsTab == "Commands") _pendingCommandsTab = null;
                DrawCommandChainsTab();
                ImGui.EndTabItem();
            }
            var ownButtonsFlags = _pendingCommandsTab == "OwnButtons"
                ? ImGuiTabItemFlags.SetSelected
                : ImGuiTabItemFlags.None;
            if (ImGui.BeginTabItem("Own Buttons", ownButtonsFlags))
            {
                if (_pendingCommandsTab == "OwnButtons") _pendingCommandsTab = null;
                DrawOwnButtonsPage();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void DrawCommandChainsTab()
    {
        ImGui.TextUnformatted("Command Chains");
        ImGui.SameLine();

        var io = ImGui.GetIO();
        bool keysDown = io.KeyCtrl && io.KeyShift;

        if (!keysDown) ImGui.BeginDisabled();
        if (keysDown) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0f, 0f, 1f));
        if (BJBGui.Button("Hard Reset…##res_cmds"))
        {
            _openCmdForceDefaultsPopup = true;
            ImGui.OpenPopup("bjb.cmd.restore.confirm");
        }
        if (keysDown) ImGui.PopStyleColor();
        if (!keysDown)
        {
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("Hold CTRL + SHIFT to reset all command chains.");
        }

        if (ImGui.BeginPopupModal("bjb.cmd.restore.confirm", ref _openCmdForceDefaultsPopup, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextColored(new Vector4(1, 0, 0, 1), "WARNING: HARD RESET");
            ImGui.TextUnformatted("This will delete all standard messages and commands and recreate them.");
            ImGui.TextUnformatted("Choose which defaults pack to restore:");
            ImGui.Spacing();

            if (BJBGui.Button("Use New Defaults (recommended)", new Vector2(260, 0)))
            {
                DefaultsMigration.SeedAllDefaultsFromV2(_config);
                _save();
                _openCmdForceDefaultsPopup = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (BJBGui.Button("Use Old Defaults", new Vector2(160, 0)))
            {
                DefaultsMigration.SeedAllDefaults(_config);
                _save();
                _openCmdForceDefaultsPopup = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (BJBGui.Button("Cancel", new Vector2(120, 0)))
            {
                _openCmdForceDefaultsPopup = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        ImGui.Separator();
        ImGui.TextDisabled("Define what happens when an action is triggered. Use <t> for the player name.");

        BJBGui.DrawFilterBar("commands", ref _filterCommands, "Search command name or text...");
        ImGui.Spacing();

        foreach (var group in _config.CommandGroups)
        {
            string displayName = group.Name switch {
                "Initial" => "Player Start",
                "Hit" => "Player Hit",
                "Stand" => "Player Stand",
                "DD" => "Player Double Down",
                "Split" => "Player Split",
                "PlayerBust" => "Player Busted",
                "DealStart" => "Dealer Start",
                "DealHit" => "Dealer Hit",
                "DealStand" => "Dealer Stand",
                "DealerBJ" => "Dealer Has Blackjack",
                "DealerBust" => "Dealer Busted",
                "BankTell" => "Bank Tell (Individual)",
                _ => group.Name
            };

            if (!BJBGui.MatchesFilter(_filterCommands, displayName, group.Commands.Select(c => (string?)c.Text))
                && !BJBGui.MatchesFilter(_filterCommands, group.Name))
                continue;

            ImGui.PushID($"group_{group.Name}");

            if (!string.IsNullOrEmpty(_filterCommands))
                ImGui.SetNextItemOpen(true, ImGuiCond.Always);

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
        if (!ImGui.BeginTable($"table_{group.Name}", 8, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            return;

        ImGui.TableSetupColumn("Act",                    ImGuiTableColumnFlags.WidthFixed,   30);
        ImGui.TableSetupColumn("Grp",                    ImGuiTableColumnFlags.WidthFixed,   45);
        ImGui.TableSetupColumn("Command / Chat Message", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Wait (s)",               ImGuiTableColumnFlags.WidthFixed,  100);
        ImGui.TableSetupColumn("1x",                    ImGuiTableColumnFlags.WidthFixed,   25);
        ImGui.TableSetupColumn("AD",                     ImGuiTableColumnFlags.WidthFixed,   25);
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
                    ImGui.TextColored(new Vector4(0.65f, 0.82f, 1f, 1f), $"  Group {cmd.GroupId}");
                    ImGui.SameLine();
                    string modeLabel = lg.Mode == SelectionMode.Iterative ? "Iterative" : "Random";
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
                if (BJBGui.DragInt("##grp", ref gid, 0.15f, 0, 9, gid == 0 ? "-" : "%d"))
                {
                    cmd.GroupId = Math.Clamp(gid, 0, 9);
                    _save();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Group (0 = no group).\nSame number = one command per trigger (iterative/random).");
            }

            // Col 2 — command text or command reference
            ImGui.TableNextColumn();
            if (cmd.GroupId != 0) ImGui.Indent(10f);
            bool wasCmdRef = cmd.IsCommandRef;
            if (wasCmdRef)
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.5f, 0.8f, 1f));
            if (BJBGui.SmallButton($"C##cmdref_{group.Name}_{i}"))
            {
                cmd.IsCommandRef = !cmd.IsCommandRef;
                _save();
            }
            if (wasCmdRef) ImGui.PopStyleColor();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(cmd.IsCommandRef ? "Command Reference (click for Message)" : "Message (click for Command Reference)");
            ImGui.SameLine();
            if (cmd.IsCommandRef)
            {
                var groupNames = _config.CommandGroups.Concat(_config.CustomCommandGroups)
                    .Where(g => !g.Name.Equals(group.Name, StringComparison.OrdinalIgnoreCase))
                    .Select(g => g.Name)
                    .Append("Payout")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                int selectedIdx = Array.FindIndex(groupNames, n => n.Equals(cmd.CommandRefName, StringComparison.OrdinalIgnoreCase));
                if (selectedIdx < 0) selectedIdx = 0;
                ImGui.SetNextItemWidth(-1);
                if (groupNames.Length > 0)
                {
                    if (BJBGui.Combo($"##cmdref_combo_{group.Name}_{i}", ref selectedIdx, groupNames, groupNames.Length))
                    {
                        cmd.CommandRefName = groupNames[selectedIdx];
                        _save();
                    }
                }
                else
                {
                    ImGui.TextDisabled("No other groups available");
                }
            }
            else
            {
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText($"##text_{group.Name}_{i}", ref cmd.Text, 256)) _save();
            }
            if (cmd.GroupId != 0) ImGui.Unindent(10f);

            // Col 3 — delay slider
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            float maxDelay = _config.UnlockWaitTimer ? 30f : 12f;
            float oldDelay = cmd.Delay;
            if (BJBGui.DragFloat("##delay", ref cmd.Delay, 0.01f, 0.01f, maxDelay, "%.2fs"))
            {
                if (_config.DelaySecondSnapping)
                {
                    float nearestInt = MathF.Round(cmd.Delay);
                    float oldDist = MathF.Abs(oldDelay - nearestInt);
                    float newDist = MathF.Abs(cmd.Delay - nearestInt);
                    if (newDist < 0.2f && oldDist > 0.2f)
                        cmd.Delay = nearestInt;
                }
                cmd.Delay = Math.Clamp(cmd.Delay, 0.01f, maxDelay);
                _save();
            }

            // Col 4 — fixed delay
            ImGui.TableNextColumn();
            if (ImGui.Checkbox("##fixed", ref cmd.FixedDelay)) _save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Fixed Delay: Ignore Command Speed multiplier.\nAlways use the base delay (1x).");

            // Col 5 — anti-double
            ImGui.TableNextColumn();
            bool isAD = cmd.NonDoubled;
            if (isAD
                ? BJBGui.SmallButtonHighlighted($"AD##ad_{group.Name}_{i}", _config.HighlightColor, _config.HighlightTextColor)
                : BJBGui.SmallButton($"AD##ad_{group.Name}_{i}"))
            { cmd.NonDoubled = !cmd.NonDoubled; _save(); }

            // Col 6 — up / down
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
