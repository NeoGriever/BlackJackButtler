using System.Collections.Generic;

namespace BlackJackButtler;

public class HandState
{
    public List<int> Cards = new();
    public long Bet = 0;
    public bool IsStand = false;
    public bool IsBust = false;
    public bool IsDoubleDown = false;
    public bool IsNaturalBlackJack = false;

    public HandState(long initialBet)
    {
        Bet = initialBet;
    }

    public HandState Clone() => new HandState(Bet)
    {
        Cards = new List<int>(Cards),
        IsStand = IsStand,
        IsBust = IsBust,
        IsDoubleDown = IsDoubleDown,
        IsNaturalBlackJack = IsNaturalBlackJack
    };
}
