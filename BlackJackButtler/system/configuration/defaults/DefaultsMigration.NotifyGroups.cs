using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlackJackButtler.Regex;
using Newtonsoft.Json;

namespace BlackJackButtler;

public static partial class DefaultsMigration
{
    private static readonly Dictionary<string, string> NotifyGroupRenames = new()
    {
        { "PlayerBJ", "Natural BlackJack Notify" },
        { "PlayerDirtyBJ", "Dirty BlackJack Notify" },
        { "PlayerCharlie", "Charlie Notify" },
    };

    private static void MigrateNotifyGroupNames(Configuration config, DefaultsSnapshot snapshot)
    {
        foreach (var (oldName, newName) in NotifyGroupRenames)
        {
            var oldGroup = config.CommandGroups.FirstOrDefault(g => g.Name == oldName);
            var newGroup = config.CommandGroups.FirstOrDefault(g => g.Name == newName);

            if (oldGroup != null && newGroup == null)
            {
                oldGroup.Name = newName;
                Plugin.Log.Information($"[DefaultsMigration] Renamed command group: {oldName} -> {newName}");
            }
            else if (oldGroup != null)
            {
                config.CommandGroups.Remove(oldGroup);
                Plugin.Log.Information($"[DefaultsMigration] Removed duplicate old group: {oldName}");
            }

            if (snapshot.Commands.TryGetValue(oldName, out var snapshotCmds))
            {
                snapshot.Commands.Remove(oldName);
                if (!snapshot.Commands.ContainsKey(newName))
                    snapshot.Commands[newName] = snapshotCmds;
            }
        }
    }

    internal static bool MigrateNotifyGroups(Configuration config)
    {
        var snapshot = LoadSnapshot();
        var container = DefaultsManager.GetRawContainer();
        if (container == null) return false;

        if (snapshot == null)
            snapshot = CreateFreshSnapshot(GetAssemblyVersion());

        bool changed = false;

        MigrateNotifyGroupNames(config, snapshot);

        var targetGroupNames = new[] { "Natural BlackJack Notify", "Dirty BlackJack Notify", "Charlie Notify" };

        if (container.Commands != null)
        {
            foreach (var groupName in targetGroupNames)
            {
                if (!container.Commands.ContainsKey(groupName)) continue;

                var codeDefaults = container.Commands[groupName];
                var configGroup = config.CommandGroups.FirstOrDefault(g => g.Name == groupName);

                var snapshotCmds = snapshot.Commands.GetValueOrDefault(groupName);

                if (configGroup == null)
                {
                    var group = new CommandGroup { Name = groupName };
                    group.Commands.AddRange(
                        codeDefaults.Select(
                            c => new PluginCommand
                            {
                                Text = c.Text ?? "",
                                Delay = c.Delay
                            }
                        )
                    );
                    config.CommandGroups.Add(group);
                    changed = true;
                }
                else if (snapshotCmds != null && CommandsMatchSnapshot(configGroup.Commands, snapshotCmds))
                {
                    configGroup.Commands.Clear();
                    configGroup.Commands.AddRange(
                        codeDefaults.Select(
                            c => new PluginCommand
                            {
                                Text = c.Text ?? "",
                                Delay = c.Delay
                            }
                        )
                    );
                    changed = true;
                }
            }
        }

        var messageBatchNames = new[]
        {
            "Player Charlie Messages",
            "Player BlackJack Messages",
            "Player BlackJack Messages Shout",
            "Player Dirty BlackJack Messages",
        };

        if (container.Messages != null)
        {
            foreach (var batchName in messageBatchNames)
            {
                if (!container.Messages.ContainsKey(batchName)) continue;

                var codeMessages = container.Messages[batchName];
                var configBatch = config.MessageBatches.FirstOrDefault(b => b.Name == batchName);
                var snapshotMessages = snapshot.Messages.GetValueOrDefault(batchName);

                if (configBatch == null)
                {
                    config.MessageBatches.Add(
                        new MessageBatch
                        {
                            Name = batchName,
                            Messages = new List<string>(codeMessages)
                        }
                    );
                    changed = true;
                }
                else if (snapshotMessages != null && configBatch.Messages.SequenceEqual(snapshotMessages))
                {
                    configBatch.Messages = new List<string>(codeMessages);
                    changed = true;
                }
            }
        }

        foreach (var batch in config.MessageBatches)
        {
            for (int i = 0; i < batch.Messages.Count; i++)
            {
                var msg = batch.Messages[i];
                if (string.IsNullOrWhiteSpace(msg)) continue;
                var trimmed = msg.TrimStart();
                if (!trimmed.StartsWith("/tell <t>", StringComparison.OrdinalIgnoreCase) &&
                    !trimmed.StartsWith("/t <t>", StringComparison.OrdinalIgnoreCase))
                    continue;

                int idx = msg.IndexOf("<t>", StringComparison.Ordinal);
                if (idx >= 0)
                {
                    batch.Messages[i] = string.Concat(msg.AsSpan(0, idx), "<.>", msg.AsSpan(idx + 3));
                    changed = true;
                }
            }
        }

        foreach (var (oldName, newName) in NotifyGroupRenames)
            snapshot.Commands.Remove(oldName);

        if (container.Commands != null)
        {
            foreach (var groupName in targetGroupNames)
            {
                if (!container.Commands.ContainsKey(groupName)) continue;
                snapshot.Commands[groupName] = container.Commands[groupName]
                    .Select(c => new SnapshotCommandDto { Text = c.Text ?? "", Delay = c.Delay })
                    .ToList();
            }
        }

        if (container.Messages != null)
        {
            foreach (var batchName in messageBatchNames)
            {
                if (!container.Messages.ContainsKey(batchName)) continue;
                snapshot.Messages[batchName] = new List<string>(container.Messages[batchName]);
            }
        }

        SaveSnapshot(snapshot);
        return changed;
    }
}
