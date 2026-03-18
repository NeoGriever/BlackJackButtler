using System;
using System.Collections.Generic;

namespace BlackJackButtler;

public sealed class DebugHand
{
    public List<DeckCard> Cards = new();
    public long Bet = 100000;
}

public static class DrawLogicDebugManager
{
    public static bool IsActive;
    public static List<DebugHand> DebugHands = new() { new DebugHand() };
    public static Dictionary<string, string> ValidScriptCache = new();

    public static void AddRandomCard(DebugHand hand)
    {
        hand.Cards.Add(new DeckCard
        {
            Value = Random.Shared.Next(1, 14),
            Suit = (CardSuit)Random.Shared.Next(4),
            DrawnAt = DateTime.UtcNow
        });
    }

    public static void Reset()
    {
        IsActive = false;
        DebugHands.Clear();
        DebugHands.Add(new DebugHand());
        ValidScriptCache.Clear();
    }

    public static (List<PlayerState> players, PlayerState dealer) BuildVirtualPlayers()
    {
        var localName = Plugin.ObjectTable.LocalPlayer?.Name.TextValue ?? "Debug Player";

        var player = new PlayerState
        {
            Name = localName,
            IsActivePlayer = true,
            IsInParty = true,
        };

        foreach (var dh in DebugHands)
        {
            var hand = new HandState(dh.Bet);
            hand.Cards.AddRange(dh.Cards);
            var best = CalcBestScore(dh.Cards);
            hand.IsBust = best > 21;
            hand.IsNaturalBlackJack = dh.Cards.Count == 2 && best == 21;
            player.Hands.Add(hand);
        }
        if (player.Hands.Count == 0)
            player.Hands.Add(new HandState(0));

        var dealer = new PlayerState
        {
            Name = localName + " (Dealer)",
        };
        dealer.Hands.Add(new HandState(0));

        return (new List<PlayerState> { player }, dealer);
    }

    private static int CalcBestScore(List<DeckCard> cards)
    {
        int total = 0, aces = 0;
        foreach (var c in cards)
        {
            if (c.Value == 1) { total += 1; aces++; }
            else if (c.Value >= 10) total += 10;
            else total += c.Value;
        }
        return (aces > 0 && total + 10 <= 21) ? total + 10 : total;
    }
}
