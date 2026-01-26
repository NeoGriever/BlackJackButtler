using System.Collections.Generic;
using System.Linq;

namespace BlackJackButtler;

public class PlayerState
{
    public bool IsDebugPlayer = false;

    public string Name = string.Empty;
    public string Alias = string.Empty;
    public uint WorldId;
    public bool IsActivePlayer = false;

    public bool IsOnHold = false;

    public bool IsInParty = true;
    public bool IsCurrentTurn = false;
    public bool HasInitialHandDealt = false;
    public bool IsDone = false;

    public long Bank = 0;
    public long CurrentBet = 0;

    public List<HandState> Hands = new();
    public int CurrentHandIndex = 0;

    public bool HighlightBet = false;
    public bool HighlightHit = false;
    public bool HighlightStand = false;
    public bool HighlightDD = false;
    public bool HighlightSplit = false;
    public bool HighlightPay = false;
    public bool IsInDebt => Bank < 0;

    public string DisplayName => !string.IsNullOrWhiteSpace(Alias) ? Alias : Name;
    public string UIID => $"{Name}_{WorldId}";

    public int GetBestScore(int handIndex)
    {
        var (min, max) = CalculatePoints(handIndex);
        return (max.HasValue && max.Value <= 21) ? max.Value : min;
    }

    public void HandleBetDeduction(long amount)
    {
        Bank -= amount;
    }

    public void ResetForNewRound()
    {
        Hands.Clear();
        Hands.Add(new HandState(CurrentBet));
        CurrentHandIndex = 0;
        IsCurrentTurn = false;
        HasInitialHandDealt = false;
    }

    public (int Min, int? Max) CalculatePoints(int handIndex)
    {
        if (handIndex >= Hands.Count) return (0, null);
        var cards = Hands[handIndex].Cards;

        int total = 0;
        int aces = 0;
        foreach (var c in cards)
        {
            if (c == 1) { total += 1; aces++; }
            else if (c >= 10) total += 10;
            else total += c;
        }

        if (aces > 0 && total + 10 <= 21)
            return (total, total + 10);

        return (total, null);
    }

    public bool IsHandDone(int index)
    {
        if (index >= Hands.Count) return true;
        var (min, max) = CalculatePoints(index);
        return Hands[index].IsStand || Hands[index].IsBust || min >= 21 || (max.HasValue && max == 21);
    }

    public static int GetCardScoreValue(int cardRawValue)
    {
        if (cardRawValue >= 10) return 10;
        return cardRawValue;
    }

    public PlayerState Clone()
    {
        return new PlayerState
        {
            Name = Name,
            WorldId = WorldId,
            IsActivePlayer = IsActivePlayer,

            IsOnHold = IsOnHold,

            IsInParty = IsInParty,
            IsCurrentTurn = IsCurrentTurn,

            Bank = Bank,
            CurrentBet = CurrentBet,

            Hands = Hands.Select(h => h.Clone()).ToList(),
            CurrentHandIndex = CurrentHandIndex,

            HighlightBet = HighlightBet,
            HighlightHit = HighlightHit,
            HighlightStand = HighlightStand,
            HighlightDD = HighlightDD,
            HighlightSplit = HighlightSplit,
            HighlightPay = HighlightPay
        };
    }
}
