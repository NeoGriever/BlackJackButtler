using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static partial class GameEngine
{
    public static async Task ActionSplit(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
        if (p.Hands.Count >= cfg.MaxHandsPerPlayer) return;

        Chat.GameLog.PushSnapshot(players, _ctxDealer!, CurrentPhase, $"Split:{p.Name}");
        CommandExecutor.SetPreActionSnapshot(Chat.GameLog.CurrentIndex);

        if (p.Bank < p.CurrentBet)
        {
            long missingAmount = p.CurrentBet - p.Bank;

            Plugin.Instance.GetMainWindow().OpenSplitMoneyPopup(p, missingAmount);

            _pendingSplitPlayer = p;
            _pendingSplitConfig = cfg;
            _pendingSplitPlayers = players;

            return;
        }

        await ExecuteSplit(p, cfg, players);
    }

    public static async Task ContinueSplitAfterPayment(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
        if (p == null)
        {
            Plugin.Log.Error("[BJB] ContinueSplitAfterPayment: Player object is null!");
            return;
        }

        try
        {
            if (p.Bank < p.CurrentBet)
            {
                Plugin.Log.Warning($"[Split] Still not enough money for {p.Name} after payment check. (Bank: {p.Bank}, Needed: {p.CurrentBet})");
                return;
            }

            await ExecuteSplit(p, cfg, players);

            _pendingSplitPlayer = null;
            _pendingSplitConfig = null;
            _pendingSplitPlayers = null;

            Plugin.Log.Debug($"[Split] Continuation successful for {p.Name}");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[BJB] CRITICAL ERROR in ContinueSplitAfterPayment: {ex}");
        }
    }

    private static async Task ExecuteSplit(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
        if (p.CurrentHandIndex < 0 || p.CurrentHandIndex >= p.Hands.Count)
            return;

        var currentHand = p.Hands[p.CurrentHandIndex];
        if (currentHand.Cards.Count != 2) return;

        p.Bank -= p.CurrentBet;
        currentHand.ActionLog.Add("Split");

        var cardToMove = currentHand.Cards[1];
        currentHand.Cards.RemoveAt(1);

        var newHand = new HandState(p.CurrentBet);
        newHand.Cards.Add(cardToMove);
        p.Hands.Add(newHand);

        TargetPlayer(p);
        SetForcedRecipient(p);
        try { await CommandExecutor.ExecuteGroup("Split", p.DisplayName, cfg); }
        finally { ClearForcedRecipient(); }

        SaveSessionIfNeeded(players);
        CompanionSyncManager.SendPlayerUpdate(cfg, p);

        Plugin.Instance.GetMainWindow().AddDebugLog($"[Split] {p.DisplayName} successfully split hand", false);
    }
}
