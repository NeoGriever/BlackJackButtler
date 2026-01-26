using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using BlackJackButtler.Chat;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawRoundLogPage()
    {
        ImGui.TextUnformatted("Round Management & Timeline Control");
        ImGui.SameLine();
        if (ImGui.Button("Clear History")) GameLog.Clear();

        ImGui.Separator();
        ImGui.TextDisabled("Rewind specific player actions. Note: This resets the entire table state to that point.");

        bool dealerHasActed = _dealer.Hands.Count > 0 && _dealer.Hands[0].Cards.Count > 1;
        if (dealerHasActed && GameEngine.CurrentPhase >= GamePhase.DealerTurn)
        {
            ImGui.TextColored(new Vector4(1, 0.4f, 0.4f, 1), "Timeline Locked: Dealer has already drawn cards.");
        }
        else
        {
            ImGui.TextColored(new Vector4(0.4f, 1, 0.4f, 1), "Timeline Active: Individual rewinds available.");
        }

        ImGui.Spacing();

        var allSnaps = GameLog.GetAllSnapshots();
        if (allSnaps.Count == 0) { ImGui.TextDisabled("No history available."); return; }

        var participants = new List<string> { _dealer.Name };
        participants.AddRange(_players.Where(p => p.IsActivePlayer).Select(p => p.Name));

        foreach (var name in participants)
        {
            bool isDealer = name == _dealer.Name;
            var playerSnaps = allSnaps.Where(s =>
                s.Snapshot.Reason.Contains(name) ||
                (s.Snapshot.Players.Any(p => p.Name == name && p.IsCurrentTurn))
            ).ToList();

            if (!playerSnaps.Any()) continue;

            if (ImGui.CollapsingHeader($"{name}##header_{name}", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.BeginTable($"table_log_{name}", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                {
                    ImGui.TableSetupColumn("Jump", ImGuiTableColumnFlags.WidthFixed, 65);
                    ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 70);
                    ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Snapshot Info", ImGuiTableColumnFlags.WidthFixed, 120);
                    ImGui.TableHeadersRow();

                    foreach (var (idx, snap) in playerSnaps)
                    {
                        bool isCurrent = GameLog.CurrentIndex == idx;
                        ImGui.TableNextRow();

                        ImGui.TableNextColumn();
                        if (isCurrent) ImGui.BeginDisabled();
                        if (ImGui.Button($"[##back_{idx}")) JumpToTimeline(idx, name);
                        if (isCurrent) ImGui.EndDisabled();

                        ImGui.SameLine();
                        bool isFuture = idx > GameLog.CurrentIndex;
                        if (!isFuture) ImGui.BeginDisabled();
                        if (ImGui.Button($"]##fwd_{idx}")) JumpToTimeline(idx, name);
                        if (!isFuture) ImGui.EndDisabled();

                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(snap.TimestampUtc.ToLocalTime().ToString("HH:mm"));

                        ImGui.TableNextColumn();
                        if (isCurrent) ImGui.TextColored(new Vector4(1, 1, 0, 1), $"-> {snap.Reason}");
                        else ImGui.TextUnformatted(snap.Reason);

                        ImGui.TableNextColumn();
                        var pState = isDealer ? snap.Dealer : snap.Players.FirstOrDefault(p => p.Name == name);
                        if (pState != null && pState.Hands.Count > 0)
                        {
                            var h = pState.Hands[pState.CurrentHandIndex];
                            var (min, max) = pState.CalculatePoints(pState.CurrentHandIndex);
                            ImGui.TextDisabled($"{string.Join(",", h.Cards)} ({(max ?? min)})");
                        }
                    }
                    ImGui.EndTable();
                }
            }
        }
    }

    private void JumpToTimeline(int index, string targetName)
    {
        var phase = GameEngine.CurrentPhase;
        GameLog.ApplySnapshot(index, _players, ref _dealer, ref phase);
        GameEngine.CurrentPhase = phase;

        AddDebugLog($"[Timeline] Jumped to Snapshot #{index} for player '{targetName}'. State restored.", false);

        var player = _players.FirstOrDefault(p => p.Name == targetName);
        if (player != null)
        {
            foreach (var p in _players) p.IsCurrentTurn = false;
            player.IsActivePlayer = true;
            player.IsCurrentTurn = true;

            if (player.Hands.Count > 0 && player.Hands[0].Cards.Count >= 2)
                player.HasInitialHandDealt = true;

            if (GameEngine.CurrentPhase != GamePhase.InitialDeal)
                GameEngine.CurrentPhase = GamePhase.PlayersTurn;

            GameEngine.TargetPlayer(player.Name);
        }

        _save();
        _page = Page.Main;
    }
}
