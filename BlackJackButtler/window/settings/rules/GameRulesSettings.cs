using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using BlackJackButtler.Regex;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawRulesSettings()
    {
        Header("Dealing Behavior");
        Indent(
            () =>
            {
                DrawDealingOrderSelector("v3_dealing_order");
                DrawSettingsToggle("Player Self Rolling", "self_rolling", ref _config.PlayerRollingForThemselves);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Players roll their own required cards with /dice 13, /dice alliance 13, or the native /random command.\nDealer rolls are unchanged.");
                DrawSettingsToggle("Hide Card Suits", "hide_suits", ref _config.HideCardSuits);
            }
        );

        Header("Dealer Rules");
        Indent(
            () =>
            {
                ImGui.TextUnformatted("Dealer stands on");
                ImGui.SameLine(260f);
                ImGui.SetNextItemWidth(90f);
                if (BJBGui.InputInt("##v3_dealer_draws_until", ref _config.DealerDrawsUntil, 1, defaultValue: 17))
                {
                    _config.DealerDrawsUntil = Math.Clamp(_config.DealerDrawsUntil, 2, 21);
                    _save();
                }
                ImGui.SameLine();
                DrawRuleActionButton("Soft", "soft", ref _config.DealerSoftRule);
            }
        );

        Header("Game Settings");
        Indent(
            () =>
            {
                Header("Win");
                Indent(() => MultiplierInput("Payout", ref _config.MultiplierNormalWin, 1f, "v3_win"));

                Header("BlackJack");
                Indent(
                    () =>
                    {
                        var tie = (int)_config.BlackjackTieRule;
                        ImGui.TextUnformatted("BlackJack Priority");
                        ImGui.SameLine(260f);
                        DrawEnumButtons(
                            "bj_tie_v3",
                            ref tie,
                            new[] { "Push", "Player", "Dealer", "Regular" },
                            idx =>
                            {
                                _config.BlackjackTieRule = (BlackjackTieRule)idx;
                                _save();
                            }
                        );

                        Header("Natural");
                        Indent(() => MultiplierInput("Payout", ref _config.MultiplierBlackjackWin, 1.5f, "v3_natbj"));

                        Header("Dirty");
                        Indent(
                            () =>
                            {
                                DrawSettingsToggle("Enabled", "dirty", ref _config.EnableDirtyBlackjack);
                                MultiplierInput("Payout", ref _config.MultiplierDirtyBlackjackWin, 1f, "v3_dirtybj");
                            }
                        );
                    }
                );

                Header("Charlie");
                Indent(
                    () =>
                    {
                        DrawSettingsToggle("Enabled", "charlie", ref _config.EnableCharlie);
                        DrawSettingsToggle("Instant win", "charlie_instant", ref _config.CharlieInstantWin);
                        DrawRuleInteger("Cards", "charlie_cards", ref _config.CharlieCardCount, 3, 9, 5);
                        MultiplierInput("Payout", ref _config.MultiplierCharlieWin, 1.5f, "v3_charlie_payout");
                    }
                );

                Header("Split");
                Indent(
                    () =>
                    {
                        DrawSettingsToggle("Enabled", "split", ref _config.EnableSplit);
                        DrawSettingsToggle("Identical Split only", "identical_split", ref _config.IdenticalSplitOnly);
                        DrawRuleInteger("Max Hands", "max_hands", ref _config.MaxHandsPerPlayer, 2, 10, 3);
                        MultiplierInput("Payout", ref _config.MultiplierSplitWin, 1f, "v3_split_payout");
                    }
                );

                Header("Double Down");
                Indent(
                    () =>
                    {
                        DrawSettingsToggle("Enabled", "double_down", ref _config.EnableDoubleDown);
                        DrawSettingsToggle("Allow DD after Split", "double_after_split", ref _config.AllowDoubleDownAfterSplit);
                        DrawSettingsToggle("Refund DD on Push", "refund_double_down", ref _config.RefundFullDoubleDownOnPush);
                        MultiplierInput("Payout", ref _config.MultiplierDoubleDownWin, 1f, "v3_dd_payout");
                    }
                );

                Header("Triple Down");
                Indent(
                    () =>
                    {
                        DrawSettingsToggle("Enabled", "triple_down", ref _config.EnableTripleDown);
                        DrawTripleDownStateRepairNotice();
                        DrawTripleDownPointsLimit();
                        DrawSettingsToggle("Allow TD after Split", "triple_after_split", ref _config.AllowTripleDownAfterSplit);
                        DrawSettingsToggle("Refund TD on Push", "refund_triple_down", ref _config.RefundFullTripleDownOnPush);
                        MultiplierInput("Payout", ref _config.MultiplierTripleDownWin, 1f, "v3_td_payout");
                    }
                );

                Header("Result");
                Indent(
                    () =>
                    {
                        DrawSettingsToggle("Short Result Messages", "short_results", ref _config.SmallResult);
                        DrawShortResultRulesEditor();
                    }
                );
            }
        );
    }

    private void DrawTripleDownPointsLimit()
    {
        ImGui.TextUnformatted("Allow TD only with");
        ImGui.SameLine(260f);
        if (BJBOnOffSwitch.Draw("v3_triple_down_point_limit", ref _config.LimitTripleDownToMaxPoints)) _save();
        ImGui.SameLine();
        ImGui.SetNextItemWidth(90f);
        if (BJBGui.InputInt("##v3_triple_down_max_points", ref _config.TripleDownMaxPoints, 1, defaultValue: 10))
        {
            _config.TripleDownMaxPoints = Math.Clamp(_config.TripleDownMaxPoints, 2, 21);
            _save();
        }
        ImGui.SameLine();
        ImGui.TextUnformatted("Points or less");
    }

    private void DrawDealingOrderSelector(string id, float labelColumn = 260f)
    {
        ImGui.TextUnformatted("Dealing order");
        ImGui.SameLine(labelColumn);
        var individual = !_config.FirstDealThenPlay;
        if (BJBOnOffSwitch.Draw(id, ref individual, "Individual", "Party", 68f))
        {
            _config.FirstDealThenPlay = !individual;
            _save();
        }
    }

    private void DrawSettingsToggle(string label, string id, ref bool value, float labelColumn = 260f)
    {
        ImGui.TextUnformatted(label);
        ImGui.SameLine(labelColumn);
        if (BJBOnOffSwitch.Draw($"v3_{id}", ref value)) _save();
    }

    private void DrawRuleActionButton(string label, string id, ref bool value)
    {
        var wasActive = value;
        if (wasActive)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1f, 0.5f, 0f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1f, 0.6f, 0.1f, 1f));
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0f, 0f, 0f, 1f));
        }
        var clicked = wasActive ? ImGui.Button($"{label}##v3_rule_{id}") : BJBGui.Button($"{label}##v3_rule_{id}");
        if (wasActive) ImGui.PopStyleColor(3);
        if (clicked)
        {
            value = !value;
            _save();
        }
    }

    private void DrawRuleInteger(string label, string id, ref int value, int min, int max, int defaultValue)
    {
        ImGui.TextUnformatted(label);
        ImGui.SameLine(260f);
        ImGui.SetNextItemWidth(90f);
        if (BJBGui.InputInt($"##v3_rule_{id}", ref value, 1, defaultValue: defaultValue))
        {
            value = Math.Clamp(value, min, max);
            _save();
        }
    }
}
