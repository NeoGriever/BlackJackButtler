using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static class BankTellQueueManager
{
    private sealed record QueueItem(string PlayerName, string DisplayName, Configuration Config, string Source);

    private static readonly ConcurrentQueue<QueueItem> Queue = new();
    private static int _isProcessing;

    public static bool IsProcessing => _isProcessing != 0;
    public static int Count => Queue.Count;

    public static void Enqueue(PlayerState player, Configuration config, string source)
    {
        Queue.Enqueue(new QueueItem(player.Name, player.DisplayName, config, source));
        EnsureProcessing();
    }

    public static void EnqueueMany(IEnumerable<PlayerState> players, Configuration config, string source)
    {
        foreach (var player in players)
            Queue.Enqueue(new QueueItem(player.Name, player.DisplayName, config, source));

        EnsureProcessing();
    }

    private static void EnsureProcessing()
    {
        if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0)
            _ = Task.Run(ProcessQueue);
    }

    private static async Task ProcessQueue()
    {
        try
        {
            while (Queue.TryDequeue(out var item))
            {
                await CommandExecutor.WaitForCurrentGroupToFinishAsync();

                var window = Plugin.Instance.GetMainWindow();
                var player = window.GetPlayers().FirstOrDefault(p =>
                    p.Name.Equals(item.PlayerName, StringComparison.OrdinalIgnoreCase) ||
                    p.DisplayName.Equals(item.DisplayName, StringComparison.OrdinalIgnoreCase));

                if (player == null)
                {
                    window.AddDebugLog($"[BankTellQueue] Skipped missing player '{item.DisplayName}' from {item.Source}");
                    continue;
                }

                window.AddDebugLog($"[BankTellQueue] Executing BankTell for {player.DisplayName} from {item.Source}");
                player.HighlightTell = false;
                GameEngine.TargetPlayer(player.Name);
                VariableManager.SetPlayerVariables(player);
                await CommandExecutor.ExecuteGroup("BankTell", player.DisplayName, item.Config);
            }

            var dealerName = Plugin.Instance.GetMainWindow().GetDealer().Name;
            if (!string.IsNullOrWhiteSpace(dealerName))
                GameEngine.TargetPlayer(dealerName);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[BankTellQueue] Failed: {ex}");
        }
        finally
        {
            Interlocked.Exchange(ref _isProcessing, 0);
            if (!Queue.IsEmpty)
                EnsureProcessing();
        }
    }
}
