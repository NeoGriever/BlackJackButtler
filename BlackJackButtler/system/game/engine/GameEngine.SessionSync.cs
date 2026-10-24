using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static partial class GameEngine
{
    private static void SaveSessionIfNeeded(List<PlayerState> players)
    {
        PlayerState? dealer;
        lock (_ctxLock) dealer = _ctxDealer;
        if (dealer == null) return;

        var mainWindow = Plugin.Instance.GetMainWindow();
        SessionManager.SaveSession(
            players,
            dealer,
            CurrentPhase,
            mainWindow.IsRecognitionActive
        );
    }

    public static void SendCompanionTableUpdate(Configuration cfg, IEnumerable<PlayerState> players)
    {
        PlayerState? dealer;
        lock (_ctxLock) dealer = _ctxDealer;
        var snapshot = new List<PlayerState>();

        if (dealer != null)
            snapshot.Add(dealer);

        snapshot.AddRange(
            players
                .Where(p => p != null)
                .OrderBy(p => p.IsCurrentTurn ? 1 : 0)
                .ThenBy(p => p.UIID)
        );

        CompanionSyncManager.SendPlayersUpdate(cfg, snapshot);
    }

}
