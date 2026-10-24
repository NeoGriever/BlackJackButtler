using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static partial class GameEngine
{
    public static async Task ActionDD(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
        if(p.Bank < p.CurrentBet)
        {
            long missingAmount = p.CurrentBet - p.Bank;
            Plugin.Instance.GetMainWindow().OpenDDMoneyPopup(p, missingAmount);
            _pendingDDPlayer = p;
            return;
        }

        await ExecuteActualDD(p, cfg, players);
    }

    public static async Task ContinueDDAfterPayment(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
        if (p == null)
        {
            Plugin.Log.Error("[BJB] ContinueDDAfterPayment was called with a null player!");
            return;
        }

        try
        {
            if (p.Bank < p.CurrentBet)
            {
                Plugin.Log.Warning($"[DD] Still not enough money for {p.Name}");
                return;
            }

            await ExecuteActualDD(p, cfg, players);

            _pendingDDPlayer = null;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[BJB] Error in ContinueDDAfterPayment: {ex}");
        }
    }


    private static async Task ExecuteActualDD(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
        await ExecutePlayerAction(
            p,
            "DD",
            cfg,
            players,
            async () => {
                var hand = p.Hands[p.CurrentHandIndex];
                hand.IsDoubleDown = true;
                hand.ActionLog.Add("DD");
                hand.Bet *= 2;
                TargetPlayer(p);
                SetForcedRecipient(p);
                try { await CommandExecutor.ExecuteGroup("DD", p.DisplayName, cfg); }
                finally { ClearForcedRecipient(); }
                hand.IsStand = true;
            }
        );
        SaveSessionIfNeeded(players);
        CompanionSyncManager.SendPlayerUpdate(cfg, p);
    }
}
