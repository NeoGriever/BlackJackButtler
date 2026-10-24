using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Themes;

public sealed class ThemeDefinition
{
    public string Name { get; set; } = "";
    public Dictionary<string, ThemeFont> Fonts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Colors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, ThemeCorners> Corners { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string BackgroundImage { get; set; } = "";
    public string HeaderColor { get; set; } = "";

    public static bool TryColor(string? value, out Vector4 color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var hex = value.Trim().TrimStart('#');
        if (hex.Length != 6 && hex.Length != 8) return false;
        if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed)) return false;
        if (hex.Length == 6) parsed = (parsed << 8) | 255;
        color = new Vector4(((parsed >> 24) & 255) / 255f, ((parsed >> 16) & 255) / 255f, ((parsed >> 8) & 255) / 255f, (parsed & 255) / 255f);
        return true;
    }
}

public sealed class ThemeFont
{
    public string Family { get; set; } = "Default";
    public float Size { get; set; } = 1f;
    public string Color { get; set; } = "";
}

public sealed class ThemeCorners
{
    public float? Rounding { get; set; }
    public bool TopLeft { get; set; } = true;
    public bool TopRight { get; set; } = true;
    public bool BottomLeft { get; set; } = true;
    public bool BottomRight { get; set; } = true;

    public ImDrawFlags Flags => (TopLeft ? ImDrawFlags.RoundCornersTopLeft : 0)
        | (TopRight ? ImDrawFlags.RoundCornersTopRight : 0)
        | (BottomLeft ? ImDrawFlags.RoundCornersBottomLeft : 0)
        | (BottomRight ? ImDrawFlags.RoundCornersBottomRight : 0);
}
