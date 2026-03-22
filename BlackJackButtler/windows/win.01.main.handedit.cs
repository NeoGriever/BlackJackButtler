using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using BlackJackButtler.Chat;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private bool _showHandEditPopup = false;
    private PlayerState? _handEditPlayer = null;
    private int _handEditHandIndex = 0;
    private List<DeckCard?> _handEditCards = new();

    private static readonly string[] _cardValueLabels = { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };
    private static readonly string[] _suitLabels = { "♦ Diamonds", "♣ Clubs", "♠ Spades", "♥ Hearts" };

    private void OpenHandEditPopup(PlayerState player, int handIndex)
    {
        _handEditPlayer = player;
        _handEditHandIndex = handIndex;
        _handEditCards.Clear();

        if (handIndex < player.Hands.Count)
        {
            foreach (var card in player.Hands[handIndex].Cards)
                _handEditCards.Add(card);
        }

        _showHandEditPopup = true;
        AddDebugLog($"[HandEdit] Opened for {player.DisplayName} hand {handIndex}", false);
    }

    private void CloseHandEditPopup()
    {
        _showHandEditPopup = false;
        _handEditPlayer = null;
        _handEditCards.Clear();
    }

    private void DrawHandEditPopup()
    {
        if (!_showHandEditPopup || _handEditPlayer == null)
            return;

        if (_handEditPlayer.IsCurrentTurn || CommandExecutor.IsRunning)
        {
            CloseHandEditPopup();
            return;
        }

        ImGui.SetNextWindowSize(new Vector2(440, 0), ImGuiCond.Always);
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

        if (ImGui.Begin($"Hand Editor: {_handEditPlayer.DisplayName}###bjb_handedit_popup", ref _showHandEditPopup,
            ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (_handEditPlayer.Hands.Count > 1)
            {
                ImGui.Text("Hand:");
                ImGui.SameLine();
                for (int i = 0; i < _handEditPlayer.Hands.Count; i++)
                {
                    bool selected = _handEditHandIndex == i;
                    if (selected)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.5f, 0.8f, 1.0f));
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.4f, 0.6f, 0.9f, 1.0f));
                    }
                    if (BJBGui.Button($"Hand {i + 1}##handedit_tab_{i}"))
                    {
                        _handEditHandIndex = i;
                        _handEditCards.Clear();
                        if (i < _handEditPlayer.Hands.Count)
                            foreach (var card in _handEditPlayer.Hands[i].Cards)
                                _handEditCards.Add(card);
                    }
                    if (selected) ImGui.PopStyleColor(2);
                    ImGui.SameLine();
                }
                ImGui.NewLine();
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
            }

            int cols = 4;
            int removeIndex = -1;

            for (int i = 0; i < _handEditCards.Count; i++)
            {
                if (i > 0 && i % cols == 0)
                    ImGui.Spacing();

                ImGui.PushID($"card_{i}");

                var card = _handEditCards[i];

                int valueIndex = card.HasValue ? card.Value.Value - 1 : 0;
                int suitIndex = card.HasValue ? (int)card.Value.Suit : 0;

                ImGui.SetNextItemWidth(50);
                if (ImGui.Combo("##val", ref valueIndex, _cardValueLabels, _cardValueLabels.Length))
                {
                    _handEditCards[i] = new DeckCard { Value = valueIndex + 1, Suit = (CardSuit)suitIndex, DrawnAt = DateTime.UtcNow };
                }
                ImGui.SameLine();
                ImGui.SetNextItemWidth(110);
                if (ImGui.Combo("##suit", ref suitIndex, _suitLabels, _suitLabels.Length))
                {
                    _handEditCards[i] = new DeckCard { Value = valueIndex + 1, Suit = (CardSuit)suitIndex, DrawnAt = DateTime.UtcNow };
                }
                ImGui.SameLine();

                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.15f, 0.15f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.2f, 0.2f, 1.0f));
                if (BJBGui.Button("X##del"))
                    removeIndex = i;
                ImGui.PopStyleColor(2);

                ImGui.PopID();
            }

            if (removeIndex >= 0)
                _handEditCards.RemoveAt(removeIndex);

            ImGui.Spacing();

            if (_handEditCards.Count < 16)
            {
                if (BJBGui.Button("+ Add Card"))
                    _handEditCards.Add(new DeckCard { Value = 1, Suit = CardSuit.Spades, DrawnAt = DateTime.UtcNow });
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawHandEditPreview();

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.2f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.8f, 0.3f, 1.0f));
            if (BJBGui.Button("OK", new Vector2(200, 30)))
            {
                ApplyHandEdit();
                CloseHandEditPopup();
            }
            ImGui.PopStyleColor(2);

            ImGui.SameLine();

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.2f, 0.2f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.3f, 0.3f, 1.0f));
            if (BJBGui.Button("Cancel", new Vector2(200, 30)))
                CloseHandEditPopup();
            ImGui.PopStyleColor(2);

            ImGui.End();
        }
        else
        {
            CloseHandEditPopup();
        }
    }

    private void DrawHandEditPreview()
    {
        var validCards = new List<DeckCard>();
        foreach (var c in _handEditCards)
            if (c.HasValue) validCards.Add(c.Value);

        if (validCards.Count == 0)
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1.0f), "No cards");
            return;
        }

        ImGui.Text("Preview:");
        ImGui.SameLine();

        foreach (var card in validCards)
        {
            Vector4 color = (card.Suit == CardSuit.Diamonds || card.Suit == CardSuit.Hearts)
                ? new Vector4(1, 0.3f, 0.3f, 1)
                : new Vector4(0.9f, 0.9f, 0.9f, 1);
            ImGui.TextColored(color, card.ToString());
            ImGui.SameLine(0, 4);
        }
        ImGui.NewLine();

        int total = 0;
        int aces = 0;
        foreach (var c in validCards)
        {
            if (c.Value == 1) { total += 1; aces++; }
            else if (c.Value >= 10) total += 10;
            else total += c.Value;
        }

        int min = total;
        int? max = (aces > 0 && total + 10 <= 21) ? total + 10 : null;
        int best = (max.HasValue && max.Value <= 21) ? max.Value : min;

        ImGui.Text("Points:");
        ImGui.SameLine();

        if (best > 21)
        {
            ImGui.TextColored(new Vector4(1.0f, 0.2f, 0.2f, 1.0f), $"BUST ({best})");
        }
        else if (best == 21 && validCards.Count == 2)
        {
            ImGui.TextColored(new Vector4(0.0f, 1.0f, 0.0f, 1.0f), "BJ (Natural)");
        }
        else if (best == 21)
        {
            ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.0f, 1.0f), "Dirty BJ (21)");
        }
        else if (max.HasValue)
        {
            ImGui.Text($"{min}/{max.Value}");
        }
        else
        {
            ImGui.Text($"{min}");
        }
    }

    private void ApplyHandEdit()
    {
        if (_handEditPlayer == null || _handEditHandIndex >= _handEditPlayer.Hands.Count)
            return;

        var validCards = new List<DeckCard>();
        foreach (var c in _handEditCards)
            if (c.HasValue) validCards.Add(c.Value);

        var hand = _handEditPlayer.Hands[_handEditHandIndex];
        hand.Cards.Clear();
        hand.Cards.AddRange(validCards);

        int total = 0;
        int aces = 0;
        foreach (var c in validCards)
        {
            if (c.Value == 1) { total += 1; aces++; }
            else if (c.Value >= 10) total += 10;
            else total += c.Value;
        }
        int best = (aces > 0 && total + 10 <= 21) ? total + 10 : total;

        hand.IsBust = best > 21;
        hand.IsNaturalBlackJack = validCards.Count == 2 && best == 21;
        hand.IsCharlie = _config.EnableCharlie && validCards.Count >= _config.CharlieCardCount && !hand.IsBust;
        if (hand.IsCharlie) hand.IsStand = true;
        if (hand.IsBust)
            hand.IsStand = true;

        AddDebugLog($"[HandEdit] Applied to {_handEditPlayer.DisplayName} hand {_handEditHandIndex}: {validCards.Count} cards, best={best}, bust={hand.IsBust}, natBJ={hand.IsNaturalBlackJack}", false);
    }
}
