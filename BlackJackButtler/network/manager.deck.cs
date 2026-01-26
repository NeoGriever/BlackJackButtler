using System;
using System.Collections.Generic;
using System.Linq;

namespace BlackJackButtler;

public static class DeckManager
{
    private static List<DeckCard> _shoe = new();
    private static readonly Random _rng = new();

    static DeckManager() { Reshuffle(); }

    public static void Reshuffle()
    {
        _shoe.Clear();
        for (int d = 0; d < 12; d++)
        {
            foreach (CardSuit suit in Enum.GetValues(typeof(CardSuit)))
            {
                for (int v = 1; v <= 13; v++)
                {
                    _shoe.Add(new DeckCard { Value = v, Suit = suit });
                }
            }
        }
    }

    public static DeckCard PullCard(int value)
    {
        var candidates = _shoe.Where(c => c.Value == value).ToList();

        if (candidates.Count == 0)
        {
            Reshuffle();
            candidates = _shoe.Where(c => c.Value == value).ToList();
        }

        var picked = candidates[_rng.Next(candidates.Count)];
        _shoe.Remove(picked);
        return picked;
    }

    public static List<DeckCard> GetShoeSnapshot() => _shoe.ToList();
    public static void RestoreShoe(List<DeckCard> snapshot) => _shoe = snapshot.ToList();
}
