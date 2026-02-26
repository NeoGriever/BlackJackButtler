using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace BlackJackButtler;

public sealed class NearbyPlayerInfo
{
    public string Name = string.Empty;
    public string World = string.Empty;
    public float Distance;
    public string FullKey => $"{Name}@{World}";
}

public static class NearbyPlayersManager
{
    private static List<NearbyPlayerInfo> _cached = new();
    private static DateTime _lastScan = DateTime.MinValue;
    private const double ScanIntervalMs = 500;

    public static List<NearbyPlayerInfo> GetNearbyPlayers(Configuration config)
    {
        if ((DateTime.Now - _lastScan).TotalMilliseconds < ScanIntervalMs)
            return _cached;

        _lastScan = DateTime.Now;

        var local = Plugin.ObjectTable.LocalPlayer;
        if (local == null)
        {
            _cached = new List<NearbyPlayerInfo>();
            return _cached;
        }

        var localPos = local.Position;
        var localName = local.Name.TextValue;

        var result = new List<NearbyPlayerInfo>();

        foreach (var obj in Plugin.ObjectTable)
        {
            if (obj.ObjectKind != ObjectKind.Player) continue;
            if (obj is not IPlayerCharacter pc) continue;

            var name = pc.Name.TextValue;
            if (name == localName) continue;

            var world = pc.HomeWorld.Value.Name.ToString();
            var dist = Vector3.Distance(localPos, pc.Position);

            result.Add(new NearbyPlayerInfo
            {
                Name = name,
                World = world,
                Distance = dist,
            });
        }

        var favSet = new HashSet<string>(config.NearbyFavorites);
        result.Sort((a, b) =>
        {
            bool aFav = favSet.Contains(a.FullKey);
            bool bFav = favSet.Contains(b.FullKey);
            if (aFav != bFav) return aFav ? -1 : 1;
            return a.Distance.CompareTo(b.Distance);
        });

        _cached = result;
        return _cached;
    }

    public static void InvalidateCache()
    {
        _lastScan = DateTime.MinValue;
    }
}
