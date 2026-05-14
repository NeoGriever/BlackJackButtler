using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler;

public static class NearbyNumberManager
{
    private static readonly Dictionary<string, int> Numbers = new(StringComparer.OrdinalIgnoreCase);

    public static int GetNumber(string fullKey)
    {
        if (Numbers.TryGetValue(fullKey, out int existing))
            return existing;

        int next = 1;
        var used = new HashSet<int>(Numbers.Values);
        while (used.Contains(next)) next++;
        Numbers[fullKey] = next;
        return next;
    }

    public static List<NearbyPlayerInfo> ApplyAndSort(IEnumerable<NearbyPlayerInfo> players)
    {
        var list = players.ToList();
        var current = list.Select(p => p.FullKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var key in Numbers.Keys.ToList())
            if (!current.Contains(key))
                Numbers.Remove(key);

        foreach (var p in list)
            GetNumber(p.FullKey);

        list.Sort((a, b) =>
        {
            int cmp = GetNumber(a.FullKey).CompareTo(GetNumber(b.FullKey));
            if (cmp != 0) return cmp;
            return string.Compare(a.FullKey, b.FullKey, StringComparison.OrdinalIgnoreCase);
        });
        return list;
    }

    public static void DrawFootNumbers(IEnumerable<NearbyPlayerInfo> players)
    {
        var drawList = ImGui.GetBackgroundDrawList();

        foreach (var p in players)
        {
            if (p.Distance >= float.MaxValue) continue;
            if (!TryFindPlayerPosition(p.Name, p.World, out var pos)) continue;
            pos.Y += 0.1f;
            if (!Plugin.GameGui.WorldToScreen(pos, out var screen)) continue;

            string label = GetNumber(p.FullKey).ToString();
            var textSize = ImGui.CalcTextSize(label);
            var padding = new Vector2(5f, 2f);
            var min = new Vector2(screen.X - textSize.X * 0.5f - padding.X, screen.Y - textSize.Y * 0.5f - padding.Y);
            var max = new Vector2(screen.X + textSize.X * 0.5f + padding.X, screen.Y + textSize.Y * 0.5f + padding.Y);

            drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.72f)), 4f);
            drawList.AddRect(min, max, ImGui.GetColorU32(new Vector4(1f, 0.85f, 0.25f, 0.95f)), 4f, ImDrawFlags.None, 1.5f);
            drawList.AddText(new Vector2(screen.X - textSize.X * 0.5f, screen.Y - textSize.Y * 0.5f),
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), label);
        }
    }

    private static bool TryFindPlayerPosition(string name, string world, out Vector3 position)
    {
        foreach (var obj in Plugin.ObjectTable)
        {
            if (obj.ObjectKind != ObjectKind.Pc) continue;
            if (obj is not IPlayerCharacter pc) continue;
            if (pc.Name.TextValue == name && pc.HomeWorld.Value.Name.ToString() == world)
            {
                position = pc.Position;
                return true;
            }
        }

        position = Vector3.Zero;
        return false;
    }
}
