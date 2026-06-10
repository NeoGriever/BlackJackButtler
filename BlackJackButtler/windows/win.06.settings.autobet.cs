using System;
using System.Linq;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawAutoBetPostCommandSelector(string id)
    {
        var commandNames = _config.CommandGroups
            .Concat(_config.CustomCommandGroups)
            .Select(g => g.Name)
            .Append("Payout")
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Where(n => !SetBetQueueManager.IsSetBetLoopbackName(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var labels = new[] { "None" }.Concat(commandNames).ToArray();
        var selected = 0;
        if (!string.IsNullOrWhiteSpace(_config.AutoBetPostCommandName))
        {
            var idx = commandNames.FindIndex(n => n.Equals(_config.AutoBetPostCommandName, StringComparison.OrdinalIgnoreCase));
            selected = idx >= 0 ? idx + 1 : 0;
        }

        ImGui.TextUnformatted("Execute Command after Bet change");
        ImGui.SameLine(_config.MainViewVersion == 2 ? 260f : 300f);
        ImGui.SetNextItemWidth(260f);
        if (BJBGui.Combo($"##autobet_post_command_{id}", ref selected, labels, labels.Length))
        {
            _config.AutoBetPostCommandName = selected <= 0 ? string.Empty : commandNames[selected - 1];
            _save();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Runs after a SetBet regex changed a player's bet.\nThe SetBet action itself is excluded to prevent loopback.");
    }

    private void DrawInsufficientBetCommandSelector(string id)
    {
        var commandNames = _config.CommandGroups
            .Concat(_config.CustomCommandGroups)
            .Select(g => g.Name)
            .Append("Payout")
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var labels = new[] { "None" }.Concat(commandNames).ToArray();
        var selected = 0;
        if (!string.IsNullOrWhiteSpace(_config.InsufficientBetCommandName))
        {
            var idx = commandNames.FindIndex(n => n.Equals(_config.InsufficientBetCommandName, StringComparison.OrdinalIgnoreCase));
            selected = idx >= 0 ? idx + 1 : 0;
        }

        ImGui.TextUnformatted("Execute Command on insufficient bank");
        ImGui.SameLine(_config.MainViewVersion == 2 ? 260f : 300f);
        ImGui.SetNextItemWidth(260f);
        if (BJBGui.Combo($"##insufficient_bet_command_{id}", ref selected, labels, labels.Length))
        {
            _config.InsufficientBetCommandName = selected <= 0 ? string.Empty : commandNames[selected - 1];
            _save();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Runs once per underfunded active player when Auto Run would start a round but bank is below bet.");
    }
}
