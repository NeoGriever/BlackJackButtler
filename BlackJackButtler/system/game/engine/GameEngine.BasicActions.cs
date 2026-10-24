using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static partial class GameEngine
{
    public static async Task ActionHit(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
        await ExecutePlayerAction(
            p,
            "Hit",
            cfg,
            players,
            async () => {
                if (p.CurrentHandIndex >= 0 && p.CurrentHandIndex < p.Hands.Count)
                    p.Hands[p.CurrentHandIndex].ActionLog.Add("Hit");
                TargetPlayer(p);
                SetForcedRecipient(p);
                try { await CommandExecutor.ExecuteGroup("Hit", p.DisplayName, cfg); }
                finally { ClearForcedRecipient(); }
                if (p.CurrentHandIndex >= 0 && p.CurrentHandIndex < p.Hands.Count && p.Hands[p.CurrentHandIndex].IsBust)
                {
                    var log = p.Hands[p.CurrentHandIndex].ActionLog;
                    if (log.Count > 0 && log[^1] == "Hit")
                        log[^1] = "Bust";
                }
            }
        );
        SaveSessionIfNeeded(players);
        CompanionSyncManager.SendPlayerUpdate(cfg, p);
    }

    public static async Task ActionStand(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
        if (p.CurrentHandIndex >= 0 && p.CurrentHandIndex < p.Hands.Count)
        {
            p.Hands[p.CurrentHandIndex].IsStand = true;
            p.Hands[p.CurrentHandIndex].ActionLog.Add("Stand");
        }

        TargetPlayer(p);
        SetForcedRecipient(p);
        try { await CommandExecutor.ExecuteGroup("Stand", p.DisplayName, cfg); }
        finally { ClearForcedRecipient(); }

        NextTurn(players, cfg);
        SaveSessionIfNeeded(players);
        CompanionSyncManager.SendPlayerUpdate(cfg, p);
    }
}
