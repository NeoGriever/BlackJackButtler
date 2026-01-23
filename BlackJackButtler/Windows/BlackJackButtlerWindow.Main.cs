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
        GameEngine.SetDebugMode(Plugin.IsDebugMode);
        GameEngine.SetRuntimeContext(_players, _dealer);

        DrawMainHeader();
        ImGui.Separator();

        ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), $"DEALER - Phase: {GameEngine.CurrentPhase}");
        if (ImGui.BeginTable("bjb_dealer_table", 8, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
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
        if (ImGui.BeginTable("bjb_main_table", 8, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY))
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
    }

    private void SetupTableColumns()
    {
        ImGui.TableSetupColumn("●", ImGuiTableColumnFlags.WidthFixed, 40);
        ImGui.TableSetupColumn("+", ImGuiTableColumnFlags.WidthFixed, 40);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 150);
        ImGui.TableSetupColumn("Bank", ImGuiTableColumnFlags.WidthStretch, 1.0f);
        ImGui.TableSetupColumn("Bet", ImGuiTableColumnFlags.WidthStretch, 1.0f);
        ImGui.TableSetupColumn("Cards", ImGuiTableColumnFlags.WidthFixed, 120);
        ImGui.TableSetupColumn("Points", ImGuiTableColumnFlags.WidthFixed, 140);
        ImGui.TableSetupColumn("Controls", ImGuiTableColumnFlags.WidthFixed, 330);
    }

    private void DrawMainHeader()
    {
        var io = ImGui.GetIO();
        bool canStop = io.KeyCtrl && io.KeyShift;

        if (!_isRecognitionActive)
        {
            if (ImGui.Button("Start Recognition", new Vector2(150, 0)))
            {
                _isRecognitionActive = true;
                SyncParty();
            }
        }
        else
        {
            if (!canStop) ImGui.BeginDisabled();
            if (ImGui.Button("Stop Recognition (Ctrl+Shift)", new Vector2(200, 0))) _isRecognitionActive = false;
            if (!canStop) ImGui.EndDisabled();
        }
        ImGui.SameLine();
        ImGui.TextDisabled(_isRecognitionActive ? "● Scanning Party..." : "○ Idle");
    }

    private void DrawPlayerRow(PlayerState p, bool isDealer)
    {
        ImGui.PushID(isDealer ? "dealer" : p.Name);
        ImGui.TableNextColumn();
        if (!isDealer && p.IsActivePlayer)
        {
            var label = p.IsCurrentTurn ? "●" : "○";

            if (ImGui.Selectable($"{label}##turn_{p.Name}", false, ImGuiSelectableFlags.None, new Vector2(0, 0)))
            {
                foreach (var pl in _players) pl.IsCurrentTurn = false;
                p.IsCurrentTurn = true;
                p.CurrentHandIndex = Math.Clamp(p.CurrentHandIndex, 0, Math.Max(0, p.Hands.Count - 1));
                GameEngine.TargetPlayer(p.Name);
            }
        }

        ImGui.TableNextColumn();
        if (!isDealer)
        {
            if (!p.IsActivePlayer) { if (ImGui.Button($">##{p.Name}", new Vector2(-1, 0))) p.IsActivePlayer = true; }
            else { if (ImGui.Button($"X##{p.Name}", new Vector2(-1, 0))) { p.IsActivePlayer = false; p.IsCurrentTurn = false; if (!p.IsInParty && p.Bank == 0) _players.Remove(p); } }
        }

        ImGui.TableNextColumn();
        var nameColor = p.IsActivePlayer ? new Vector4(1, 1, 1, 1) : new Vector4(0.5f, 0.5f, 0.5f, 1f);
        if (p.IsCurrentTurn) nameColor = new Vector4(1f, 1f, 0.2f, 1f);
        ImGui.TextColored(nameColor, p.Name);
        if (!p.IsDebugPlayer && !p.IsInParty && p.IsActivePlayer && !isDealer)
            DrawOfflineUnderline();

        if (p.IsActivePlayer)
        {
            ImGui.TableNextColumn();
            if (isDealer) { ImGui.Text("-"); }
            else
            {
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputLong($"##bank_{p.Name}", ref p.Bank, 1000, 10000)) _save();
            }

            ImGui.TableNextColumn();
            if (isDealer) { ImGui.Text("-"); }
            else
            {
                ImGui.SetNextItemWidth(-1);
                if (p.HighlightBet) ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.6f, 0.4f, 0, 1));
                if (ImGui.InputLong($"##bet_{p.Name}", ref p.CurrentBet, 500, 5000)) { p.HighlightBet = false; _save(); }
                if (p.HighlightBet) ImGui.PopStyleColor();
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
            if (cards.Count == 0)
            {
                if (p.IsCurrentTurn && p.CurrentHandIndex == i)
                    ImGui.TextColored(new Vector4(1, 1, 0, 1), "> -");
                else
                    ImGui.Text("-");
                continue;
            }

            var cardStr = string.Join("", cards.Select(c => c == 1 ? "A" : (c >= 10 ? "X" : c.ToString())));
            if (p.IsCurrentTurn && p.CurrentHandIndex == i)
                ImGui.TextColored(new Vector4(1, 1, 0, 1), $"> {cardStr}");
            else
                ImGui.Text(cardStr);
        }
    }

    private void DrawMultiHandPoints(PlayerState p)
    {
        if (p.Hands.Count == 0) { ImGui.Text("-"); return; }

        for (int i = 0; i < p.Hands.Count; i++)
        {
            if (p.Hands[i].Cards.Count == 0)
            {
                ImGui.Text("-");
                continue;
            }

            var (min, max) = p.CalculatePoints(i);
            var display = max.HasValue ? $"{min}/{max}" : $"{min}";
            ImGui.Text(display);
        }
    }

    private void DrawPlayerControls(PlayerState p)
    {
        var phase = GameEngine.CurrentPhase;

        if (phase == GamePhase.Payout)
        {
            if (p.HighlightPay) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1, 0.5f, 0, 1));
            if (ImGui.SmallButton($"Pay Out##{p.Name}"))
            {
                p.HighlightPay = false;
            }
            if (p.HighlightPay) ImGui.PopStyleColor();
            return;
        }

        if (!p.IsCurrentTurn)
        {
            ImGui.TextDisabled("Waiting for turn...");
            return;
        }

        if (phase == GamePhase.InitialDeal && !p.HasInitialHandDealt)
        {
            if (ImGui.SmallButton($"Deal Hand##{p.Name}"))
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

            bool canHit = min < 21 && !currentHand.IsDoubleDown && !currentHand.IsStand;
            bool canDD = canHit && currentHand.Cards.Count == 2;
            bool canSplit = canHit && currentHand.Cards.Count == 2 && currentHand.Cards[0] == currentHand.Cards[1] && p.Hands.Count < _config.MaxHandsPerPlayer;
            bool canStand = !currentHand.IsStand && !currentHand.IsBust;

            HighlightActionButton("Draw", ref p.HighlightHit, canHit, () =>
            {
                BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, $"PlayerHit:{p.Name}");
                Task.Run(() => GameEngine.ActionHit(p, _config, _players));
            });
            ImGui.SameLine();

            HighlightActionButton("DD", ref p.HighlightDD, canDD, () =>
            {
                BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, $"PlayerDD:{p.Name}");
                Task.Run(() => GameEngine.ActionDD(p, _config, _players));
            });
            ImGui.SameLine();

            HighlightActionButton("Spl", ref p.HighlightSplit, canSplit, () =>
            {
                BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, $"PlayerSplit:{p.Name}");
                Task.Run(() => GameEngine.ActionSplit(p, _config));
            });
            ImGui.SameLine();

            HighlightActionButton("Stand", ref p.HighlightStand, canStand, () =>
            {
                BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, $"PlayerStand:{p.Name}");
                Task.Run(() => GameEngine.ActionStand(p, _config, _players));
            });
        }
    }

    private void DrawDealerControls()
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
        else
        {
            ImGui.TextDisabled("Waiting for players...");
        }
    }

    private void HighlightActionButton(string label, ref bool highlightField, bool enabled, Action onClick)
    {
        if (!enabled) ImGui.BeginDisabled();

        bool shouldHighlight = highlightField && enabled;
        if (shouldHighlight) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0.8f, 0.8f, 1));

        if (ImGui.SmallButton(label))
        {
            highlightField = false;
            onClick?.Invoke();
        }

        if (shouldHighlight) ImGui.PopStyleColor();
        if (!enabled) ImGui.EndDisabled();
    }

    private void CreateTestData()
    {
        _players.RemoveAll(p => p.IsDebugPlayer);
        _players.Add(new PlayerState { Name = "[DBG] User 1", IsActivePlayer = true, IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank = 100000, CurrentBet = 10000 });
        _players.Add(new PlayerState { Name = "[DBG] User 2", IsActivePlayer = true, IsDebugPlayer = true, IsInParty = true, IsCurrentTurn = false, Bank = 50000, CurrentBet = 5000 });
    }

    public void SyncParty()
    {
        if (Plugin.IsDebugMode)
        {
            foreach (var p in _players)
            {
                if (p.IsDebugPlayer)
                p.IsInParty = true;
            }
            return;
        }
        foreach (var p in _players) p.IsInParty = false;
        foreach (var member in Plugin.PartyList)
        {
            var name = member.Name.TextValue;
            var worldId = member.World.RowId;

            var existing = _players.FirstOrDefault(x => x.Name == name);
            if (existing != null)
            {
                existing.IsInParty = true;
                existing.WorldId = worldId;
            }
            else
            {
                _players.Add(new PlayerState
                {
                    Name = name,
                    WorldId = worldId,
                    IsInParty = true,
                    IsActivePlayer = false
                });
            }
        }
        _players.RemoveAll(x => !x.IsInParty && !x.IsActivePlayer && x.Bank == 0);
    }

    private void DrawOfflineUnderline()
    {
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        ImGui.GetWindowDrawList().AddLine(
        new Vector2(min.X, max.Y),
        new Vector2(max.X, max.Y),
        ImGui.GetColorU32(new Vector4(1, 0.5f, 0, 1)),
        2.0f
        );
    }
}
