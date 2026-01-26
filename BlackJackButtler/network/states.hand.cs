using System;
using System.Collections.Generic;

namespace BlackJackButtler;

public enum CardSuit { Diamonds, Clubs, Spades, Hearts }

public struct DeckCard
{
    public int Value;
    public CardSuit Suit;

    public string Symbol => Suit switch {
        CardSuit.Diamonds => "♦",
        CardSuit.Clubs => "♣",
        CardSuit.Spades => "♠",
        CardSuit.Hearts => "♥",
        _ => "?"
    };

    public string ValueLabel => Value switch {
        1 => "A",
        11 => "J",
        12 => "Q",
        13 => "K",
        _ => Value.ToString()
    };

    public override string ToString() => $"{Symbol}{ValueLabel}";

    public static bool operator ==(DeckCard a, DeckCard b) => a.Value == b.Value && a.Suit == b.Suit;
    public static bool operator !=(DeckCard a, DeckCard b) => !(a == b);
    public override bool Equals(object? obj) => obj is DeckCard other && this == other;
    public override int GetHashCode() => HashCode.Combine(Value, Suit);
}

public class HandState
{
    public List<DeckCard> Cards = new();
    public long Bet = 0;
    public bool IsStand = false;
    public bool IsBust = false;
    public bool IsDoubleDown = false;
    public bool IsNaturalBlackJack = false;

    public HandState(long initialBet) { Bet = initialBet; }

    public HandState Clone() => new HandState(Bet)
    {
        Cards = new List<DeckCard>(Cards),
        IsStand = IsStand,
        IsBust = IsBust,
        IsDoubleDown = IsDoubleDown,
        IsNaturalBlackJack = IsNaturalBlackJack
    };
}
