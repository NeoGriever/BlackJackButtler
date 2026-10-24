using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlackJackButtler.Regex;
using Newtonsoft.Json;

namespace BlackJackButtler;

public static partial class DefaultsMigration
{
    internal static bool MergeNewEntries(DefaultsSnapshot fileSnapshot, Configuration config)
    {
        bool changed = false;
        var container = DefaultsManager.GetRawContainer();
        if (container == null) return false;

        if (container.Messages != null)
        {
            foreach (var kvp in container.Messages)
            {
                if (!fileSnapshot.Messages.ContainsKey(kvp.Key))
                {
                    fileSnapshot.Messages[kvp.Key] = kvp.Value;

                    if (!config.MessageBatches.Any(b => b.Name == kvp.Key))
                    {
                        config.MessageBatches.Add(
                            new MessageBatch
                            {
                                Name = kvp.Key,
                                Messages = kvp.Value
                            }
                        );
                        changed = true;
                        Plugin.Log.Information($"[DefaultsMigration] Added new message batch: {kvp.Key}");
                    }
                }
                else
                {
                    var snapshotMessages = fileSnapshot.Messages[kvp.Key];
                    var configBatch = config.MessageBatches.FirstOrDefault(b => b.Name == kvp.Key);

                    if (configBatch != null && configBatch.Messages.SequenceEqual(snapshotMessages))
                    {
                        configBatch.Messages = new List<string>(kvp.Value);
                        changed = true;
                        Plugin.Log.Information($"[DefaultsMigration] Updated unchanged message batch: {kvp.Key}");
                    }
                    else if (configBatch == null)
                    {
                        config.MessageBatches.Add(
                            new MessageBatch
                            {
                                Name = kvp.Key,
                                Messages = new List<string>(kvp.Value)
                            }
                        );
                        changed = true;
                        Plugin.Log.Information($"[DefaultsMigration] Recovered missing message batch: {kvp.Key}");
                    }

                    fileSnapshot.Messages[kvp.Key] = kvp.Value;
                }
            }
        }

        if (container.Commands != null)
        {
            foreach (var kvp in container.Commands)
            {
                var codeCommands = kvp.Value.Select(
                    c => new SnapshotCommandDto
                    {
                        Text = c.Text ?? "",
                        Delay = c.Delay
                    }
                ).ToList();

                if (!fileSnapshot.Commands.ContainsKey(kvp.Key))
                {
                    fileSnapshot.Commands[kvp.Key] = codeCommands;

                    if (!config.CommandGroups.Any(g => g.Name == kvp.Key))
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
                        Plugin.Log.Information($"[DefaultsMigration] Added new command group: {kvp.Key}");
                    }
                }
                else
                {
                    var snapshotCommands = fileSnapshot.Commands[kvp.Key];
                    var configGroup = config.CommandGroups.FirstOrDefault(g => g.Name == kvp.Key);

                    if (configGroup != null && CommandsMatchSnapshot(configGroup.Commands, snapshotCommands))
                    {
                        configGroup.Commands.Clear();
                        configGroup.Commands.AddRange(
                            kvp.Value.Select(
                                c => new PluginCommand
                                {
                                    Text = c.Text ?? "",
                                    Delay = c.Delay
                                }
                            )
                        );
                        changed = true;
                        Plugin.Log.Information($"[DefaultsMigration] Updated unchanged command group: {kvp.Key}");
                    }
                    else if (configGroup == null)
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
                        Plugin.Log.Information($"[DefaultsMigration] Recovered missing command group: {kvp.Key}");
                    }

                    fileSnapshot.Commands[kvp.Key] = codeCommands;
                }
            }
        }

        FixGilRegexPatterns(config);

        if (container.TradeRegex != null)
        {
            foreach (var r in container.TradeRegex)
            {
                var name = r.Name ?? "";
                if (!fileSnapshot.Regex.Any(x => x.Name == name))
                {
                    fileSnapshot.Regex.Add(
                        new SnapshotRegexDto
                        {
                            Name = name,
                            Patterns = r.Patterns ?? new(),
                            Action = r.Action ?? ""
                        }
                    );

                    if (!config.UserRegexes.Any(x => x.Name == name))
                    {
                        config.UserRegexes.Add(
                            new UserRegexEntry
                            {
                                Name = name,
                                Patterns = r.Patterns ?? new(),
                                Action = Enum.TryParse<RegexAction>(r.Action, out var act) ? act : RegexAction.None,
                                Mode = RegexEntryMode.Trigger,
                                Enabled = true
                            }
                        );
                        changed = true;
                        Plugin.Log.Information($"[DefaultsMigration] Added new regex entry: {name}");
                    }
                }
            }
        }

        return changed;
    }

    private static bool CommandsMatchSnapshot(List<PluginCommand> configCmds, List<SnapshotCommandDto> snapshotCmds)
    {
        if (configCmds.Count != snapshotCmds.Count) return false;
        for (int i = 0; i < configCmds.Count; i++)
        {
            if (configCmds[i].Text != snapshotCmds[i].Text) return false;
            if (Math.Abs(configCmds[i].Delay - snapshotCmds[i].Delay) > 0.001f) return false;
        }
        return true;
    }
}
