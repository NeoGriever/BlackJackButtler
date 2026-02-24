using System;
using System.Numerics;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using System.Threading.Tasks;
using BlackJackButtler.Chat;
using BlackJackButtler.Regex;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawMainPage()
    {
        DrawMainHeader();
        ImGui.Separator();
        DrawCustomButtonBar();

        ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), $"DEALER - Phase: {GameEngine.CurrentPhase}");

        if (ImGui.BeginTable("bjb_dealer_table", 4, ImGuiTableFlags.Borders))
        {

            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 150);
            ImGui.TableSetupColumn("Cards", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            ImGui.TableSetupColumn("Points", ImGuiTableColumnFlags.WidthFixed, 55);
            ImGui.TableSetupColumn("Controls", ImGuiTableColumnFlags.WidthFixed, 350);

            ImGui.TableNextRow();
            DrawDealerRow();
            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.4f, 0.8f, 1, 1), "PLAYERS");
        ImGui.SameLine();
        {
            var tellPhase = GameEngine.CurrentPhase;
            bool canTell = (tellPhase == GamePhase.Waiting || tellPhase == GamePhase.Payout) && !CommandExecutor.IsRunning;
            if (!canTell) ImGui.BeginDisabled();
            if (BJBGui.SmallButton("Bank /tell"))
            {
                var snapshot = _players.Where(p => p.IsActivePlayer && !p.IsOnHold).ToList();
                Task.Run(async () => {
                    foreach (var p in snapshot)
                    {
                        GameEngine.TargetPlayer(p.Name);
                        VariableManager.SetPlayerVariables(p);
                        await CommandExecutor.ExecuteGroup("BankTell", p.DisplayName, _config);
                    }
                    GameEngine.TargetPlayer(_dealer.Name);
                });
            }
            if (!canTell) ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("Post bank/bet info for all active players to party chat");
        }
        {
            float checkboxSize = ImGui.GetFrameHeight();
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            float rightEdge = ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX();

            if (!_notepadWindow.IsOpen)
            {
                ImGui.SameLine(rightEdge - checkboxSize - spacing - checkboxSize);
                ImGui.PushFont(UiBuilder.IconFont);
                if (BJBGui.Button(FontAwesomeIcon.StickyNote.ToIconString() + "##notepad_btn", new Vector2(checkboxSize, checkboxSize)))
                {
                    if (!_notepadLoaded) { _notepadLoaded = true; _notepadWindow.LoadContent(); }
                    _notepadWindow.IsOpen = true;
                }
                ImGui.PopFont();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Open Notepad");
            }

            ImGui.SameLine(rightEdge - checkboxSize);
            if (ImGui.Checkbox("##enable_bank_input", ref _config.EnableBankInput)) _save();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Enable Bank input");
        }
        if (ImGui.BeginTable("bjb_main_table", 10, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY))
        {
            SetupTableColumns();
            ImGui.TableHeadersRow();

            var playerSnapshot = _players.ToList();
            foreach (var player in playerSnapshot)
            {
                ImGui.TableNextRow();
                DrawPlayerRow(player, false);
            }
            ImGui.EndTable();
        }

        if (_triggerAliasPopup)
        {
            ImGui.OpenPopup("bjb_alias_popup");
            _isAliasModalOpen = true;
            _triggerAliasPopup = false;
        }

        DrawAliasModal();

        if (_triggerVipConfirmPopup)
        {
            ImGui.OpenPopup("bjb_vip_confirm_popup");
            _isVipConfirmOpen = true;
            _triggerVipConfirmPopup = false;
        }
        DrawVipConfirmModal();

        if (_triggerVenuePopup)
        {
            ImGui.OpenPopup("bjb_venue_name_popup");
            _isVenuePopupOpen = true;
            _triggerVenuePopup = false;
        }
        DrawVenueNameModal();
    }

    private void DrawDealerRow()
    {
        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(0.05f, 0.05f, 0.05f, 1f)));

        ImGui.PushID("dealer_row");

        ImGui.TableNextColumn();
        var nameColor = new Vector4(1f, 1f, 0.2f, 1f);
        ImGui.TextColored(nameColor, _dealer.DisplayName);

        ImGui.TableNextColumn();
        DrawMultiHandCards(_dealer);

        ImGui.TableNextColumn();
        DrawMultiHandPoints(_dealer);

        ImGui.TableNextColumn();
        DrawDealerControls();

        ImGui.PopID();
    }

    private void DrawAliasModal()
    {
        if (ImGui.BeginPopupModal("bjb_alias_popup", ref _isAliasModalOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (_editingAliasPlayer == null)
            {
                _isAliasModalOpen = false;
                ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
                return;
            }

            ImGui.Text($"Set Alias for: {_editingAliasPlayer.Name}");
            ImGui.Spacing();

            ImGui.SetNextItemWidth(250f);
            ImGui.InputText("##alias_input", ref _aliasInputBuffer, 32);

            ImGui.Spacing();
            if (BJBGui.Button("Save", new Vector2(120, 0)))
            {
                var input = _aliasInputBuffer.Trim();
                if (string.IsNullOrWhiteSpace(input) || input.Equals(_editingAliasPlayer.Name, StringComparison.OrdinalIgnoreCase))
                    _editingAliasPlayer.Alias = string.Empty;
                else
                    _editingAliasPlayer.Alias = input;

                _editingAliasPlayer = null;
                _isAliasModalOpen = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (BJBGui.Button("Cancel", new Vector2(120, 0)))
            {
                _editingAliasPlayer = null;
                _isAliasModalOpen = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    private void DrawVipConfirmModal()
    {
        if (ImGui.BeginPopupModal("bjb_vip_confirm_popup", ref _isVipConfirmOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (_vipConfirmPlayer == null)
            {
                _isVipConfirmOpen = false;
                ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
                return;
            }

            string oldState = _vipConfirmOldTier == 0 || _vipConfirmOldTier > _config.VipBetTiers.Count
                ? "No VIP"
                : _config.VipBetTiers[_vipConfirmOldTier - 1].Name;
            string newState = _vipConfirmNewTier == 0 || _vipConfirmNewTier > _config.VipBetTiers.Count
                ? "No VIP"
                : _config.VipBetTiers[_vipConfirmNewTier - 1].Name;

            ImGui.Text($"Change VIP of {_vipConfirmPlayer.DisplayName} from {oldState} to {newState}?");
            ImGui.Spacing();

            if (BJBGui.Button("Yes", new Vector2(120, 0)))
            {
                ApplyVipChange(_vipConfirmPlayer, _vipConfirmNewTier);
                _vipConfirmPlayer = null;
                _isVipConfirmOpen = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (BJBGui.Button("No", new Vector2(120, 0)))
            {
                _vipConfirmPlayer = null;
                _isVipConfirmOpen = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    private void ApplyVipChange(PlayerState player, int newTier)
    {
        var venue = VenueManager.GetCurrentVenue();
        string worldName = VenueManager.ResolveWorldName(player.WorldId);

        if (venue != null)
        {
            VenueManager.SetPlayerTier(venue, player.Name, worldName, newTier);
        }
        else
        {
            var addr = VenueManager.GetCurrentAddress();
            _pendingVenueAddress = addr ?? new VenueAddress();
            _pendingVipPlayer = player;
            _pendingVipTier = newTier;
            _venueNameBuffer = VenueManager.GetNextVenueName();
            _triggerVenuePopup = true;
        }
    }

    private void DrawVenueNameModal()
    {
        if (ImGui.BeginPopupModal("bjb_venue_name_popup", ref _isVenuePopupOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (_pendingVipPlayer == null)
            {
                _isVenuePopupOpen = false;
                ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
                return;
            }

            ImGui.Text("No venue found for current location.");
            ImGui.Text("Enter a name for this venue:");
            ImGui.Spacing();

            ImGui.SetNextItemWidth(250f);
            ImGui.InputText("##venue_name_input", ref _venueNameBuffer, 64);

            if (_pendingVenueAddress != null && !string.IsNullOrEmpty(_pendingVenueAddress.Housing))
            {
                ImGui.TextDisabled($"{_pendingVenueAddress.Housing}, Ward {_pendingVenueAddress.Ward}, Plot {_pendingVenueAddress.Plot} ({_pendingVenueAddress.World})");
            }
            else
            {
                ImGui.TextDisabled("Not in housing area");
            }

            ImGui.Spacing();
            if (BJBGui.Button("Save", new Vector2(120, 0)))
            {
                var name = _venueNameBuffer.Trim();
                if (string.IsNullOrWhiteSpace(name)) name = VenueManager.GetNextVenueName();

                var venue = VenueManager.FindOrCreateVenue(_pendingVenueAddress ?? new VenueAddress(), name);
                string worldName = VenueManager.ResolveWorldName(_pendingVipPlayer.WorldId);
                VenueManager.SetPlayerTier(venue, _pendingVipPlayer.Name, worldName, _pendingVipTier);

                _pendingVipPlayer = null;
                _pendingVenueAddress = null;
                _isVenuePopupOpen = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (BJBGui.Button("Cancel", new Vector2(120, 0)))
            {
                _pendingVipPlayer = null;
                _pendingVenueAddress = null;
                _isVenuePopupOpen = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    private void SetupTableColumns()
    {
        ImGui.TableSetupColumn("V", ImGuiTableColumnFlags.WidthFixed, 25);
        ImGui.TableSetupColumn("A", ImGuiTableColumnFlags.WidthFixed, 25);
        ImGui.TableSetupColumn("J", ImGuiTableColumnFlags.WidthFixed, 25);
        ImGui.TableSetupColumn("P", ImGuiTableColumnFlags.WidthFixed, 25);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 130);
        ImGui.TableSetupColumn("Bank", ImGuiTableColumnFlags.WidthStretch, 1.0f);
        ImGui.TableSetupColumn("Bet", ImGuiTableColumnFlags.WidthStretch, 1.0f);
        ImGui.TableSetupColumn("Cards", ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn("Points", ImGuiTableColumnFlags.WidthFixed, 55);
        ImGui.TableSetupColumn("Controls", ImGuiTableColumnFlags.WidthFixed, 200);
    }

    private void DrawMainHeader()
    {
        var io = ImGui.GetIO();
        bool canStop = io.KeyCtrl && io.KeyShift;

        var recon_text = IsRecognitionActive ? "● Group Detector" : "○ Group Detector";
        var auto_deal_text = _config.AutoInitialDeal ? "● Auto Player Hand" : "○ Auto Player Hand";
        var auto_dealer_text = _config.AutoDealerDraw ? "● Auto Dealer Draw" : "○ Auto Dealer Draw";

        if (BJBGui.Button(recon_text, new Vector2(200, 0)))
        {
            IsRecognitionActive = !IsRecognitionActive;
            if (IsRecognitionActive)
            {
                SyncParty();
                ViewDirectionManager.CaptureCurrentRotation(_config);
            }

            if (!IsRecognitionActive)
            {
                SessionManager.ClearSession();
                _players.RemoveAll(p => !p.IsActivePlayer && p.Bank == 0);
                AddDebugLog("[SessionManager] Session cleared (Group Detector deactivated)", false);
            }
        }

        ImGui.SameLine();

        bool autoDealActive = _config.AutoInitialDeal;
        if (autoDealActive) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.5f, 0.0f, 1.0f));

        if (BJBGui.Button(auto_deal_text))
        {
            _config.AutoInitialDeal = !_config.AutoInitialDeal;
            _save();
        }

        if (autoDealActive) ImGui.PopStyleColor();


        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Automatically deals the initial hand to players during the Initial Deal phase.");

        ImGui.SameLine();

        bool autoDealerActive = _config.AutoDealerDraw;
        if (autoDealerActive) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.5f, 0.0f, 1.0f));

        if (BJBGui.Button(auto_dealer_text))
        {
            _config.AutoDealerDraw = !_config.AutoDealerDraw;
            _save();
        }

        if (autoDealerActive) ImGui.PopStyleColor();

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"Automatically draws cards for the dealer until {_config.DealerDrawsUntil}, then stands.");

        bool hasAutoTriggers = _config.UserRegexes.Any(r =>
            r.Enabled && r.Mode == RegexEntryMode.Trigger &&
            (r.Action == RegexAction.WantHit || r.Action == RegexAction.WantStand ||
             r.Action == RegexAction.WantDD || r.Action == RegexAction.WantSplit));

        if (hasAutoTriggers)
        {
            ImGui.SameLine();

            var auto_run_text = _config.AutoRun ? "● Auto Run" : "○ Auto Run";
            bool autoRunActive = _config.AutoRun;
            if (autoRunActive) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.5f, 0.0f, 1.0f));

            if (BJBGui.Button(auto_run_text))
            {
                _config.AutoRun = !_config.AutoRun;
                _save();
            }

            if (autoRunActive) ImGui.PopStyleColor();

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("When ON, player action triggers (Hit/Stand/DD/Split) execute automatically.\nWhen OFF, they highlight the corresponding button instead.");
        }

        {
            var enabledWebhooks = _config.Webhooks.FindAll(w => w.Enabled);
            if (enabledWebhooks.Count > 0)
            {
                float comboWidth = 150f;
                float rightEdge = ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX();
                ImGui.SameLine(rightEdge - comboWidth);

                var phase = GameEngine.CurrentPhase;
                bool locked = phase != GamePhase.Waiting && phase != GamePhase.Payout;
                if (locked) ImGui.BeginDisabled();

                var labels = new string[enabledWebhooks.Count + 1];
                labels[0] = "None";
                for (int i = 0; i < enabledWebhooks.Count; i++)
                    labels[i + 1] = enabledWebhooks[i].Name;

                int comboIndex = _selectedWebhookIndex + 1;
                ImGui.SetNextItemWidth(comboWidth);
                if (BJBGui.Combo("##webhook_select", ref comboIndex, labels, labels.Length))
                    _selectedWebhookIndex = comboIndex - 1;

                if (locked) ImGui.EndDisabled();
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip(locked ? "Webhook selection is locked during an active round" : "Select a Discord webhook for round results");
            }
        }

        if (IsRecognitionActive && !IsLocalPlayerPartyLeader())
        {
            var leaderName = GetPartyLeaderName();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.6f, 0.2f, 1.0f));
            ImGui.TextUnformatted($"You're not the group leader. Current leader is {leaderName}");
            ImGui.PopStyleColor();
            if (ImGui.IsItemClicked())
                GameEngine.TargetPlayer(leaderName);
        }

        if (IsRecognitionActive && _config.CurrentLevel == UserLevel.Dev
            && (!IsLocalPlayerPartyLeader() || Plugin.PartyList.Length == 0))
        {
            if (BJBGui.SmallButton("Activate debug mode"))
            {
                IsRecognitionActive = false;
                SessionManager.ClearSession();
                EnableDebugMode();
            }
        }
    }

    private void DrawCustomButtonBar()
    {
        if (_config.CustomCommandGroups.Count == 0) return;

        bool isRunning = CommandExecutor.IsRunning;
        if (isRunning) ImGui.BeginDisabled();

        for (int i = 0; i < _config.CustomCommandGroups.Count; i++)
        {
            var group = _config.CustomCommandGroups[i];
            if (i > 0) ImGui.SameLine();
            if (BJBGui.SmallButton($"{group.Name}##custom_{i}"))
            {
                var targetName = _players.FirstOrDefault(p => p.IsCurrentTurn)?.DisplayName
                              ?? _dealer.DisplayName;
                var groupName = group.Name;
                Task.Run(() => CommandExecutor.ExecuteGroup(groupName, targetName, _config));
            }
        }

        if (isRunning) ImGui.EndDisabled();
        ImGui.Spacing();
    }

    private bool IsLocalPlayerPartyLeader()
    {
        var localName = Plugin.ObjectTable.LocalPlayer?.Name.TextValue;
        if (string.IsNullOrEmpty(localName) || Plugin.PartyList.Length == 0)
            return true;
        var leader = Plugin.PartyList[(int)Plugin.PartyList.PartyLeaderIndex];
        return leader != null
            && string.Equals(leader.Name.TextValue, localName, StringComparison.Ordinal);
    }

    private string GetPartyLeaderName()
    {
        if (Plugin.PartyList.Length == 0) return string.Empty;
        var leader = Plugin.PartyList[(int)Plugin.PartyList.PartyLeaderIndex];
        return leader?.Name.TextValue ?? string.Empty;
    }

    private void DrawPlayerRow(PlayerState p, bool isDealer)
    {
        uint bgColor;
        if (!p.IsActivePlayer) bgColor = ImGui.GetColorU32(new Vector4(0, 0, 0, 1));
        else if (p.IsCurrentTurn) bgColor = ImGui.GetColorU32(new Vector4(0.0f, 0.25f, 0.0f, 1f));
        else if (p.IsOnHold) bgColor = ImGui.GetColorU32(new Vector4(0.15f, 0.15f, 0.15f, 1f));
        else bgColor = ImGui.GetColorU32(new Vector4(0.0f, 0.0f, 0.2f, 1f));

        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, bgColor);
        ImGui.PushID(p.Name);

        ImGui.TableNextColumn();
        if (p.IsActivePlayer && _config.VipBetTiers.Count > 0)
        {
            var venue = VenueManager.GetCurrentVenue();
            string worldName = VenueManager.ResolveWorldName(p.WorldId);
            int currentTier = venue != null ? VenueManager.GetPlayerTier(venue, p.Name, worldName) : 0;

            bool isVip = currentTier > 0;
            var btnColor = isVip
                ? new Vector4(1.0f, 0.6f, 0.0f, 1f)
                : new Vector4(0.35f, 0.22f, 0.08f, 1f);
            var txtColor = isVip
                ? new Vector4(0.8f, 0.1f, 0.1f, 1f)
                : new Vector4(0.5f, 0.5f, 0.5f, 1f);

            ImGui.PushStyleColor(ImGuiCol.Button, btnColor);
            ImGui.PushStyleColor(ImGuiCol.Text, txtColor);

            if (BJBGui.Button($"V##vip_{p.UIID}"))
            {
                int newTier = isVip ? 0 : 1;
                _vipConfirmPlayer = p;
                _vipConfirmOldTier = currentTier;
                _vipConfirmNewTier = newTier;
                _triggerVipConfirmPopup = true;
            }

            ImGui.PopStyleColor(2);

            if (ImGui.BeginPopupContextItem($"vip_ctx_{p.UIID}"))
            {
                if (ImGui.Selectable("No VIP"))
                {
                    _vipConfirmPlayer = p;
                    _vipConfirmOldTier = currentTier;
                    _vipConfirmNewTier = 0;
                    _triggerVipConfirmPopup = true;
                }
                for (int ti = 0; ti < _config.VipBetTiers.Count; ti++)
                {
                    var tier = _config.VipBetTiers[ti];
                    bool selected = currentTier == ti + 1;
                    if (ImGui.Selectable($"{tier.Name} (Max: {tier.MaxBet:N0})", selected))
                    {
                        _vipConfirmPlayer = p;
                        _vipConfirmOldTier = currentTier;
                        _vipConfirmNewTier = ti + 1;
                        _triggerVipConfirmPopup = true;
                    }
                }
                ImGui.EndPopup();
            }

            if (ImGui.IsItemHovered())
            {
                if (isVip && currentTier <= _config.VipBetTiers.Count)
                    ImGui.SetTooltip($"{_config.VipBetTiers[currentTier - 1].Name} (Max: {_config.VipBetTiers[currentTier - 1].MaxBet:N0})\nRight-click for tier selection");
                else
                    ImGui.SetTooltip("Click to set VIP\nRight-click for tier selection");
            }
        }

        ImGui.TableNextColumn();
        if (p.IsActivePlayer) {
            bool hlAlias = p.HighlightAlias;
            if (hlAlias)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, _config.HighlightColor);
                ImGui.PushStyleColor(ImGuiCol.Text, _config.HighlightTextColor);
            }
            if (BJBGui.Button($"A##alias_btn_{p.UIID}")) {
                p.HighlightAlias = false;
                _editingAliasPlayer = p;
                _aliasInputBuffer = !string.IsNullOrWhiteSpace(p.Alias) ? p.Alias : p.Name;
                _triggerAliasPopup = true;
            }
            if (hlAlias) ImGui.PopStyleColor(2);
        }

        ImGui.TableNextColumn();
        if (!p.IsActivePlayer) {
            bool hlJoin = p.HighlightJoin;
            if (hlJoin)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, _config.HighlightColor);
                ImGui.PushStyleColor(ImGuiCol.Text, _config.HighlightTextColor);
            }
            if (BJBGui.Button($">##{p.UIID}", new Vector2(-1, 0)))
            {
                p.HighlightJoin = false;
                p.IsActivePlayer = true;
                var joinPhase = GameEngine.CurrentPhase;
                if (joinPhase == GamePhase.InitialDeal || joinPhase == GamePhase.PlayersTurn || joinPhase == GamePhase.DealerTurn)
                    p.JoinedMidRound = true;
                ActivityLogManager.LogPlayerJoin(p.DisplayName);
            }
            if (hlJoin) ImGui.PopStyleColor(2);
        }
        else {
            bool hlLeave = p.HighlightLeave;
            if (hlLeave)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, _config.HighlightColor);
                ImGui.PushStyleColor(ImGuiCol.Text, _config.HighlightTextColor);
            }
            if (BJBGui.Button($"X##{p.UIID}", new Vector2(-1, 0))) {
                p.HighlightLeave = false;
                var leavePhase = GameEngine.CurrentPhase;
                if (leavePhase == GamePhase.InitialDeal || leavePhase == GamePhase.PlayersTurn || leavePhase == GamePhase.DealerTurn)
                {
                    GameEngine.DeactivatePlayerMidRound(p, _players, _config);
                }
                else
                {
                    if (p.Bank == 0) ActivityLogManager.LogPlayerLeave(p.DisplayName);
                    p.IsActivePlayer = false;
                    p.IsCurrentTurn = false;
                }
            }
            if (hlLeave) ImGui.PopStyleColor(2);
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
                ImGui.PushStyleColor(ImGuiCol.Button, _config.HighlightColor);
                ImGui.PushStyleColor(ImGuiCol.Text, _config.HighlightTextColor);
                colorsPushed = 2;
            }

            if (BJBGui.Button($"H##hold_{p.UIID}"))
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
        }

        ImGui.TableNextColumn();
        var nameColor = p.IsCurrentTurn ? new Vector4(1f, 1f, 0.2f, 1f) : new Vector4(1, 1, 1, 1);
        ImGui.TextColored(nameColor, p.DisplayName);

        ImGui.TableNextColumn();
        {
            var phase = GameEngine.CurrentPhase;
            bool canTellPlayer = (phase == GamePhase.Waiting || phase == GamePhase.Payout) && !CommandExecutor.IsRunning;
            float tButtonWidth = 25f;

            // Bank Input (mit Sperre und Delta-Tracking)
            long bankBefore = p.Bank;
            if (!_config.EnableBankInput) ImGui.BeginDisabled();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - tButtonWidth - ImGui.GetStyle().ItemSpacing.X);
            if (BJBGui.InputLong($"##bank_{p.UIID}", ref p.Bank, 1000, 10000)) _save();
            if (ImGui.IsItemActivated()) _bankSnapshot[p.UIID] = bankBefore;
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                if (_bankSnapshot.TryGetValue(p.UIID, out long oldBank))
                {
                    long delta = p.Bank - oldBank;
                    if (delta > 0) StatsManager.RecordIncome(delta);
                    else if (delta < 0) StatsManager.RecordExpense(-delta);
                    ActivityLogManager.LogBankChange(p.DisplayName, oldBank, p.Bank);
                    _bankSnapshot.Remove(p.UIID);
                }
            }
            if (!_config.EnableBankInput) ImGui.EndDisabled();

            // Tell Button
            ImGui.SameLine();
            if (!canTellPlayer) ImGui.BeginDisabled();
            bool hlTell = p.HighlightTell && canTellPlayer;
            if (hlTell)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, _config.HighlightColor);
                ImGui.PushStyleColor(ImGuiCol.Text, _config.HighlightTextColor);
            }
            if (BJBGui.SmallButton($"T##tell_{p.UIID}"))
            {
                p.HighlightTell = false;
                Task.Run(async () => {
                    GameEngine.TargetPlayer(p.Name);
                    VariableManager.SetPlayerVariables(p);
                    await CommandExecutor.ExecuteGroup("BankTell", p.DisplayName, _config);
                    GameEngine.TargetPlayer(_dealer.Name);
                });
            }
            if (hlTell) ImGui.PopStyleColor(2);
            if (!canTellPlayer) ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("Post bank/bet info for this player to party chat");
        }

        ImGui.TableNextColumn();
        long effectiveMaxBet = p.GetEffectiveMaxBet(_config);
        bool betOutOfRange = p.CurrentBet < _config.MinBet || p.CurrentBet > effectiveMaxBet;
        if (betOutOfRange)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
            if (BJBGui.SmallButton($"!##bet_warn_{p.UIID}"))
            {
                _page = Page.Settings;
                _pendingSettingsFocus = p.CurrentBet < _config.MinBet ? "min_bet" : "max_bet";
            }
            ImGui.PopStyleColor();
            if (ImGui.IsItemHovered())
            {
                if (p.CurrentBet < _config.MinBet)
                    ImGui.SetTooltip($"Bet is below minimum ({_config.MinBet:N0})");
                else
                {
                    var vipName = p.GetVipTierName(_config);
                    if (!string.IsNullOrEmpty(vipName))
                        ImGui.SetTooltip($"Bet is above {vipName} maximum ({effectiveMaxBet:N0})");
                    else
                        ImGui.SetTooltip($"Bet is above maximum ({effectiveMaxBet:N0})");
                }
            }
            ImGui.SameLine();
        }
        bool hlBet = p.HighlightBet;
        if (hlBet)
        {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, _config.HighlightColor);
            ImGui.PushStyleColor(ImGuiCol.Text, _config.HighlightTextColor);
        }
        long betBefore = p.CurrentBet;
        ImGui.SetNextItemWidth(-1);
        if (BJBGui.InputLong($"##bet_{p.UIID}", ref p.CurrentBet, 500, 5000))
        {
            p.HighlightBet = false;
            _save();
        }
        if (ImGui.IsItemActivated()) _betSnapshot[p.UIID] = betBefore;
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            if (_betSnapshot.TryGetValue(p.UIID, out long oldBet) && p.CurrentBet != oldBet)
            {
                ActivityLogManager.LogBetSet(p.DisplayName, p.CurrentBet);
                _betSnapshot.Remove(p.UIID);
            }
        }
        if (hlBet) ImGui.PopStyleColor(2);

        ImGui.TableNextColumn();
        DrawMultiHandCards(p);
        ImGui.TableNextColumn();
        DrawMultiHandPoints(p);
        ImGui.TableNextColumn();
        DrawPlayerControls(p);

        ImGui.PopID();
    }

    private void DrawMultiHandCards(PlayerState p)
    {
        if (p.Hands.Count == 0) { ImGui.Text("-"); return; }
        for (int i = 0; i < p.Hands.Count; i++)
        {
            var cards = p.Hands[i].Cards;
            if (cards.Count == 0) {
                if (p.IsCurrentTurn && p.CurrentHandIndex == i) ImGui.TextColored(new Vector4(1, 1, 0, 1), "[-]");
                else ImGui.Text(" - ");
                continue;
            }

            foreach (var card in cards)
            {
                Vector4 color = (card.Suit == CardSuit.Diamonds || card.Suit == CardSuit.Hearts)
                    ? new Vector4(1, 0.3f, 0.3f, 1)
                    : new Vector4(0.9f, 0.9f, 0.9f, 1);

                ImGui.TextColored(color, card.ToString());
                ImGui.SameLine(0, 4);
            }
            ImGui.Text(" ");
        }
    }

    private void DrawMultiHandPoints(PlayerState p)
    {
        if (p.Hands.Count == 0) { ImGui.Text("-"); return; }

        for (int i = 0; i < p.Hands.Count; i++)
        {
            var hand = p.Hands[i];
            if (hand.Cards.Count == 0) { ImGui.Text("-"); continue; }

            var (min, max) = p.CalculatePoints(i);
            int best = max.HasValue ? max.Value : min;

            if (best == 21)
            {
                if (hand.IsNaturalBlackJack)
                {
                    ImGui.TextColored(new Vector4(0.0f, 1.0f, 0.0f, 1.0f), "BJ");
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Natural BlackJack (2 Cards)");
                }
                else
                {
                    ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.0f, 1.0f), "BJ");
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Dirty BlackJack (3+ Cards)");
                }
            }
            else if (hand.IsBust)
            {
                var color = new Vector4(1.0f, 0.2f, 0.2f, 1.0f);
                ImGui.TextColored(color, $"{best}");

                Vector2 minPos = ImGui.GetItemRectMin();
                Vector2 maxPos = ImGui.GetItemRectMax();

                float midY = minPos.Y + (maxPos.Y - minPos.Y) * 0.5f;

                ImGui.GetWindowDrawList().AddLine(
                    new Vector2(minPos.X, midY),
                    new Vector2(maxPos.X, midY),
                    ImGui.GetColorU32(color),
                    1.5f
                );

                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Bust!");
            }
            else
            {
                var display = max.HasValue ? $"{min}/{max}" : $"{min}";
                ImGui.Text(display);
            }
        }
    }

    private void DrawPlayerControls(PlayerState p)
    {
        bool globalLock = CommandExecutor.IsRunning || _showSplitMoneyPopup || _showDDMoneyPopup;
        if (globalLock) ImGui.BeginDisabled();

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
                    GameEngine.TargetPlayer(p.Name);
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
            if (shouldHighlight)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, _config.HighlightColor);
                ImGui.PushStyleColor(ImGuiCol.Text, _config.HighlightTextColor);
            }
            if (BJBGui.SmallButton($"Pay Out##{p.UIID}"))
            {
                p.HighlightPay = false;
                DropboxIntegration.PayOut(p);
            }
            if (shouldHighlight) ImGui.PopStyleColor(2);
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
                    Task.Run(() => GameEngine.ActionDealHand(p, _config, _players));
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

            bool canHit = min < 21 && !currentHand.IsDoubleDown && !currentHand.IsStand;

            bool canDD = canHit && currentHand.Cards.Count == 2;
            if (isSplitHand && !_config.AllowDoubleDownAfterSplit) canDD = false;

            bool canSplit = false;
            if (canHit && currentHand.Cards.Count == 2 && p.Hands.Count < _config.MaxHandsPerPlayer)
            {
                if (_config.IdenticalSplitOnly)
                canSplit = currentHand.Cards[0].Value == currentHand.Cards[1].Value;
                else
                canSplit = PlayerState.GetCardScoreValue(currentHand.Cards[0].Value) == PlayerState.GetCardScoreValue(currentHand.Cards[1].Value);
            }

            bool canStand = !currentHand.IsStand && !currentHand.IsBust;

            HighlightActionButton(p, "Draw", ref p.HighlightHit, canHit, () =>
            {
                BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, $"PlayerHit:{p.Name}");
                Task.Run(() => GameEngine.ActionHit(p, _config, _players));
            });
            ImGui.SameLine();

            HighlightActionButton(p, "DD", ref p.HighlightDD, canDD, () =>
            {
                BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, $"PlayerDD:{p.Name}");
                Task.Run(() => GameEngine.ActionDD(p, _config, _players));
            });
            ImGui.SameLine();

            HighlightActionButton(p, "Spl", ref p.HighlightSplit, canSplit, () =>
            {
                BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, $"PlayerSplit:{p.Name}");
                Task.Run(() => GameEngine.ActionSplit(p, _config, _players));
            });
            ImGui.SameLine();

            HighlightActionButton(p, "Stand", ref p.HighlightStand, canStand, () =>
            {
                BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, $"PlayerStand:{p.Name}");
                Task.Run(() => GameEngine.ActionStand(p, _config, _players));
            });
        }
    }

    private void DrawDealerControls()
    {
        bool globalLock = CommandExecutor.IsRunning;
        if (globalLock) ImGui.BeginDisabled();

        InnerDealerControls();

        if (globalLock) ImGui.EndDisabled();
    }

    private void InnerDealerControls()
    {
        var phase = GameEngine.CurrentPhase;

        if (phase == GamePhase.Waiting || phase == GamePhase.Payout)
        {
            bool hlNewRound = _highlightNewRound;
            if (hlNewRound)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, _config.HighlightColor);
                ImGui.PushStyleColor(ImGuiCol.Text, _config.HighlightTextColor);
            }
            if (BJBGui.SmallButton("Start New Round"))
            {
                _highlightNewRound = false;
                BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, "DealStart");
                Task.Run(() => GameEngine.StartInitialDeal(_players, _config));
            }
            if (hlNewRound) ImGui.PopStyleColor(2);
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
                    Task.Run(() => GameEngine.DealerHit(_config, _players));
                }
                ImGui.SameLine();
                if (BJBGui.SmallButton("Stand"))
                {
                    BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, "DealerStand");
                    Task.Run(async () => {
                        await GameEngine.DealerStand(_config, _players);
                        await GameEngine.EvaluateFinalResults(_players, _dealer, _config);
                    });
                }
            }
        }
        else { ImGui.TextDisabled("Waiting..."); }
    }

    private void HighlightActionButton(PlayerState p, string label, ref bool highlightField, bool enabled, Action onClick)
    {
        if (!enabled) ImGui.BeginDisabled();
        bool shouldHighlight = highlightField && enabled;
        if (shouldHighlight)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, _config.HighlightColor);
            ImGui.PushStyleColor(ImGuiCol.Text, _config.HighlightTextColor);
        }

        if (BJBGui.Button($"{label}##btn_{label}_{p.UIID}"))
        {
            highlightField = false;
            onClick?.Invoke();
        }

        if (shouldHighlight) ImGui.PopStyleColor(2);
        if (!enabled) ImGui.EndDisabled();
    }

    public void SyncParty()
    {
        if (Plugin.IsDebugMode) return;
        foreach (var p in _players) p.IsInParty = false;

        var leaderIndex = Plugin.PartyList.PartyLeaderIndex;

        for (int i = 0; i < Plugin.PartyList.Length; i++)
        {
            var member = Plugin.PartyList[i];
            if (member == null) continue;

            var name = member.Name.TextValue;
            if (string.IsNullOrEmpty(name)) continue;

            if (i == leaderIndex)
            {
                _dealer.Name = name;
                _dealer.WorldId = member.World.RowId;
                continue;
            }

            var existing = _players.FirstOrDefault(x => x.Name == name);
            if (existing != null) { existing.IsInParty = true; }
            else { _players.Add(new PlayerState { Name = name, WorldId = member.World.RowId, IsInParty = true }); }
        }
        _players.RemoveAll(x => !x.IsInParty && !x.IsActivePlayer && x.Bank == 0);
    }

    private void DrawOfflineUnderline()
    {
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        ImGui.GetWindowDrawList().AddLine(new Vector2(min.X, max.Y), new Vector2(max.X, max.Y), ImGui.GetColorU32(new Vector4(1, 0.5f, 0, 1)), 2.0f);
    }

    private void CreateTestData()
    {
        _players.RemoveAll(p => p.IsDebugPlayer);
        _players.Add(new PlayerState { Name = "Lorem Ipsum",           IsActivePlayer = true,  IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank =    120, CurrentBet =  100 });
        _players.Add(new PlayerState { Name = "Dolor Sit",             IsActivePlayer = true,  IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank =    900, CurrentBet =  500 });
        _players.Add(new PlayerState { Name = "Ahmet Consentetuer",    IsActivePlayer = false, IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank = 500000, CurrentBet = 1000 });
        _players.Add(new PlayerState { Name = "Adipisci Lorem",        IsActivePlayer = false, IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank = 500000, CurrentBet = 2000 });
        _players.Add(new PlayerState { Name = "Sit Amet",              IsActivePlayer = false, IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank = 500000, CurrentBet = 3000 });
        _players.Add(new PlayerState { Name = "Consentetuer Adipisci", IsActivePlayer = false, IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank = 500000, CurrentBet = 4000 });
        _players.Add(new PlayerState { Name = "Setue Vetue",           IsActivePlayer = false, IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank = 500000, CurrentBet = 5000 });
    }

    public WebhookEntry? GetSelectedWebhook()
    {
        var enabled = _config.Webhooks.FindAll(w => w.Enabled);
        if (_selectedWebhookIndex < 0 || _selectedWebhookIndex >= enabled.Count)
            return null;
        return enabled[_selectedWebhookIndex];
    }

    private void SendPaymentTell(PlayerState p, long amount, string action)
    {
        var batch = _config.MessageBatches.FirstOrDefault(b => b.Name == "Payment Reminder");
        string raw = batch?.GetNextMessage() ?? "Please pay ${missingGil} gil for your ${action}.";

        string tReplacement = !string.IsNullOrWhiteSpace(p.Alias) ? p.Alias : "<t>";

        string processed = raw
            .Replace("<t>", tReplacement)
            .Replace("${missingGil}", amount.ToString("N0"))
            .Replace("${action}", action);

        ChatCommandRouter.Send($"/t <t> {processed}", _config, "PaymentTell");
    }
}
