using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using BlackJackButtler.Chat;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawDistanceCircle()
    {
        if (!_distSliderHovered && !_config.NearbyAlwaysShowCircle) return;
        var local = Plugin.ObjectTable.LocalPlayer;
        if (local == null) return;

        var area = NearbyPlayersManager.GetArea(_config);
        if (_config.NearbyShape == NearbyShapeMode.Rectangle)
        {
            DrawDistanceRectangle(area);
            return;
        }

        var center = area.Center;
        float radius = area.Radius;
        const int segments = 64;

        var worldPoints = new Vector3[segments];
        var screenPoints = new Vector2[segments];
        var visible = new bool[segments];

        for (int i = 0; i < segments; i++)
        {
            float angle = 2f * MathF.PI * i / segments;
            worldPoints[i] = new Vector3(
                center.X + radius * MathF.Cos(angle),
                center.Y,
                center.Z + radius * MathF.Sin(angle)
            );
            visible[i] = Plugin.GameGui.WorldToScreen(worldPoints[i], out screenPoints[i]);
        }

        var drawList = ImGui.GetBackgroundDrawList();
        var color = ImGui.GetColorU32(new Vector4(1f, 0.85f, 0f, 0.5f));

        var stroke = new List<Vector2>();

        for (int i = 0; i < segments; i++)
        {
            int j = (i + 1) % segments;

            if (visible[i] && visible[j])
            {
                if (stroke.Count == 0) stroke.Add(screenPoints[i]);
                stroke.Add(screenPoints[j]);
            }
            else if (!visible[i] && !visible[j])
            {
                FlushStroke(drawList, stroke, color);
            }
            else if (visible[i] && !visible[j])
            {
                if (stroke.Count == 0) stroke.Add(screenPoints[i]);
                var edge = FindEdgePoint(worldPoints[i], worldPoints[j]);
                if (edge.HasValue) stroke.Add(edge.Value);
                FlushStroke(drawList, stroke, color);
            }
            else
            {
                FlushStroke(drawList, stroke, color);
                var edge = FindEdgePoint(worldPoints[j], worldPoints[i]);
                if (edge.HasValue) stroke.Add(edge.Value);
                stroke.Add(screenPoints[j]);
            }
        }

        FlushStroke(drawList, stroke, color);
    }

    private void DrawDistanceRectangle(NearbyArea area)
    {
        var halfX = area.Radius * Math.Clamp(area.AspectRatio, 0.1f, 10f);
        var halfZ = area.Radius;
        var cos = MathF.Cos(area.RotationRadians);
        var sin = MathF.Sin(area.RotationRadians);
        var localCorners = new[]
        {
            new Vector2(-halfX, -halfZ),
            new Vector2( halfX, -halfZ),
            new Vector2( halfX,  halfZ),
            new Vector2(-halfX,  halfZ),
        };

        var worldCorners = new Vector3[4];
        for (int i = 0; i < localCorners.Length; i++)
        {
            var c = localCorners[i];
            var x = c.X * cos - c.Y * sin;
            var z = c.X * sin + c.Y * cos;
            worldCorners[i] = new Vector3(area.Center.X + x, area.Center.Y, area.Center.Z + z);
        }

        var drawList = ImGui.GetBackgroundDrawList();
        var color = ImGui.GetColorU32(new Vector4(1f, 0.85f, 0f, 0.5f));
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4;
            DrawClippedWorldSegment(drawList, worldCorners[i], worldCorners[j], color);
        }
    }

    private static void DrawClippedWorldSegment(ImDrawListPtr drawList, Vector3 start, Vector3 end, uint color)
    {
        var sampleCount = Math.Clamp((int)MathF.Ceiling(Vector3.Distance(start, end) / 2f), 16, 256);
        var previousWorld = start;
        var previousVisible = Plugin.GameGui.WorldToScreen(previousWorld, out var previousScreen);

        for (var sample = 1; sample <= sampleCount; sample++)
        {
            var currentWorld = Vector3.Lerp(start, end, sample / (float)sampleCount);
            var currentVisible = Plugin.GameGui.WorldToScreen(currentWorld, out var currentScreen);

            if (previousVisible && currentVisible)
            {
                drawList.AddLine(previousScreen, currentScreen, color, 2f);
            }
            else if (previousVisible)
            {
                var edge = FindEdgePoint(previousWorld, currentWorld);
                if (edge.HasValue) drawList.AddLine(previousScreen, edge.Value, color, 2f);
            }
            else if (currentVisible)
            {
                var edge = FindEdgePoint(currentWorld, previousWorld);
                if (edge.HasValue) drawList.AddLine(edge.Value, currentScreen, color, 2f);
            }

            previousWorld = currentWorld;
            previousScreen = currentScreen;
            previousVisible = currentVisible;
        }
    }

    private static Vector2? FindEdgePoint(Vector3 visibleWorld, Vector3 invisibleWorld)
    {
        var a = visibleWorld;
        var b = invisibleWorld;
        for (int k = 0; k < 6; k++)
        {
            var mid = (a + b) * 0.5f;
            if (Plugin.GameGui.WorldToScreen(mid, out _))
                a = mid;
            else
                b = mid;
        }
        return Plugin.GameGui.WorldToScreen(a, out var result) ? result : null;
    }

    private static void FlushStroke(ImDrawListPtr drawList, List<Vector2> stroke, uint color)
    {
        if (stroke.Count >= 2)
        {
            for (int i = 0; i < stroke.Count - 1; i++)
                drawList.AddLine(stroke[i], stroke[i + 1], color, 2f);
        }
        stroke.Clear();
    }
}
