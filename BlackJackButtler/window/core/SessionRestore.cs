using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using BlackJackButtler.Chat;
using Newtonsoft.Json.Linq;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void RestoreSessionFromFile()
    {
        if (SessionManager.RestoreSession(
            out var players,
            out var dealer,
            out var phase,
            out var history,
            out var historyIndex
        ))
        {
            foreach (var player in _players)
                CompanionSyncManager.ClearPlayer(_config, player);

            _players = players;
            _dealer = dealer;
            GameEngine.CurrentPhase = phase;
            CompanionSyncManager.SendPlayersUpdate(_config, _players);

            GameLog.Clear();
            foreach (var (idx, snapshot) in history)
            {
                GameLog.RestoreSnapshot(idx, snapshot);
            }
            GameLog.SetIndex(historyIndex);

            IsRecognitionActive = true;
            Plugin.Instance.UpdateEventHooks();

            _showRestoreSessionButton = false;
            _page = Page.Main;

            AddDebugLog("[SessionManager] Session restored successfully!", false);
            Plugin.Log.Information($"[SessionManager] Restored to Phase: {phase} with {players.Count} players");
        }
        else
        {
            _showRestoreSessionButton = SessionManager.HasRestorableGameSession();
            AddDebugLog("[SessionManager] Failed to restore session!", false);
        }
    }
}
