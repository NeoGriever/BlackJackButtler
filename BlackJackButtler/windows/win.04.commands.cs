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
        if (ImGui.Button("Reset Commands to Default##res_cmds"))
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
                if (ImGui.BeginTable($"table_{group.Name}", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                {
                    ImGui.TableSetupColumn("Act", ImGuiTableColumnFlags.WidthFixed, 30);
                    ImGui.TableSetupColumn("Command / Chat Message", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Wait (s)", ImGuiTableColumnFlags.WidthFixed, 100);
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 50);
                    ImGui.TableSetupColumn("X", ImGuiTableColumnFlags.WidthFixed, 30);
                    ImGui.TableHeadersRow();

                    for (int i = 0; i < group.Commands.Count; i++)
                    {
                        var cmd = group.Commands[i];
                        ImGui.TableNextRow();
                        ImGui.PushID(i);

                        ImGui.TableNextColumn();
                        if (ImGui.Checkbox("##active", ref cmd.Enabled)) _save();

                        ImGui.TableNextColumn();
                        ImGui.SetNextItemWidth(-1);
                        if (ImGui.InputText("##text", ref cmd.Text, 256)) _save();

                        ImGui.TableNextColumn();
                        ImGui.SetNextItemWidth(-1);
                        if (ImGui.SliderFloat("##delay", ref cmd.Delay, 0.5f, 8.0f, "%.1fs"))
                        {
                            if (cmd.Delay < 0.5f) cmd.Delay = 0.5f;
                            _save();
                        }

                        ImGui.TableNextColumn();
                        bool isFirst = i == 0;
                        bool isLast = i == group.Commands.Count - 1;

                        if (isFirst) ImGui.BeginDisabled();
                        if (ImGui.SmallButton("^##up"))
                        {
                            (group.Commands[i - 1], group.Commands[i]) = (group.Commands[i], group.Commands[i - 1]);
                            _save();
                            ImGui.PopID();
                            break;
                        }
                        if (isFirst) ImGui.EndDisabled();

                        ImGui.SameLine();

                        if (isLast) ImGui.BeginDisabled();
                        if (ImGui.SmallButton("v##down"))
                        {
                            (group.Commands[i], group.Commands[i + 1]) = (group.Commands[i + 1], group.Commands[i]);
                            _save();
                            ImGui.PopID();
                            break;
                        }
                        if (isLast) ImGui.EndDisabled();

                        ImGui.TableNextColumn();
                        if (ImGui.Button("X##del"))
                        {
                            group.Commands.RemoveAt(i);
                            _save();
                            ImGui.PopID();
                            break;
                        }

                        ImGui.PopID();
                    }
                    ImGui.EndTable();
                }

                if (ImGui.Button("+ Add Command Step"))
                {
                    group.Commands.Add(new PluginCommand { Text = "/p New step...", Delay = 1.0f });
                    _save();
                }
            }
            ImGui.PopID();
            ImGui.Spacing();
        }
    }
}
