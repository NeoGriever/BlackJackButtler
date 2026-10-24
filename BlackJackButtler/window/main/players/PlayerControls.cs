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
    private void DrawPlayerControls(PlayerState p)
    {
        bool showClear = (!p.IsActivePlayer && !p.IsInParty) || _partyDissolved;
        if (showClear && (p.Bank > 0 || p.CurrentBet > 0))
        {
            var io = ImGui.GetIO();
            bool shift = io.KeyShift;
            bool ctrl = io.KeyCtrl;

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.0f, 0.0f, 1f));
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));
            if (BJBGui.SmallButton($"CLEAR##clear_{p.UIID}"))
            {
                if (shift)
                {
                    foreach (var pl in _players.ToList())
                    {
                        bool plClearable = ((!pl.IsActivePlayer && !pl.IsInParty) || _partyDissolved)
                                           && (pl.Bank > 0 || pl.CurrentBet > 0);
                        if (plClearable)
                            ClearPlayer(pl, ctrl);
                    }
                }
                else
                {
                    ClearPlayer(p, ctrl);
                }
            }
            ImGui.PopStyleColor(2);

            if (ImGui.IsItemHovered())
            {
                string tip = ctrl ? "CTRL: Bank \u2192 Tips, Bet = 0" : "Bank = 0, Bet = 0";
                if (shift) tip = "SHIFT: " + tip + " (all clearable)";
                ImGui.SetTooltip(tip);
            }
            return;
        }

        bool isExecutingForThis = CommandExecutor.IsRunning
            && CommandExecutor.CurrentGroupHasDice
            && CommandExecutor.CurrentTargetPlayer.Equals(p.Name, StringComparison.OrdinalIgnoreCase);

        if (isExecutingForThis)
        {
            DrawEmergencyStopRow(p);
            return;
        }

        bool globalLock = GameActionQueueManager.IsBusy || CommandExecutor.IsRunning || _showSplitMoneyPopup || _showDDMoneyPopup;
        if (globalLock) ImGui.BeginDisabled();

        if (p.IsCurrentTurn && p.CurrentHandIndex > 0)
            ImGui.Dummy(new Vector2(0, p.CurrentHandIndex * ImGui.GetFrameHeightWithSpacing()));

        InnerPlayerControls(p);

        if (globalLock) ImGui.EndDisabled();

        var phase = GameEngine.CurrentPhase;
        if (phase == GamePhase.Waiting || phase == GamePhase.Payout)
        {
            var currentTarget = GameEngine.GetCurrentTargetName();
            bool isTargeted = p.Name.Equals(currentTarget, StringComparison.OrdinalIgnoreCase);
            if (!isTargeted)
            {
                ImGui.SameLine();
                ImGui.PushFont(UiBuilder.IconFont);
                if (BJBGui.SmallButton($"{FontAwesomeIcon.Crosshairs.ToIconString()}##target_{p.UIID}"))
                {
                    GameEngine.TargetPlayer(p);
                    VariableManager.SetPlayerVariables(p);
                }
                ImGui.PopFont();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Target this player");
            }
        }
    }

    private void InnerPlayerControls(PlayerState p)
    {
        var phase = GameEngine.CurrentPhase;

        if (phase == GamePhase.Payout)
        {
            bool shouldHighlight = p.HighlightPay;
            bool clickedPay = shouldHighlight ? BJBGui.SmallButtonHighlighted($"Pay Out##{p.UIID}", _config.HighlightColor, _config.HighlightTextColor) : BJBGui.SmallButton($"Pay Out##{p.UIID}");
            if (clickedPay)
            {
                p.HighlightPay = false;
                PayoutManagement.StartPayout(p);
            }
            return;
        }

        if (!p.IsCurrentTurn)
        {
            ImGui.TextDisabled("Waiting for turn ...");
            return;
        }

        if (_showSplitMoneyPopup || _showDDMoneyPopup)
        {
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "Awaiting payment ...");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Buttons are disabled until the payment is processed or cancelled.");
            return;
        }

        if (phase == GamePhase.InitialDeal && !p.HasInitialHandDealt)
        {
            if (_config.AutoInitialDeal)
            {
                ImGui.TextDisabled("Auto...");
            }
            else
            {
                if (BJBGui.SmallButton($"Deal Hand##deal_{p.UIID}"))
                {
                    GameActionQueueManager.Enqueue(
                        $"DealHand:{p.Name}",
                        () => GameEngine.ActionDealHand(
                            p,
                            _config,
                            _players
                        ),
                        $"PlayerAction:{p.Name}",
                        () => p.IsCurrentTurn && GameEngine.CurrentPhase == GamePhase.InitialDeal && !p.HasInitialHandDealt
                    );
                }
            }
            return;
        }

        if (phase == GamePhase.PlayersTurn && p.HasInitialHandDealt)
        {
            if (p.Hands.Count == 0) return;
            var currentHand = p.Hands[p.CurrentHandIndex];
            var (min, max) = p.CalculatePoints(p.CurrentHandIndex);

            bool isSplitHand = p.Hands.Count > 1;

            bool canHit = min < 21 && !currentHand.IsDoubleDown && !currentHand.IsTripleDown && !currentHand.IsStand;

            bool canDD = canHit && currentHand.Cards.Count == 2;
            if (!_config.EnableDoubleDown) canDD = false;
            if (isSplitHand && !_config.AllowDoubleDownAfterSplit) canDD = false;

            bool canTD = canHit && GameEngine.CanTripleDown(p, currentHand, _config);

            bool canSplit = false;
            if (_config.EnableSplit && canHit && currentHand.Cards.Count == 2 && p.Hands.Count < _config.MaxHandsPerPlayer)
            {
                if (_config.IdenticalSplitOnly)
                canSplit = currentHand.Cards[0].Value == currentHand.Cards[1].Value;
                else
                canSplit = PlayerState.GetCardScoreValue(currentHand.Cards[0].Value) == PlayerState.GetCardScoreValue(currentHand.Cards[1].Value);
            }

            bool canStand = !currentHand.IsStand && !currentHand.IsBust;

            HighlightActionButton(
                p,
                "Draw",
                ref p.HighlightHit,
                canHit,
                () =>
                {
                    BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, $"PlayerHit:{p.Name}");
                    GameActionQueueManager.Enqueue($"Hit:{p.Name}", () => GameEngine.ActionHit(p, _config, _players), $"PlayerAction:{p.Name}", () => p.IsCurrentTurn && GameEngine.CurrentPhase == GamePhase.PlayersTurn);
                }
            );
            ImGui.SameLine();

            HighlightActionButton(
                p,
                "DD",
                ref p.HighlightDD,
                canDD,
                () =>
                {
                    BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, $"PlayerDD:{p.Name}");
                    GameActionQueueManager.Enqueue($"DD:{p.Name}", () => GameEngine.ActionDD(p, _config, _players), $"PlayerAction:{p.Name}", () => p.IsCurrentTurn && GameEngine.CurrentPhase == GamePhase.PlayersTurn);
                }
            );
            ImGui.SameLine();

            HighlightActionButton(
                p,
                "TD",
                ref p.HighlightTD,
                canTD,
                () =>
                {
                    BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, $"PlayerTD:{p.Name}");
                    GameActionQueueManager.Enqueue($"TD:{p.Name}", () => GameEngine.ActionTD(p, _config, _players), $"PlayerAction:{p.Name}", () => p.IsCurrentTurn && GameEngine.CurrentPhase == GamePhase.PlayersTurn);
                }
            );
            ImGui.SameLine();

            HighlightActionButton(
                p,
                "Spl",
                ref p.HighlightSplit,
                canSplit,
                () =>
                {
                    BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, $"PlayerSplit:{p.Name}");
                    GameActionQueueManager.Enqueue($"Split:{p.Name}", () => GameEngine.ActionSplit(p, _config, _players), $"PlayerAction:{p.Name}", () => p.IsCurrentTurn && GameEngine.CurrentPhase == GamePhase.PlayersTurn);
                }
            );
            ImGui.SameLine();

            HighlightActionButton(
                p,
                "Stand",
                ref p.HighlightStand,
                canStand,
                () =>
                {
                    BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, $"PlayerStand:{p.Name}");
                    GameActionQueueManager.Enqueue($"Stand:{p.Name}", () => GameEngine.ActionStand(p, _config, _players), $"PlayerAction:{p.Name}", () => p.IsCurrentTurn && GameEngine.CurrentPhase == GamePhase.PlayersTurn);
                }
            );
            ImGui.SameLine();

            DrawRecallButton(p);
        }
    }

    private void DrawRecallButton(PlayerState p)
    {
        bool hasState = !string.IsNullOrEmpty(CommandExecutor.LastStateGroupName);
        double elapsed = hasState ? (DateTime.Now - CommandExecutor.LastStateFiredAt).TotalSeconds : 0;
        float unlock = _config.RecallUnlockSeconds;
        bool unlocked = hasState && elapsed >= unlock;

        if (!unlocked) ImGui.BeginDisabled();
        string label;
        if (!hasState) label = "Recall";
        else if (!unlocked) label = $"Recall ({Math.Max(0, (int)Math.Ceiling(unlock - elapsed))}s)";
        else label = "Recall";

        if (BJBGui.Button($"{label}##btn_recall_{p.UIID}"))
        {
            string grp = CommandExecutor.LastStateGroupName;
            string tgt = CommandExecutor.LastStateTargetName;
            GameActionQueueManager.Enqueue($"Recall:{tgt}", () => CommandExecutor.ExecuteGroup(grp, tgt, _config), $"Recall:{tgt}");
        }
        if (!unlocked) ImGui.EndDisabled();
    }
}
