using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace BlackJackButtler;

public static class PresetStorage
{
    private static string _presetsPath = "";
    private static string _configDir = "";

    public static void Initialize(string configDir)
    {
        _configDir = configDir;
        _presetsPath = Path.Combine(configDir, "presets.json");
    }

    public static bool PresetsFileExists() => File.Exists(_presetsPath);

    public static List<PresetEntry> Load()
    {
        if (!File.Exists(_presetsPath)) return new List<PresetEntry>();
        try
        {
            var json = File.ReadAllText(_presetsPath);
            return JsonConvert.DeserializeObject<List<PresetEntry>>(json) ?? new List<PresetEntry>();
        }
        catch
        {
            return new List<PresetEntry>();
        }
    }

    public static void Save(List<PresetEntry> presets)
    {
        var json = JsonConvert.SerializeObject(presets, Formatting.Indented);
        File.WriteAllText(_presetsPath, json);
    }

    public static string WriteBackup(Configuration config)
    {
        int n = 1;
        string path;
        var date = DateTime.Now.ToString("dd-MM-yyyy");
        do
        {
            path = Path.Combine(_configDir, $"BACKUP-{date}-{n:D4}.json");
            n++;
        }
        while (File.Exists(path));

        var json = JsonConvert.SerializeObject(config, Formatting.Indented);
        File.WriteAllText(path, json);
        return path;
    }

    public static bool VerifyMigration(List<PresetEntry> original, List<PresetEntry> loaded)
    {
        if (original.Count != loaded.Count) return false;
        foreach (var orig in original)
        {
            var match = loaded.FirstOrDefault(l => l.PresetId == orig.PresetId);
            if (match == null) return false;
            if (match.Name != orig.Name) return false;
            if (match.SnapshotJson != orig.SnapshotJson) return false;
        }
        return true;
    }
}
