using System;
using System.Collections.Generic;
using System.Linq;

namespace BlackJackButtler.Chat;

public sealed class GameSnapshot
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public string Reason { get; init; } = string.Empty;
    public GamePhase Phase { get; init; }
    public PlayerState Dealer { get; init; } = new();
    public List<PlayerState> Players { get; init; } = new();
    public List<DeckCard> ShoeState { get; init; } = new();
}

public static class GameLog
{
    private static readonly object _lock = new();
    private static readonly List<GameSnapshot> _snapshots = new();

    private static int _currentIndex = -1;

    public static int CurrentIndex => _currentIndex;
    public static int SnapshotCount => _snapshots.Count;

    public static void Clear()
    {
        lock (_lock)
        {
            _snapshots.Clear();
            _currentIndex = -1;
        }
    }

    public static void PushSnapshot(List<PlayerState> players, PlayerState dealer, GamePhase phase, string reason)
    {
        lock (_lock)
        {
            if (_currentIndex < _snapshots.Count - 1)
            {
                _snapshots.RemoveRange(_currentIndex + 1, _snapshots.Count - (_currentIndex + 1));
            }

            var snap = new GameSnapshot
            {
                TimestampUtc = DateTime.UtcNow,
                Reason = reason ?? "Manual Sync",
                Phase = phase,
                Dealer = dealer.Clone(),
                ShoeState = DeckManager.GetShoeSnapshot(),
                Players = players.Select(p => p.Clone()).ToList()
            };


            _snapshots.Add(snap);
            if (_snapshots.Count > 100) _snapshots.RemoveAt(0);

            _currentIndex = _snapshots.Count - 1;
        }
    }

    public static GameSnapshot? GetSnapshot(int index)
    {
        lock (_lock)
        {
            if (index < 0 || index >= _snapshots.Count) return null;
            return _snapshots[index];
        }
    }

    public static List<(int Index, GameSnapshot Snapshot)> GetAllSnapshots()
    {
        lock (_lock)
        {
            return _snapshots.Select((s, i) => (i, s)).ToList();
        }
    }

    public static void ApplySnapshot(int index, List<PlayerState> players, ref PlayerState dealer, ref GamePhase phase)
    {
        lock (_lock)
        {
            var snap = GetSnapshot(index);
            if (snap == null) return;

            _currentIndex = index;
            players.Clear();
            foreach (var p in snap.Players) players.Add(p.Clone());
            dealer = snap.Dealer.Clone();
            DeckManager.RestoreShoe(snap.ShoeState);
            phase = snap.Phase;
        }
    }
}
