using System;
using System.Numerics;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Game.ClientState.Objects.SubKinds;
using System.Threading.Tasks;
using BlackJackButtler.Chat;
using BlackJackButtler.Regex;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private async void ExecutePanic()
    {
        if (_panicInProgress)
            return;

        _panicInProgress = true;
        try
        {
            AddDebugLog("[PANIC] Cancelling queued and running round actions...", false);
            await GameActionQueueManager.SuspendCancelAndDrainAsync(cancelCurrentCommand: true);

            if (RoundRollbackManager.Restore(_players, _dealer, out var restoredPhase))
            {
                GameEngine.CurrentPhase = restoredPhase;
                AddDebugLog("[PANIC] Restored the session state captured before round start.", false);
            }
            else
            {
                foreach (var p in _players)
                {
                    p.Hands.Clear();
                    p.Hands.Add(new HandState(p.CurrentBet));
                    p.CurrentHandIndex = 0;
                    p.IsCurrentTurn = false;
                    p.HasInitialHandDealt = false;
                    p.IsDone = false;
                    p.LastRoundResult = 0;
                    p.JoinedMidRound = false;
                    p.IsOnHold = false;
                    p.WasOnHoldThisRound = false;
                    p.IsOnBench = false;
                    p.ResetHighlightsAll();
                }

                _dealer.Hands.Clear();
                _dealer.Hands.Add(new HandState(0));
                _dealer.CurrentHandIndex = 0;
                _dealer.IsCurrentTurn = false;
                _dealer.HasInitialHandDealt = false;
                _dealer.IsDone = false;
                _dealer.ResetHighlightsAll();
                GameEngine.CurrentPhase = GamePhase.Waiting;
                AddDebugLog("[PANIC] No round-start snapshot existed; reset to Waiting.", false);
            }

            CommandExecutor.ClearLastState();
            GameEngine.ClearForcedRecipient();
            GameEngine.SetRuntimeContext(_players, _dealer);
            SessionManager.SaveSession(_players, _dealer, GameEngine.CurrentPhase, IsRecognitionActive);
            CompanionSyncManager.SendPlayersUpdate(_config, _players);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "[PANIC] Rollback failed");
            AddDebugLog($"[PANIC] Rollback failed: {ex.Message}", false);
        }
        finally
        {
            GameActionQueueManager.Resume();
            _panicInProgress = false;
        }

        AddDebugLog("[PANIC] Rollback complete. Logs and statistics were left unchanged.", false);
    }
}
