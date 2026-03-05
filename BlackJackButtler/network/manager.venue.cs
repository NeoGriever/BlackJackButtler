using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlackJackButtler;

[Serializable]
public sealed class VenueData
{
    public string Name = "";
    public Dictionary<string, int> Vips = new();
}

public static class VenueManager
{
    private static string _filePath = string.Empty;
    private static Dictionary<string, VenueData> _venues = new();

    private static VenueData? _cachedVenue;
    private static string? _selectedKey;
    private static long _cacheFrame = -1;

    public static void InvalidateCache() { _cacheFrame = -1; }

    public static void Init(string configDir)
    {
        _filePath = Path.Combine(configDir, "BJB.venues.json");
        Load();
    }

    public static void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json)) return;

            var token = JToken.Parse(json);
            if (token.Type == JTokenType.Object)
            {
                _venues = token.ToObject<Dictionary<string, VenueData>>() ?? new();
            }
            else if (token.Type == JTokenType.Array)
            {
                MigrateFromLegacy(token);
            }
        }
        catch { _venues = new(); }
    }

    private static void MigrateFromLegacy(JToken arrayToken)
    {
        _venues = new();
        foreach (var item in arrayToken)
        {
            var addr = item["Address"];
            if (addr == null) continue;

            string housing = addr.Value<string>("Housing") ?? "";
            int ward = addr.Value<int>("Ward");
            int plot = addr.Value<int>("Plot");
            string world = addr.Value<string>("World") ?? "";

            string key = string.IsNullOrEmpty(housing)
                ? ""
                : $"{housing}/{ward}/{plot}/{world}";

            if (string.IsNullOrEmpty(key)) continue;

            var venue = new VenueData
            {
                Name = item.Value<string>("Name") ?? ""
            };

            var vips = item["Vips"] as JArray;
            if (vips != null)
            {
                foreach (var vip in vips)
                {
                    string name = vip.Value<string>("Name") ?? "";
                    string vipWorld = vip.Value<string>("World") ?? "";
                    int tier = vip.Value<int>("Tier");
                    if (!string.IsNullOrEmpty(name) && tier > 0)
                        venue.Vips[$"{name}@{vipWorld}"] = tier;
                }
            }

            _venues[key] = venue;
        }
        Save();
    }

    public static void Save()
    {
        try
        {
            var json = JsonConvert.SerializeObject(_venues, Formatting.Indented);
            File.WriteAllText(_filePath, json);
        }
        catch { }
    }

    public static unsafe string? GetCurrentAddressKey()
    {
        var hm = FFXIVClientStructs.FFXIV.Client.Game.HousingManager.Instance();
        if (hm == null) return null;

        short ward = hm->GetCurrentWard();
        short plot = hm->GetCurrentPlot();
        if (ward < 0 || plot < 0) return null;

        var local = Plugin.ObjectTable.LocalPlayer;
        if (local == null) return null;

        string worldName = local.CurrentWorld.Value.Name.ToString();
        string housingArea = GetHousingAreaName(Plugin.ClientState.TerritoryType);
        if (string.IsNullOrEmpty(housingArea)) return null;

        return $"{housingArea}/{ward + 1}/{plot + 1}/{worldName}";
    }

    public static string GetPlayerKey(string name, string world) => $"{name}@{world}";

    public static int GetPlayerTier(VenueData venue, string name, string world)
    {
        return venue.Vips.TryGetValue(GetPlayerKey(name, world), out var tier) ? tier : 0;
    }

    public static void SetPlayerTier(VenueData venue, string name, string world, int tier)
    {
        var key = GetPlayerKey(name, world);
        if (tier == 0)
            venue.Vips.Remove(key);
        else
            venue.Vips[key] = tier;
        Save();
        InvalidateCache();
    }

    public static VenueData GetOrCreateVenue(string addressKey)
    {
        if (_venues.TryGetValue(addressKey, out var existing))
            return existing;

        var venue = new VenueData();
        _venues[addressKey] = venue;
        _selectedKey = addressKey;
        Save();
        InvalidateCache();
        return venue;
    }

    public static VenueData? GetCurrentVenue()
    {
        long frame = (long)ImGui.GetFrameCount();
        if (frame == _cacheFrame)
            return _cachedVenue;

        _cacheFrame = frame;
        var key = GetCurrentAddressKey();

        if (key != null && _venues.TryGetValue(key, out var venue))
        {
            _cachedVenue = venue;
            _selectedKey = key;
        }
        else if (_selectedKey != null && _venues.TryGetValue(_selectedKey, out var fallback))
        {
            _cachedVenue = fallback;
        }
        else
        {
            _cachedVenue = null;
        }

        return _cachedVenue;
    }

    public static string? GetCurrentKey()
    {
        var key = GetCurrentAddressKey();
        return key ?? _selectedKey;
    }

    public static string ResolveWorldName(uint worldId)
    {
        if (worldId == 0) return string.Empty;

        var local = Plugin.ObjectTable.LocalPlayer;
        if (local != null && local.CurrentWorld.RowId == worldId)
            return local.CurrentWorld.Value.Name.ToString();

        for (int i = 0; i < Plugin.PartyList.Length; i++)
        {
            var member = Plugin.PartyList[i];
            if (member != null && member.World.RowId == worldId)
                return member.World.Value.Name.ToString();
        }

        return string.Empty;
    }

    private static string GetHousingAreaName(ushort territoryId)
    {
        return territoryId switch
        {
            339 or 340 => "Mist",
            341 or 342 => "The Lavender Beds",
            345 or 346 => "The Goblet",
            641 or 642 => "Shirogane",
            979 or 980 => "Empyreum",
            _ => ""
        };
    }
}
