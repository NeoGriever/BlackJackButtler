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
    private void DrawDealerControls()
    {
        bool globalLock = GameActionQueueManager.IsBusy || CommandExecutor.IsRunning;
        if (globalLock) ImGui.BeginDisabled();

        InnerDealerControls();

        if (globalLock) ImGui.EndDisabled();
    }

    private void InnerDealerControls()
    {
        var phase = GameEngine.CurrentPhase;

        if (GameEngine.CanAcceptInterRoundDetectors())
        {
            bool canStartRound = GameEngine.CanStartInitialDeal(_players, out var startBlockedReason);
            if (!canStartRound) ImGui.BeginDisabled();
            bool hlNewRound = _highlightNewRound;
            bool clickedNewRound = hlNewRound ? BJBGui.SmallButtonHighlighted("Start New Round", _config.HighlightColor, _config.HighlightTextColor) : BJBGui.SmallButton("Start New Round");
            if (clickedNewRound)
            {
                _highlightNewRound = false;
                BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, "DealStart");
                GameActionQueueManager.Enqueue(
                    "ManualRoundStart",
                    () => GameEngine.StartInitialDeal(
                        _players,
                        _config
                    ),
                    "RoundStart"
                );
            }
            if (!canStartRound) ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && !canStartRound)
                ImGui.SetTooltip(startBlockedReason);
        }
        else if (phase == GamePhase.DealerTurn)
        {
            if (_config.AutoDealerDraw)
            {
                ImGui.TextDisabled("Auto...");
            }
            else
            {
                if (BJBGui.SmallButton("Hit"))
                {
                    BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, "DealerHit");
                    GameActionQueueManager.Enqueue("DealerHit", () => GameEngine.DealerHit(_config, _players), "DealerAction", () => GameEngine.CurrentPhase == GamePhase.DealerTurn);
                }
                ImGui.SameLine();
                if (BJBGui.SmallButton("Stand"))
                {
                    BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, "DealerStand");
                    GameActionQueueManager.Enqueue(
                        "DealerStand",
                        async () => {
                            await GameEngine.DealerStand(_config, _players);
                            await GameEngine.EvaluateFinalResults(_players, _dealer, _config);
                        },
                        "DealerAction",
                        () => GameEngine.CurrentPhase == GamePhase.DealerTurn
                    );
                }
            }
        }
        else { ImGui.TextDisabled("Waiting..."); }
    }
}
