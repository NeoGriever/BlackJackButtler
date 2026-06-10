using System;
using System.Collections.Generic;
using System.Linq;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static class RoundRollbackManager
{
    private sealed record RoundSnapshot(
        GamePhase Phase,
        PlayerState Dealer,
        List<PlayerState> Players,
        List<DeckCard> Shoe);

    private static readonly object Gate = new();
    private static RoundSnapshot? _snapshot;

    public static bool HasSnapshot
    {
        get { lock (Gate) return _snapshot != null; }
    }

    public static void Capture(List<PlayerState> players, PlayerState dealer, GamePhase phase)
    {
        lock (Gate)
        {
            _snapshot = new RoundSnapshot(
                phase,
                dealer.Clone(),
                players.Select(p => p.Clone()).ToList(),
                DeckManager.GetShoeSnapshot());
        }
    }

    public static bool Restore(List<PlayerState> players, PlayerState dealer, out GamePhase phase)
    {
        RoundSnapshot? snapshot;
        lock (Gate)
        {
            snapshot = _snapshot;
            _snapshot = null;
        }

        if (snapshot == null)
        {
            phase = GamePhase.Waiting;
            return false;
        }

        foreach (var saved in snapshot.Players)
        {
            var current = players.FirstOrDefault(p => SameIdentity(p, saved));
            if (current == null)
            {
                current = saved.Clone();
                players.Add(current);
            }
            RestoreRoundState(current, saved);
        }

        RestoreRoundState(dealer, snapshot.Dealer);
        DeckManager.RestoreShoe(snapshot.Shoe);
        phase = snapshot.Phase;
        return true;
    }

    public static void Clear()
    {
        lock (Gate)
            _snapshot = null;
    }

    private static bool SameIdentity(PlayerState left, PlayerState right)
        => left.Name.Equals(right.Name, StringComparison.OrdinalIgnoreCase)
            && (left.WorldId == right.WorldId || left.WorldId == 0 || right.WorldId == 0);

    private static void RestoreRoundState(PlayerState target, PlayerState source)
    {
        target.Bank = source.Bank;
        target.CurrentBet = source.CurrentBet;
        target.BankAtRoundStart = source.BankAtRoundStart;
        target.LastRoundResult = source.LastRoundResult;
        target.IsActivePlayer = source.IsActivePlayer;
        target.IsOnHold = source.IsOnHold;
        target.WasOnHoldThisRound = source.WasOnHoldThisRound;
        target.IsOnBench = source.IsOnBench;
        target.BenchedAt = source.BenchedAt;
        target.JoinedMidRound = source.JoinedMidRound;
        target.ReadySkip = source.ReadySkip;
        target.IsCurrentTurn = source.IsCurrentTurn;
        target.HasInitialHandDealt = source.HasInitialHandDealt;
        target.IsDone = source.IsDone;
        target.CurrentHandIndex = source.CurrentHandIndex;
        target.Hands = source.Hands.Select(h => h.Clone()).ToList();
        target.ResetHighlightsAll();
    }
}
