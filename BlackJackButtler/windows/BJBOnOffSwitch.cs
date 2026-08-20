using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

/// <summary>
/// Shared two-state control for every On/Off setting. It intentionally draws
/// the two halves itself: ImGui's regular button API only supports symmetric
/// corner rounding, while this control needs a joined left/right capsule.
/// </summary>
internal static class BJBOnOffSwitch
{
    private static readonly Vector4 Selected = new(1f, 0.5f, 0f, 1f);
    private static readonly Vector4 SelectedHovered = new(1f, 0.6f, 0.1f, 1f);
    private static readonly Vector4 SelectedActive = new(0.9f, 0.4f, 0f, 1f);

    public static bool Draw(string id, ref bool value, float halfWidth = 54f)
        => Draw(id, ref value, "On", "Off", halfWidth);

    public static bool Draw(string id, ref bool value, string trueLabel, string falseLabel, float halfWidth = 54f)
    {
        var selected = value;
        var style = ImGui.GetStyle();
        var size = new Vector2(Math.Max(1f, halfWidth), ImGui.GetFrameHeight());

        var onPosition = ImGui.GetCursorScreenPos();
        var selectOn = ImGui.InvisibleButton($"##{id}_true", size);
        var onHovered = ImGui.IsItemHovered();
        var onActive = ImGui.IsItemActive();

        // Zero spacing joins the halves. No border is drawn along this inner edge.
        ImGui.SameLine(0f, 0f);
        var offPosition = ImGui.GetCursorScreenPos();
        var selectOff = ImGui.InvisibleButton($"##{id}_false", size);
        var offHovered = ImGui.IsItemHovered();
        var offActive = ImGui.IsItemActive();

        var drawList = ImGui.GetWindowDrawList();
        var rounding = Math.Min(Math.Max(4f, style.FrameRounding), Math.Min(size.X, size.Y) * 0.5f);
        DrawHalf(drawList, onPosition, size, true, GetColor(selected, onHovered, onActive, style), rounding);
        DrawHalf(drawList, offPosition, size, false, GetColor(!selected, offHovered, offActive, style), rounding);
        DrawCenteredText(drawList, onPosition, size, trueLabel, selected);
        DrawCenteredText(drawList, offPosition, size, falseLabel, !selected);

        var next = selectOn ? true : selectOff ? false : selected;
        if (next == selected) return false;
        value = next;
        return true;
    }

    private static Vector4 GetColor(bool isSelected, bool hovered, bool active, ImGuiStylePtr style)
    {
        if (isSelected)
            return active ? SelectedActive : hovered ? SelectedHovered : Selected;

        return style.Colors[(int)(active ? ImGuiCol.ButtonActive : hovered ? ImGuiCol.ButtonHovered : ImGuiCol.Button)];
    }

    private static void DrawHalf(ImDrawListPtr drawList, Vector2 position, Vector2 size, bool leftHalf,
        Vector4 color, float rounding)
    {
        var max = position + size;
        var colorU32 = ImGui.GetColorU32(color);
        drawList.AddRectFilled(position, max, colorU32, rounding);

        // Fill the inner rounded corners again, leaving only the outer corners rounded.
        if (rounding <= 0f) return;
        var squareMin = leftHalf
            ? new Vector2(max.X - rounding, position.Y)
            : position;
        var squareMax = leftHalf
            ? max
            : new Vector2(position.X + rounding, max.Y);
        drawList.AddRectFilled(squareMin, squareMax, colorU32);
    }

    private static void DrawCenteredText(ImDrawListPtr drawList, Vector2 position, Vector2 size, string label,
        bool isSelected)
    {
        var textSize = ImGui.CalcTextSize(label);
        var textPosition = position + (size - textSize) * 0.5f;
        var textColor = isSelected ? new Vector4(0f, 0f, 0f, 1f) : BJBGui.ButtonTextColor;
        drawList.AddText(textPosition, ImGui.GetColorU32(textColor), label);
    }
}
