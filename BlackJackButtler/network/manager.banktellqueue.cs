using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static class BankTellQueueManager
{
    private static int _pendingCount;

    public static bool IsProcessing => Volatile.Read(ref _pendingCount) > 0;
    public static int Count => Math.Max(0, Volatile.Read(ref _pendingCount));

    public static void Enqueue(PlayerState player, Configuration config, string source)
    {
        var playerName = player.Name;
        var displayName = player.DisplayName;
        Interlocked.Increment(ref _pendingCount);
        if (!GameActionQueueManager.Enqueue(
                $"BankTell:{displayName}",
                () => Execute(playerName, displayName, config, source),
                null,
                null,
                () => Interlocked.Decrement(ref _pendingCount)))
        {
            Interlocked.Decrement(ref _pendingCount);
        }
    }

    public static void EnqueueMany(IEnumerable<PlayerState> players, Configuration config, string source)
    {
        foreach (var player in players)
            Enqueue(player, config, source);
    }

    private static async Task Execute(string playerName, string displayName, Configuration config, string source)
    {
        try
        {
            var window = Plugin.Instance.GetMainWindow();
            var player = window.GetPlayers().FirstOrDefault(p =>
                p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase)
                || p.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));

            if (player == null)
            {
                window.AddDebugLog($"[BankTellQueue] Skipped missing player '{displayName}' from {source}");
                return;
            }

            window.AddDebugLog($"[BankTellQueue] Executing BankTell for {player.DisplayName} from {source}");
            player.HighlightTell = false;
            GameEngine.TargetPlayer(player);
            VariableManager.SetPlayerVariables(player);
            await CommandExecutor.ExecuteGroup("BankTell", player.DisplayName, config);
        }
        finally
        {
            var dealerName = Plugin.Instance.GetMainWindow().GetDealer().Name;
            if (!string.IsNullOrWhiteSpace(dealerName))
                GameEngine.TargetPlayer(dealerName);
        }
    }
}
