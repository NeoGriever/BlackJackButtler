using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static partial class GameEngine
{
    public static bool HasPlayerUnableToCoverBet(IEnumerable<PlayerState> players)
    {
        return players.Any(IsPlayerUnableToCoverBet);
    }

    public static bool IsPlayerUnableToCoverBet(PlayerState p)
    {
        return p.IsActivePlayer && !p.IsOnHold && p.CurrentBet > 0 && p.Bank < p.CurrentBet;
    }
}
