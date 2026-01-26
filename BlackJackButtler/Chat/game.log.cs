using System;
using System.Collections.Generic;
using System.Linq;

namespace BlackJackButtler.Chat;

public sealed class GameLogEntry
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public string Reason { get; init; } = string.Empty;
    public GamePhase Phase { get; init; }
    public int SnapshotIndex { get; init; }
}

public sealed class GameSnapshot
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public string Reason { get; init; } = string.Empty;
    public GamePhase Phase { get; init; }

    public PlayerState Dealer { get; init; } = new();
    public List<PlayerState> Players { get; init; } = new();
}

public static class GameLog
{
    private static readonly object _lock = new();

    private static readonly List<GameSnapshot> _snapshots = new();
    private static readonly List<GameLogEntry> _entries = new();

    public static int SnapshotCount
    {
        get { lock (_lock) return _snapshots.Count; }
    }

    public static IReadOnlyList<GameLogEntry> Entries
    {
        get { lock (_lock) return _entries.ToList(); }
    }

    public static void Clear()
    {
        lock (_lock)
        {
            _snapshots.Clear();
            _entries.Clear();
        }
    }

    public static void PushSnapshot(List<PlayerState> players, PlayerState dealer, GamePhase phase, string reason, int maxSnapshots = 25)
    {
        if (players == null) return;
        if (dealer == null) return;

        lock (_lock)
        {
            var snap = new GameSnapshot
            {
                TimestampUtc = DateTime.UtcNow,
                Reason = reason ?? string.Empty,
                Phase = phase,
                Dealer = dealer.Clone(),
                Players = players.Select(p => p.Clone()).ToList()
            };

            _snapshots.Add(snap);
            if (_snapshots.Count > maxSnapshots)
                _snapshots.RemoveAt(0);

            _entries.Insert(0, new GameLogEntry
            {
                TimestampUtc = snap.TimestampUtc,
                Reason = snap.Reason,
                Phase = snap.Phase,
                SnapshotIndex = _snapshots.Count - 1
            });

            if (_entries.Count > 200)
                _entries.RemoveAt(200);
        }
    }

    public static bool TryPopSnapshot(out GameSnapshot snapshot)
    {
        lock (_lock)
        {
            if (_snapshots.Count == 0)
            {
                snapshot = null!;
                return false;
            }

            var idx = _snapshots.Count - 1;
            snapshot = _snapshots[idx];
            _snapshots.RemoveAt(idx);
            return true;
        }
    }

    public static void UndoLast(List<PlayerState> players, ref PlayerState dealer, ref GamePhase phase)
    {
        lock (_lock)
        {
            if (_snapshots.Count == 0) return;

            var lastSnap = _snapshots.Last();
            _snapshots.RemoveAt(_snapshots.Count - 1);
            if (_entries.Count > 0) _entries.RemoveAt(0);

            players.Clear();
            foreach (var p in lastSnap.Players) players.Add(p.Clone());
            dealer = lastSnap.Dealer.Clone();
            phase = lastSnap.Phase;
        }
    }
}
