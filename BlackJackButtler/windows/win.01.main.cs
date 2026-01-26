using System;
using System.Numerics;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawMainPage()
    {
        DrawMainHeader();
        ImGui.Separator();

        ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), $"DEALER - Phase: {GameEngine.CurrentPhase}");
        if (ImGui.BeginTable("bjb_dealer_table", 9, ImGuiTableFlags.Borders))
        {
            SetupTableColumns();
            ImGui.TableNextRow();
            DrawPlayerRow(_dealer, true);
            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.4f, 0.8f, 1, 1), "PLAYERS");
        if (ImGui.BeginTable("bjb_main_table", 9, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY))
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
            if (ImGui.Button("Save", new Vector2(120, 0)))
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
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                _editingAliasPlayer = null;
                _isAliasModalOpen = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    private void SetupTableColumns()
    {
        ImGui.TableSetupColumn("A", ImGuiTableColumnFlags.WidthFixed, 40);
        ImGui.TableSetupColumn("J", ImGuiTableColumnFlags.WidthFixed, 40);
        ImGui.TableSetupColumn("P", ImGuiTableColumnFlags.WidthFixed, 40);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn("Bank", ImGuiTableColumnFlags.WidthStretch, 1.0f);
        ImGui.TableSetupColumn("Bet", ImGuiTableColumnFlags.WidthStretch, 1.0f);
        ImGui.TableSetupColumn("Cards", ImGuiTableColumnFlags.WidthFixed, 120);
        ImGui.TableSetupColumn("Points", ImGuiTableColumnFlags.WidthFixed, 80);
        ImGui.TableSetupColumn("Controls", ImGuiTableColumnFlags.WidthFixed, 330);
    }

    private void DrawMainHeader()
    {
        var io = ImGui.GetIO();
        bool canStop = io.KeyCtrl && io.KeyShift;

        if (!_isRecognitionActive)
        {
            if (ImGui.Button("Start Recognition", new Vector2(200, 0)))
            {
                _isRecognitionActive = true;
                SyncParty();
            }
        }
        else
        {
            if (!canStop) ImGui.BeginDisabled();
            if (ImGui.Button("Stop Recognition", new Vector2(200, 0))) _isRecognitionActive = false;
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("Hold down CTRL + Shift");
            if (!canStop) ImGui.EndDisabled();
        }
        ImGui.SameLine();
        ImGui.TextDisabled(_isRecognitionActive ? "● Scanning Party..." : "○ Idle");
    }

    private void DrawPlayerRow(PlayerState p, bool isDealer)
    {
        uint bgColor;
        if (isDealer)
            bgColor = ImGui.GetColorU32(new Vector4(0.05f, 0.05f, 0.05f, 1f));
        else if (!p.IsActivePlayer)
            bgColor = ImGui.GetColorU32(new Vector4(0, 0, 0, 1));
        else if (p.IsCurrentTurn)
            bgColor = ImGui.GetColorU32(new Vector4(0.0f, 0.25f, 0.0f, 1f));
        else if (p.IsActivePlayer && !p.IsInParty && !p.IsDebugPlayer)
            bgColor = ImGui.GetColorU32(new Vector4(0.25f, 0.1f, 0.05f, 1f));
        else if (p.IsOnHold)
            bgColor = ImGui.GetColorU32(new Vector4(0.15f, 0.15f, 0.15f, 1f));
        else
            bgColor = ImGui.GetColorU32(new Vector4(0.0f, 0.0f, 0.2f, 1f));

        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, bgColor);

        ImGui.PushID(isDealer ? "dealer" : p.Name);

        ImGui.TableNextColumn();
        if (!isDealer && p.IsActivePlayer)
        {
            if (ImGui.Button($"A##alias_btn_{p.UIID}"))
            {
                _editingAliasPlayer = p;
                _aliasInputBuffer = !string.IsNullOrWhiteSpace(p.Alias) ? p.Alias : p.Name;
                _triggerAliasPopup = true;
            }
        }

        ImGui.TableNextColumn();
        if (!isDealer)
        {
            if (!p.IsActivePlayer) { if (ImGui.Button($">##{p.UIID}", new Vector2(-1, 0))) p.IsActivePlayer = true; }
            else { if (ImGui.Button($"X##{p.UIID}", new Vector2(-1, 0))) { p.IsActivePlayer = false; p.IsCurrentTurn = false; if (!p.IsInParty && p.Bank == 0) _players.Remove(p); } }
        }

        ImGui.TableNextColumn();
        if (!isDealer && p.IsActivePlayer) {
            var phase = GameEngine.CurrentPhase;
            bool canToggleHold = phase == GamePhase.Waiting || phase == GamePhase.Payout;
            canToggleHold = true;

            if (!canToggleHold) ImGui.BeginDisabled();

            if (p.IsOnHold) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1f));
            if (ImGui.Button($"H##hold_{p.UIID}")) { p.IsOnHold = !p.IsOnHold; _save(); }
            if (p.IsOnHold) ImGui.PopStyleColor();

            if (!canToggleHold) ImGui.EndDisabled();
        }

        ImGui.TableNextColumn();
        var nameColor = p.IsActivePlayer ? new Vector4(1, 1, 1, 1) : new Vector4(0.5f, 0.5f, 0.5f, 1f);
        if (p.IsOnHold) nameColor = new Vector4(0.5f, 0.5f, 0.5f, 0.8f);
        if (p.IsCurrentTurn) nameColor = new Vector4(1f, 1f, 0.2f, 1f);
        ImGui.TextColored(nameColor, p.DisplayName);
        if (!p.IsDebugPlayer && !p.IsInParty && p.IsActivePlayer && !isDealer)
            DrawOfflineUnderline();

        if (p.IsActivePlayer)
        {
            ImGui.TableNextColumn();
            if (isDealer) { ImGui.Text("-"); }
            else
            {
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputLong($"##bank_{p.UIID}", ref p.Bank, 1000, 10000)) _save();
            }

            ImGui.TableNextColumn();
            if (isDealer)
            {
                ImGui.Text("-");
            }
            else
            {
                ImGui.SetNextItemWidth(-1);
                bool shouldHighlight = p.HighlightBet;
                if (shouldHighlight) ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.6f, 0.4f, 0, 1));
                if (ImGui.InputLong($"##bet_{p.UIID}", ref p.CurrentBet, 500, 5000)) { p.HighlightBet = false; _save(); }
                if (shouldHighlight) ImGui.PopStyleColor();
            }

            ImGui.TableNextColumn();
            DrawMultiHandCards(p);
            ImGui.TableNextColumn();
            DrawMultiHandPoints(p);

            ImGui.TableNextColumn();
            if (isDealer) DrawDealerControls();
            else DrawPlayerControls(p);
        }
        else { for (int i = 0; i < 5; i++) ImGui.TableNextColumn(); }

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
            var cardStr = string.Join("", cards.Select(c => c switch {
                1 => "A",
                11 => "J",
                12 => "Q",
                13 => "K",
                _ => c.ToString()
            }));

            if (p.IsCurrentTurn && p.CurrentHandIndex == i) ImGui.TextColored(new Vector4(1, 1, 0, 1), $"[{cardStr}]");
            else ImGui.Text($" {cardStr} ");
        }
    }

    private void DrawMultiHandPoints(PlayerState p)
    {
        if (p.Hands.Count == 0) { ImGui.Text("-"); return; }
        for (int i = 0; i < p.Hands.Count; i++)
        {
            if (p.Hands[i].Cards.Count == 0) { ImGui.Text("-"); continue; }
            var (min, max) = p.CalculatePoints(i);
            var display = max.HasValue ? $"{min}/{max}" : $"{min}";
            ImGui.Text(display);
        }
    }

    private void DrawPlayerControls(PlayerState p)
    {
        bool globalLock = CommandExecutor.IsRunning;
        if (globalLock) ImGui.BeginDisabled();

        InnerPlayerControls(p);

        if (globalLock) ImGui.EndDisabled();
    }

    private void InnerPlayerControls(PlayerState p)
    {
        var phase = GameEngine.CurrentPhase;

        if (phase == GamePhase.Payout)
        {
            bool shouldHighlight = p.HighlightPay;
            if (shouldHighlight) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1, 0.5f, 0, 1));
            if (ImGui.SmallButton($"Pay Out##{p.UIID}"))
            {
                p.HighlightPay = false;
                DropboxIntegration.PayOut(p);
            }
            if (shouldHighlight) ImGui.PopStyleColor();
            return;
        }

        if (!p.IsCurrentTurn)
        {
            ImGui.TextDisabled("Waiting...");
            return;
        }

        if (phase == GamePhase.InitialDeal && !p.HasInitialHandDealt)
        {
            if (ImGui.SmallButton($"Deal Hand##deal_{p.UIID}"))
            {
                Task.Run(() => GameEngine.ActionDealHand(p, _config, _players));
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
                canSplit = currentHand.Cards[0] == currentHand.Cards[1];
                else
                canSplit = PlayerState.GetCardScoreValue(currentHand.Cards[0]) == PlayerState.GetCardScoreValue(currentHand.Cards[1]);
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
            if (ImGui.SmallButton("Start New Round"))
            {
                BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, "DealStart");
                Task.Run(() => GameEngine.StartInitialDeal(_players, _config));
            }
        }
        else if (phase == GamePhase.DealerTurn)
        {
            if (ImGui.SmallButton("Hit"))
            {
                BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, "DealerHit");
                Task.Run(() => GameEngine.DealerHit(_config));
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("Stand"))
            {
                BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, "DealerStand");
                Task.Run(async () => {
                    await GameEngine.DealerStand(_config);
                    await GameEngine.EvaluateFinalResults(_players, _dealer, _config);
                });
            }
        }
        else { ImGui.TextDisabled("Waiting..."); }
    }

    private void HighlightActionButton(PlayerState p, string label, ref bool highlightField, bool enabled, Action onClick)
    {
        if (!enabled) ImGui.BeginDisabled();
        bool shouldHighlight = highlightField && enabled;
        if (shouldHighlight) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0.8f, 0.8f, 1));

        if (ImGui.SmallButton($"{label}##btn_{label}_{p.UIID}"))
        {
            highlightField = false;
            onClick?.Invoke();
        }

        if (shouldHighlight) ImGui.PopStyleColor();
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
        _players.Add(new PlayerState { Name = "[DBG] User 1", IsActivePlayer = true, IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank = 500000, CurrentBet = 100 });
        _players.Add(new PlayerState { Name = "[DBG] User 2", IsActivePlayer = true, IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank = 500000, CurrentBet = 500 });
        _players.Add(new PlayerState { Name = "[DBG] User 3", IsActivePlayer = true, IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank = 500000, CurrentBet = 1000 });
        _players.Add(new PlayerState { Name = "[DBG] User 4", IsActivePlayer = true, IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank = 500000, CurrentBet = 2000 });
        _players.Add(new PlayerState { Name = "[DBG] User 5", IsActivePlayer = true, IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank = 500000, CurrentBet = 3000 });
        _players.Add(new PlayerState { Name = "[DBG] User 6", IsActivePlayer = true, IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank = 500000, CurrentBet = 4000 });
        _players.Add(new PlayerState { Name = "[DBG] User 7", IsActivePlayer = true, IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank = 500000, CurrentBet = 5000 });
        _players.Add(new PlayerState { Name = "[DBG] User 8", IsActivePlayer = true, IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank = 500000, CurrentBet = 6000 });
        _players.Add(new PlayerState { Name = "[DBG] User 9", IsActivePlayer = true, IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank = 500000, CurrentBet = 7000 });
    }
}
