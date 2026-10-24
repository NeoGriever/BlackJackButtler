using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static partial class GameEngine
{
    public static string GetStatePromptGroup(PlayerState p, Configuration cfg)
    {
        if (p.Hands.Count == 0) return string.Empty;
        var hand = p.Hands[p.CurrentHandIndex];

        bool canSplit = false;
        if (cfg.EnableSplit && hand.Cards.Count == 2 && p.Hands.Count < cfg.MaxHandsPerPlayer)
        {
            if (cfg.IdenticalSplitOnly)
                canSplit = hand.Cards[0].Value == hand.Cards[1].Value;
            else
                canSplit = PlayerState.GetCardScoreValue(hand.Cards[0].Value) == PlayerState.GetCardScoreValue(hand.Cards[1].Value);
        }

        bool isSplitHand = p.Hands.Count > 1;
        bool canDD = cfg.EnableDoubleDown && hand.Cards.Count == 2
            && (!isSplitHand || cfg.AllowDoubleDownAfterSplit);
        bool canTD = CanTripleDown(p, hand, cfg);

        if (canSplit && canDD && canTD) return "StateHSDTS";
        if (canSplit && canTD)          return "StateHSTS";
        if (canDD && canTD)             return "StateHSDT";
        if (canTD)                      return "StateHST";
        if (canSplit)                   return "StateHSDS";
        if (canDD)                      return "StateHSD";
        return "StateHS";
    }

    public static bool CanTripleDown(PlayerState player, HandState hand, Configuration cfg)
    {
        if (!cfg.EnableTripleDown || hand.Cards.Count != 2 || hand.IsDoubleDown || hand.IsTripleDown)
            return false;
        if (player.Hands.Count > 1 && !cfg.AllowTripleDownAfterSplit)
            return false;
        return !cfg.LimitTripleDownToMaxPoints
            || player.GetBestScore(player.CurrentHandIndex) <= cfg.TripleDownMaxPoints;
    }
}
