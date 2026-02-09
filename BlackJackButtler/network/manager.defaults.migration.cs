using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlackJackButtler.Regex;
using Newtonsoft.Json;

namespace BlackJackButtler;

[Serializable]
public class DefaultsSnapshot
{
    public string Version { get; set; } = "";
    public Dictionary<string, List<string>> Messages { get; set; } = new();
    public Dictionary<string, List<DefaultsMigration.SnapshotCommandDto>> Commands { get; set; } = new();
    public List<DefaultsMigration.SnapshotRegexDto> Regex { get; set; } = new();
}

public static class DefaultsMigration
{
    [Serializable]
    public class SnapshotCommandDto
    {
        public string Text { get; set; } = "";
        public float Delay { get; set; }
    }

    [Serializable]
    public class SnapshotRegexDto
    {
        public string Name { get; set; } = "";
        public List<string> Patterns { get; set; } = new();
        public string Action { get; set; } = "";
    }

    private static string GetSnapshotFilePath()
    {
        var pluginDir = Plugin.PluginInterface.GetPluginConfigDirectory();
        return Path.Combine(pluginDir, "defaults.json");
    }

    private static string GetAssemblyVersion()
    {
        return typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";
    }

    public static bool RunMigration(Configuration config)
    {
        try
        {
            var assemblyVersion = GetAssemblyVersion();
            var snapshot = LoadSnapshot();

            if (snapshot == null)
            {
                bool isExistingUser = config.DefaultBatchesSeeded
                                   || config.DefaultRegexSeeded
                                   || config.DefaultCommandsSeeded;

                if (isExistingUser)
                {
                    var fresh = CreateFreshSnapshot(assemblyVersion);
                    SaveSnapshot(fresh);
                    Plugin.Log.Information("[DefaultsMigration] Existing user detected. Created defaults.json without modifying config.");
                    return false;
                }
                else
                {
                    SeedAllDefaults(config);
                    var fresh = CreateFreshSnapshot(assemblyVersion);
                    SaveSnapshot(fresh);
                    Plugin.Log.Information("[DefaultsMigration] New installation detected. Seeded all defaults and created defaults.json.");
                    return true;
                }
            }
            else
            {
                if (snapshot.Version == assemblyVersion)
                {
                    Plugin.Log.Debug("[DefaultsMigration] Version matches. No migration needed.");
                    return false;
                }

                Plugin.Log.Information($"[DefaultsMigration] Version changed: {snapshot.Version} -> {assemblyVersion}. Running migration.");
                bool changed = MergeNewEntries(snapshot, config);
                UpdateSnapshotFile(snapshot, assemblyVersion);
                return changed;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[DefaultsMigration] Migration failed: {ex.Message}");
            return config.EnsureDefaultsOnce();
        }
    }

    private static void SeedAllDefaults(Configuration config)
    {
        config.ForceResetStandardBatches();
        config.ForceResetStandardRegexes();
        config.ForceResetCommandGroups();
    }

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
                        config.MessageBatches.Add(new MessageBatch
                        {
                            Name = kvp.Key,
                            Messages = kvp.Value
                        });
                        changed = true;
                        Plugin.Log.Information($"[DefaultsMigration] Added new message batch: {kvp.Key}");
                    }
                }
            }
        }

        if (container.Commands != null)
        {
            foreach (var kvp in container.Commands)
            {
                if (!fileSnapshot.Commands.ContainsKey(kvp.Key))
                {
                    fileSnapshot.Commands[kvp.Key] = kvp.Value.Select(c => new SnapshotCommandDto
                    {
                        Text = c.Text ?? "",
                        Delay = c.Delay
                    }).ToList();

                    if (!config.CommandGroups.Any(g => g.Name == kvp.Key))
                    {
                        var group = new CommandGroup { Name = kvp.Key };
                        group.Commands.AddRange(kvp.Value.Select(c => new PluginCommand
                        {
                            Text = c.Text ?? "",
                            Delay = c.Delay
                        }));
                        config.CommandGroups.Add(group);
                        changed = true;
                        Plugin.Log.Information($"[DefaultsMigration] Added new command group: {kvp.Key}");
                    }
                }
            }
        }

        if (container.TradeRegex != null)
        {
            foreach (var r in container.TradeRegex)
            {
                var name = r.Name ?? "";
                if (!fileSnapshot.Regex.Any(x => x.Name == name))
                {
                    fileSnapshot.Regex.Add(new SnapshotRegexDto
                    {
                        Name = name,
                        Patterns = r.Patterns ?? new(),
                        Action = r.Action ?? ""
                    });

                    if (!config.UserRegexes.Any(x => x.Name == name))
                    {
                        config.UserRegexes.Add(new UserRegexEntry
                        {
                            Name = name,
                            Patterns = r.Patterns ?? new(),
                            Action = Enum.TryParse<RegexAction>(r.Action, out var act) ? act : RegexAction.None,
                            Mode = RegexEntryMode.Trigger,
                            Enabled = true
                        });
                        changed = true;
                        Plugin.Log.Information($"[DefaultsMigration] Added new regex entry: {name}");
                    }
                }
            }
        }

        return changed;
    }

    private static void UpdateSnapshotFile(DefaultsSnapshot snapshot, string newVersion)
    {
        snapshot.Version = newVersion;
        SaveSnapshot(snapshot);
    }

    public static DefaultsSnapshot? LoadSnapshot()
    {
        var path = GetSnapshotFilePath();
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<DefaultsSnapshot>(json);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[DefaultsMigration] Failed to load defaults.json: {ex.Message}");
            return null;
        }
    }

    public static void SaveSnapshot(DefaultsSnapshot snapshot)
    {
        try
        {
            var path = GetSnapshotFilePath();
            var dir = Path.GetDirectoryName(path);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonConvert.SerializeObject(snapshot, Formatting.Indented);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[DefaultsMigration] Failed to save defaults.json: {ex.Message}");
        }
    }

    public static DefaultsSnapshot CreateFreshSnapshot(string version)
    {
        var container = DefaultsManager.GetRawContainer();
        var snapshot = new DefaultsSnapshot { Version = version };

        if (container != null)
        {
            snapshot.Messages = container.Messages ?? new();

            if (container.Commands != null)
            {
                snapshot.Commands = container.Commands.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(c => new SnapshotCommandDto
                    {
                        Text = c.Text ?? "",
                        Delay = c.Delay
                    }).ToList()
                );
            }

            if (container.TradeRegex != null)
            {
                snapshot.Regex = container.TradeRegex.Select(r => new SnapshotRegexDto
                {
                    Name = r.Name ?? "",
                    Patterns = r.Patterns ?? new(),
                    Action = r.Action ?? ""
                }).ToList();
            }
        }

        return snapshot;
    }

    public static void ResetSnapshotFile()
    {
        var version = GetAssemblyVersion();
        var fresh = CreateFreshSnapshot(version);
        SaveSnapshot(fresh);
        Plugin.Log.Information("[DefaultsMigration] defaults.json has been reset to code defaults.");
    }

    public static List<MessageBatch>? GetSnapshotMessages()
    {
        var snapshot = LoadSnapshot();
        if (snapshot == null || snapshot.Messages.Count == 0)
            return null;

        return snapshot.Messages.Select(kvp => new MessageBatch
        {
            Name = kvp.Key,
            Messages = kvp.Value
        }).ToList();
    }

    public static List<CommandGroup>? GetSnapshotCommands()
    {
        var snapshot = LoadSnapshot();
        if (snapshot == null || snapshot.Commands.Count == 0)
            return null;

        return snapshot.Commands.Select(kvp =>
        {
            var g = new CommandGroup { Name = kvp.Key };
            g.Commands.AddRange(kvp.Value.Select(c => new PluginCommand
            {
                Text = c.Text,
                Delay = c.Delay
            }));
            return g;
        }).ToList();
    }

    public static List<UserRegexEntry>? GetSnapshotRegex()
    {
        var snapshot = LoadSnapshot();
        if (snapshot == null || snapshot.Regex.Count == 0)
            return null;

        return snapshot.Regex.Select(r => new UserRegexEntry
        {
            Name = r.Name,
            Patterns = r.Patterns,
            Action = Enum.TryParse<RegexAction>(r.Action, out var act) ? act : RegexAction.None,
            Mode = RegexEntryMode.Trigger,
            Enabled = true
        }).ToList();
    }
}
