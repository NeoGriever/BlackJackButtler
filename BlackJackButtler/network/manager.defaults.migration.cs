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

    /// <summary>
    /// Main entry point called at plugin startup.
    /// Returns true if the configuration was changed and needs saving.
    /// </summary>
    public static bool RunMigration(Configuration config)
    {
        bool coreChanged = RunMigrationCore(config);
        bool protectedAdded = EnsureProtectedEntriesExist(config);
        return coreChanged || protectedAdded;
    }

    private static bool RunMigrationCore(Configuration config)
    {
        try
        {
            var assemblyVersion = GetAssemblyVersion();
            var snapshot = LoadSnapshot();

            if (snapshot == null)
            {
                // defaults.json does not exist
                bool isExistingUser = config.DefaultBatchesSeeded
                                   || config.DefaultRegexSeeded
                                   || config.DefaultCommandsSeeded;

                if (isExistingUser)
                {
                    // Existing user upgrading: create defaults.json but don't touch config
                    var fresh = CreateFreshSnapshot(assemblyVersion);
                    SaveSnapshot(fresh);
                    Plugin.Log.Information("[DefaultsMigration] Existing user detected. Created defaults.json without modifying config.");
                    return false;
                }
                else
                {
                    SeedAllDefaultsFromV2(config);
                    var fresh = CreateFreshSnapshot(assemblyVersion);
                    SaveSnapshot(fresh);
                    Plugin.Log.Information("[DefaultsMigration] New installation detected. Seeded V2 defaults and created defaults.json.");
                    return true;
                }
            }
            else
            {
                // defaults.json exists - check version
                if (snapshot.Version == assemblyVersion)
                {
                    Plugin.Log.Debug("[DefaultsMigration] Version matches. No migration needed.");
                    return false;
                }

                // Version differs - merge new entries
                Plugin.Log.Information($"[DefaultsMigration] Version changed: {snapshot.Version} -> {assemblyVersion}. Running migration.");
                MigrateNotifyGroupNames(config, snapshot);
                bool changed = MergeNewEntries(snapshot, config);
                UpdateSnapshotFile(snapshot, assemblyVersion);
                return changed;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[DefaultsMigration] Migration failed: {ex.Message}");
            // Fallback: ensure defaults are seeded at minimum
            return config.EnsureDefaultsOnce();
        }
    }

    /// <summary>
    /// Seeds all code defaults into the configuration (for new installations).
    /// </summary>
    public static void SeedAllDefaults(Configuration config)
    {
        config.ForceResetStandardBatches();
        config.ForceResetStandardRegexes();
        config.ForceResetCommandGroups();
    }

    public static void SeedAllDefaultsFromV2(Configuration config)
    {
        var container = DefaultsManagerV2.GetRawContainer();
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
                g.Commands.AddRange(kv.Value.Select(c => new PluginCommand
                {
                    Text = c.Text ?? "",
                    Delay = c.Delay
                }));
                config.CommandGroups.Add(g);
            }
            config.DefaultCommandsSeeded = true;
        }

        config.ForceResetStandardRegexes();
    }

    public static bool EnsureProtectedEntriesExist(Configuration config)
    {
        var container = DefaultsManagerV2.GetRawContainer();
        if (container == null) return false;
        bool changed = false;

        if (container.Messages != null)
        {
            foreach (var kvp in container.Messages)
            {
                if (config.MessageBatches.All(b => b.Name != kvp.Key))
                {
                    config.MessageBatches.Add(new MessageBatch
                    {
                        Name = kvp.Key,
                        Messages = new List<string>(kvp.Value)
                    });
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
                    group.Commands.AddRange(kvp.Value.Select(c => new PluginCommand
                    {
                        Text = c.Text ?? "",
                        Delay = c.Delay
                    }));
                    config.CommandGroups.Add(group);
                    changed = true;
                    Plugin.Log.Information($"[DefaultsMigration] Re-seeded missing protected command group: {kvp.Key}");
                }
            }
        }

        return changed;
    }

    /// <summary>
    /// Finds entries in code defaults that are NOT in the file snapshot,
    /// and inserts them into the config if they don't already exist there.
    /// Returns true if config was modified.
    /// </summary>
    internal static bool MergeNewEntries(DefaultsSnapshot fileSnapshot, Configuration config)
    {
        bool changed = false;
        var container = DefaultsManager.GetRawContainer();
        if (container == null) return false;

        // --- Messages ---
        if (container.Messages != null)
        {
            foreach (var kvp in container.Messages)
            {
                if (!fileSnapshot.Messages.ContainsKey(kvp.Key))
                {
                    // New entry: add to snapshot and config
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
                else
                {
                    // Existing entry: update config if user hasn't changed it
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
                        config.MessageBatches.Add(new MessageBatch
                        {
                            Name = kvp.Key,
                            Messages = new List<string>(kvp.Value)
                        });
                        changed = true;
                        Plugin.Log.Information($"[DefaultsMigration] Recovered missing message batch: {kvp.Key}");
                    }

                    fileSnapshot.Messages[kvp.Key] = kvp.Value;
                }
            }
        }

        // --- Commands ---
        if (container.Commands != null)
        {
            foreach (var kvp in container.Commands)
            {
                var codeCommands = kvp.Value.Select(c => new SnapshotCommandDto
                {
                    Text = c.Text ?? "",
                    Delay = c.Delay
                }).ToList();

                if (!fileSnapshot.Commands.ContainsKey(kvp.Key))
                {
                    // New entry: add to snapshot and config
                    fileSnapshot.Commands[kvp.Key] = codeCommands;

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
                else
                {
                    // Existing entry: update config if user hasn't changed it
                    var snapshotCommands = fileSnapshot.Commands[kvp.Key];
                    var configGroup = config.CommandGroups.FirstOrDefault(g => g.Name == kvp.Key);

                    if (configGroup != null && CommandsMatchSnapshot(configGroup.Commands, snapshotCommands))
                    {
                        configGroup.Commands.Clear();
                        configGroup.Commands.AddRange(kvp.Value.Select(c => new PluginCommand
                        {
                            Text = c.Text ?? "",
                            Delay = c.Delay
                        }));
                        changed = true;
                        Plugin.Log.Information($"[DefaultsMigration] Updated unchanged command group: {kvp.Key}");
                    }
                    else if (configGroup == null)
                    {
                        var group = new CommandGroup { Name = kvp.Key };
                        group.Commands.AddRange(kvp.Value.Select(c => new PluginCommand
                        {
                            Text = c.Text ?? "",
                            Delay = c.Delay
                        }));
                        config.CommandGroups.Add(group);
                        changed = true;
                        Plugin.Log.Information($"[DefaultsMigration] Recovered missing command group: {kvp.Key}");
                    }

                    fileSnapshot.Commands[kvp.Key] = codeCommands;
                }
            }
        }

        // --- Fix Gil regex patterns (period -> comma) ---
        FixGilRegexPatterns(config);

        // --- Regex ---
        if (container.TradeRegex != null)
        {
            foreach (var r in container.TradeRegex)
            {
                var name = r.Name ?? "";
                if (!fileSnapshot.Regex.Any(x => x.Name == name))
                {
                    // Add to snapshot
                    fileSnapshot.Regex.Add(new SnapshotRegexDto
                    {
                        Name = name,
                        Patterns = r.Patterns ?? new(),
                        Action = r.Action ?? ""
                    });

                    // Insert into config if not already present
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

    private static void FixGilRegexPatterns(Configuration config)
    {
        var names = new[] { "Trade: Gil In", "Trade: Gil Out" };
        foreach (var name in names)
        {
            var entry = config.UserRegexes.FirstOrDefault(x => x.Name == name);
            if (entry == null) continue;

            for (int i = 0; i < entry.Patterns.Count; i++)
            {
                // Fix only the English variant: it contains lowercase "gil" and the wrong [\d.]+ pattern.
                if (entry.Patterns[i].Contains(@"[\d.]") && entry.Patterns[i].Contains("gil"))
                {
                    entry.Patterns[i] = entry.Patterns[i].Replace(@"[\d.]", @"[\d,]");
                    Plugin.Log.Information($"[DefaultsMigration] Fixed Gil pattern (period->comma) in: {name}");
                }
            }
        }
    }

    internal static bool EnsureGameplayRegexPatterns(Configuration config)
    {
        if (config.GameplayRegexPatternsMigrated)
            return false;

        var changed = false;
        changed |= EnsureRegexPatterns(
            config,
            RegexAction.BankTell,
            "Bank Tell",
            "^bank\\?$",
            "^bank please\\.?$",
            "^what does my bank say\\??$");
        changed |= EnsureRegexPatterns(
            config,
            RegexAction.NextRound,
            "New Round",
            "^ready\\s*[!?.]*$",
            "^rdy$",
            "^let'?s go[!?.]*$");

        config.GameplayRegexPatternsMigrated = true;
        return true;
    }

    private static bool EnsureRegexPatterns(
        Configuration config,
        RegexAction action,
        string fallbackName,
        params string[] patterns)
    {
        var entry = config.UserRegexes.FirstOrDefault(x => x.Action == action)
            ?? config.UserRegexes.FirstOrDefault(x => x.Name.Equals(fallbackName, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
            return false;

        entry.Patterns ??= new List<string>();
        var changed = false;
        foreach (var pattern in patterns)
        {
            if (entry.Patterns.Any(x => x.Equals(pattern, StringComparison.Ordinal)))
                continue;

            entry.Patterns.Add(pattern);
            changed = true;
            Plugin.Log.Information($"[DefaultsMigration] Added gameplay regex pattern to {entry.Name}: {pattern}");
        }

        return changed;
    }

    internal static bool EnsureBankTransferRegex(Configuration config)
    {
        var entry = config.UserRegexes.FirstOrDefault(x =>
            x.Action == RegexAction.BankTransfer
            || x.Name.Equals("Bank Transfer", StringComparison.OrdinalIgnoreCase));
        var changed = false;
        if (entry == null)
        {
            config.UserRegexes.Add(new UserRegexEntry
            {
                Name = "Bank Transfer",
                Patterns = new List<string>
                {
                    @"^transfer\s+(-?(?:(?:\d[\d.,]*\s*[km]?)|half|50%|min|max))\s*$"
                },
                Action = RegexAction.BankTransfer,
                Mode = RegexEntryMode.Trigger,
                Enabled = true,
            });
            changed = true;
            Plugin.Log.Information("[DefaultsMigration] Added Bank Transfer regex entry");
        }

        if (!config.BankTransferRegexMigrated)
        {
            config.BankTransferRegexMigrated = true;
            changed = true;
        }

        return changed;
    }

    internal static bool MigrateTellDotToken(Configuration config)
    {
        if (config.DotTokenMigrated) return false;
        config.DotTokenMigrated = true;

        foreach (var group in config.CommandGroups.Concat(config.CustomCommandGroups))
        {
            foreach (var cmd in group.Commands)
            {
                if (string.IsNullOrWhiteSpace(cmd.Text)) continue;
                var trimmed = cmd.Text.TrimStart();
                if (!trimmed.StartsWith("/tell <t>", StringComparison.OrdinalIgnoreCase) &&
                    !trimmed.StartsWith("/t <t>", StringComparison.OrdinalIgnoreCase))
                    continue;

                int idx = cmd.Text.IndexOf("<t>", StringComparison.Ordinal);
                if (idx >= 0)
                    cmd.Text = string.Concat(cmd.Text.AsSpan(0, idx), "<.>", cmd.Text.AsSpan(idx + 3));
            }
        }

        return true;
    }

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
                    group.Commands.AddRange(codeDefaults.Select(c => new PluginCommand
                    {
                        Text = c.Text ?? "",
                        Delay = c.Delay
                    }));
                    config.CommandGroups.Add(group);
                    changed = true;
                }
                else if (snapshotCmds != null && CommandsMatchSnapshot(configGroup.Commands, snapshotCmds))
                {
                    configGroup.Commands.Clear();
                    configGroup.Commands.AddRange(codeDefaults.Select(c => new PluginCommand
                    {
                        Text = c.Text ?? "",
                        Delay = c.Delay
                    }));
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
                    config.MessageBatches.Add(new MessageBatch
                    {
                        Name = batchName,
                        Messages = new List<string>(codeMessages)
                    });
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

    /// <summary>
    /// Updates the snapshot file with the (possibly modified) snapshot and new version.
    /// </summary>
    private static void UpdateSnapshotFile(DefaultsSnapshot snapshot, string newVersion)
    {
        snapshot.Version = newVersion;
        SaveSnapshot(snapshot);
    }

    /// <summary>
    /// Loads the defaults snapshot from defaults.json. Returns null if file doesn't exist.
    /// </summary>
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

    /// <summary>
    /// Saves the snapshot to defaults.json.
    /// </summary>
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

    /// <summary>
    /// Creates a fresh snapshot from code defaults with the given version.
    /// </summary>
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

    /// <summary>
    /// Completely overwrites defaults.json with current code defaults.
    /// Called by the "Reset Default Config File" button.
    /// </summary>
    public static void ResetSnapshotFile()
    {
        var version = GetAssemblyVersion();
        var fresh = CreateFreshSnapshot(version);
        SaveSnapshot(fresh);
        Plugin.Log.Information("[DefaultsMigration] defaults.json has been reset to code defaults.");
    }

    /// <summary>
    /// Loads messages from defaults.json for ForceReset. Returns null if unavailable.
    /// </summary>
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

    /// <summary>
    /// Loads commands from defaults.json for ForceReset. Returns null if unavailable.
    /// </summary>
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

    /// <summary>
    /// Loads regex entries from defaults.json for ForceReset. Returns null if unavailable.
    /// </summary>
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
