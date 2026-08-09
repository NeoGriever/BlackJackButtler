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
    public static readonly Vector4 OrangeHighlightTextColor = new Vector4(0f, 0f, 0f, 1f);
    public static GilVisualMode GilVisual = GilVisualMode.FixedGroup;
    private sealed class LongInputState
    {
        public bool IsEditing;
        public bool FocusPending;
        public string Text = string.Empty;
    }

    private static readonly Dictionary<string, LongInputState> LongInputStates = new();

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

    public static bool Button(string label, Vector2 size, Vector4 textColor)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, textColor);
        var r = ImGui.Button(label, size);
        ImGui.PopStyleColor();
        return r;
    }

    public static bool Button(string label, Vector4 textColor)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, textColor);
        var r = ImGui.Button(label);
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

    public static bool SmallButton(string label, Vector4 textColor)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, textColor);
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
        var r = BJBStepInput.InputInt(label, ref v, step);
        ImGui.PopStyleColor();
        return r;
    }

    public static bool InputLong(string label, ref long v, long step, long step_fast)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = BJBStepInput.InputLong(label, ref v, step, step_fast);
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
        if (!LongInputStates.TryGetValue(label, out var state))
        {
            state = new LongInputState();
            LongInputStates[label] = state;
        }

        ImGui.PushFont(UiBuilder.MonoFont);
        if (!state.IsEditing)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, textColor ?? ButtonTextColor);
            ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.25f, 0.25f, 0.25f, 0.35f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.35f, 0.35f, 0.35f, 0.45f));
            ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign, new Vector2(1f, 0.5f));
            var clicked = ImGui.Button($"{FormatGil(value, GilVisual)}##gil_label_{label}", new Vector2(ImGui.CalcItemWidth(), 0f));
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(4);
            if (clicked)
            {
                state.Text = value.ToString(CultureInfo.InvariantCulture);
                state.IsEditing = true;
                state.FocusPending = true;
            }
            ImGui.PopFont();
            return false;
        }

        if (state.FocusPending)
        {
            ImGui.SetKeyboardFocusHere();
            state.FocusPending = false;
        }
        ImGui.PushStyleColor(ImGuiCol.Text, textColor ?? ButtonTextColor);
        var submitted = ImGui.InputText(label, ref state.Text, 64,
            ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.PopStyleColor();

        var valueChanged = false;
        if (submitted)
        {
            var normalized = state.Text.Replace(",", string.Empty, StringComparison.Ordinal).Trim();
            if (long.TryParse(normalized, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed)
                && parsed != value)
            {
                value = parsed;
                valueChanged = true;
            }
            state.IsEditing = false;
        }
        else if (ImGui.IsItemDeactivated())
        {
            // Leaving the editor without Enter discards temporary text and restores the label.
            state.IsEditing = false;
        }

        ImGui.PopFont();
        return valueChanged;
    }

    private static string FormatGil(long value, GilVisualMode mode) => mode switch
    {
        GilVisualMode.Plain => value.ToString(CultureInfo.InvariantCulture),
        GilVisualMode.Grouped => value.ToString("N0", CultureInfo.InvariantCulture),
        _ => FormatFixedGil(value),
    };

    private static string FormatFixedGil(long value)
    {
        // Right alignment is supplied by the display button. The persisted/displayed
        // number itself must never be padded with spaces.
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }


    public static bool InputFloat(string label, ref float v, float step, float step_fast, string format)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = BJBStepInput.InputFloat(label, ref v, step, step_fast, format);
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

    public static bool Combo(string label, ref int currentItem, string itemsSeparatedByZeros, float popupWidth)
    {
        // This affects the selection popup only; the closed combo keeps the
        // width assigned by its surrounding layout or table column.
        ImGui.SetNextWindowSizeConstraints(
            new Vector2(popupWidth, 0f),
            new Vector2(popupWidth, float.MaxValue));
        return Combo(label, ref currentItem, itemsSeparatedByZeros);
    }

    public static bool Combo(string label, ref int current_item, string[] items, int items_count)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.Combo(label, ref current_item, items, items_count);
        ImGui.PopStyleColor();
        return r;
    }
}
