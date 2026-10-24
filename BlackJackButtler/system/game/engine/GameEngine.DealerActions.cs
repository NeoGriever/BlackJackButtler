using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static partial class GameEngine
{
    public static async Task DealerHit(Configuration cfg, List<PlayerState> players)
    {
        PlayerState? dealer;
        lock (_ctxLock) dealer = _ctxDealer;
        if (dealer == null) return;

        CurrentPhase = GamePhase.DealerTurn;
        dealer.IsCurrentTurn = true;
        dealer.CurrentHandIndex = 0;

        if (dealer.Hands.Count > 0)
            dealer.Hands[0].ActionLog.Add("Hit");

        TargetPlayer(dealer);
        SetForcedRecipient(dealer);
        try { await CommandExecutor.ExecuteGroup("DealHit", dealer.DisplayName, cfg); }
        finally { ClearForcedRecipient(); }

        if (dealer.Hands.Count > 0 && dealer.Hands[0].IsBust)
        {
            var log = dealer.Hands[0].ActionLog;
            if (log.Count > 0 && log[^1] == "Hit")
                log[^1] = "Bust";
        }

        SaveSessionIfNeeded(players);
        SendCompanionTableUpdate(cfg, players.Where(x => x.IsActivePlayer));
    }

    public static async Task DealerStand(Configuration cfg, List<PlayerState> players)
    {
        PlayerState? dealer;
        lock (_ctxLock) dealer = _ctxDealer;
        if (dealer == null) return;

        BeginPayoutOutput();
        dealer.IsCurrentTurn = false;

        if (dealer.Hands.Count > 0)
        {
            dealer.Hands[0].ActionLog.Add("Stand");
            dealer.Hands[0].IsStand = true;
        }

        TargetPlayer(dealer);
        SetForcedRecipient(dealer);
        try { await CommandExecutor.ExecuteGroup("DealStand", dealer.DisplayName, cfg); }
        finally { ClearForcedRecipient(); }
        SaveSessionIfNeeded(players);
        SendCompanionTableUpdate(cfg, players.Where(x => x.IsActivePlayer));
    }
}
