using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static partial class GameEngine
{
    public static async Task ActionDealHand(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
        Plugin.Instance.GetMainWindow().AddDebugLog($"[ActionDealHand] Start: {p.DisplayName}");
        await ExecutePlayerAction(
            p,
            "Initial",
            cfg,
            players,
            async () => {
                TargetPlayer(p);
                SetForcedRecipient(p);
                try { await CommandExecutor.ExecuteGroup("Initial", p.DisplayName, cfg); }
                finally { ClearForcedRecipient(); }
                p.HasInitialHandDealt = true;
            }
        );
        Plugin.Instance.GetMainWindow().AddDebugLog($"[ActionDealHand] Complete: {p.DisplayName}, HasInitialHandDealt={p.HasInitialHandDealt}");
    }
}
