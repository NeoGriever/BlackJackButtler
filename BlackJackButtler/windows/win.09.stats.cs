using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawStatsPage()
    {
        ImGui.TextUnformatted("Stats");
        ImGui.Separator();

        if (ImGui.BeginTabBar("##stats_tabs"))
        {
            if (ImGui.BeginTabItem("Session Stats"))
            {
                DrawStatsBlock(StatsManager.SessionRounds, StatsManager.SessionIncome, StatsManager.SessionExpense);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Overall Stats"))
            {
                DrawStatsBlock(_config.OverallRounds, _config.OverallIncome, _config.OverallExpense);

                ImGui.Separator();

                bool canReset =
                    GameEngine.CurrentPhase == GamePhase.Waiting &&
                    !IsRecognitionActive &&
                    (_players.Count == 0 || _players.All(p => p.Bank == 0));

                var io = ImGui.GetIO();
                bool holdingModifiers = io.KeyCtrl && io.KeyShift;

                ImGui.BeginDisabled(!canReset || !holdingModifiers);
                if (ImGui.Button("Reset Stats (Ctrl+Shift)"))
                {
                    StatsManager.ResetOverall();
                }
                ImGui.EndDisabled();

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    if (!canReset)
                        ImGui.SetTooltip("Only available when no game is active, detector is off, and all banks are zero");
                    else if (!holdingModifiers)
                        ImGui.SetTooltip("Hold Ctrl+Shift and click to reset");
                }

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawStatsBlock(int rounds, long income, long expense)
    {
        long net = income - expense;

        ImGui.Spacing();
        ImGui.TextUnformatted($"Rounds:     {rounds:N0}");
        ImGui.TextUnformatted($"Income:     {income:N0}");
        ImGui.TextUnformatted($"Expense:    {expense:N0}");

        var color = net >= 0
            ? new Vector4(0.0f, 1.0f, 0.0f, 1.0f)
            : new Vector4(1.0f, 0.0f, 0.0f, 1.0f);

        string sign = net >= 0 ? "+" : "";
        ImGui.TextColored(color, $"Net:        {sign}{net:N0}");
    }
}
