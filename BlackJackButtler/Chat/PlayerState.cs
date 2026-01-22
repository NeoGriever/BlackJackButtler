using System.Collections.Generic;
using System.Linq;

namespace BlackJackButtler;

public class PlayerState
{
    public string Name = string.Empty;
    public uint WorldId;
    public bool IsActivePlayer = false;
    public bool IsInParty = true;
    public bool IsCurrentTurn = false;

    public long Bank = 0;
    public long CurrentBet = 0;

    public List<int> Cards = new();

    public bool HighlightBet = false;
    public bool HighlightHit = false;
    public bool HighlightStand = false;
    public bool HighlightDD = false;
    public bool HighlightSplit = false;
    public bool HighlightPay = false;

    public (int Min, int? Max) CalculatePoints()
    {
        int total = 0;
        int aces = 0;
        foreach (var c in Cards)
        {
            if (c == 1) { total += 1; aces++; }
            else if (c >= 10) total += 10;
            else total += c;
        }

        if (aces > 0 && total + 10 <= 21)
        return (total, total + 10);

        return (total, null);
    }
}
