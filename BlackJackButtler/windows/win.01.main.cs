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

        if (ImGui.BeginTable("bjb_dealer_table", 4, ImGuiTableFlags.Borders))
        {

            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 150);
            ImGui.TableSetupColumn("Cards", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            ImGui.TableSetupColumn("Points", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Controls", ImGuiTableColumnFlags.WidthFixed, 400);

            ImGui.TableNextRow();
            DrawDealerRow();
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

        var recon_text = IsRecognitionActive ? "● Group Detector" : "○ Group Detector";
        var auto_deal_text = _config.AutoInitialDeal ? "● Auto Deal" : "○ Auto Deal";

        if (ImGui.Button(recon_text, new Vector2(200, 0)))
        {
            IsRecognitionActive = !IsRecognitionActive;
            SyncParty();
        }

        ImGui.SameLine();

        bool autoDealActive = _config.AutoInitialDeal;
        if (autoDealActive) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.5f, 0.0f, 1.0f));

        if (ImGui.Button(auto_deal_text))
        {
            _config.AutoInitialDeal = !_config.AutoInitialDeal;
            _save();
        }

        if (autoDealActive) ImGui.PopStyleColor();


        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Automatically deals the first two cards to players during the Initial Deal phase.");
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
        if (p.IsActivePlayer) {
            if (ImGui.Button($"A##alias_btn_{p.UIID}")) {
                _editingAliasPlayer = p;
                _aliasInputBuffer = !string.IsNullOrWhiteSpace(p.Alias) ? p.Alias : p.Name;
                _triggerAliasPopup = true;
            }
        }

        ImGui.TableNextColumn();
        if (!p.IsActivePlayer) { if (ImGui.Button($">##{p.UIID}", new Vector2(-1, 0))) p.IsActivePlayer = true; }
        else { if (ImGui.Button($"X##{p.UIID}", new Vector2(-1, 0))) { p.IsActivePlayer = false; p.IsCurrentTurn = false; } }

        ImGui.TableNextColumn();
        if (p.IsActivePlayer) {
            if (p.IsOnHold) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1f));
            if (ImGui.Button($"H##hold_{p.UIID}")) { p.IsOnHold = !p.IsOnHold; _save(); }
            if (p.IsOnHold) ImGui.PopStyleColor();
        }

        ImGui.TableNextColumn();
        var nameColor = p.IsCurrentTurn ? new Vector4(1f, 1f, 0.2f, 1f) : new Vector4(1, 1, 1, 1);
        ImGui.TextColored(nameColor, p.DisplayName);

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputLong($"##bank_{p.UIID}", ref p.Bank, 1000, 10000)) _save();

        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputLong($"##bet_{p.UIID}", ref p.CurrentBet, 500, 5000)) _save();

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
        _players.Add(new PlayerState { Name = "Test Player A", IsActivePlayer = true, IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank = 500000, CurrentBet = 100 });
        _players.Add(new PlayerState { Name = "Test Player B", IsActivePlayer = true, IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank = 500000, CurrentBet = 500 });
        _players.Add(new PlayerState { Name = "Test Player C", IsActivePlayer = true, IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank = 500000, CurrentBet = 1000 });
        _players.Add(new PlayerState { Name = "Test Player D", IsActivePlayer = true, IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank = 500000, CurrentBet = 2000 });
        _players.Add(new PlayerState { Name = "Test Player E", IsActivePlayer = false, IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank = 500000, CurrentBet = 3000 });
        _players.Add(new PlayerState { Name = "Test Player F", IsActivePlayer = false, IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank = 500000, CurrentBet = 4000 });
        _players.Add(new PlayerState { Name = "Test Player G", IsActivePlayer = false, IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank = 500000, CurrentBet = 5000 });
        _players.Add(new PlayerState { Name = "Test Player H", IsActivePlayer = false, IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank = 500000, CurrentBet = 6000 });
        _players.Add(new PlayerState { Name = "Test Player I", IsActivePlayer = false, IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank = 500000, CurrentBet = 7000 });
    }
}
