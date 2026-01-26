using System.Numerics;
using Dalamud.Bindings.ImGui;
using BlackJackButtler.Chat;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawLogPage()
    {
        ImGui.TextUnformatted("Game History & Rollback");
        ImGui.SameLine();
        if (ImGui.Button("Clear History")) GameLog.Clear();

        ImGui.Separator();
        ImGui.TextDisabled("Every card deal and action creates a snapshot. Use 'Undo' to revert the last step.");
        ImGui.Spacing();

        if (ImGui.Button("UNDO LAST ACTION", new Vector2(-1, 40)))
        {
            var phase = GameEngine.CurrentPhase;
            GameLog.UndoLast(_players, ref _dealer, ref phase);
            GameEngine.CurrentPhase = phase;
            _save();
        }

        ImGui.Spacing();

        if (ImGui.BeginTable("bjb_log_table", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Phase", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Action / Reason", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            foreach (var entry in GameLog.Entries)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(entry.TimestampUtc.ToLocalTime().ToString("HH:mm:ss"));
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(entry.Phase.ToString());
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(entry.Reason);
            }
            ImGui.EndTable();
        }
    }
}
