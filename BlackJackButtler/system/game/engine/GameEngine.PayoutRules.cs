using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static partial class GameEngine
{
    internal static int EvaluateBJTie(HandState hand, PlayerState dealer, BlackjackTieRule rule)
    {
        bool playerNat = hand.IsNaturalBlackJack;
        bool dealerNat = dealer.Hands.Count > 0 && dealer.Hands[0].IsNaturalBlackJack;

        return rule switch
        {
            BlackjackTieRule.PlayerNatBJWins => playerNat ? 1 : 0,
            BlackjackTieRule.DealerNatBJWins => dealerNat ? -1 : 0,
            BlackjackTieRule.NatBJBeatsDirty => playerNat == dealerNat ? 0 : (playerNat ? 1 : -1),
            _ => 0,
        };
    }

    private static float GetWinMultiplier(PlayerState player, HandState hand, int score, Configuration cfg)
    {
        if (hand.IsNaturalBlackJack) return cfg.MultiplierBlackjackWin;
        if (hand.IsCharlie) return cfg.MultiplierCharlieWin;
        if (score == 21) return cfg.MultiplierDirtyBlackjackWin;
        if (hand.IsTripleDown) return cfg.MultiplierTripleDownWin;
        if (hand.IsDoubleDown) return cfg.MultiplierDoubleDownWin;
        if (player.Hands.Count > 1) return cfg.MultiplierSplitWin;
        return cfg.MultiplierNormalWin;
    }

    private static long GetPushRefund(HandState hand, Configuration cfg)
    {
        if (hand.IsTripleDown)
            return cfg.RefundFullTripleDownOnPush ? hand.Bet : hand.Bet / 3;
        if (hand.IsDoubleDown)
            return cfg.RefundFullDoubleDownOnPush ? hand.Bet : hand.Bet / 2;
        return hand.Bet;
    }

    internal static long CalculateWinAmount(long bet, float multiplier)
    {
        return (long)(bet * (decimal)multiplier);
    }
}
