using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static partial class GameEngine
{
    public static void HandlePostCardEvents(Configuration cfg, List<PlayerState> players, PlayerState dealer)
    {

        var targetName = GetCurrentTargetName();
        PlayerState? target =
        (!string.IsNullOrWhiteSpace(targetName) && dealer.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase)) ? dealer : players.FirstOrDefault(p => p.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));

        target ??= players.FirstOrDefault(p => p.IsCurrentTurn) ?? dealer;

        if (target.Hands.Count == 0) return;

        if (target.CurrentHandIndex < 0 || target.CurrentHandIndex >= target.Hands.Count)
        target.CurrentHandIndex = 0;

    }

    private static async Task ExecutePlayerAction(PlayerState p, string actionName, Configuration cfg, List<PlayerState> players, Func<Task> logic)
    {
        Chat.GameLog.PushSnapshot(players, _ctxDealer!, CurrentPhase, $"{actionName}:{p.Name}");
        CommandExecutor.SetPreActionSnapshot(Chat.GameLog.CurrentIndex);

        long cost = 0;
        if (actionName == "Initial") cost = p.CurrentBet;
        if (actionName == "DD")      cost = p.CurrentBet;
        if (actionName == "TD")      cost = p.CurrentBet * 2;
        if (actionName == "Split")   cost = p.CurrentBet;

        p.Bank -= cost;

        await logic();
    }
}
