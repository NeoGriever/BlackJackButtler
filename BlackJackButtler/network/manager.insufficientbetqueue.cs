using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static class InsufficientBetQueueManager
{
    private sealed record QueueItem(string PlayerName, string DisplayName, Configuration Config, string Source);

    private static readonly ConcurrentQueue<QueueItem> Queue = new();
    private static readonly HashSet<string> PendingPlayers = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object LockObj = new();
    private static int _isProcessing;

    public static bool IsProcessing => _isProcessing == 1;

    public static void EnqueueMany(IEnumerable<PlayerState> players, Configuration config, string source)
    {
        if (!config.EnableAutomation || !config.ShowAutoRunButton || !config.AutoRun)
            return;

        if (string.IsNullOrWhiteSpace(config.InsufficientBetCommandName))
            return;

        lock (LockObj)
        {
            foreach (var player in players)
            {
                if (PendingPlayers.Add(player.Name))
                    Queue.Enqueue(new QueueItem(player.Name, player.DisplayName, config, source));
            }
        }

        EnsureProcessing();
    }

    private static void EnsureProcessing()
    {
        if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0)
            _ = Task.Run(ProcessQueue);
    }

    private static async Task ProcessQueue()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            while (Queue.TryDequeue(out var item))
            {
                if (cts.Token.IsCancellationRequested)
                {
                    while (Queue.TryDequeue(out var skipped))
                        lock (LockObj) PendingPlayers.Remove(skipped.PlayerName);
                    Plugin.Log.Warning("[InsufficientBetQueue] Batch timeout (30s) — remaining players skipped.");
                    break;
                }

                try
                {
                    await CommandExecutor.WaitForCurrentGroupToFinishAsync();

                    if (cts.Token.IsCancellationRequested)
                        continue;

                    var window = Plugin.Instance.GetMainWindow();
                    var player = window.GetPlayers().FirstOrDefault(p =>
                        p.Name.Equals(item.PlayerName, StringComparison.OrdinalIgnoreCase) ||
                        p.DisplayName.Equals(item.DisplayName, StringComparison.OrdinalIgnoreCase));

                    if (player == null || !GameEngine.IsPlayerUnableToCoverBet(player))
                        continue;

                    var command = ResolveCommand(item.Config);
                    if (command == null)
                        continue;

                    window.AddDebugLog($"[InsufficientBetQueue] Executing '{command.Name}' for {player.DisplayName} from {item.Source}");
                    GameEngine.TargetPlayer(player.Name);
                    VariableManager.SetPlayerVariables(player);
                    await CommandExecutor.ExecuteGroup(command.Name, player.DisplayName, item.Config);
                }
                finally
                {
                    lock (LockObj)
                        PendingPlayers.Remove(item.PlayerName);
                }
            }

            var dealerName = Plugin.Instance.GetMainWindow().GetDealer().Name;
            if (!string.IsNullOrWhiteSpace(dealerName))
                GameEngine.TargetPlayer(dealerName);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[InsufficientBetQueue] Failed: {ex}");
        }
        finally
        {
            Interlocked.Exchange(ref _isProcessing, 0);
            if (!Queue.IsEmpty)
                EnsureProcessing();
        }
    }

    private static CommandGroup? ResolveCommand(Configuration config)
    {
        if (string.IsNullOrWhiteSpace(config.InsufficientBetCommandName))
            return null;

        return config.CommandGroups
            .Concat(config.CustomCommandGroups)
            .FirstOrDefault(g => g.Name.Equals(config.InsufficientBetCommandName, StringComparison.OrdinalIgnoreCase)
                && g.IsActive);
    }
}
