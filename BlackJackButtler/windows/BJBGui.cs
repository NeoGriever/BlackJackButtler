using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace BlackJackButtler.Windows;

internal static class BJBGui
{
    public static Vector4 ButtonTextColor = new Vector4(1f, 1f, 1f, 1f);
    public static GilVisualMode GilVisual = GilVisualMode.FixedGroup;
    private static readonly Dictionary<string, string> LongInputBuffers = new();

    public static bool MatchesFilter(string filter, params string?[] haystacks)
    {
        if (string.IsNullOrWhiteSpace(filter)) return true;
        if (haystacks == null) return false;
        foreach (var h in haystacks)
        {
            if (!string.IsNullOrEmpty(h) && h.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    public static bool MatchesFilter(string filter, string? name, IEnumerable<string?>? lines)
    {
        if (string.IsNullOrWhiteSpace(filter)) return true;
        if (!string.IsNullOrEmpty(name) && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (lines != null)
        {
            foreach (var l in lines)
                if (!string.IsNullOrEmpty(l) && l.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    public static void DrawFilterBar(string id, ref string filter, string placeholder = "Search...")
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        ImGui.SetNextItemWidth(300f);
        ImGui.InputTextWithHint($"##bjb_filter_{id}", placeholder, ref filter, 128);
        ImGui.PopStyleColor();
        if (!string.IsNullOrEmpty(filter))
        {
            ImGui.SameLine();
            if (ImGui.SmallButton($"X##bjb_filter_clear_{id}")) filter = string.Empty;
        }
    }

    public static bool Button(string label)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.Button(label);
        ImGui.PopStyleColor();
        return r;
    }

    public static bool Button(string label, Vector2 size)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.Button(label, size);
        ImGui.PopStyleColor();
        return r;
    }

    public static bool ButtonHighlighted(string label, Vector4 buttonColor, Vector4 textColor)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, buttonColor);
        ImGui.PushStyleColor(ImGuiCol.Text, textColor);
        var r = ImGui.Button(label);
        ImGui.PopStyleColor(2);
        return r;
    }

    public static bool ButtonHighlighted(string label, Vector2 size, Vector4 buttonColor, Vector4 textColor)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, buttonColor);
        ImGui.PushStyleColor(ImGuiCol.Text, textColor);
        var r = ImGui.Button(label, size);
        ImGui.PopStyleColor(2);
        return r;
    }

    public static bool SmallButton(string label)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.SmallButton(label);
        ImGui.PopStyleColor();
        return r;
    }

    public static bool SmallButtonHighlighted(string label, Vector4 buttonColor, Vector4 textColor)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, buttonColor);
        ImGui.PushStyleColor(ImGuiCol.Text, textColor);
        var r = ImGui.SmallButton(label);
        ImGui.PopStyleColor(2);
        return r;
    }

    public static bool InputInt(string label, ref int v, int step)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.InputInt(label, ref v, step);
        ImGui.PopStyleColor();
        return r;
    }

    public static bool InputLong(string label, ref long v, long step, long step_fast)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.InputLong(label, ref v, step, step_fast);
        ImGui.PopStyleColor();
        return r;
    }

    public static bool InputLongNoButtons(string label, ref long v)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.InputLong(label, ref v, 0, 0);
        ImGui.PopStyleColor();
        return r;
    }

    public static bool InputLongFormatted(string label, ref long value, Vector4? textColor = null)
    {
        var mode = GilVisual;
        if (!LongInputBuffers.TryGetValue(label, out var text))
            text = FormatGil(value, mode);

        ImGui.PushFont(UiBuilder.MonoFont);
        var itemWidth = ImGui.CalcItemWidth();
        if (!LongInputBuffers.ContainsKey(label))
            text = AlignGil(text, itemWidth, mode);
        ImGui.PushStyleColor(ImGuiCol.Text, textColor ?? ButtonTextColor);
        var edited = ImGui.InputText(label, ref text, 64);
        ImGui.PopStyleColor();

        var valueChanged = false;
        if (edited)
        {
            LongInputBuffers[label] = text;
            var normalized = text
                .Replace(",", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Trim();
            if (long.TryParse(
                    normalized,
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out var parsed)
                && parsed != value)
            {
                value = parsed;
                valueChanged = true;
            }
        }

        if (!ImGui.IsItemActive())
            LongInputBuffers[label] = AlignGil(FormatGil(value, mode), itemWidth, mode);

        ImGui.PopFont();

        return valueChanged;
    }

    private static string FormatGil(long value, GilVisualMode mode) => mode switch
    {
        GilVisualMode.Plain => value.ToString(CultureInfo.InvariantCulture),
        GilVisualMode.Grouped => value.ToString("N0", CultureInfo.InvariantCulture),
        _ => FormatFixedGil(value),
    };

    private static string AlignGil(string text, float itemWidth, GilVisualMode mode)
        => mode == GilVisualMode.Plain ? text : PadToRightEdge(text, itemWidth);

    private static string FormatFixedGil(long value)
    {
        var negative = value < 0;
        var absolute = value == long.MinValue ? (ulong)long.MaxValue + 1UL : (ulong)Math.Abs(value);
        if (absolute > 999_999_999_999UL)
            return value.ToString("N0", CultureInfo.InvariantCulture);

        var numericGroups = new ulong[4];
        for (var i = 3; i >= 0; i--)
        {
            numericGroups[i] = absolute % 1000UL;
            absolute /= 1000UL;
        }

        var firstValueGroup = Array.FindIndex(numericGroups, group => group != 0UL);
        if (firstValueGroup < 0)
            firstValueGroup = 3;

        var groups = new string[4];
        for (var i = 0; i < groups.Length; i++)
            groups[i] = i < firstValueGroup
                ? "   "
                : i == firstValueGroup
                    ? numericGroups[i].ToString(CultureInfo.InvariantCulture).PadLeft(3)
                    : numericGroups[i].ToString("D3", CultureInfo.InvariantCulture);

        var formatted = string.Join(',', groups);
        return negative ? $"-{formatted}" : formatted;
    }

    private static string PadToRightEdge(string text, float itemWidth)
    {
        var available = Math.Max(0f, itemWidth - ImGui.GetStyle().FramePadding.X * 2f);
        var missing = available - ImGui.CalcTextSize(text).X;
        if (missing <= 0f)
            return text;

        var spaceWidth = Math.Max(1f, ImGui.CalcTextSize(" ").X);
        return new string(' ', (int)(missing / spaceWidth)) + text;
    }

    public static bool InputFloat(string label, ref float v, float step, float step_fast, string format)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.InputFloat(label, ref v, step, step_fast, format);
        ImGui.PopStyleColor();
        return r;
    }

    public static bool DragInt(string label, ref int v, float speed, int min, int max, string format)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.DragInt(label, ref v, speed, min, max, format);
        ImGui.PopStyleColor();
        return r;
    }

    public static bool SliderFloat(string label, ref float v, float v_min, float v_max, string format)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.SliderFloat(label, ref v, v_min, v_max, format);
        ImGui.PopStyleColor();
        return r;
    }

    public static bool DragFloat(string label, ref float v, float v_speed, float v_min, float v_max, string format)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.DragFloat(label, ref v, v_speed, v_min, v_max, format);
        ImGui.PopStyleColor();
        return r;
    }

    public static bool Combo(string label, ref int current_item, string items_separated_by_zeros)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.Combo(label, ref current_item, items_separated_by_zeros);
        ImGui.PopStyleColor();
        return r;
    }

    public static bool Combo(string label, ref int current_item, string[] items, int items_count)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.Combo(label, ref current_item, items, items_count);
        ImGui.PopStyleColor();
        return r;
    }
}
