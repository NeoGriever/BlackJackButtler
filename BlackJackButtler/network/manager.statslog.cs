using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using Newtonsoft.Json;

namespace BlackJackButtler;

public sealed class StatsLogIndex
{
    public int NextNumber { get; set; } = 1;
    public int CurrentNumber { get; set; } = 0;
    public DateTime? LastStoppedAt { get; set; }
}

public static class StatsLogManager
{
    private static readonly TimeSpan ReuseWindow = TimeSpan.FromHours(5);
    private const int DebugLogNumber = 0;
    private static string _logDir = string.Empty;
    private static string _indexPath = string.Empty;
    private static StatsLogIndex _index = new();

    public static string NormalLogPath => _index.CurrentNumber > 0 ? GetLogPath(_index.CurrentNumber) : string.Empty;
    public static string DebugLogPath => GetLogPath(DebugLogNumber);
    public static string CurrentLogPath => Plugin.IsDebugMode ? DebugLogPath : NormalLogPath;
    public static bool HasActiveLog => _index.CurrentNumber > 0;

    public static void Init(string configDir)
    {
        _logDir = Path.Combine(configDir, "statistics_logs");
        _indexPath = Path.Combine(_logDir, "index.json");
        Directory.CreateDirectory(_logDir);
        LoadIndex();
    }

    public static void OnRoundStarted()
    {
        if (!Plugin.IsDebugMode)
            return;

        try { File.WriteAllText(DebugLogPath, string.Empty); }
        catch (Exception ex) { Plugin.Log.Error($"[StatsLog] Debug log reset failed: {ex.Message}"); }
    }

    public static void OnGroupDetectorStarted()
    {
        var now = DateTime.Now;
        if (_index.CurrentNumber > 0
            && _index.LastStoppedAt.HasValue
            && now - _index.LastStoppedAt.Value <= ReuseWindow
            && File.Exists(GetLogPath(_index.CurrentNumber)))
        {
            SaveIndex();
            return;
        }

        var number = Math.Max(1, _index.NextNumber);
        _index.CurrentNumber = number;
        _index.NextNumber = number + 1;
        _index.LastStoppedAt = null;

        var path = GetLogPath(number);
        if (!File.Exists(path))
            File.WriteAllText(path, string.Empty);

        SaveIndex();
    }

    public static void OnGroupDetectorStopped()
    {
        if (_index.CurrentNumber <= 0)
            return;

        _index.LastStoppedAt = DateTime.Now;
        SaveIndex();
    }

    public static void AppendPartyCommand(string commandText)
    {
        if (!TryExtractPartyMessage(commandText, out var message))
            return;

        EnsureLog();
        if (!Plugin.IsDebugMode && _index.CurrentNumber <= 0)
            return;

        File.AppendAllText(CurrentLogPath, $"{FormatTimestamp(DateTime.Now)} {message}{Environment.NewLine}");
    }

    public static void AppendDiceResult(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        EnsureLog();
        if (!Plugin.IsDebugMode && _index.CurrentNumber <= 0)
            return;

        File.AppendAllText(CurrentLogPath, $"{FormatTimestamp(DateTime.Now)} {message.Trim()}{Environment.NewLine}");
    }

    public static void AppendTrade(PlayerState player, long delta)
    {
        if (player == null || delta == 0)
            return;

        EnsureLog();
        if (!Plugin.IsDebugMode && _index.CurrentNumber <= 0)
            return;

        var amount = FormatGil(Math.Abs(delta));
        var arrow = delta > 0 ? "-->" : "<--";
        var timestampPadding = new string(' ', FormatTimestamp(DateTime.Now).Length);
        var lines = $"{FormatTimestamp(DateTime.Now)} [{player.DisplayName}] {amount} {arrow} DEALER{Environment.NewLine}"
            + $"{timestampPadding} [{player.DisplayName}] {FormatGil(player.Bank)}{Environment.NewLine}";
        File.AppendAllText(CurrentLogPath, lines);
    }

    public static IReadOnlyList<string> ReadCurrentLines(bool debugLog)
    {
        try
        {
            var path = debugLog ? DebugLogPath : NormalLogPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return Array.Empty<string>();

            return File.ReadAllLines(path).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public static bool ExportCurrentHtml(bool debugLog)
    {
        try
        {
            var path = debugLog ? DebugLogPath : NormalLogPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            var htmlPath = Path.ChangeExtension(path, ".htm");
            var lines = File.ReadAllLines(path);
            var body = string.Join(Environment.NewLine, lines.Select(l =>
                $"<div class=\"line\">{Colorize(WebUtility.HtmlEncode(l))}</div>"));

            var html = "<!doctype html>\n"
                + "<html><head><meta charset=\"utf-8\"><title>BJB Statistics Log</title>\n"
                + "<style>body{margin:24px;background:#111;color:#ddd;font:14px Consolas,monospace}"
                + ".line{white-space:pre-wrap;line-height:1.45}.ts{color:#8ab4f8}.name{color:#ffd166}.gil{color:#7bd88f}.warn{color:#ff7b72}</style>\n"
                + "</head><body>\n"
                + body
                + "\n</body></html>\n";

            File.WriteAllText(htmlPath, html);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[StatsLog] HTML export failed: {ex.Message}");
            return false;
        }
    }

    public static bool EraseNormalLog()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(NormalLogPath) || !File.Exists(NormalLogPath))
                return false;

            File.Delete(NormalLogPath);
            var html = Path.ChangeExtension(NormalLogPath, ".htm");
            if (File.Exists(html))
                File.Delete(html);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[StatsLog] Erase normal log failed: {ex.Message}");
            return false;
        }
    }

    private static void EnsureLog()
    {
        if (Plugin.IsDebugMode)
        {
            if (!File.Exists(DebugLogPath))
                File.WriteAllText(DebugLogPath, string.Empty);
            return;
        }

        if (_index.CurrentNumber <= 0)
            OnGroupDetectorStarted();
    }

    private static bool TryExtractPartyMessage(string commandText, out string message)
    {
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(commandText))
            return false;

        var trimmed = commandText.TrimStart();
        string[] prefixes = { "/p ", "/party " };
        foreach (var prefix in prefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                message = trimmed[prefix.Length..].Trim();
                return !string.IsNullOrWhiteSpace(message);
            }
        }

        return false;
    }

    private static string Colorize(string encoded)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(encoded, @"^\[[^\]]+\]", m => $"<span class=\"ts\">{m.Value}</span>");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\b\d{1,3}(,\d{3})+\b", m => $"<span class=\"gil\">{m.Value}</span>");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\b(BUST|BLACKJACK|Charlie|PAYOUT|Winner|Lost)\b", m => $"<span class=\"warn\">{m.Value}</span>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return text;
    }

    private static string FormatTimestamp(DateTime time)
        => $"[{time:dd.MM.yyyy - HH:mm:ss}]";

    private static string FormatGil(long value)
        => value.ToString("N0", CultureInfo.GetCultureInfo("de-DE"));

    private static string GetLogPath(int number)
        => Path.Combine(_logDir, $"{number:D8}.log");

    private static void LoadIndex()
    {
        try
        {
            if (File.Exists(_indexPath))
                _index = JsonConvert.DeserializeObject<StatsLogIndex>(File.ReadAllText(_indexPath)) ?? new();
        }
        catch
        {
            _index = new();
        }

        if (_index.NextNumber < 1)
            _index.NextNumber = 1;
    }

    private static void SaveIndex()
    {
        try
        {
            File.WriteAllText(_indexPath, JsonConvert.SerializeObject(_index, Formatting.Indented));
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[StatsLog] Save index failed: {ex.Message}");
        }
    }
}
