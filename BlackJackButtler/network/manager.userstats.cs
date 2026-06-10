using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace BlackJackButtler;

public sealed record UserStatisticsPlayer(
    string Identity,
    long TradedIn,
    long PaidOut)
{
    public long Result => PaidOut - TradedIn;
}

public sealed record UserStatisticsSession(
    string FilePath,
    DateTime StartedAt,
    DateTime? EndedAt,
    bool IsActive,
    IReadOnlyList<UserStatisticsPlayer> Players);

public static class UserStatisticsManager
{
    private sealed class MutablePlayer
    {
        public string Identity = string.Empty;
        public long TradedIn;
        public long PaidOut;
    }

    private static readonly object Gate = new();
    private static readonly Dictionary<string, MutablePlayer> CurrentPlayers =
        new(StringComparer.OrdinalIgnoreCase);
    private static string _directory = string.Empty;
    private static string? _currentFilePath;
    private static DateTime _startedAt;
    private static DateTime? _endedAt;
    private static bool _isActive;

    public static string? CurrentFilePath
    {
        get { lock (Gate) return _currentFilePath; }
    }

    public static bool IsActive
    {
        get { lock (Gate) return _isActive; }
    }

    public static void Init(string pluginConfigDirectory)
    {
        lock (Gate)
        {
            _directory = Path.Combine(pluginConfigDirectory, "userstats");
            Directory.CreateDirectory(_directory);
        }
    }

    public static void StartSession()
    {
        lock (Gate)
        {
            EnsureInitialized();
            CurrentPlayers.Clear();
            _startedAt = DateTime.Now;
            _endedAt = null;
            _isActive = true;
            _currentFilePath = CreateUniqueFilePath(_startedAt);
            WriteCurrentLocked();
        }
    }

    public static void StopSession()
    {
        lock (Gate)
        {
            if (_currentFilePath == null)
                return;

            _endedAt = DateTime.Now;
            _isActive = false;
            WriteCurrentLocked();
        }
    }

    public static void ResumeSession(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return;

        var session = ReadSession(filePath);
        if (session == null)
            return;

        lock (Gate)
        {
            CurrentPlayers.Clear();
            foreach (var player in session.Players)
            {
                if (!CurrentPlayers.TryGetValue(player.Identity, out var existing))
                {
                    existing = new MutablePlayer { Identity = player.Identity };
                    CurrentPlayers[player.Identity] = existing;
                }

                existing.TradedIn += player.TradedIn;
                existing.PaidOut += player.PaidOut;
            }

            _currentFilePath = filePath;
            _startedAt = session.StartedAt;
            _endedAt = null;
            _isActive = true;
            WriteCurrentLocked();
        }
    }

    public static bool HasSessions()
    {
        lock (Gate)
        {
            EnsureInitialized();
            return Directory.EnumerateFiles(_directory, "*.txt").Any();
        }
    }

    public static void ContinueCurrentOrLatestSession()
    {
        string? filePath;
        lock (Gate)
        {
            EnsureInitialized();
            filePath = !string.IsNullOrWhiteSpace(_currentFilePath) && File.Exists(_currentFilePath)
                ? _currentFilePath
                : Directory.EnumerateFiles(_directory, "*.txt")
                    .OrderByDescending(File.GetLastWriteTime)
                    .FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(filePath))
            StartSession();
        else
            ResumeSession(filePath);
    }

    public static void RecordTrade(PlayerState player, long bankDelta)
    {
        if (bankDelta == 0)
            return;

        lock (Gate)
        {
            if (!_isActive || _currentFilePath == null)
                return;

            var identity = BuildIdentity(player);
            if (!CurrentPlayers.TryGetValue(identity, out var entry))
            {
                entry = new MutablePlayer { Identity = identity };
                CurrentPlayers[identity] = entry;
            }

            if (bankDelta > 0)
                entry.TradedIn += bankDelta;
            else
                entry.PaidOut += Math.Abs(bankDelta);

            WriteCurrentLocked();
        }
    }

    public static IReadOnlyList<UserStatisticsSession> GetSessions()
    {
        lock (Gate)
        {
            EnsureInitialized();
            return Directory.EnumerateFiles(_directory, "*.txt")
                .Select(ReadSession)
                .Where(session => session != null)
                .Cast<UserStatisticsSession>()
                .OrderByDescending(session => session.StartedAt)
                .ToList();
        }
    }

    public static UserStatisticsSession? ReadSession(string filePath)
    {
        try
        {
            var lines = File.ReadAllLines(filePath);
            DateTime startedAt = File.GetCreationTime(filePath);
            DateTime? endedAt = null;
            var active = false;
            var players = new List<UserStatisticsPlayer>();

            string? identity = null;
            long tradedIn = 0;
            long paidOut = 0;

            void FlushPlayer()
            {
                if (!string.IsNullOrWhiteSpace(identity))
                    players.Add(new UserStatisticsPlayer(identity, tradedIn, paidOut));
                identity = null;
                tradedIn = 0;
                paidOut = 0;
            }

            foreach (var line in lines)
            {
                if (line.StartsWith("Started:", StringComparison.OrdinalIgnoreCase))
                {
                    if (DateTime.TryParse(
                            line["Started:".Length..].Trim(),
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeLocal,
                            out var parsedStart))
                        startedAt = parsedStart;
                }
                else if (line.StartsWith("Ended:", StringComparison.OrdinalIgnoreCase))
                {
                    var value = line["Ended:".Length..].Trim();
                    active = value.Equals("Active", StringComparison.OrdinalIgnoreCase);
                    if (!active && DateTime.TryParse(
                            value,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeLocal,
                            out var parsedEnd))
                        endedAt = parsedEnd;
                }
                else if (!line.StartsWith(' ')
                         && line.Contains('@')
                         && !line.StartsWith("BlackJack", StringComparison.OrdinalIgnoreCase))
                {
                    FlushPlayer();
                    identity = line.Trim();
                }
                else if (identity != null && line.TrimStart().StartsWith("Traded in:", StringComparison.OrdinalIgnoreCase))
                {
                    tradedIn = ParseAmount(line);
                }
                else if (identity != null && line.TrimStart().StartsWith("Paid out:", StringComparison.OrdinalIgnoreCase))
                {
                    paidOut = ParseAmount(line);
                }
            }

            FlushPlayer();
            return new UserStatisticsSession(filePath, startedAt, endedAt, active, players);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, $"Failed to read user statistics file: {filePath}");
            return null;
        }
    }

    private static void WriteCurrentLocked()
    {
        if (_currentFilePath == null)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("BlackJack Buttler User Statistics");
        sb.AppendLine($"Started: {_startedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Ended: {(_isActive ? "Active" : _endedAt?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "Unknown")}");
        sb.AppendLine();

        foreach (var player in CurrentPlayers.Values.OrderBy(player => player.Identity, StringComparer.OrdinalIgnoreCase))
        {
            var result = player.PaidOut - player.TradedIn;
            sb.AppendLine(player.Identity);
            sb.AppendLine($"  Traded in: {player.TradedIn,18:N0}");
            sb.AppendLine($"  Paid out:  {player.PaidOut,18:N0}");
            sb.AppendLine($"  Result:    {(result >= 0 ? "+" : "-")} {Math.Abs(result),16:N0}");
            sb.AppendLine();
        }

        var tempPath = _currentFilePath + ".tmp";
        File.WriteAllText(tempPath, sb.ToString());
        File.Move(tempPath, _currentFilePath, true);
    }

    private static string BuildIdentity(PlayerState player)
    {
        var world = VipManager.ResolveWorldName(player.WorldId);
        if (string.IsNullOrWhiteSpace(world))
            world = player.WorldId == 0 ? "Unknown" : player.WorldId.ToString(CultureInfo.InvariantCulture);
        return $"{player.Name}@{world}";
    }

    private static long ParseAmount(string line)
    {
        var digits = System.Text.RegularExpressions.Regex.Replace(line, @"[^\d]", string.Empty);
        return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var amount)
            ? amount
            : 0;
    }

    private static string CreateUniqueFilePath(DateTime timestamp)
    {
        var baseName = timestamp.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
        var path = Path.Combine(_directory, baseName + ".txt");
        var suffix = 1;
        while (File.Exists(path))
            path = Path.Combine(_directory, $"{baseName}_{suffix++}.txt");
        return path;
    }

    private static void EnsureInitialized()
    {
        if (string.IsNullOrWhiteSpace(_directory))
            throw new InvalidOperationException("UserStatisticsManager has not been initialized.");
        Directory.CreateDirectory(_directory);
    }
}
