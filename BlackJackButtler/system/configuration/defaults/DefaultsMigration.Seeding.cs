using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlackJackButtler.Regex;
using Newtonsoft.Json;

namespace BlackJackButtler;

public static partial class DefaultsMigration
{
    public static void SeedAllDefaults(Configuration config)
    {
        config.ForceResetStandardBatches();
        config.ForceResetStandardRegexes();
        config.ForceResetCommandGroups();
    }

    public static void SeedDefaultsFromEmbeddedConfiguration(Configuration config)
    {
        var container = DefaultsManager.GetRawContainer();
        if (container == null)
        {
            SeedAllDefaults(config);
            return;
        }

        if (container.Messages != null)
        {
            var names = container.Messages.Keys.ToHashSet();
            config.MessageBatches.RemoveAll(b => names.Contains(b.Name));
            foreach (var kv in container.Messages)
                config.MessageBatches.Add(new MessageBatch { Name = kv.Key, Messages = new List<string>(kv.Value) });
            config.DefaultBatchesSeeded = true;
        }

        if (container.Commands != null)
        {
            var names = container.Commands.Keys.ToHashSet();
            config.CommandGroups.RemoveAll(g => names.Contains(g.Name));
            foreach (var kv in container.Commands)
            {
                var g = new CommandGroup { Name = kv.Key };
                g.Commands.AddRange(
                    kv.Value.Select(
                        c => new PluginCommand
                        {
                            Text = c.Text ?? "",
                            Delay = c.Delay
                        }
                    )
                );
                config.CommandGroups.Add(g);
            }
            config.DefaultCommandsSeeded = true;
        }

        config.ForceResetStandardRegexes();
    }

    public static bool EnsureProtectedEntriesExist(Configuration config)
    {
        var container = DefaultsManager.GetRawContainer();
        if (container == null) return false;
        bool changed = false;

        if (container.Messages != null)
        {
            foreach (var kvp in container.Messages)
            {
                if (config.MessageBatches.All(b => b.Name != kvp.Key))
                {
                    config.MessageBatches.Add(
                        new MessageBatch
                        {
                            Name = kvp.Key,
                            Messages = new List<string>(kvp.Value)
                        }
                    );
                    changed = true;
                    Plugin.Log.Information($"[DefaultsMigration] Re-seeded missing protected message batch: {kvp.Key}");
                }
            }
        }

        if (container.Commands != null)
        {
            foreach (var kvp in container.Commands)
            {
                if (config.CommandGroups.All(g => g.Name != kvp.Key))
                {
                    var group = new CommandGroup { Name = kvp.Key };
                    group.Commands.AddRange(
                        kvp.Value.Select(
                            c => new PluginCommand
                            {
                                Text = c.Text ?? "",
                                Delay = c.Delay
                            }
                        )
                    );
                    config.CommandGroups.Add(group);
                    changed = true;
                    Plugin.Log.Information($"[DefaultsMigration] Re-seeded missing protected command group: {kvp.Key}");
                }
            }
        }

        return changed;
    }
}
