using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace BlackJackButtler;

public static class NearbyAutoActManager
{
    private sealed record QueueItem(string Name, string World);

    private static readonly HashSet<string> Inside = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, DateTime> LastTriggered = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Queue<QueueItem> Queue = new();
    private static readonly HashSet<string> Queued = new(StringComparer.OrdinalIgnoreCase);

    public static void Update(List<NearbyPlayerInfo> nearby, Configuration cfg)
    {
        if (!cfg.NearbyAutoActEnabled || string.IsNullOrWhiteSpace(cfg.NearbyAutoActCommandName))
        {
            Inside.Clear();
            Queue.Clear();
            Queued.Clear();
            return;
        }

        var current = nearby.Where(p => p.IsInRange).ToList();
        var currentKeys = current.Select(p => p.FullKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var oldKey in Inside.Where(k => !currentKeys.Contains(k)).ToList())
            Inside.Remove(oldKey);

        var ignored = new HashSet<string>(cfg.NearbyAutoActIgnoreList ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        var timeout = TimeSpan.FromMinutes(Math.Clamp(cfg.NearbyAutoActTimeoutMinutes, 1f, 1440f));

        foreach (var player in current)
        {
            if (!Inside.Add(player.FullKey))
                continue;

            if (ignored.Contains(player.FullKey))
                continue;

            if (LastTriggered.TryGetValue(player.FullKey, out var last) && DateTime.Now - last < timeout)
                continue;

            if (Queued.Add(player.FullKey))
                Queue.Enqueue(new QueueItem(player.Name, player.World));
        }

        ProcessQueue(cfg);
    }

    private static void ProcessQueue(Configuration cfg)
    {
        if (Queue.Count == 0) return;
        if (GameActionQueueManager.IsBusy || CommandExecutor.IsRunning || CommandExecutor.IsFollowUpPending) return;

        var item = Queue.Dequeue();
        var key = $"{item.Name}@{item.World}";
        Queued.Remove(key);

        var window = Plugin.Instance.GetMainWindow();
        window.AddDebugLog($"[NearbyAutoAct] Executing '{cfg.NearbyAutoActCommandName}' for {key}");
        LastTriggered[key] = DateTime.Now;

        var currentTarget = Plugin.TargetManager.Target;
        var previousName = currentTarget?.Name.TextValue ?? string.Empty;
        var previousWorld = currentTarget is IPlayerCharacter pc
            ? pc.HomeWorld.Value.Name.ToString()
            : string.Empty;

        Plugin.Instance.RunAutoAction(
            "NearbyAutoAct",
            async () =>
            {
                GameEngine.TargetPlayer(item.Name, item.World);
                var targetName = string.IsNullOrWhiteSpace(item.World) ? item.Name : $"{item.Name}@{item.World}";
                await CommandExecutor.ExecuteGroup(cfg.NearbyAutoActCommandName, targetName, cfg);
                if (!string.IsNullOrWhiteSpace(previousName))
                    GameEngine.TargetPlayer(previousName, previousWorld);
            },
            () => cfg.NearbyAutoActEnabled);
    }
}
