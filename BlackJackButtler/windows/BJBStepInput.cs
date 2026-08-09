using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

/// <summary>
/// Central number input with joined decrement/increment controls.  Keeping the
/// buttons here makes every stepped numeric input use the same compact,
/// borderless inner edge instead of ImGui's separately rounded +/- buttons.
/// </summary>
internal static class BJBStepInput
{
    private const float PreferredStepButtonWidth = 20f;
    private const float MinimumStepButtonWidth = 14f;
    private const float MinimumInputWidth = 20f;

    public static bool InputInt(string label, ref int value, int step)
    {
        var totalWidth = GetTotalWidth();
        var stepButtonWidth = GetStepButtonWidth(totalWidth);
        var inputWidth = Math.Max(MinimumInputWidth, totalWidth - (stepButtonWidth * 2f));

        var adjustment = DrawStepButton(label, true, stepButtonWidth);
        ImGui.SameLine(0f, 0f);
        BeginInputField(inputWidth);
        var changed = ImGui.InputInt(label, ref value, 0, 0);
        EndInputField();
        ImGui.SameLine(0f, 0f);
        adjustment += DrawStepButton(label, false, stepButtonWidth);
        if (adjustment == 0) return changed;

        value += adjustment * step;
        return true;
    }

    public static bool InputLong(string label, ref long value, long step, long stepFast)
    {
        var totalWidth = GetTotalWidth();
        var stepButtonWidth = GetStepButtonWidth(totalWidth);
        var inputWidth = Math.Max(MinimumInputWidth, totalWidth - (stepButtonWidth * 2f));

        var adjustment = DrawStepButton(label, true, stepButtonWidth);
        ImGui.SameLine(0f, 0f);
        BeginInputField(inputWidth);
        var changed = ImGui.InputLong(label, ref value, 0, 0);
        EndInputField();
        ImGui.SameLine(0f, 0f);
        adjustment += DrawStepButton(label, false, stepButtonWidth);
        if (adjustment == 0) return changed;

        var amount = ImGui.GetIO().KeyCtrl ? stepFast : step;
        value += adjustment * amount;
        return true;
    }

    public static bool InputFloat(string label, ref float value, float step, float stepFast, string format)
    {
        var totalWidth = GetTotalWidth();
        var stepButtonWidth = GetStepButtonWidth(totalWidth);
        var inputWidth = Math.Max(MinimumInputWidth, totalWidth - (stepButtonWidth * 2f));

        var adjustment = DrawStepButton(label, true, stepButtonWidth);
        ImGui.SameLine(0f, 0f);
        BeginInputField(inputWidth);
        var changed = ImGui.InputFloat(label, ref value, 0f, 0f, format);
        EndInputField();
        ImGui.SameLine(0f, 0f);
        adjustment += DrawStepButton(label, false, stepButtonWidth);
        if (adjustment == 0) return changed;

        var amount = ImGui.GetIO().KeyCtrl ? stepFast : step;
        value += adjustment * amount;
        return true;
    }

    private static float GetTotalWidth() => Math.Max(
        MinimumInputWidth + (MinimumStepButtonWidth * 2f),
        ImGui.CalcItemWidth());

    private static float GetStepButtonWidth(float totalWidth)
    {
        // Keep enough space for a readable number even in narrow table cells.
        return Math.Max(MinimumStepButtonWidth,
            Math.Min(PreferredStepButtonWidth, (totalWidth - MinimumInputWidth) * 0.5f));
    }

    private static void BeginInputField(float width)
    {
        // The center must meet both buttons as a square, borderless segment.
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
        ImGui.SetNextItemWidth(width);
    }

    private static void EndInputField()
    {
        ImGui.PopStyleVar(2);
    }

    private static int DrawStepButton(string inputLabel, bool decrement, float width)
    {
        var style = ImGui.GetStyle();
        var size = new Vector2(width, ImGui.GetFrameHeight());
        var position = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(
            decrement ? $"##bjb_step_minus_{inputLabel}" : $"##bjb_step_plus_{inputLabel}", size);
        var hovered = ImGui.IsItemHovered();
        var active = ImGui.IsItemActive();

        var drawList = ImGui.GetWindowDrawList();
        var rounding = Math.Min(Math.Max(4f, style.FrameRounding), Math.Min(size.X, size.Y) * 0.5f);
        DrawHalf(drawList, position, size, decrement, GetButtonColor(hovered, active, style), rounding);
        DrawCenteredText(drawList, position, size, decrement ? "-" : "+");
        return clicked ? decrement ? -1 : 1 : 0;
    }

    private static Vector4 GetButtonColor(bool hovered, bool active, ImGuiStylePtr style) =>
        style.Colors[(int)(active ? ImGuiCol.ButtonActive : hovered ? ImGuiCol.ButtonHovered : ImGuiCol.Button)];

    private static void DrawHalf(ImDrawListPtr drawList, Vector2 position, Vector2 size, bool leftHalf,
        Vector4 color, float rounding)
    {
        var max = position + size;
        var colorU32 = ImGui.GetColorU32(color);
        drawList.AddRectFilled(position, max, colorU32, rounding);

        // Square only the joined edge, retaining the outer corners of the pair.
        if (rounding <= 0f) return;
        var squareMin = leftHalf ? new Vector2(max.X - rounding, position.Y) : position;
        var squareMax = leftHalf ? max : new Vector2(position.X + rounding, max.Y);
        drawList.AddRectFilled(squareMin, squareMax, colorU32);
    }

    private static void DrawCenteredText(ImDrawListPtr drawList, Vector2 position, Vector2 size, string label)
    {
        var textSize = ImGui.CalcTextSize(label);
        drawList.AddText(position + (size - textSize) * 0.5f, ImGui.GetColorU32(BJBGui.ButtonTextColor), label);
    }
}
