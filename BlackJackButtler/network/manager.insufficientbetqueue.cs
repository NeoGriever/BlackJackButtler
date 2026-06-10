using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static class InsufficientBetQueueManager
{
    private static readonly HashSet<string> PendingPlayers = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();

    public static bool IsProcessing
    {
        get { lock (Gate) return PendingPlayers.Count > 0; }
    }

    public static void EnqueueMany(IEnumerable<PlayerState> players, Configuration config, string source)
    {
        if (!config.EnableAutomation || !config.ShowAutoRunButton || !config.AutoRun
            || string.IsNullOrWhiteSpace(config.InsufficientBetCommandName))
            return;

        foreach (var player in players)
        {
            var playerName = player.Name;
            var displayName = player.DisplayName;
            lock (Gate)
            {
                if (!PendingPlayers.Add(playerName))
                    continue;
            }

            GameActionQueueManager.Enqueue(
                $"InsufficientBet:{displayName}",
                () => Execute(playerName, displayName, config, source),
                $"InsufficientBet:{playerName}",
                () => config.EnableAutomation && config.ShowAutoRunButton && config.AutoRun,
                () =>
                {
                    lock (Gate) PendingPlayers.Remove(playerName);
                });
        }
    }

    private static async Task Execute(string playerName, string displayName, Configuration config, string source)
    {
        var window = Plugin.Instance.GetMainWindow();
        var player = window.GetPlayers().FirstOrDefault(p =>
            p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase)
            || p.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));

        if (player == null || !GameEngine.IsPlayerUnableToCoverBet(player))
            return;

        var commandName = ResolveCommandName(config);
        if (commandName == null)
            return;

        window.AddDebugLog($"[InsufficientBetQueue] Executing '{commandName}' for {player.DisplayName} from {source}");
        GameEngine.TargetPlayer(player);
        VariableManager.SetPlayerVariables(player);
        await CommandExecutor.ExecuteGroup(commandName, player.DisplayName, config);

        var dealerName = window.GetDealer().Name;
        if (!string.IsNullOrWhiteSpace(dealerName))
            GameEngine.TargetPlayer(dealerName);
    }

    private static string? ResolveCommandName(Configuration config)
    {
        if (string.IsNullOrWhiteSpace(config.InsufficientBetCommandName))
            return null;

        if (config.InsufficientBetCommandName.Equals("Payout", StringComparison.OrdinalIgnoreCase))
            return "Payout";

        return config.CommandGroups
            .Concat(config.CustomCommandGroups)
            .FirstOrDefault(g => g.Name.Equals(config.InsufficientBetCommandName, StringComparison.OrdinalIgnoreCase)
                && g.IsActive)
            ?.Name;
    }
}
