using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlackJackButtler.Regex;
using Newtonsoft.Json;

namespace BlackJackButtler;

public static partial class DefaultsMigration
{
    private static string GetSnapshotFilePath()
    {
        var pluginDir = Plugin.PluginInterface.GetPluginConfigDirectory();
        return Path.Combine(pluginDir, "defaults.json");
    }

    private static string GetAssemblyVersion()
    {
        return typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";
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
                    kvp => kvp.Value.Select(
                        c => new SnapshotCommandDto
                        {
                            Text = c.Text ?? "",
                            Delay = c.Delay
                        }
                    ).ToList()
                );
            }

            if (container.TradeRegex != null)
            {
                snapshot.Regex = container.TradeRegex.Select(
                    r => new SnapshotRegexDto
                    {
                        Name = r.Name ?? "",
                        Patterns = r.Patterns ?? new(),
                        Action = r.Action ?? ""
                    }
                ).ToList();
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

        return snapshot.Messages.Select(
            kvp => new MessageBatch
            {
                Name = kvp.Key,
                Messages = kvp.Value
            }
        ).ToList();
    }

    public static List<CommandGroup>? GetSnapshotCommands()
    {
        var snapshot = LoadSnapshot();
        if (snapshot == null || snapshot.Commands.Count == 0)
            return null;

        return snapshot.Commands.Select(
            kvp =>
            {
                var g = new CommandGroup { Name = kvp.Key };
                g.Commands.AddRange(
                    kvp.Value.Select(
                        c => new PluginCommand
                        {
                            Text = c.Text,
                            Delay = c.Delay
                        }
                    )
                );
                return g;
            }
        ).ToList();
    }

    public static List<UserRegexEntry>? GetSnapshotRegex()
    {
        var snapshot = LoadSnapshot();
        if (snapshot == null || snapshot.Regex.Count == 0)
            return null;

        return snapshot.Regex.Select(
            r => new UserRegexEntry
            {
                Name = r.Name,
                Patterns = r.Patterns,
                Action = Enum.TryParse<RegexAction>(r.Action, out var act) ? act : RegexAction.None,
                Mode = RegexEntryMode.Trigger,
                Enabled = true
            }
        ).ToList();
    }
}
