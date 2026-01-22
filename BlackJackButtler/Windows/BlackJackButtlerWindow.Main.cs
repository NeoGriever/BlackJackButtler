using System.Numerics;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawMainPage()
    {
        DrawMainHeader();
        ImGui.Separator();

        if (ImGui.BeginTable("bjb_main_table", 8, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY))
        {
            ImGui.TableSetupColumn("●", ImGuiTableColumnFlags.WidthFixed, 30);
            ImGui.TableSetupColumn("Join", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 0);
            ImGui.TableSetupColumn("Bank", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            ImGui.TableSetupColumn("Bet", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            ImGui.TableSetupColumn("Cards", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn("Points", ImGuiTableColumnFlags.WidthFixed, 140);
            ImGui.TableSetupColumn("Controls", ImGuiTableColumnFlags.WidthFixed, 300);

            ImGui.TableHeadersRow();

            if (_players.Count == 0) CreateTestData();

            foreach (var player in _players)
            {
                ImGui.TableNextRow();
                DrawPlayerRow(player);
            }
            ImGui.EndTable();
        }
    }

    private void DrawMainHeader()
    {
        var io = ImGui.GetIO();
        bool canStop = io.KeyCtrl && io.KeyShift;

        if (!_isRecognitionActive)
        {
            if (ImGui.Button("Start Recognition", new Vector2(150, 0))) _isRecognitionActive = true;
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

    private void DrawPlayerRow(PlayerState p)
    {
        ImGui.TableNextColumn();
        if (p.IsActivePlayer)
        {
            var label = p.IsCurrentTurn ? "●" : "○";
            if (ImGui.Selectable($"{label}##turn_{p.Name}"))
            {
                if (p.IsCurrentTurn)
                ImGui.OpenPopup($"deselect_confirm_{p.Name}");
                else
                {
                    foreach (var pl in _players) pl.IsCurrentTurn = false;
                    p.IsCurrentTurn = true;
                }
            }

            if (ImGui.BeginPopupModal($"deselect_confirm_{p.Name}", ref p.IsCurrentTurn, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text($"Do you want to end the turn of {p.Name}?");
                if (ImGui.Button("Yes")) { p.IsCurrentTurn = false; ImGui.CloseCurrentPopup(); }
                ImGui.SameLine();
                if (ImGui.Button("No")) { ImGui.CloseCurrentPopup(); }
                ImGui.EndPopup();
            }
        }

        ImGui.TableNextColumn();
        if (!p.IsActivePlayer)
        {
            if (ImGui.Button($">##{p.Name}", new Vector2(-1, 0))) p.IsActivePlayer = true;
        }
        else
        {
            if (ImGui.Button($"X##{p.Name}", new Vector2(-1, 0)))
            {
                p.IsActivePlayer = false;
                p.IsCurrentTurn = false;
            }
        }

        ImGui.TableNextColumn();
        var color = p.IsActivePlayer ? new Vector4(1, 1, 1, 1) : new Vector4(0.5f, 0.5f, 0.5f, 1f);
        if (p.IsCurrentTurn) color = new Vector4(1f, 1f, 0.2f, 1f);
        ImGui.TextColored(color, p.Name);
        if (!p.IsInParty && p.IsActivePlayer) DrawOfflineUnderline();

        if (p.IsActivePlayer)
        {
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputLong($"##bank_{p.Name}", ref p.Bank, 1000, 10000)) _save();

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            if (p.HighlightBet) ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.6f, 0.4f, 0, 1));
            if (ImGui.InputLong($"##bet_{p.Name}", ref p.CurrentBet, 500, 5000)) { p.HighlightBet = false; _save(); }
            if (p.HighlightBet) ImGui.PopStyleColor();

            ImGui.TableNextColumn();
            var cardStr = string.Join(" ", p.Cards.Select(c => c == 1 ? "A" : (c >= 10 ? "X" : c.ToString())));
            ImGui.TextUnformatted(cardStr);

            ImGui.TableNextColumn();
            DrawPointsValue(p);

            ImGui.TableNextColumn();
            DrawPlayerControls(p);
        }
        else
        {
            for (int i = 0; i < 5; i++) ImGui.TableNextColumn();
        }
    }

    private void DrawPointsValue(PlayerState p)
    {
        var (min, max) = p.CalculatePoints();
        var display = max.HasValue ? $"{min}/{max}" : $"{min}";
        var color = new Vector4(1, 1, 1, 1);

        if (min > 21) color = new Vector4(1, 0.2f, 0.2f, 1);
        else if (min >= 17 || (max.HasValue && max >= 17)) color = new Vector4(0.2f, 1, 0.2f, 1);

        if (p.Cards.Count == 2 && (max == 21 || (min == 21 && !max.HasValue)))
            ImGui.TextColored(new Vector4(0.5f, 1f, 0.5f, 1f), $"BJ ({display})");
        else
            ImGui.TextColored(color, display);
    }

    private void DrawPlayerControls(PlayerState p)
    {
        void HighlightButton(string label, ref bool highlightField, bool isTurn) {
            if (highlightField && isTurn) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0.8f, 0.8f, 1));
            if (ImGui.SmallButton($"{label}##{p.Name}")) { highlightField = false; }
            if (highlightField && isTurn) ImGui.PopStyleColor();
        }

        HighlightButton("Draw", ref p.HighlightHit, p.IsCurrentTurn); ImGui.SameLine();
        HighlightButton("DD", ref p.HighlightDD, p.IsCurrentTurn); ImGui.SameLine();
        HighlightButton("Spl", ref p.HighlightSplit, p.IsCurrentTurn); ImGui.SameLine();
        HighlightButton("Stand", ref p.HighlightStand, p.IsCurrentTurn); ImGui.SameLine();

        ImGui.SameLine();
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddLine(new Vector2(cursor.X, cursor.Y), new Vector2(cursor.X, cursor.Y + ImGui.GetFrameHeight()), ImGui.GetColorU32(ImGuiCol.Separator));
        ImGui.Dummy(new Vector2(8, 0)); ImGui.SameLine();

        if (p.HighlightPay) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1, 0.5f, 0, 1));
        if (ImGui.SmallButton($"Pay##{p.Name}")) { p.HighlightPay = false; }
        if (p.HighlightPay) ImGui.PopStyleColor();
    }

    private void DrawOfflineUnderline()
    {
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        ImGui.GetWindowDrawList().AddLine(new Vector2(min.X, max.Y), new Vector2(max.X, max.Y), ImGui.GetColorU32(new Vector4(1, 0.5f, 0, 1)), 2.0f);
    }

    private void CreateTestData()
    {
        _players.Add(new PlayerState { Name = "Valenth Siveria", IsActivePlayer = true, Bank = 1500000, CurrentBet = 50000, Cards = new List<int>{1, 10}, IsInParty = true, IsCurrentTurn = true });
        _players.Add(new PlayerState { Name = "Test Player 2", IsActivePlayer = true, Bank = 200, CurrentBet = 10, Cards = new List<int>{10, 10, 2}, IsInParty = false });
        _players.Add(new PlayerState { Name = "Inactive Bob", IsActivePlayer = false, IsInParty = true });
    }
}
