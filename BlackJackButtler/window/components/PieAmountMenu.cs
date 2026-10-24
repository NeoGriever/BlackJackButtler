using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

internal enum PieAmountOperation { Set, Add, Subtract }

internal readonly record struct PieAmountOption(long Amount, string Label);

internal readonly record struct PieAmountSelection(long Amount, PieAmountOperation Operation)
{
    public long ApplyTo(long current) => Operation switch
    {
        PieAmountOperation.Set => Amount,
        PieAmountOperation.Add => current > long.MaxValue - Amount ? long.MaxValue : current + Amount,
        PieAmountOperation.Subtract => current < long.MinValue + Amount ? long.MinValue : current - Amount,
        _ => current,
    };
}

internal sealed class PieAmountMenu
{
    private const float DotRadius = 4.4f;
    private const float HitRadius = 9f;
    private const float DeadZone = 42f;
    private const float OuterRadius = 118f;
    private string? _activeId;

    public PieAmountSelection? Draw(string id, IReadOnlyList<PieAmountOption> options)
    {
        if (options.Count == 0) return null;
        var center = ImGui.GetCursorScreenPos() + new Vector2(HitRadius);
        ImGui.InvisibleButton($"##pie_amount_{id}", new Vector2(HitRadius * 2f));
        if (ImGui.IsItemActivated()) _activeId = id;
        var dragging = _activeId == id;
        var windowDraw = ImGui.GetWindowDrawList();
        windowDraw.AddCircleFilled(center, DotRadius, ImGui.GetColorU32(new Vector4(1f, 0.55f, 0.08f, 1f)), 24);
        windowDraw.AddCircle(center, DotRadius, ImGui.GetColorU32(Vector4.One), 24);

        var io = ImGui.GetIO();
        var offset = io.MousePos - center;
        var distance = offset.Length();
        var hovered = dragging && distance >= DeadZone && distance <= OuterRadius ? SliceAt(offset, options.Count) : -1;
        var operation = io.KeyShift ? io.KeyCtrl ? PieAmountOperation.Subtract : PieAmountOperation.Add : PieAmountOperation.Set;
        PieAmountSelection? selection = null;
        if (dragging && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            if (hovered >= 0)
                selection = new PieAmountSelection(options[hovered].Amount, operation);
            _activeId = null;
            dragging = false;
        }
        if (dragging && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            _activeId = null;
            dragging = false;
        }

        var draw = ImGui.GetForegroundDrawList();
        if (dragging)
        {
            draw.AddCircleFilled(center, OuterRadius, ImGui.GetColorU32(new Vector4(0.07f, 0.08f, 0.11f, 0.8f)), 96);
            var sliceAngle = MathF.PI * 2f / options.Count;
            for (var index = 0; index < options.Count; index++)
            {
                var middle = -MathF.PI * 0.5f + index * sliceAngle;
                var from = middle - sliceAngle * 0.5f;
                var points = new Vector2[10];
                for (var step = 0; step <= 4; step++)
                {
                    var angle = from + step * sliceAngle / 4f;
                    var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    points[step] = center + direction * DeadZone;
                    points[9 - step] = center + direction * OuterRadius;
                }
                draw.AddConvexPolyFilled(
                    ref points[0],
                    points.Length,
                    ImGui.GetColorU32(
                        index == hovered ? new Vector4(1f, 0.55f, 0.08f, 0.85f) : new Vector4(0.22f, 0.25f, 0.31f, 0.4f)
                    )
                );
                var label = operation switch
                {
                    PieAmountOperation.Add => "+ " + options[index].Label,
                    PieAmountOperation.Subtract => "- " + options[index].Label,
                    _ => options[index].Label,
                };
                var labelCenter = center + new Vector2(MathF.Cos(middle), MathF.Sin(middle)) * 81f;
                draw.AddText(labelCenter - ImGui.CalcTextSize(label) * 0.5f, ImGui.GetColorU32(Vector4.One), label);
            }
            draw.AddCircle(center, DeadZone, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.3f)), 48);
            DrawLegend(draw, center);
            var handle = distance > 0f ? center + offset / distance * Math.Min(distance, OuterRadius) : center;
            var windowMin = ImGui.GetWindowPos();
            draw.PushClipRect(windowMin, windowMin + ImGui.GetWindowSize());
            draw.AddCircleFilled(handle, DotRadius, ImGui.GetColorU32(new Vector4(1f, 0.55f, 0.08f, 1f)), 24);
            draw.AddCircle(handle, DotRadius, ImGui.GetColorU32(Vector4.One), 24);
            draw.PopClipRect();
        }
        return selection;
    }

    private static int SliceAt(Vector2 offset, int optionCount)
    {
        var angle = MathF.Atan2(offset.Y, offset.X) + MathF.PI * 0.5f;
        if (angle < 0f) angle += MathF.PI * 2f;
        return (int)MathF.Floor(
            (angle + MathF.PI / optionCount)
                / (MathF.PI * 2f / optionCount)
        ) % optionCount;
    }

    private static void DrawLegend(ImDrawListPtr draw, Vector2 center)
    {
        const string legend = "Drag: set   SHIFT: +   SHIFT + CTRL: -";
        var textSize = ImGui.CalcTextSize(legend);
        var topLeft = center + new Vector2(-textSize.X * 0.5f - 9f, OuterRadius + 12f);
        var bottomRight = topLeft + textSize + new Vector2(18f, 10f);
        draw.AddRectFilled(topLeft, bottomRight, ImGui.GetColorU32(new Vector4(0.07f, 0.08f, 0.11f, 0.85f)), 4f);
        draw.AddRect(topLeft, bottomRight, ImGui.GetColorU32(new Vector4(1f, 0.65f, 0.25f, 0.8f)), 4f);
        draw.AddText(topLeft + new Vector2(9f, 5f), ImGui.GetColorU32(Vector4.One), legend);
    }
}
