#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Jint;
using Newtonsoft.Json;

namespace BlackJackButtler.Windows;

internal sealed class DebugVisualCanvasRenderer : IDisposable
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _workAvailable = new(0);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _worker;
    private readonly Exception? _startupFailure;
    private RenderRequest? _pending;
    private CancellationTokenSource? _activeRenderCancellation;
    private long _requestedVersion;
    private string _lastPayloadJson = string.Empty;
    private CanvasFrame _frontBuffer = CanvasFrame.Empty;

    public DebugVisualCanvasRenderer()
    {
        try
        {
            // Force CLR assembly resolution while the plugin starts. The normal
            // first-use load would also work, but a missing DLL should be
            // reported before the user switches to Visual mode.
            _ = new Engine();
            _worker = Task.Run(WorkerAsync);
        }
        catch (Exception ex)
        {
            _startupFailure = ex;
            Status = $"Visual runtime unavailable: {ex.Message}";
            _worker = Task.CompletedTask;
        }
    }

    public string Status { get; private set; } = "Waiting for a visual frame.";

    public void Request(DebugVisualPayload payload)
    {
        if (_startupFailure != null)
            return;

        var payloadJson = JsonConvert.SerializeObject(payload);
        lock (_gate)
        {
            if (payloadJson == _lastPayloadJson)
                return;

            _lastPayloadJson = payloadJson;
            var request = new RenderRequest(++_requestedVersion, payload, payloadJson);
            _pending = request;
            _activeRenderCancellation?.Cancel();
            _workAvailable.Release();
        }
    }

    public void Draw(Vector2 origin, Vector2 available)
    {
        var frame = Volatile.Read(ref _frontBuffer);
        if (frame.Commands.Count == 0)
        {
            ImGui.Dummy(new Vector2(Math.Max(available.X, 1f), Math.Max(available.Y, 1f)));
            return;
        }

        var scaleX = available.X / Math.Max(frame.Width, 1f);
        var scaleY = available.Y / Math.Max(frame.Height, 1f);
        var drawList = ImGui.GetWindowDrawList();
        foreach (var command in frame.Commands)
        {
            var position = origin + new Vector2(command.X * scaleX, command.Y * scaleY);
            var size = new Vector2(command.Width * scaleX, command.Height * scaleY);
            switch (command.Kind)
            {
                case CanvasCommandKind.ClearRect:
                    drawList.AddRectFilled(position, position + size, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0f)));
                    break;
                case CanvasCommandKind.FillRect:
                    drawList.AddRectFilled(position, position + size, ImGui.GetColorU32(command.Color));
                    break;
                case CanvasCommandKind.StrokeRect:
                    drawList.AddRect(position, position + size, ImGui.GetColorU32(command.Color), 0f, ImDrawFlags.None,
                        Math.Max(command.LineWidth * Math.Min(scaleX, scaleY), 1f));
                    break;
                case CanvasCommandKind.FillText:
                    drawList.AddText(position, ImGui.GetColorU32(command.Color), command.Text ?? string.Empty);
                    break;
            }
        }

        ImGui.Dummy(new Vector2(Math.Max(available.X, 1f), Math.Max(available.Y, 1f)));
    }

    private async Task WorkerAsync()
    {
        try
        {
            while (true)
            {
                await _workAvailable.WaitAsync(_shutdown.Token).ConfigureAwait(false);
                RenderRequest? request;
                CancellationTokenSource? renderCancellation;
                lock (_gate)
                {
                    request = _pending;
                    _pending = null;
                    if (request == null) continue;
                    renderCancellation = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
                    _activeRenderCancellation = renderCancellation;
                }

                CanvasFrame? frame = null;
                string status;
                try
                {
                    frame = Execute(request, renderCancellation.Token);
                    status = frame == null ? "render() returned false." : "Visual frame is current.";
                }
                catch (OperationCanceledException)
                {
                    status = "Visual frame superseded.";
                }
                catch (Exception ex)
                {
                    status = $"Visual script error: {ex.Message}";
                }
                lock (_gate)
                {
                    if (ReferenceEquals(_activeRenderCancellation, renderCancellation))
                        _activeRenderCancellation = null;

                    if (frame != null && request.Version == _requestedVersion)
                        Volatile.Write(ref _frontBuffer, frame);
                    Status = status;
                }
                renderCancellation.Dispose();
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
    }

    private static CanvasFrame? Execute(RenderRequest request, CancellationToken cancellationToken)
    {
        var assemblyDirectory = Path.GetDirectoryName(typeof(DebugVisualCanvasRenderer).Assembly.Location)
            ?? AppContext.BaseDirectory;
        var scriptPath = Path.Combine(assemblyDirectory, "visual-canvas.js");
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException("Debug visual script was not found.", scriptPath);

        var source = File.ReadAllText(scriptPath);
        var canvas = new CanvasRecordingContext(request.Payload.Canvas.Width, request.Payload.Canvas.Height, cancellationToken);
        var engine = new Engine(options => options
            .LimitRecursion(64)
            .MaxStatements(100_000)
            .TimeoutInterval(TimeSpan.FromMilliseconds(25))
            .CancellationToken(cancellationToken));

        engine.SetValue("canvas", canvas);
        engine.SetValue("__bjbPayloadJson", request.PayloadJson);
        engine.Execute(source, scriptPath);
        var accepted = engine.Evaluate("Boolean(render(JSON.parse(__bjbPayloadJson), canvas))").AsBoolean();
        cancellationToken.ThrowIfCancellationRequested();
        return accepted ? canvas.Freeze() : null;
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        lock (_gate) _activeRenderCancellation?.Cancel();
        _workAvailable.Release();
        try { _worker.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _workAvailable.Dispose();
        _shutdown.Dispose();
    }

    private sealed record RenderRequest(long Version, DebugVisualPayload Payload, string PayloadJson);
}

internal sealed class CanvasRecordingContext
{
    private readonly List<CanvasCommand> _commands = new();
    private readonly CancellationToken _cancellationToken;

    public CanvasRecordingContext(float width, float height, CancellationToken cancellationToken)
    {
        canvasWidth = width;
        canvasHeight = height;
        _cancellationToken = cancellationToken;
    }

    public float canvasWidth { get; }
    public float canvasHeight { get; }
    public string fillStyle { get; set; } = "#ffffff";
    public string strokeStyle { get; set; } = "#ffffff";
    public double lineWidth { get; set; } = 1d;
    public string font { get; set; } = "16px sans-serif";

    public void clearRect(double x, double y, double width, double height) => Add(CanvasCommandKind.ClearRect, x, y, width, height, Vector4.Zero);
    public void fillRect(double x, double y, double width, double height) => Add(CanvasCommandKind.FillRect, x, y, width, height, ParseColor(fillStyle));
    public void strokeRect(double x, double y, double width, double height) => Add(CanvasCommandKind.StrokeRect, x, y, width, height, ParseColor(strokeStyle));
    public void fillText(string text, double x, double y) => Add(CanvasCommandKind.FillText, x, y, 0d, 0d, ParseColor(fillStyle), text);

    public CanvasFrame Freeze() => new(canvasWidth, canvasHeight, _commands.ToArray());

    private void Add(CanvasCommandKind kind, double x, double y, double width, double height, Vector4 color, string? text = null)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        _commands.Add(new CanvasCommand(kind, (float)x, (float)y, (float)width, (float)height, color, (float)lineWidth, text));
    }

    private static Vector4 ParseColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Vector4.One;
        var text = value.Trim();
        if (text.StartsWith('#'))
        {
            var hex = text[1..];
            if (hex.Length is 3 or 4)
                hex = string.Concat(hex.Select(c => new string(c, 2)));
            if (hex.Length == 6 || hex.Length == 8)
            {
                if (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed))
                {
                    var alpha = hex.Length == 8 ? (parsed & 0xff) / 255f : 1f;
                    var shift = hex.Length == 8 ? 8 : 0;
                    return new Vector4(((parsed >> (16 + shift)) & 0xff) / 255f, ((parsed >> (8 + shift)) & 0xff) / 255f,
                        ((parsed >> shift) & 0xff) / 255f, alpha);
                }
            }
        }

        var open = text.IndexOf('(');
        var close = text.LastIndexOf(')');
        if (open > 0 && close > open)
        {
            var parts = text[(open + 1)..close].Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length is 3 or 4 && float.TryParse(parts[0], CultureInfo.InvariantCulture, out var r)
                && float.TryParse(parts[1], CultureInfo.InvariantCulture, out var g)
                && float.TryParse(parts[2], CultureInfo.InvariantCulture, out var b))
            {
                var a = 1f;
                if (parts.Length == 4 && !float.TryParse(parts[3], CultureInfo.InvariantCulture, out a)) return Vector4.One;
                return new Vector4(r / 255f, g / 255f, b / 255f, a);
            }
        }

        return Vector4.One;
    }
}

internal enum CanvasCommandKind { ClearRect, FillRect, StrokeRect, FillText }
internal sealed record CanvasCommand(CanvasCommandKind Kind, float X, float Y, float Width, float Height, Vector4 Color, float LineWidth, string? Text);
internal sealed record CanvasFrame(float Width, float Height, IReadOnlyList<CanvasCommand> Commands)
{
    public static CanvasFrame Empty { get; } = new(1f, 1f, Array.Empty<CanvasCommand>());
}

internal sealed record DebugVisualCanvasSize(float Width, float Height);
internal sealed record DebugVisualCard(int Value, string Label, string Suit, string Symbol);
internal sealed record DebugVisualHand(int Index, long Bet, int Points, bool Stand, bool Bust, bool Blackjack, bool Charlie, bool DoubleDown, bool TripleDown, IReadOnlyList<DebugVisualCard> Cards);
internal sealed record DebugVisualPlayer(string Id, string Name, string Alias, string DisplayName, uint WorldId, bool Vip, bool Active, bool OnHold, bool OnBench, bool CurrentTurn, bool Imaginary, long Bank, long Bet, int CurrentHandIndex, IReadOnlyList<DebugVisualHand> Hands);
internal sealed record DebugVisualPayload(DebugVisualCanvasSize Canvas, string Phase, bool RecognitionActive, DebugVisualPlayer Dealer, IReadOnlyList<DebugVisualPlayer> Players);
#endif
