using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace BlackJackButtler.Windows;

public enum PathCommandType { MoveTo, LineTo }

public readonly struct PathCommand
{
    public PathCommandType Type { get; }
    public Vector3 Position { get; }

    public PathCommand(PathCommandType type, Vector3 position)
    {
        Type = type;
        Position = position;
    }
}

public sealed class DrawPath
{
    public List<PathCommand> Commands { get; } = new();
    public uint Color { get; set; }
    public bool IsClosed { get; set; }
    public uint? FillColor { get; set; }
}

public sealed class Shape
{
    public Vector3 Origin { get; private set; }
    public List<DrawPath> Paths { get; } = new();
    public List<Shape> Children { get; } = new();
    public float LineThickness { get; set; } = 2f;
    public float RotationAngle { get; set; }

    private float? _rotateTargetX;
    private float? _rotateTargetZ;

    public Shape(Vector3 origin)
    {
        Origin = origin;
    }

    public void Move(float x, float y, float z)
    {
        Origin += new Vector3(x, y, z);
    }

    public void Rotate(float angle)
    {
        RotationAngle += angle;
    }

    public void RotateTowards(float worldX, float worldZ)
    {
        _rotateTargetX = worldX;
        _rotateTargetZ = worldZ;
    }

    public void Draw()
    {
        Draw(Vector3.Zero);
    }

    internal void Draw(Vector3 parentWorldOffset)
    {
        var worldPos = parentWorldOffset + Origin;

        if (_rotateTargetX.HasValue && _rotateTargetZ.HasValue)
        {
            RotationAngle = MathF.Atan2(_rotateTargetX.Value - worldPos.X, _rotateTargetZ.Value - worldPos.Z);
            _rotateTargetX = null;
            _rotateTargetZ = null;
        }

        var angle = RotationAngle;
        var drawList = ImGui.GetBackgroundDrawList();

        foreach (var path in Paths)
            RenderPath(drawList, path, worldPos, angle);

        foreach (var child in Children)
        {
            var rotatedChildOrigin = ApplyRotation(child.Origin, angle);
            child.Draw(worldPos + rotatedChildOrigin);
        }
    }

    private void RenderPath(ImDrawListPtr drawList, DrawPath path, Vector3 worldOffset, float angle)
    {
        if (path.Commands.Count == 0) return;

        var worldPoints = new Vector3[path.Commands.Count];
        var screenPoints = new Vector2[path.Commands.Count];
        var visible = new bool[path.Commands.Count];

        for (int i = 0; i < path.Commands.Count; i++)
        {
            var rotated = ApplyRotation(path.Commands[i].Position, angle);
            worldPoints[i] = worldOffset + rotated;
            visible[i] = Plugin.GameGui.WorldToScreen(worldPoints[i], out screenPoints[i]);
        }

        if (path.FillColor.HasValue && path.IsClosed)
        {
            var allVisible = true;
            for (int i = 0; i < visible.Length; i++)
                if (!visible[i]) { allVisible = false; break; }

            if (allVisible && screenPoints.Length >= 3)
                drawList.AddConvexPolyFilled(ref screenPoints[0], screenPoints.Length, path.FillColor.Value);
        }

        var stroke = new List<Vector2>();
        var segmentCount = path.IsClosed ? path.Commands.Count : path.Commands.Count - 1;

        for (int i = 0; i < segmentCount; i++)
        {
            int j;
            if (path.Commands[i].Type == PathCommandType.MoveTo && i > 0)
            {
                FlushStroke(drawList, stroke, path.Color);
            }

            j = path.IsClosed ? (i + 1) % path.Commands.Count : i + 1;

            if (visible[i] && visible[j])
            {
                if (stroke.Count == 0) stroke.Add(screenPoints[i]);
                stroke.Add(screenPoints[j]);
            }
            else if (!visible[i] && !visible[j])
            {
                FlushStroke(drawList, stroke, path.Color);
            }
            else if (visible[i] && !visible[j])
            {
                if (stroke.Count == 0) stroke.Add(screenPoints[i]);
                var edge = FindEdgePoint(worldPoints[i], worldPoints[j]);
                if (edge.HasValue) stroke.Add(edge.Value);
                FlushStroke(drawList, stroke, path.Color);
            }
            else
            {
                FlushStroke(drawList, stroke, path.Color);
                var edge = FindEdgePoint(worldPoints[j], worldPoints[i]);
                if (edge.HasValue) stroke.Add(edge.Value);
                stroke.Add(screenPoints[j]);
            }
        }

        FlushStroke(drawList, stroke, path.Color);
    }

    private static Vector3 ApplyRotation(Vector3 localPoint, float angle)
    {
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        return new Vector3(
            localPoint.X * cos + localPoint.Z * sin,
            localPoint.Y,
            -localPoint.X * sin + localPoint.Z * cos);
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

    private void FlushStroke(ImDrawListPtr drawList, List<Vector2> stroke, uint color)
    {
        if (stroke.Count >= 2)
        {
            for (int i = 0; i < stroke.Count - 1; i++)
                drawList.AddLine(stroke[i], stroke[i + 1], color, LineThickness);
        }
        stroke.Clear();
    }
}

public sealed class WorldDrawing
{
    private readonly struct ShapeContext
    {
        public Vector3 Origin { get; }
        public List<DrawPath> Paths { get; }
        public List<Shape> Children { get; }
        public float LineThickness { get; }

        public ShapeContext(Vector3 origin, float lineThickness)
        {
            Origin = origin;
            Paths = new List<DrawPath>();
            Children = new List<Shape>();
            LineThickness = lineThickness;
        }
    }

    private readonly Stack<ShapeContext> _shapeStack = new();
    private Vector4 _currentColor = new(1f, 1f, 1f, 1f);
    private Vector4? _currentFillColor;
    private List<PathCommand>? _currentPathCommands;

    public float DefaultLineThickness { get; set; } = 2f;

    public Vector3 GetPlayerRootBonePosition(string playerName)
    {
        foreach (var obj in Plugin.ObjectTable)
        {
            if (obj.ObjectKind != ObjectKind.Pc) continue;
            if (obj is not IPlayerCharacter pc) continue;
            if (pc.Name.TextValue == playerName)
                return pc.Position;
        }
        return Vector3.Zero;
    }

    public Vector3 GetPlayerRootBonePosition(string playerName, string worldName)
    {
        foreach (var obj in Plugin.ObjectTable)
        {
            if (obj.ObjectKind != ObjectKind.Pc) continue;
            if (obj is not IPlayerCharacter pc) continue;
            if (pc.Name.TextValue == playerName && pc.HomeWorld.Value.Name.ToString() == worldName)
                return pc.Position;
        }
        return Vector3.Zero;
    }

    public void BeginShape(float x, float y, float z)
    {
        _shapeStack.Push(new ShapeContext(new Vector3(x, y, z), DefaultLineThickness));
    }

    public void SetDrawColor(float r, float g, float b, float a)
    {
        _currentColor = new Vector4(r, g, b, a);
    }

    public void SetFillColor(float r, float g, float b, float a)
    {
        _currentFillColor = a > 0f ? new Vector4(r, g, b, a) : null;
    }

    public void BeginPath()
    {
        _currentPathCommands = new List<PathCommand>();
    }

    public void MoveTo(float x, float y, float z)
    {
        _currentPathCommands?.Add(new PathCommand(PathCommandType.MoveTo, new Vector3(x, y, z)));
    }

    public void LineTo(float x, float y, float z)
    {
        _currentPathCommands?.Add(new PathCommand(PathCommandType.LineTo, new Vector3(x, y, z)));
    }

    public void EndPath()
    {
        FinalizePath(false);
    }

    public void ClosePath()
    {
        FinalizePath(true);
    }

    public Shape FinishShape()
    {
        if (_shapeStack.Count == 0)
            throw new InvalidOperationException("No shape context on stack. Call BeginShape first.");

        var ctx = _shapeStack.Pop();
        var shape = new Shape(ctx.Origin)
        {
            LineThickness = ctx.LineThickness,
        };
        foreach (var p in ctx.Paths) shape.Paths.Add(p);
        foreach (var c in ctx.Children) shape.Children.Add(c);

        if (_shapeStack.Count > 0)
        {
            var parent = _shapeStack.Peek();
            parent.Children.Add(shape);
        }

        return shape;
    }

    private void FinalizePath(bool closed)
    {
        if (_currentPathCommands == null || _shapeStack.Count == 0) return;

        var path = new DrawPath
        {
            Color = ImGui.GetColorU32(_currentColor),
            IsClosed = closed,
            FillColor = closed && _currentFillColor.HasValue
                ? ImGui.GetColorU32(_currentFillColor.Value)
                : null,
        };
        foreach (var cmd in _currentPathCommands) path.Commands.Add(cmd);

        _shapeStack.Peek().Paths.Add(path);
        _currentPathCommands = null;
    }
}
