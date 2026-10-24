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
    private void DrawPlayerRow(PlayerState p, bool isDealer)
    {
        uint bgColor;
        if (!p.IsActivePlayer) bgColor = ImGui.GetColorU32(new Vector4(0, 0, 0, 1));
        else if (p.IsCurrentTurn) bgColor = ImGui.GetColorU32(new Vector4(0.0f, 0.25f, 0.0f, 1f));
        else if (p.IsOnHold) bgColor = ImGui.GetColorU32(new Vector4(0.15f, 0.15f, 0.15f, 1f));
        else bgColor = ImGui.GetColorU32(new Vector4(0.0f, 0.0f, 0.2f, 1f));

        if (p.IsImaginaryPlayer)
        {
            bgColor = !p.IsActivePlayer ? ImGui.GetColorU32(new Vector4(0.08f, 0.08f, 0.08f, 1f)) : p.IsCurrentTurn ? ImGui.GetColorU32(new Vector4(0.16f, 0.30f, 0.16f, 1f)) : p.IsOnHold ? ImGui.GetColorU32(new Vector4(0.24f, 0.24f, 0.24f, 1f)) : ImGui.GetColorU32(new Vector4(0.14f, 0.14f, 0.26f, 1f));
        }

        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, bgColor);
        ImGui.PushID(p.Name);
        PlayerSoundEffects.ApplySavedPreference(_config, p);

        ImGui.TableNextColumn();
        if (p.IsActivePlayer && _config.VipBetTiers.Count > 0)
        {
            string worldName = VipManager.ResolveWorldName(p.WorldId);
            int currentTier = VipManager.GetPlayerTier(p.Name, worldName);

            bool isVip = currentTier > 0;
            var btnColor = isVip ? new Vector4(1.0f, 0.6f, 0.0f, 1f) : new Vector4(0.35f, 0.22f, 0.08f, 1f);
            var txtColor = isVip ? new Vector4(0.8f, 0.1f, 0.1f, 1f) : new Vector4(0.5f, 0.5f, 0.5f, 1f);

            ImGui.PushStyleColor(ImGuiCol.Button, btnColor);
            ImGui.PushStyleColor(ImGuiCol.Text, txtColor);

            string vipLabel = isVip && currentTier <= _config.VipBetTiers.Count ? $"{currentTier}##vip_{p.UIID}" : $"V##vip_{p.UIID}";
            if (BJBGui.Button(vipLabel))
            {
                VipManager.CycleTier(p.Name, worldName, _config.VipBetTiers.Count);
            }

            ImGui.PopStyleColor(2);

            if (ImGui.IsItemHovered())
            {
                if (isVip && currentTier <= _config.VipBetTiers.Count)
                    ImGui.SetTooltip($"{_config.VipBetTiers[currentTier - 1].Name} (Max: {_config.VipBetTiers[currentTier - 1].MaxBet:N0})\nClick to cycle tier");
                else
                    ImGui.SetTooltip("Click to cycle tier");
            }
        }


        ImGui.TableNextColumn();
        if (!p.IsActivePlayer) {
            bool hlJoin = p.HighlightJoin;
            bool clickedJoin = hlJoin ? BJBGui.ButtonHighlighted($">##{p.UIID}", new Vector2(-1, 0), _config.HighlightColor, _config.HighlightTextColor) : BJBGui.Button($">##{p.UIID}", new Vector2(-1, 0));
            if (clickedJoin)
            {
                ActivatePlayer(p);
            }
        }
        else {
            bool hlLeave = p.HighlightLeave;
            bool removeRowAfterLeave = false;
            bool clickedLeave = hlLeave ? BJBGui.ButtonHighlighted($"X##{p.UIID}", new Vector2(-1, 0), _config.HighlightColor, _config.HighlightTextColor) : BJBGui.Button($"X##{p.UIID}", new Vector2(-1, 0));
            if (clickedLeave) {
                _newTradingPlayerKeys.Remove(GetGroupMemberKey(p.Name, p.WorldId));
                p.HighlightLeave = false;
                var leavePhase = GameEngine.CurrentPhase;
                if (leavePhase == GamePhase.InitialDeal || leavePhase == GamePhase.PlayersTurn || leavePhase == GamePhase.DealerTurn)
                {
                    GameEngine.DeactivatePlayerMidRound(p, _players, _config);
                }
                else
                {
                    if (p.Bank == 0) ActivityLogManager.LogPlayerLeave(p.DisplayName);
                    RoundLogManager.RecordPlayerLeave(p.DisplayName);
                    p.IsActivePlayer = false;
                    p.IsCurrentTurn = false;
                    p.ReadySkip = false;
                }
                SaveSessionFromUI();
                if (TryRemoveEmptyInactiveGeneratedPlayer(p))
                    removeRowAfterLeave = true;
                else
                    CompanionSyncManager.SendPlayerBankBetUpdate(_config, p);
            }
            if (removeRowAfterLeave) { ImGui.PopID(); return; }
        }

        ImGui.TableNextColumn();
        if (p.IsActivePlayer && !p.IsOnHold && !p.IsOnBench
            && GameEngine.CanAcceptInterRoundDetectors())
        {
            int activeCountR = _players.Count(pl => pl.IsActivePlayer && !pl.IsOnHold);
            bool canToggleR = activeCountR >= 2;
            if (!canToggleR) ImGui.BeginDisabled();
            bool wasReadyR = p.ReadySkip;
            if (wasReadyR) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.8f, 0.2f, 1f));
            if (BJBGui.SmallButton($"R##{p.UIID}_readyskip_col"))
            {
                p.ReadySkip = !p.ReadySkip;
                _save();
                if (p.ReadySkip) RegexEngine.CheckAutoReadyStart(_players, _config);
            }
            if (wasReadyR) ImGui.PopStyleColor();
            if (!canToggleR) ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip(
                    canToggleR ? (p.ReadySkip ? "Ready-skip ON: Auto-counted as voted" : "Click to auto-count as voted for next round") : "Need 2+ active players to enable ready-skip"
                );
        }

        ImGui.TableNextColumn();
        if (p.IsActivePlayer) {
            var currentPhase = GameEngine.CurrentPhase;
            bool isActiveRound = currentPhase == GamePhase.InitialDeal || currentPhase == GamePhase.PlayersTurn;

            bool isDisabled = false;
            if (p.IsOnBench)
            {
                isDisabled = (currentPhase == GamePhase.DealerTurn || currentPhase == GamePhase.Payout);
            }
            else if (p.IsOnHold)
            {
                isDisabled = false;
            }
            else
            {
                if (isActiveRound)
                    isDisabled = !GameEngine.CanMovePlayerToBench(p, _players);
            }

            if (isDisabled) ImGui.BeginDisabled();

            int colorsPushed = 0;
            bool holdHighlighted = false;
            if (p.IsOnBench)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.5f, 0.0f, 1f));
                colorsPushed = 1;
            }
            else if (p.IsOnHold)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1f));
                colorsPushed = 1;
            }
            else if (p.HighlightPause)
            {
                holdHighlighted = true;
            }

            bool clickedHold = holdHighlighted ? BJBGui.ButtonHighlighted($"H##hold_{p.UIID}", _config.HighlightColor, _config.HighlightTextColor) : p.IsOnBench ? BJBGui.Button($"H##hold_{p.UIID}", BJBGui.OrangeHighlightTextColor) : BJBGui.Button($"H##hold_{p.UIID}");
            if (clickedHold)
            {
                p.HighlightPause = false;

                if (p.IsOnBench)
                {
                    if (currentPhase != GamePhase.DealerTurn && currentPhase != GamePhase.Payout)
                        GameEngine.MovePlayerFromBench(p);
                }
                else if (p.IsOnHold)
                {
                    if (isActiveRound)
                    {
                        p.IsOnHold = false;
                        p.IsOnBench = true;
                        p.BenchedAt = DateTime.UtcNow;
                        p.WasOnHoldThisRound = true;
                    }
                    else
                    {
                        p.IsOnHold = false;
                    }
                }
                else
                {
                    if (isActiveRound && GameEngine.CanMovePlayerToBench(p, _players))
                    {
                        bool wasCurrentTurn = p.IsCurrentTurn;
                        GameEngine.MovePlayerToBench(p, _players);
                        if (wasCurrentTurn)
                            GameEngine.NextTurn(_players, _config);
                    }
                    else
                    {
                        p.IsOnHold = !p.IsOnHold;
                        if (p.IsOnHold) p.ReadySkip = false;
                    }
                }
                _save();
            }

            if (colorsPushed > 0) ImGui.PopStyleColor(colorsPushed);
            if (isDisabled) ImGui.EndDisabled();

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                if (p.IsOnBench)
                    ImGui.SetTooltip("On bench - Click to return");
                else if (p.IsOnHold && isActiveRound)
                    ImGui.SetTooltip("On hold - Click to join via bench (late entry)");
                else if (p.IsOnHold)
                    ImGui.SetTooltip("On hold - Click to reactivate for next round");
                else if (isActiveRound)
                    ImGui.SetTooltip("Click to move to bench (pause during round)");
                else
                    ImGui.SetTooltip("Click to hold (skip next round)");
            }

            if (ImGui.GetIO().KeyCtrl && !p.IsDebugPlayer && !p.IsImaginaryPlayer)
            {
                ImGui.SameLine();
                bool hasImaginaryPlayer = _players.Any(
                    candidate =>
                        PlayerIdentityManager.References(candidate, p)
                );
                if (hasImaginaryPlayer) ImGui.BeginDisabled();
                if (BJBGui.SmallButton($"i##imaginary_{p.UIID}"))
                    AddImaginaryPlayer(p);
                if (hasImaginaryPlayer) ImGui.EndDisabled();

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip(
                        hasImaginaryPlayer ? "This player already has an imaginary player" : "Add an imaginary player controlled by this real player"
                    );
            }
        }

        ImGui.TableNextColumn();
        var hasAlias = !string.IsNullOrWhiteSpace(p.Alias);
        var listName = hasAlias ? p.Alias! : p.Name;
        var nameColor = hasAlias ? new Vector4(1f, 0.85f, 0.2f, 1f) : new Vector4(0.45f, 0.8f, 1f, 1f);

        var transparent = new Vector4(0f, 0f, 0f, 0f);
        ImGui.PushStyleColor(ImGuiCol.Text, nameColor);
        ImGui.PushStyleColor(ImGuiCol.Button, transparent);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, transparent);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, transparent);
        ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign, new Vector2(0f, 0.5f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
        var soundButtonWidth = 34f;
        var nameButtonWidth = MathF.Max(1f, ImGui.GetContentRegionAvail().X - soundButtonWidth - ImGui.GetStyle().ItemSpacing.X);
        var aliasClicked = ImGui.Button($"{listName}##alias_name_{p.UIID}", new Vector2(nameButtonWidth, 0f));
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(4);
        if (aliasClicked)
            OpenAliasPopupForPlayer(p);

        ImGui.SameLine();
        var soundLabel = p.SoundEffectId == 0 ? "S-" : $"S{p.SoundEffectId}";
        if (BJBGui.SmallButton($"{soundLabel}##player_sound_{p.UIID}"))
            ImGui.OpenPopup($"player_sound_popup_{p.UIID}");

        if (ImGui.IsItemHovered())
        {
            var soundStatus = p.SoundEffectId > 0 && !PlayerSoundEffects.IsAllowed(_config, p.SoundEffectId) ? " (currently unavailable)" : string.Empty;
            ImGui.SetTooltip($"Player sound: {p.SoundEffectId}{soundStatus}. Click to choose; scroll down to increase or up to decrease. 0 disables it.");
            var wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0f)
            {
                PlayerSoundEffects.SetPreference(_config, p, PlayerSoundEffects.NextAllowedSound(_config, p.SoundEffectId, wheel < 0f ? 1 : -1));
                _save();
                SaveSessionFromUI();
            }
        }

        if (ImGui.BeginPopup($"player_sound_popup_{p.UIID}"))
        {
            if (ImGui.Selectable("0 - Disabled", p.SoundEffectId == 0))
            {
                PlayerSoundEffects.SetPreference(_config, p, 0);
                _save();
                SaveSessionFromUI();
            }
            for (var soundId = 1; soundId <= PlayerSoundEffects.MaxSoundId; soundId++)
            {
                var allowed = PlayerSoundEffects.IsAllowed(_config, soundId);
                if (!allowed) ImGui.BeginDisabled();
                if (ImGui.Selectable($"{soundId}##select_player_sound_{soundId}", p.SoundEffectId == soundId))
                {
                    PlayerSoundEffects.SetPreference(_config, p, soundId);
                    _save();
                    SaveSessionFromUI();
                }
                if (!allowed) ImGui.EndDisabled();
            }
            ImGui.EndPopup();
        }

        ImGui.TableNextColumn();
        {
            var ringOptions = GetMoneyRingOptions();
            bool showMoneyRing = ringOptions.Length > 0;
            bool canTellPlayer = GameEngine.CanAcceptInterRoundDetectors();
            float tButtonWidth = 25f;
            float heartButtonWidth = 25f;
            float mButtonWidth = 25f;
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            float reservedSpacing = spacing * 3f + (showMoneyRing ? 18f + spacing : 0f);

            long bankBefore = p.Bank;
            bool removeRowAfterBankEdit = false;
            if (!_config.EnableBankInput) ImGui.BeginDisabled();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - tButtonWidth - heartButtonWidth - mButtonWidth - reservedSpacing);
            bool bankEdited = BJBGui.InputLongFormatted($"##bank_{p.UIID}", ref p.Bank);
            if (bankEdited)
            {
                _bankChanged.Add(p.UIID);
                _save();
            }
            if (ImGui.IsItemActivated()) _bankSnapshot[p.UIID] = bankBefore;
            if (ImGui.IsItemDeactivated())
            {
                bool bankChanged = _bankChanged.Remove(p.UIID);
                if (_bankSnapshot.TryGetValue(p.UIID, out long oldBank))
                {
                    if (bankChanged && p.Bank != oldBank)
                        ActivityLogManager.LogBankChange(p.DisplayName, oldBank, p.Bank);
                    _bankSnapshot.Remove(p.UIID);
                }
                if (bankChanged)
                {
                    SaveSessionFromUI();
                    if (TryRemoveEmptyInactiveGeneratedPlayer(p))
                        removeRowAfterBankEdit = true;
                    else
                        CompanionSyncManager.SendPlayerBankBetUpdate(_config, p);
                }
            }
            if (showMoneyRing)
            {
                ImGui.SameLine();
                var selection = _moneyRing.Draw($"bank_{p.UIID}", ringOptions);
                if (selection.HasValue && _config.EnableBankInput)
                {
                    var oldBank = p.Bank;
                    p.Bank = selection.Value.ApplyTo(oldBank);
                    if (p.Bank != oldBank)
                    {
                        ActivityLogManager.LogBankChange(p.DisplayName, oldBank, p.Bank);
                        _save();
                        SaveSessionFromUI();
                        if (TryRemoveEmptyInactiveGeneratedPlayer(p))
                            removeRowAfterBankEdit = true;
                        else
                            CompanionSyncManager.SendPlayerBankBetUpdate(_config, p);
                    }
                }
            }
            if (!_config.EnableBankInput) ImGui.EndDisabled();
            if (removeRowAfterBankEdit) { ImGui.PopID(); return; }

            ImGui.SameLine();
            if (!canTellPlayer) ImGui.BeginDisabled();
            bool hlTell = p.HighlightTell && canTellPlayer;
            bool clickedTell = hlTell ? BJBGui.SmallButtonHighlighted($"T##tell_{p.UIID}", _config.HighlightColor, _config.HighlightTextColor) : BJBGui.SmallButton($"T##tell_{p.UIID}");
            if (clickedTell)
            {
                p.HighlightTell = false;
                BankTellQueueManager.Enqueue(p, _config, "PlayerButton");
            }
            if (!canTellPlayer) ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("Post bank/bet info for this player to party chat");

            {
                ImGui.SameLine();
                bool hasUndo = _bankToTipUndo.TryGetValue(p.UIID, out var undoEntry)
                               && (DateTime.Now - undoEntry.clickedAt).TotalSeconds < 10;
                if (hasUndo)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.5f, 0.0f, 1f));
                    if (BJBGui.SmallButton($"U##heart_{p.UIID}", BJBGui.OrangeHighlightTextColor))
                    {
                        p.Bank = undoEntry.amount;
                        StatsManager.AddTip(-undoEntry.amount);
                        _bankToTipUndo.Remove(p.UIID);
                        SaveSessionFromUI();
                        CompanionSyncManager.SendPlayerBankBetUpdate(_config, p);
                    }
                    ImGui.PopStyleColor();
                    if (ImGui.IsItemHovered())
                    {
                        int remaining = Math.Max(0, 10 - (int)(DateTime.Now - undoEntry.clickedAt).TotalSeconds);
                        ImGui.SetTooltip($"Undo Bank->Tip ({remaining}s)");
                    }
                }
                else
                {
                    _bankToTipUndo.Remove(p.UIID);
                    bool canHeart = StatsManager.IsRunning && p.Bank > 0;
                    bool ctrlDown = ImGui.GetIO().KeyCtrl;
                    if (!canHeart || !ctrlDown) ImGui.BeginDisabled();
                    ImGui.PushFont(UiBuilder.IconFont);
                    if (BJBGui.SmallButton(FontAwesomeIcon.Heart.ToIconString() + $"##heart_{p.UIID}"))
                    {
                        long amount = p.Bank;
                        StatsManager.AddTip(amount);
                        p.Bank = 0;
                        _bankToTipUndo[p.UIID] = (amount, DateTime.Now);
                        SaveSessionFromUI();
                        CompanionSyncManager.SendPlayerBankBetUpdate(_config, p);
                    }
                    ImGui.PopFont();
                    if (!canHeart || !ctrlDown) ImGui.EndDisabled();
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    {
                        if (!StatsManager.IsRunning)
                            ImGui.SetTooltip("Start a stats session first");
                        else if (!ctrlDown)
                            ImGui.SetTooltip("Hold CTRL to transfer bank to tips");
                        else
                            ImGui.SetTooltip("Transfer entire bank to tips");
                    }
                }

                ImGui.SameLine();
                if (BJBGui.SmallButton($"M##maxbet_{p.UIID}"))
                {
                    p.CurrentBet = Math.Min(p.GetEffectiveMaxBet(_config), p.Bank);
                    _save();
                    SaveSessionFromUI();
                    CompanionSyncManager.SendPlayerBankBetUpdate(_config, p);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"Set bet to lower value of max bet ({p.GetEffectiveMaxBet(_config):N0}) or bank ({p.Bank:N0})");
            }
        }

        ImGui.TableNextColumn();
        long effectiveMaxBet = p.GetEffectiveMaxBet(_config);
        bool betOutOfRange = p.CurrentBet < _config.MinBet || p.CurrentBet > effectiveMaxBet;
        bool hlBet = p.HighlightBet;
        if (hlBet)
            ImGui.PushStyleColor(ImGuiCol.FrameBg, _config.HighlightColor);
        long betBefore = p.CurrentBet;
        var betRingOptions = GetMoneyRingOptions();
        bool showBetRing = betRingOptions.Length > 0;
        ImGui.SetNextItemWidth(
            showBetRing ? Math.Max(32f, ImGui.GetContentRegionAvail().X - 18f - ImGui.GetStyle().ItemSpacing.X) : -1f
        );
        Vector4? betTextColor = betOutOfRange ? new Vector4(1f, 0.55f, 0.1f, 1f) : hlBet ? _config.HighlightTextColor : null;
        bool betEdited = BJBGui.InputLongFormatted($"##bet_{p.UIID}", ref p.CurrentBet, betTextColor);
        if (betEdited)
        {
            p.HighlightBet = false;
            _betChanged.Add(p.UIID);
            _save();
        }
        if (ImGui.IsItemActivated()) _betSnapshot[p.UIID] = betBefore;
        if (ImGui.IsItemDeactivated())
        {
            bool betChanged = _betChanged.Remove(p.UIID);
            if (_betSnapshot.TryGetValue(p.UIID, out long oldBet))
            {
                if (betChanged && p.CurrentBet != oldBet)
                    ActivityLogManager.LogBetSet(p.DisplayName, p.CurrentBet);
                _betSnapshot.Remove(p.UIID);
            }
            if (betChanged)
            {
                SaveSessionFromUI();
                CompanionSyncManager.SendPlayerBankBetUpdate(_config, p);
            }
        }
        if (showBetRing)
        {
            ImGui.SameLine();
            var selection = _moneyRing.Draw($"bet_{p.UIID}", betRingOptions);
            if (selection.HasValue)
            {
                var oldBet = p.CurrentBet;
                p.CurrentBet = selection.Value.ApplyTo(oldBet);
                if (p.CurrentBet != oldBet)
                {
                    p.HighlightBet = false;
                    ActivityLogManager.LogBetSet(p.DisplayName, p.CurrentBet);
                    _save();
                    SaveSessionFromUI();
                    CompanionSyncManager.SendPlayerBankBetUpdate(_config, p);
                }
            }
        }
        if (hlBet) ImGui.PopStyleColor();

        ImGui.TableNextColumn();
        DrawMultiHandCards(p);
        ImGui.TableNextColumn();
        DrawMultiHandPoints(p);
        if (_config.PlayerRollingForThemselves)
        {
            ImGui.TableNextColumn();
            bool selfRoll = PlayerRollPreferenceManager.GetPreference(_config, p);
            if (ImGui.Checkbox($"##self_roll_{p.UIID}", ref selfRoll))
            {
                PlayerRollPreferenceManager.SetPreference(_config, p, selfRoll);
                _save();
                SaveSessionFromUI();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(
                    selfRoll ? "This player rolls their own 13-sided dice." : "The dealer system rolls automatically for this player."
                );
        }
        ImGui.TableNextColumn();
        DrawPlayerControls(p);

        ImGui.PopID();
    }
}
