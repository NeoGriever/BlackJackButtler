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
    public bool IsInRange;
    public string FullKey => $"{Name}@{World}";
}

public readonly record struct NearbyArea(Vector3 Center, float Radius, NearbyShapeMode Shape, float AspectRatio, float RotationRadians)
{
    public bool Contains(Vector3 position)
    {
        var verticalDelta = MathF.Abs(position.Y - Center.Y);
        var dx = position.X - Center.X;
        var dz = position.Z - Center.Z;

        if (Shape == NearbyShapeMode.Circle)
            return MathF.Sqrt(dx * dx + dz * dz) <= Radius;

        if (verticalDelta > 1f)
            return false;

        var aspect = Math.Clamp(AspectRatio, 0.1f, 10f);
        var halfX = Radius * aspect;
        var halfZ = Radius;
        var cos = MathF.Cos(-RotationRadians);
        var sin = MathF.Sin(-RotationRadians);
        var localX = dx * cos - dz * sin;
        var localZ = dx * sin + dz * cos;

        return MathF.Abs(localX) <= halfX && MathF.Abs(localZ) <= halfZ;
    }

    public float HorizontalDistance(Vector3 position)
    {
        var dx = position.X - Center.X;
        var dz = position.Z - Center.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}

public static class NearbyPlayersManager
{
    private static List<NearbyPlayerInfo> _cached = new();
    private static DateTime _lastScan = DateTime.MinValue;
    private const double ScanIntervalMs = 500;

    public static bool PauseSorting { get; set; }

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

        var area = GetArea(config);
        var localName = local.Name.TextValue;

        if (PauseSorting)
        {
            foreach (var info in _cached)
            {
                foreach (var obj in Plugin.ObjectTable)
                {
                    if (obj.ObjectKind != ObjectKind.Pc) continue;
                    if (obj is not IPlayerCharacter pc) continue;
                    if (pc.Name.TextValue == info.Name && pc.HomeWorld.Value.Name.ToString() == info.World)
                    {
                        info.Distance = area.HorizontalDistance(pc.Position);
                        info.IsInRange = area.Contains(pc.Position);
                        break;
                    }
                }
            }
            return _cached;
        }

        var result = new List<NearbyPlayerInfo>();

        foreach (var obj in Plugin.ObjectTable)
        {
            if (obj.ObjectKind != ObjectKind.Pc) continue;
            if (obj is not IPlayerCharacter pc) continue;

            var name = pc.Name.TextValue;
            if (name == localName) continue;

            var world = pc.HomeWorld.Value.Name.ToString();
            var dist = area.HorizontalDistance(pc.Position);

            result.Add(new NearbyPlayerInfo
            {
                Name = name,
                World = world,
                Distance = dist,
                IsInRange = area.Contains(pc.Position),
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

    public static NearbyArea GetArea(Configuration config)
    {
        var local = Plugin.ObjectTable.LocalPlayer;
        var baseCenter = local?.Position ?? Vector3.Zero;

        if (config.NearbyUseFixedPosition && config.NearbyFixedCenterCaptured)
            baseCenter = new Vector3(config.NearbyFixedCenterX, config.NearbyFixedCenterY, config.NearbyFixedCenterZ);
        else
            baseCenter = new Vector3(baseCenter.X + config.NearbyOffsetX, baseCenter.Y, baseCenter.Z + config.NearbyOffsetZ);

        return new NearbyArea(
            baseCenter,
            Math.Clamp(config.NearbyDistanceCap, 2f, 100f),
            config.NearbyShape,
            Math.Clamp(config.NearbyRectangleAspectRatio, 0.1f, 10f),
            DegreesToRadians(config.NearbyRectangleRotation));
    }

    public static void CaptureFixedCenter(Configuration config)
    {
        var local = Plugin.ObjectTable.LocalPlayer;
        if (local == null) return;

        var pos = local.Position;
        config.NearbyFixedCenterX = pos.X + config.NearbyOffsetX;
        config.NearbyFixedCenterY = pos.Y;
        config.NearbyFixedCenterZ = pos.Z + config.NearbyOffsetZ;
        config.NearbyFixedCenterCaptured = true;
    }

    public static bool IsPlayerInRange(string name, string world, Configuration config)
    {
        var obj = FindPlayerObject(name, world);
        return obj != null && GetArea(config).Contains(obj.Position);
    }

    private static IPlayerCharacter? FindPlayerObject(string name, string world)
    {
        foreach (var obj in Plugin.ObjectTable)
        {
            if (obj.ObjectKind != ObjectKind.Pc) continue;
            if (obj is not IPlayerCharacter pc) continue;
            if (pc.Name.TextValue.Equals(name, StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(world) || pc.HomeWorld.Value.Name.ToString().Equals(world, StringComparison.OrdinalIgnoreCase)))
                return pc;
        }
        return null;
    }

    private static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180f;
}
