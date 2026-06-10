using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlackJackButtler;

public static class VipManager
{
    private static string _filePath = string.Empty;
    private static Dictionary<string, int> _vips = new();

    public static void Init(string configDir)
    {
        _filePath = Path.Combine(configDir, "BJB.vips.json");

        if (!File.Exists(_filePath))
            MigrateFromVenues(configDir);

        Load();
    }

    public static void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json)) return;
            _vips = JsonConvert.DeserializeObject<Dictionary<string, int>>(json) ?? new();
        }
        catch { _vips = new(); }
    }

    public static void Save()
    {
        try
        {
            var json = JsonConvert.SerializeObject(_vips, Formatting.Indented);
            File.WriteAllText(_filePath, json);
        }
        catch { }
    }

    public static string GetPlayerKey(string name, string world) => $"{name}@{world}";

    public static int GetPlayerTier(string name, string world)
    {
        return _vips.TryGetValue(GetPlayerKey(name, world), out var tier) ? tier : 0;
    }

    public static void SetPlayerTier(string name, string world, int tier)
    {
        var key = GetPlayerKey(name, world);
        if (tier == 0)
            _vips.Remove(key);
        else
            _vips[key] = tier;
        Save();
    }

    public static void CycleTier(string name, string world, int totalTiers)
    {
        int current = GetPlayerTier(name, world);
        int next = (current + 1) % (totalTiers + 1);
        SetPlayerTier(name, world, next);
    }

    public static string ResolveWorldName(uint worldId)
    {
        if (worldId == 0) return string.Empty;

        var local = Plugin.ObjectTable.LocalPlayer;
        if (local != null && local.HomeWorld.RowId == worldId)
            return local.HomeWorld.Value.Name.ToString();

        foreach (var member in GroupContextManager.GetCurrentMembers(Plugin.Instance.Configuration))
        {
            if (member != null && member.World.RowId == worldId)
                return member.World.Value.Name.ToString();
        }

        return string.Empty;
    }

    private static void MigrateFromVenues(string configDir)
    {
        var venuesPath = Path.Combine(configDir, "BJB.venues.json");
        if (!File.Exists(venuesPath)) return;

        try
        {
            var json = File.ReadAllText(venuesPath);
            if (string.IsNullOrWhiteSpace(json)) return;

            var token = JToken.Parse(json);
            var merged = new Dictionary<string, int>();

            if (token.Type == JTokenType.Object)
            {
                var venues = token.ToObject<Dictionary<string, VenueData>>();
                if (venues != null)
                {
                    foreach (var venue in venues.Values)
                    {
                        foreach (var (key, tier) in venue.Vips)
                        {
                            if (!merged.TryGetValue(key, out var existing) || tier > existing)
                                merged[key] = tier;
                        }
                    }
                }
            }
            else if (token.Type == JTokenType.Array)
            {
                foreach (var item in token)
                {
                    var vips = item["Vips"] as JArray;
                    if (vips == null) continue;
                    foreach (var vip in vips)
                    {
                        string name = vip.Value<string>("Name") ?? "";
                        string world = vip.Value<string>("World") ?? "";
                        int tier = vip.Value<int>("Tier");
                        if (string.IsNullOrEmpty(name) || tier <= 0) continue;
                        var key = $"{name}@{world}";
                        if (!merged.TryGetValue(key, out var existing) || tier > existing)
                            merged[key] = tier;
                    }
                }
            }

            _vips = merged;
            Save();

            var bakPath = venuesPath + ".bak";
            if (File.Exists(bakPath)) File.Delete(bakPath);
            File.Move(venuesPath, bakPath);
        }
        catch { }
    }
}

[Serializable]
public sealed class VenueData
{
    public string Name = "";
    public Dictionary<string, int> Vips = new();
}
