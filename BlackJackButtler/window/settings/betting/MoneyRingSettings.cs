using System;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawMoneyRingSettings()
    {
        Header("Money Ring");
        DrawSettingsToggle("Money Ring", "money_ring_enabled", ref _config.MoneyRingEnabled);
        _config.MoneyRingEntries ??= Configuration.CreateDefaultMoneyRingEntries();

        if (ImGui.BeginTable("money_ring_entries", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Active", ImGuiTableColumnFlags.WidthFixed, 55f);
            ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, 110f);
            ImGui.TableSetupColumn("Amount", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Order", ImGuiTableColumnFlags.WidthFixed, 58f);
            ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, 25f);
            ImGui.TableHeadersRow();

            for (var i = 0; i < _config.MoneyRingEntries.Count; i++)
            {
                var entry = _config.MoneyRingEntries[i];
                if (entry == null) continue;
                ImGui.PushID(i);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (ImGui.Checkbox("##enabled", ref entry.Enabled)) _save();

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.InputText("##label", ref entry.Label, 24)) _save();

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1f);
                if (BJBGui.InputLongFormatted("##amount", ref entry.Amount))
                {
                    entry.Amount = Math.Clamp(entry.Amount, 1, 1_000_000_000);
                    _save();
                }

                ImGui.TableNextColumn();
                if (i == 0) ImGui.BeginDisabled();
                var moveUp = ImGui.ArrowButton("##up", ImGuiDir.Up);
                if (i == 0) ImGui.EndDisabled();
                ImGui.SameLine();
                if (i == _config.MoneyRingEntries.Count - 1) ImGui.BeginDisabled();
                var moveDown = ImGui.ArrowButton("##down", ImGuiDir.Down);
                if (i == _config.MoneyRingEntries.Count - 1) ImGui.EndDisabled();
                if (moveUp || moveDown)
                {
                    var target = i + (moveUp ? -1 : 1);
                    (_config.MoneyRingEntries[i], _config.MoneyRingEntries[target]) =
                        (_config.MoneyRingEntries[target], _config.MoneyRingEntries[i]);
                    _save();
                    ImGui.PopID();
                    break;
                }

                ImGui.TableNextColumn();
                if (BJBGui.SmallButton("X##delete"))
                {
                    _config.MoneyRingEntries.RemoveAt(i);
                    _save();
                    ImGui.PopID();
                    break;
                }
                ImGui.PopID();
            }
            ImGui.EndTable();
        }

        if (BJBGui.Button("Add entry##money_ring_add"))
        {
            _config.MoneyRingEntries.Add(new MoneyRingEntry { Label = "New", Amount = 50_000 });
            _save();
        }
        ImGui.SameLine();
        if (BJBGui.Button("Reset default##money_ring_reset"))
        {
            _config.MoneyRingEntries = Configuration.CreateDefaultMoneyRingEntries();
            _save();
        }
    }
}
