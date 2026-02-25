using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json;

namespace BlackJackButtler;

[Serializable]
public sealed class VenueAddress
{
    public int Plot = 0;
    public int Ward = 0;
    public string World = "";
    public string Housing = "";
}

[Serializable]
public sealed class VipPlayerEntry
{
    public string Name = "";
    public string Note = "";
    public int Tier = 0;
    public string World = "";
}

[Serializable]
public sealed class VenueData
{
    public string Name = "Venue 1";
    public List<VipPlayerEntry> Vips = new();
    public VenueAddress Address = new();
}

public static class VenueManager
{
    private static string _filePath = string.Empty;
    private static List<VenueData> _venues = new();

    private static VenueData? _cachedVenue;
    private static VenueData? _selectedVenue;
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
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                _venues = JsonConvert.DeserializeObject<List<VenueData>>(json) ?? new();
            }
        }
        catch { _venues = new(); }
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

    public static List<VenueData> GetAllVenues() => _venues;

    public static unsafe VenueAddress? GetCurrentAddress()
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

        return new VenueAddress
        {
            Ward = ward + 1,
            Plot = plot + 1,
            World = worldName,
            Housing = housingArea
        };
    }

    public static VenueData? FindVenueByAddress(VenueAddress addr)
    {
        return _venues.FirstOrDefault(v =>
            v.Address.World.Equals(addr.World, StringComparison.OrdinalIgnoreCase) &&
            v.Address.Housing.Equals(addr.Housing, StringComparison.OrdinalIgnoreCase) &&
            v.Address.Ward == addr.Ward &&
            v.Address.Plot == addr.Plot);
    }

    public static VenueData FindOrCreateVenue(VenueAddress addr, string name)
    {
        var existing = FindVenueByAddress(addr);
        if (existing != null) return existing;

        var venue = new VenueData
        {
            Name = name,
            Address = addr
        };
        _venues.Add(venue);
        _selectedVenue = venue;
        Save();
        InvalidateCache();
        return venue;
    }

    public static int GetPlayerTier(VenueData venue, string name, string world)
    {
        var entry = venue.Vips.FirstOrDefault(v =>
            v.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
            v.World.Equals(world, StringComparison.OrdinalIgnoreCase));
        return entry?.Tier ?? 0;
    }

    public static void SetPlayerTier(VenueData venue, string name, string world, int tier)
    {
        var entry = venue.Vips.FirstOrDefault(v =>
            v.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
            v.World.Equals(world, StringComparison.OrdinalIgnoreCase));

        if (tier == 0)
        {
            if (entry != null)
                venue.Vips.Remove(entry);
        }
        else
        {
            if (entry != null)
                entry.Tier = tier;
            else
                venue.Vips.Add(new VipPlayerEntry { Name = name, World = world, Tier = tier });
        }
        Save();
        InvalidateCache();
    }

    public static VenueData? GetCurrentVenue()
    {
        long frame = (long)ImGui.GetFrameCount();
        if (frame == _cacheFrame)
            return _cachedVenue;

        _cacheFrame = frame;
        var addr = GetCurrentAddress();
        _cachedVenue = addr != null ? FindVenueByAddress(addr) : null;

        if (_cachedVenue != null)
            _selectedVenue = _cachedVenue;
        else if (_selectedVenue != null && _venues.Contains(_selectedVenue))
            _cachedVenue = _selectedVenue;

        return _cachedVenue;
    }

    public static string GetNextVenueName()
    {
        int n = _venues.Count + 1;
        while (_venues.Any(v => v.Name == $"Venue {n}")) n++;
        return $"Venue {n}";
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
