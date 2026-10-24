using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static partial class GameEngine
{
    public static async Task ActionTD(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
        if (p.CurrentHandIndex < 0 || p.CurrentHandIndex >= p.Hands.Count
            || !CanTripleDown(p, p.Hands[p.CurrentHandIndex], cfg))
            return;

        var additionalBet = p.CurrentBet * 2;
        if (p.Bank < additionalBet)
        {
            Plugin.Instance.GetMainWindow().OpenTDMoneyPopup(p, additionalBet - p.Bank);
            _pendingTDPlayer = p;
            return;
        }

        await ExecuteActualTD(p, cfg, players);
    }

    public static async Task ContinueTDAfterPayment(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
        if (p == null)
        {
            Plugin.Log.Error("[BJB] ContinueTDAfterPayment was called with a null player!");
            return;
        }

        try
        {
            if (p.CurrentHandIndex < 0 || p.CurrentHandIndex >= p.Hands.Count
                || !CanTripleDown(p, p.Hands[p.CurrentHandIndex], cfg))
                return;

            var additionalBet = p.CurrentBet * 2;
            if (p.Bank < additionalBet)
            {
                Plugin.Log.Warning($"[TD] Still not enough money for {p.Name}");
                return;
            }

            await ExecuteActualTD(p, cfg, players);
            _pendingTDPlayer = null;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[BJB] Error in ContinueTDAfterPayment: {ex}");
        }
    }

    private static async Task ExecuteActualTD(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
        await ExecutePlayerAction(
            p,
            "TD",
            cfg,
            players,
            async () => {
                var hand = p.Hands[p.CurrentHandIndex];
                hand.IsTripleDown = true;
                hand.ActionLog.Add("TD");
                hand.Bet *= 3;
                TargetPlayer(p);
                SetForcedRecipient(p);
                try { await CommandExecutor.ExecuteGroup("TD", p.DisplayName, cfg); }
                finally { ClearForcedRecipient(); }
                hand.IsStand = true;
            }
        );
        SaveSessionIfNeeded(players);
        CompanionSyncManager.SendPlayerUpdate(cfg, p);
    }
}
