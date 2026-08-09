using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BlackJackButtler.Regex;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawSettingsPage()
    {
        if (_openImportConfirmPopup) {
            _showImportModal = true;
            ImGui.OpenPopup("import_confirm_popup");
            _openImportConfirmPopup = false;
        }

        if (_openHashedStatsConfirm) {
            _showHashedStatsModal = true;
            ImGui.OpenPopup("hashedstats_confirm_popup");
            _openHashedStatsConfirm = false;
        }

        int level = (int)_config.CurrentLevel;

        if (_config.MainViewVersion == 3)
        {
            DrawSettingsPageV3(level);
        }
        else if (_config.MainViewVersion == 2)
        {
            DrawSettingsPageV2(level);
        }
        else
        if (ImGui.BeginTabBar("##settings_tabs"))
        {
            DrawSettingsTab_General(level);

            if (level >= (int)UserLevel.Advanced)
                DrawSettingsTab_Gameplay(level);

            {
                var flags = ImGuiTabItemFlags.None;
                if (_pendingSettingsTab == "Betting")
                {
                    flags = ImGuiTabItemFlags.SetSelected;
                    _pendingSettingsTab = null;
                }
                if (ImGui.BeginTabItem("Betting", flags))
                {
                    DrawSettingsTab_Betting(level);
                    ImGui.EndTabItem();
                }
            }

            DrawSettingsTab_Visual(level);

            if (level >= (int)UserLevel.Advanced)
                DrawSettingsTab_OwnButtons(level);

            DrawSettingsTab_Sound(level);

            DrawSettingsTab_Alliance();
            DrawSettingsTab_PresetSetup();

            if (level >= (int)UserLevel.Advanced)
                DrawSettingsTab_System(level);

            ImGui.EndTabBar();
        }

        if (ImGui.BeginPopupModal("import_confirm_popup", ref _showImportModal, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("How do you want to import?");
            if (BJBGui.Button("Full Replace (Wipe current)")) {
                DoFullReplace();
                _showImportModal = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (BJBGui.Button("Merge (Keep custom items)")) {
                DoMerge();
                _showImportModal = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        if (ImGui.BeginPopupModal("hashedstats_confirm_popup", ref _showHashedStatsModal, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextColored(new Vector4(1, 0.6f, 0, 1), "WARNING");
            ImGui.Spacing();
            ImGui.TextWrapped("Disabling stats integrity hashing may cause credibility issues with venue operators and managers.");
            ImGui.Spacing();
            ImGui.TextWrapped("Please confirm that disabling this feature has been explicitly discussed with the relevant venue operator or manager.");
            ImGui.Spacing();
            ImGui.Spacing();
            if (BJBGui.Button("Confirm Disable##hashedstats"))
            {
                _config.HashedStats = false;
                _save();
                _showHashedStatsModal = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (BJBGui.Button("Cancel##hashedstats"))
            {
                _showHashedStatsModal = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    private void DrawSettingsTab_General(int level)
    {
        if (ImGui.BeginTabItem("General"))
        {
            ImGui.Spacing();
            ImGui.TextUnformatted("User Level");
            ImGui.SameLine(300f);
            ImGui.SetNextItemWidth(200f);
            if (BJBGui.Combo("##user_level", ref level, "Beginner\0Advanced\0Dev\0Custom\0")) {
                _config.CurrentLevel = (UserLevel)level;
                _save();
            }
            if (_config.CurrentLevel == UserLevel.Custom)
            {
                ImGui.SameLine();
                var wasCustomEditMode = _customEditMode;
                if (wasCustomEditMode) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.2f, 0.9f));
                if (BJBGui.SmallButton("\u270F##custom_edit_settings")) _customEditMode = !_customEditMode;
                if (wasCustomEditMode) ImGui.PopStyleColor();
            }

            ImGui.Spacing();
            if (ImGui.CollapsingHeader("Time Zone & Delay"))
                DrawSettingsV3TimeDelay();

            ImGui.Separator();
            ImGui.Spacing();
            ImGui.TextUnformatted("Main View");
            ImGui.SameLine(300f);
            ImGui.SetNextItemWidth(200f);
            int mainViewIdx = _config.MainViewVersion switch { 2 => 1, 3 => 2, _ => 0 };
            if (BJBGui.Combo("##main_view_version", ref mainViewIdx, "Classic\0Version 2\0Version 3\0"))
            {
                _config.MainViewVersion = mainViewIdx + 1;
                _save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Classic keeps the current main page layout.\nVersion 2 uses the reorganized main page.\nVersion 3 uses the new settings layout.");

            ImGui.Spacing();
            ImGui.TextUnformatted("Menu Style");
            ImGui.SameLine(300f);
            ImGui.SetNextItemWidth(200f);
            int menuModeIdx = (int)_config.MenuStyle;
            if (BJBGui.Combo("##menu_style", ref menuModeIdx, "Sidebar\0Burger Menu\0Top Tabs (experimental)\0"))
            {
                _config.MenuStyle = (MenuStyleMode)menuModeIdx;
                _save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Sidebar: left navigation column.\nBurger Menu: compact top button with a popup.\nTop Tabs: page tabs across the top.");

            ImGui.Spacing();
            ImGui.TextUnformatted("Gil visual");
            ImGui.SameLine(300f);
            ImGui.SetNextItemWidth(200f);
            int gilVisualIdx = (int)_config.GilVisual;
            if (BJBGui.Combo("##gil_visual", ref gilVisualIdx, "12345678\0     12,345,678\0   , 12,345,678\0"))
            {
                _config.GilVisual = (GilVisualMode)gilVisualIdx;
                _save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("How Gil amounts are displayed in all Gil input fields.");

            ImGui.Spacing();
            if (ImGui.Checkbox("Show Nearby Players", ref _config.ShowNearbyPlayers)) _save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Show a list of nearby players below the player table.");
            if (_config.ShowNearbyPlayers)
            {
                ImGui.TextUnformatted("Columns");
                ImGui.SameLine(300f);
                ImGui.SetNextItemWidth(100f);
                if (BJBGui.DragInt("##nearby_columns", ref _config.NearbyColumns, 0.1f, 1, 5, "%d"))
                {
                    _config.NearbyColumns = Math.Clamp(_config.NearbyColumns, 1, 5);
                    _save();
                }

                if (ImGui.Checkbox("Always show distance circle", ref _config.NearbyAlwaysShowCircle)) _save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Show the distance circle permanently when Group Detector is active.\nOtherwise only visible when hovering the distance slider.");

                DrawCommandSelector("Nearby ? Command", ref _config.NearbyQuestionCommandName);
            }

            ImGui.EndTabItem();
        }
    }

    private void DrawSettingsTab_Gameplay(int level)
    {
        if (ImGui.BeginTabItem("Gameplay"))
        {
            ImGui.Spacing();
            Header("Automation");
            DrawSharedAutomationTimingControls("v1");
            if (ImGui.Checkbox("Auto Activate Trading Players", ref _config.AutoActivateTradingPlayers))
                _save();

            ImGui.TextUnformatted("Rules");
            ImGui.Separator();

            ImGui.Spacing();
            DrawV3RuleActionButton("First Deal, then play", "v1_first_deal", ref _config.FirstDealThenPlay);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Active: First deal every player their hands.\nInactive: Deal hand and direct play per player.");

            ImGui.Spacing();
            DrawV3RuleActionButton("Player self-rolling", "v1_self_rolling", ref _config.PlayerRollingForThemselves);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Players roll their own required cards with a 13-sided dice. Dealer rolls are unchanged.");

            ImGui.Spacing();
            DrawV3OnOff("Identical Split Only", "v1_identical_split", ref _config.IdenticalSplitOnly);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Active: Only same cards (e.g. J+J) can split.\nInactive: Same score (e.g. J+K) can split.");

            ImGui.Spacing();
            DrawV3OnOff("Allow Double Down after Split", "v1_double_after_split", ref _config.AllowDoubleDownAfterSplit);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Active: Allows the player to Double Down on hands that resulted from a split.\nInactive: Splitted hands don't allow to Double Down.");

            ImGui.Spacing();
            DrawV3OnOff("Refund DD on push", "v1_refund_dd", ref _config.RefundFullDoubleDownOnPush);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Active: If a player has DD and got pushed, the DD bet gets pushed too.\nInactive: If a player has DD and got pushed, the DD bet is lost.");

            ImGui.Spacing();
            int bjTieRule = (int)_config.BlackjackTieRule;
            ImGui.SetNextItemWidth(250f);
            if (BJBGui.Combo("BJ Tie Rule##bj_tie_rule", ref bjTieRule, "Always Push\0Player NatBJ Wins\0Dealer NatBJ Wins\0NatBJ Beats Dirty\0"))
            {
                _config.BlackjackTieRule = (BlackjackTieRule)bjTieRule;
                _save();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Determines the outcome when both player and dealer have 21:\n- Always Push: Any 21 tie is a push.\n- Player NatBJ Wins: Player's Natural BJ wins the tie.\n- Dealer NatBJ Wins: Dealer's Natural BJ wins the tie.\n- NatBJ Beats Dirty: Natural BJ beats Dirty 21, same type pushes.");

            ImGui.Spacing();
            DrawV3OnOff("Enable Charlie", "v1_charlie", ref _config.EnableCharlie);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("A player who draws N cards without busting\nautomatically wins with BJ payout (1.5x).");
            if (_config.EnableCharlie)
            {
                ImGui.SameLine(300f);
                ImGui.SetNextItemWidth(200f);
                if (BJBGui.InputInt("##charlie_card_count", ref _config.CharlieCardCount, 1))
                {
                    _config.CharlieCardCount = Math.Clamp(_config.CharlieCardCount, 3, 7);
                    _save();
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Number of cards needed for Charlie (3-7).");

                ImGui.Spacing();
                DrawV3OnOff("Instant-Win", "v1_charlie_instant", ref _config.CharlieInstantWin);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("When active, Charlies win immediately.\nWhen inactive, they compete against the dealer's score normally.");
            }

            ImGui.Spacing();
            ImGui.TextUnformatted("Dealer stands on");
            ImGui.SameLine();
            DrawV3RuleActionButton("Soft", "v1_dealer_soft", ref _config.DealerSoftRule);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Soft: Dealer hits on soft threshold (e.g. Ace+6 = hit).\nHard: Dealer stands on any score >= threshold.");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100f);
            if (BJBGui.InputInt("##dealer_draws_until", ref _config.DealerDrawsUntil, 1))
            {
                _config.DealerDrawsUntil = Math.Clamp(_config.DealerDrawsUntil, 3, 20);
                _save();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Dealer draws until this score (3-20).");

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextUnformatted("Round Behavior");
            ImGui.Separator();

            ImGui.Spacing();
            DrawV3OnOff("Player Ready Start requires 2 or more participants", "v1_ready_start", ref _config.AutostartRoundOnlyOnMultiplePlayers);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Enabled: the Player Ready / RoundStart trigger requires at least 2 active participants.\nDisabled: one active participant is enough.\nThe dealer is not counted. This does not affect the Auto-Continue timer.");

            ImGui.Spacing();
            DrawV3OnOff("Short Result Messages", "v1_short_result", ref _config.SmallResult);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Active: Collects all results and sends a single compressed message.\nInactive: Sends individual result messages for every player hand.");

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextUnformatted("Limits");
            ImGui.Separator();

            ImGui.Spacing();
            ImGui.TextUnformatted("Max Hands per Player (Splits)");
            ImGui.SameLine(300f);
            ImGui.SetNextItemWidth(200f);
            if (BJBGui.InputInt("##max_hands", ref _config.MaxHandsPerPlayer, 1))
            {
                _config.MaxHandsPerPlayer = Math.Clamp(_config.MaxHandsPerPlayer, 2, 10);
                _save();
            }

            ImGui.Spacing();
            ImGui.TextUnformatted("Auto Continue Delay");
            ImGui.SameLine(300f);
            ImGui.SetNextItemWidth(200f);
            if (BJBGui.SliderFloat("##auto_continue_delay", ref _config.AutoContinueDelay, 5f, 120f, "%.0fs"))
            {
                _config.AutoContinueDelay = MathF.Round(_config.AutoContinueDelay);
                _config.AutoContinueDelay = Math.Clamp(_config.AutoContinueDelay, 5f, 120f);
                _save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Time in seconds without chat activity before auto-starting the next round.\nOnly active when Auto Continue is enabled.");

            ImGui.Spacing();
            ImGui.TextUnformatted("Auto Continue Bar Color");
            ImGui.SameLine(300f);
            ImGui.SetNextItemWidth(200f);
            if (ImGui.ColorEdit4("##auto_continue_bar_color", ref _config.AutoContinueBarColor,
                ImGuiColorEditFlags.NoAlpha))
            {
                _config.AutoContinueBarColor.W = 1.0f;
                _save();
            }

            ImGui.Spacing();
            ImGui.TextUnformatted("Auto Continue Bar Height");
            ImGui.SameLine(300f);
            ImGui.SetNextItemWidth(200f);
            float minBarHeight = _config.AutoContinueBarShowText ? 20f : 1f;
            if (BJBGui.SliderFloat("##auto_continue_bar_height", ref _config.AutoContinueBarHeight,
                minBarHeight, 40f, "%.0fpx"))
            {
                _config.AutoContinueBarHeight = MathF.Round(_config.AutoContinueBarHeight);
                _config.AutoContinueBarHeight = Math.Clamp(_config.AutoContinueBarHeight, minBarHeight, 40f);
                _save();
            }

            ImGui.Spacing();
            if (ImGui.Checkbox("Show Remaining Seconds", ref _config.AutoContinueBarShowText))
            {
                if (_config.AutoContinueBarShowText && _config.AutoContinueBarHeight < 20f)
                    _config.AutoContinueBarHeight = 20f;
                _save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Show remaining countdown seconds centered in the progress bar.\nMinimum bar height becomes 20px when enabled.");

            ImGui.EndTabItem();
        }
    }

    private void DrawSettingsTab_Betting(int level)
    {
        ImGui.Spacing();
        ImGui.TextUnformatted("Bet Limits");
        ImGui.Separator();

        ImGui.Spacing();
        ImGui.TextUnformatted("Minimum");
        ImGui.SameLine(300f);
        ImGui.SetNextItemWidth(200f);
        if (_pendingSettingsFocus == "min_bet")
        {
            ImGui.SetKeyboardFocusHere();
            _pendingSettingsFocus = null;
        }
        if (BJBGui.InputLong("##min_bet", ref _config.MinBet, 1, 1000))
        {
            _config.MinBet = Math.Clamp(_config.MinBet, 1, _config.MaxBet);
            _save();
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Maximum");
        ImGui.SameLine(300f);
        ImGui.SetNextItemWidth(200f);
        if (_pendingSettingsFocus == "max_bet")
        {
            ImGui.SetKeyboardFocusHere();
            _pendingSettingsFocus = null;
        }
        if (BJBGui.InputLong("##max_bet", ref _config.MaxBet, 1, 10000))
        {
            _config.MaxBet = Math.Max(_config.MaxBet, _config.MinBet);
            _save();
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.TextUnformatted("VIP Bet Tiers");
        ImGui.Separator();

        for (int i = 0; i < _config.VipBetTiers.Count; i++)
        {
            var tier = _config.VipBetTiers[i];
            ImGui.PushID($"vip_tier_{i}");

            ImGui.SetNextItemWidth(150f);
            if (ImGui.InputText("##tier_name", ref tier.Name, 32)) _save();

            ImGui.SameLine();
            ImGui.TextUnformatted("Max:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150f);
            if (BJBGui.InputLong("##tier_max", ref tier.MaxBet, 10000, 100000))
            {
                tier.MaxBet = Math.Max(tier.MaxBet, 1);
                _save();
            }

            ImGui.SameLine();
            var io = ImGui.GetIO();
            bool ctrlDown = io.KeyCtrl;
            if (!ctrlDown) ImGui.BeginDisabled();
            if (BJBGui.SmallButton("X##del_tier"))
            {
                _config.VipBetTiers.RemoveAt(i);
                _save();
                ImGui.PopID();
                break;
            }
            if (!ctrlDown)
            {
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip("Hold CTRL to delete this tier.\nExisting VIP assignments remain unchanged.");
            }

            ImGui.PopID();
        }

        if (BJBGui.SmallButton("+##add_vip_tier"))
        {
            int n = _config.VipBetTiers.Count + 1;
            _config.VipBetTiers.Add(new VipBetTier
            {
                Name = $"VIP {n}",
                MaxBet = _config.MaxBet * 2
            });
            _save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add a new VIP tier");

        ImGui.Spacing();
        if (ImGui.Checkbox("Short Bet Format", ref _config.ShortBetFormat)) _save();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("On: 50k, 1m, 5m\nOff: 50,000 Gil, 1,000,000 Gil");

        ImGui.Spacing();
        ImGui.TextUnformatted("Auto-Bet Detection");
        ImGui.Separator();
        DrawAutoBetPostCommandSelector("classic");
        DrawInsufficientBetCommandSelector("classic");

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.TextUnformatted("Multipliers");
        ImGui.Separator();

        if (level >= (int)UserLevel.Advanced)
        {
            ImGui.Spacing();
            DrawMultiplierInput("Normal Win Multiplier", ref _config.MultiplierNormalWin);
        }

        ImGui.Spacing();
        DrawMultiplierInput("Natural BJ Multiplier (2 Cards)", ref _config.MultiplierBlackjackWin);

        if (level >= (int)UserLevel.Advanced)
        {
            ImGui.Spacing();
            DrawMultiplierInput("Dirty BJ Multiplier (3+ Cards)", ref _config.MultiplierDirtyBlackjackWin);
        }
    }

    private void DrawSettingsTab_Visual(int level)
    {
        if (ImGui.BeginTabItem("Visual"))
        {
            ImGui.Spacing();
            ImGui.TextUnformatted("Button Style");
            ImGui.Separator();

            ImGui.Spacing();
            ImGui.TextUnformatted("Button Colors");
            ImGui.SameLine(300f);
            ImGui.SetNextItemWidth(95f);
            if (ImGui.ColorEdit4("Bg##btn_color", ref _config.ButtonColor, ImGuiColorEditFlags.NoAlpha))
            {
                _config.ButtonColor.W = 1.0f;
                _save();
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(95f);
            if (ImGui.ColorEdit4("Text##btn_text_color", ref _config.ButtonTextColor, ImGuiColorEditFlags.NoAlpha))
            {
                _config.ButtonTextColor.W = 1.0f;
                _save();
            }

            ImGui.Spacing();
            DrawV3RuleActionButton("Hide card suits", "v1_hide_suits", ref _config.HideCardSuits);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Hide suit icons from card display.\nCards show only their value (A, 2-10, J, Q, K).");

            if (level >= (int)UserLevel.Advanced)
            {
                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.TextUnformatted("Highlight");
                ImGui.Separator();

                ImGui.Spacing();
                ImGui.TextUnformatted("Highlight Color");
                ImGui.SameLine(300f);
                ImGui.SetNextItemWidth(200f);
                if (ImGui.ColorEdit4("##highlight_color", ref _config.HighlightColor, ImGuiColorEditFlags.NoAlpha))
                {
                    _config.HighlightColor.W = 1.0f;
                    _save();
                }

                ImGui.Spacing();
                ImGui.TextUnformatted("Highlight Text Color");
                ImGui.SameLine(300f);
                ImGui.SetNextItemWidth(200f);
                if (ImGui.ColorEdit4("##highlight_text_color", ref _config.HighlightTextColor, ImGuiColorEditFlags.NoAlpha))
                {
                    _config.HighlightTextColor.W = 1.0f;
                    _save();
                }

                ImGui.Spacing();
                if (ImGui.Checkbox("Auto-Rotate on phase change", ref _config.LookEveryTime))
                    _save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Active: Face the configured direction when entering Waiting, Deal, Dealer, or Payout phase.\nInactive: Only at round start or via /initialviewdirection command.");

                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.TextUnformatted("Draw Logic");
                ImGui.Separator();

                ImGui.Spacing();
                ImGui.TextUnformatted("Scale");
                ImGui.SameLine(300f);
                ImGui.SetNextItemWidth(200f);
                if (BJBGui.SliderFloat("##dl_scale", ref _config.DrawLogicScale, 0.05f, 4.0f, "%.2fx"))
                {
                    _config.DrawLogicScale = (float)(Math.Round(_config.DrawLogicScale / 0.05) * 0.05);
                    _config.DrawLogicScale = Math.Clamp(_config.DrawLogicScale, 0.05f, 4.0f);
                    _save();
                }

                ImGui.Spacing();
                ImGui.TextUnformatted("Offset X");
                ImGui.SameLine(300f);
                ImGui.SetNextItemWidth(200f);
                if (ImGui.DragFloat("##dl_offset_x", ref _config.DrawLogicOffsetX, 0.01f, -100f, 100f, "%.2f"))
                    _save();

                ImGui.Spacing();
                ImGui.TextUnformatted("Offset Y");
                ImGui.SameLine(300f);
                ImGui.SetNextItemWidth(200f);
                if (ImGui.DragFloat("##dl_offset_y", ref _config.DrawLogicOffsetY, 0.01f, -100f, 100f, "%.2f"))
                    _save();

                ImGui.Spacing();
                ImGui.TextUnformatted("Offset Z");
                ImGui.SameLine(300f);
                ImGui.SetNextItemWidth(200f);
                if (ImGui.DragFloat("##dl_offset_z", ref _config.DrawLogicOffsetZ, 0.01f, -100f, 100f, "%.2f"))
                    _save();

                ImGui.Spacing();
                ImGui.TextUnformatted("Offset Rotation");
                ImGui.SameLine(300f);
                ImGui.SetNextItemWidth(200f);
                if (BJBGui.SliderFloat("##dl_offset_r", ref _config.DrawLogicOffsetR, 0.0f, 359.99f, "%.2f\u00b0"))
                    _save();

                ImGui.Spacing();
                ImGui.TextUnformatted("Suit Colors");
                ImGui.Separator();

                ImGui.Spacing();
                ImGui.TextUnformatted("Spades");
                ImGui.SameLine(300f);
                ImGui.SetNextItemWidth(200f);
                if (ImGui.ColorEdit4("##dl_color_spades", ref _config.DrawLogicColorSpades, ImGuiColorEditFlags.NoAlpha))
                {
                    _config.DrawLogicColorSpades.W = 1.0f;
                    _save();
                }

                ImGui.Spacing();
                ImGui.TextUnformatted("Clubs");
                ImGui.SameLine(300f);
                ImGui.SetNextItemWidth(200f);
                if (ImGui.ColorEdit4("##dl_color_clubs", ref _config.DrawLogicColorClubs, ImGuiColorEditFlags.NoAlpha))
                {
                    _config.DrawLogicColorClubs.W = 1.0f;
                    _save();
                }

                ImGui.Spacing();
                ImGui.TextUnformatted("Hearts");
                ImGui.SameLine(300f);
                ImGui.SetNextItemWidth(200f);
                if (ImGui.ColorEdit4("##dl_color_hearts", ref _config.DrawLogicColorHearts, ImGuiColorEditFlags.NoAlpha))
                {
                    _config.DrawLogicColorHearts.W = 1.0f;
                    _save();
                }

                ImGui.Spacing();
                ImGui.TextUnformatted("Diamonds");
                ImGui.SameLine(300f);
                ImGui.SetNextItemWidth(200f);
                if (ImGui.ColorEdit4("##dl_color_diamonds", ref _config.DrawLogicColorDiamonds, ImGuiColorEditFlags.NoAlpha))
                {
                    _config.DrawLogicColorDiamonds.W = 1.0f;
                    _save();
                }
            }

            ImGui.EndTabItem();
        }
    }

    private void DrawSettingsTab_OwnButtons(int level)
    {
        if (ImGui.BeginTabItem("Own Buttons"))
        {
            ImGui.Spacing();
            ImGui.TextUnformatted("Padding");
            ImGui.Separator();
            ImGui.Spacing();

            DrawPaddingFields("##global", ref _config.CustomButtonPaddingH, ref _config.CustomButtonPaddingV);

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextUnformatted("Font");
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextUnformatted("Font");
            ImGui.SameLine(300f);
            ImGui.SetNextItemWidth(200f);
            int fontIdx = _config.CustomButtonUseMono ? 1 : 0;
            if (BJBGui.Combo("##global_font", ref fontIdx, "Default\0Mono\0"))
            {
                _config.CustomButtonUseMono = fontIdx == 1;
                _save();
            }

            ImGui.Spacing();
            ImGui.TextUnformatted("Size");
            ImGui.SameLine(300f);
            ImGui.SetNextItemWidth(200f);
            if (BJBGui.SliderFloat("##global_font_scale", ref _config.CustomButtonFontScale, 0.5f, 2.0f, "%.2fx"))
            {
                _config.CustomButtonFontScale = (float)(Math.Round(_config.CustomButtonFontScale / 0.05) * 0.05);
                _config.CustomButtonFontScale = Math.Clamp(_config.CustomButtonFontScale, 0.5f, 2.0f);
                _save();
            }

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextUnformatted("Popout Bar");
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextUnformatted("Transparent Background");
            ImGui.SameLine(300f);
            if (ImGui.Checkbox("##bar_nobg", ref _config.ButtonBarNoBackground)) _save();

            ImGui.TextUnformatted("Lock Position");
            ImGui.SameLine(300f);
            if (ImGui.Checkbox("##bar_lock", ref _config.ButtonBarLocked)) _save();

            ImGui.EndTabItem();
        }
    }

    private void DrawPaddingFields(string idSuffix, ref float paddingH, ref float paddingV)
    {
        bool synced = Math.Abs(paddingH - paddingV) < 0.001f;

        ImGui.TextUnformatted("All");
        ImGui.SameLine(300f);
        ImGui.SetNextItemWidth(200f);
        if (!synced) ImGui.BeginDisabled();
        float allVal = synced ? paddingH : 0f;
        string allDisplay = synced ? "%.1f" : "—";
        if (ImGui.DragFloat($"##pad_all{idSuffix}", ref allVal, 0.5f, 0f, 50f, allDisplay))
        {
            allVal = Math.Clamp(allVal, 0f, 50f);
            paddingH = allVal;
            paddingV = allVal;
            _save();
        }
        if (!synced) ImGui.EndDisabled();

        ImGui.Spacing();
        ImGui.TextUnformatted("Horizontal");
        ImGui.SameLine(300f);
        ImGui.SetNextItemWidth(200f);
        if (ImGui.DragFloat($"##pad_h{idSuffix}", ref paddingH, 0.5f, 0f, 50f, "%.1f"))
        {
            paddingH = Math.Clamp(paddingH, 0f, 50f);
            _save();
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Vertical");
        ImGui.SameLine(300f);
        ImGui.SetNextItemWidth(200f);
        if (ImGui.DragFloat($"##pad_v{idSuffix}", ref paddingV, 0.5f, 0f, 50f, "%.1f"))
        {
            paddingV = Math.Clamp(paddingV, 0f, 50f);
            _save();
        }
    }

    private void DrawSettingsTab_Sound(int level)
    {
        if (ImGui.BeginTabItem("Sound"))
        {
            ImGui.Spacing();
            DrawNearbySoundSettings("v1");

            ImGui.EndTabItem();
        }
    }

    private void DrawSettingsTab_System(int level)
    {
        if (ImGui.BeginTabItem("System"))
        {
            ImGui.Spacing();

            if (ImGui.Checkbox("Disable update popup", ref _config.DisableUpdatePopup)) _save();
            if (BJBGui.Button("Open Changelog"))
                Plugin.Instance.OpenChangelog();
            if (ImGui.Checkbox("Hide Thanks page", ref _config.HideThanksPage)) _save();
            var allowZeroBet = GameEngine.AllowZeroBetForSession;
            if (ImGui.Checkbox("Allow 0 bet", ref allowZeroBet))
                GameEngine.AllowZeroBetForSession = allowZeroBet;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Session-only setting. Resets to disabled when the plugin reloads.");

            /* Card-Companion App UI is intentionally hidden, but retained for later reuse.
            ImGui.Spacing();
            ImGui.TextUnformatted("Card-Companion App");
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.Checkbox("Enable Companion Synchronization", ref _config.EnableCompanionSync)) _save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Mirrors active player card state to the configured companion server.");

            ImGui.TextUnformatted("Server Address");
            ImGui.SameLine(300f);
            ImGui.SetNextItemWidth(260f);
            if (ImGui.InputText("##companion_server", ref _config.CompanionServerAddress, 255))
                _save();

            ImGui.TextUnformatted("Timeout");
            ImGui.SameLine(300f);
            ImGui.SetNextItemWidth(200f);
            var companionTimeout = _config.CompanionTimeoutMs;
            if (ImGui.SliderInt("##companion_timeout", ref companionTimeout, 1, 1000, "%d ms"))
            {
                _config.CompanionTimeoutMs = Math.Clamp(companionTimeout, 1, 1000);
                _save();
            }
            */

            /* Defaults reset section is intentionally hidden with its reset button.
            ImGui.Spacing();
            ImGui.TextUnformatted("Defaults");
            ImGui.Separator();
            ImGui.Spacing();
            */

            var io = ImGui.GetIO();
            bool keysDown = io.KeyCtrl && io.KeyShift;

            /* The defaults-file reset UI is deliberately hidden, but its implementation remains here.
            if (!keysDown) ImGui.BeginDisabled();
            if (keysDown) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0f, 0f, 1f));

            if (BJBGui.Button("Reset Default Config File##reset_defaults_file"))
            {
                DefaultsMigration.ResetSnapshotFile();
            }

            if (keysDown) ImGui.PopStyleColor();
            if (!keysDown)
            {
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip("Hold CTRL + SHIFT to unlock this button.");
            }

            if (keysDown)
            {
                ImGui.TextColored(new Vector4(1, 0, 0, 1),
                    "WARNING: This will completely reset the defaults file. All accumulated updates will be lost.");
            }
            */

            if (level >= (int)UserLevel.Dev)
            {
                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.TextUnformatted("Wait Timer");
                ImGui.Separator();

                bool unlockWait = _config.UnlockWaitTimer;
                if (ImGui.Checkbox("Unlock wait timer##dev_unlock_wait", ref unlockWait))
                {
                    _config.UnlockWaitTimer = unlockWait;
                    if (!unlockWait)
                    {
                        foreach (var g in _config.CommandGroups.Concat(_config.CustomCommandGroups))
                            foreach (var c in g.Commands)
                                if (c.Delay > 12f) c.Delay = 12f;
                    }
                    _save();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Increases the maximum wait time to 30 s.\nWhen disabled, all entries above 12 s are capped at 12 s.");

                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.TextUnformatted("Stats Integrity");
                ImGui.Separator();

                if (_config.HashedStats)
                {
                    if (!keysDown) ImGui.BeginDisabled();
                    bool hashedVal = _config.HashedStats;
                    if (ImGui.Checkbox("Hashed Stats##dev_hashed_stats", ref hashedVal))
                    {
                        _config.HashedStats = true;
                        _openHashedStatsConfirm = true;
                    }
                    if (!keysDown)
                    {
                        ImGui.EndDisabled();
                        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                            ImGui.SetTooltip("Hold CTRL + SHIFT to unlock this option.");
                    }
                }
                else
                {
                    bool hashedVal = _config.HashedStats;
                    if (ImGui.Checkbox("Hashed Stats##dev_hashed_stats", ref hashedVal))
                    {
                        _config.HashedStats = true;
                        _save();
                    }
                }

                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.TextUnformatted("Config File");
                ImGui.Separator();
                ImGui.Spacing();

                if (BJBGui.Button("Export##cfg")) {
                    var exportObj = JObject.FromObject(_config);
                    exportObj.Remove("Presets");
                    var json = exportObj.ToString(Formatting.Indented);
                    _fileDialogManager.SaveFileDialog(
                        "Export Config", "JSON Files{.json}", "bjb_config", ".json",
                        (ok, path) => {
                            if (ok && !string.IsNullOrWhiteSpace(path))
                                System.IO.File.WriteAllText(path, json);
                        });
                }

                ImGui.SameLine();

                if (BJBGui.Button("Import##cfg")) {
                    _fileDialogManager.OpenFileDialog(
                        "Import Config", "JSON Files{.json}",
                        (ok, path) => {
                            if (!ok || string.IsNullOrWhiteSpace(path)) return;
                            try {
                                var json = System.IO.File.ReadAllText(path);
                                _tempImportJson = JObject.Parse(json);
                                if (!_tempImportJson.ContainsKey("UnlockWaitTimer")
                                    || !_tempImportJson.ContainsKey("EnableBankInput")
                                    || !_tempImportJson.ContainsKey("NotepadText"))
                                    DoMerge();
                                else
                                    _openImportConfirmPopup = true;
                            } catch { }
                        });
                }

                ImGui.Spacing();
                ImGui.TextUnformatted("Export and Import is in beta phase");
            }

            ImGui.EndTabItem();
        }
    }

    private void DrawMultiplierInput(string label, ref float value)
    {
        ImGui.TextUnformatted(label);
        ImGui.SameLine(300f);
        ImGui.SetNextItemWidth(200f);
        if (BJBGui.InputFloat($"##input_{label}", ref value, 0.25f, 0.5f, "%.2fx"))
        {
            value = Math.Clamp(value, 0.0f, 5.0f);
            _save();
        }
    }

    private void TryApply<T>(JObject j, string key, Action<T> setter)
    {
        if (j.TryGetValue(key, out var token))
            setter(token.ToObject<T>()!);
    }

    private void ApplyScalars(JObject j)
    {
        TryApply<bool>  (j, "FirstDealThenPlay",                      v => _config.FirstDealThenPlay = v);
        TryApply<bool>  (j, "PlayerRollingForThemselves",             v => _config.PlayerRollingForThemselves = v);
        TryApply<bool>  (j, "IdenticalSplitOnly",                     v => _config.IdenticalSplitOnly = v);
        TryApply<bool>  (j, "EnableSplit",                            v => _config.EnableSplit = v);
        TryApply<bool>  (j, "EnableDoubleDown",                       v => _config.EnableDoubleDown = v);
        TryApply<bool>  (j, "EnableDirtyBlackjack",                   v => _config.EnableDirtyBlackjack = v);
        TryApply<bool>  (j, "AllowDoubleDownAfterSplit",               v => _config.AllowDoubleDownAfterSplit = v);
        TryApply<int>   (j, "MaxHandsPerPlayer",                      v => _config.MaxHandsPerPlayer = v);
        TryApply<float> (j, "MultiplierNormalWin",                    v => _config.MultiplierNormalWin = v);
        TryApply<float> (j, "MultiplierBlackjackWin",                 v => _config.MultiplierBlackjackWin = v);
        TryApply<float> (j, "MultiplierDirtyBlackjackWin",            v => _config.MultiplierDirtyBlackjackWin = v);
        TryApply<bool>  (j, "RefundFullDoubleDownOnPush",             v => _config.RefundFullDoubleDownOnPush = v);
        TryApply<int>   (j, "BlackjackTieRule",                       v => _config.BlackjackTieRule = (BlackjackTieRule)v);
        TryApply<bool>  (j, "EnableCharlie",                         v => _config.EnableCharlie = v);
        TryApply<int>   (j, "CharlieCardCount",                      v => _config.CharlieCardCount = v);
        TryApply<bool>  (j, "CharlieInstantWin",                     v => _config.CharlieInstantWin = v);
        TryApply<bool>  (j, "EnableBankInput",                        v => _config.EnableBankInput = v);
        TryApply<string>(j, "AutoBetPostCommandName",                 v => _config.AutoBetPostCommandName = v);
        TryApply<string>(j, "InsufficientBetCommandName",             v => _config.InsufficientBetCommandName = v);
        TryApply<bool>  (j, "EnableCompanionSync",                    v => _config.EnableCompanionSync = v);
        TryApply<string>(j, "CompanionServerAddress",                 v => _config.CompanionServerAddress = v);
        TryApply<int>   (j, "CompanionTimeoutMs",                     v => _config.CompanionTimeoutMs = Math.Clamp(v, 1, 1000));
        TryApply<bool>  (j, "EnableAntiDouble",                       v => _config.EnableAntiDouble = v);
        TryApply<long>  (j, "MinBet",                                 v => _config.MinBet = v);
        TryApply<long>  (j, "MaxBet",                                 v => _config.MaxBet = v);
        TryApply<bool>  (j, "ShortBetFormat",                         v => _config.ShortBetFormat = v);
        TryApply<bool>  (j, "HideCardSuits",                         v => _config.HideCardSuits = v);
        TryApply<bool>  (j, "AutoInitialDeal",                        v => _config.AutoInitialDeal = v);
        TryApply<bool>  (j, "AutoDealerDraw",                         v => _config.AutoDealerDraw = v);
        TryApply<bool>  (j, "AutoRun",                                v => _config.AutoRun = v);
        TryApply<bool>  (j, "AutoActivateTradingPlayers",             v => _config.AutoActivateTradingPlayers = v);
        TryApply<bool>  (j, "EnableAutomation",                       v => _config.EnableAutomation = v);
        TryApply<bool>  (j, "ShowAutoDealerDrawButton",               v => _config.ShowAutoDealerDrawButton = v);
        TryApply<bool>  (j, "ShowAutoPlayerHandButton",               v => _config.ShowAutoPlayerHandButton = v);
        TryApply<bool>  (j, "ShowAutoContinueButton",                 v => _config.ShowAutoContinueButton = v);
        TryApply<bool>  (j, "ShowAutoRunButton",                      v => _config.ShowAutoRunButton = v);
        TryApply<int>   (j, "DealerDrawsUntil",                       v => _config.DealerDrawsUntil = v);
        TryApply<bool>  (j, "DealerSoftRule",                         v => _config.DealerSoftRule = v);
        TryApply<bool>  (j, "SmallResult",                            v => _config.SmallResult = v);
        TryApply<string>(j, "ResultTemplate",                         v => _config.ResultTemplate = v);
        TryApply<List<ShortResultRule>>(j, "ShortResultRules",        v => _config.ShortResultRules = v);
        TryApply<bool>  (j, "AutostartRoundOnlyOnMultiplePlayers",     v => _config.AutostartRoundOnlyOnMultiplePlayers = v);
        TryApply<float> (j, "CommandSpeedMultiplier",                  v => _config.CommandSpeedMultiplier = v);
        TryApply<Vector4>(j, "HighlightColor",                        v => _config.HighlightColor = v);
        TryApply<Vector4>(j, "HighlightTextColor",                    v => _config.HighlightTextColor = v);
        TryApply<Vector4>(j, "ButtonColor",                           v => _config.ButtonColor = v);
        TryApply<Vector4>(j, "ButtonTextColor",                       v => _config.ButtonTextColor = v);
        TryApply<bool>  (j, "HideStandardBatches",                    v => _config.HideStandardBatches = v);
        TryApply<bool>  (j, "AllowEditingStandardRegex",               v => _config.AllowEditingStandardRegex = v);
        TryApply<UserLevel>(j, "CurrentLevel",                        v => _config.CurrentLevel = v);
        TryApply<float> (j, "NearbyDistanceCap",                      v => _config.NearbyDistanceCap = v);
        TryApply<bool>  (j, "ShowNearbyPlayers",                      v => _config.ShowNearbyPlayers = v);
        TryApply<bool>  (j, "NearbySticky",                           v => _config.NearbySticky = v);
        TryApply<int>   (j, "NearbyColumns",                          v => _config.NearbyColumns = v);
        TryApply<string>(j, "NearbyQuestionCommandName",              v => _config.NearbyQuestionCommandName = v);
        TryApply<bool>  (j, "NoAutoDequeue",                          v => _config.NoAutoDequeue = v);
        TryApply<bool>  (j, "NearbyShowFootNumbers",                  v => _config.NearbyShowFootNumbers = v);
        TryApply<float> (j, "NearbyOffsetX",                          v => _config.NearbyOffsetX = v);
        TryApply<float> (j, "NearbyOffsetZ",                          v => _config.NearbyOffsetZ = v);
        TryApply<NearbyShapeMode>(j, "NearbyShape",                   v => _config.NearbyShape = v);
        TryApply<float> (j, "NearbyRectangleAspectRatio",             v => _config.NearbyRectangleAspectRatio = Math.Clamp(v, 0.1f, 10f));
        TryApply<float> (j, "NearbyRectangleRotation",                v => _config.NearbyRectangleRotation = Math.Clamp(v, -180f, 180f));
        TryApply<bool>  (j, "NearbyUseFixedPosition",                 v => _config.NearbyUseFixedPosition = v);
        TryApply<float> (j, "NearbyFixedCenterX",                     v => _config.NearbyFixedCenterX = v);
        TryApply<float> (j, "NearbyFixedCenterY",                     v => _config.NearbyFixedCenterY = v);
        TryApply<float> (j, "NearbyFixedCenterZ",                     v => _config.NearbyFixedCenterZ = v);
        TryApply<bool>  (j, "NearbyFixedCenterCaptured",              v => _config.NearbyFixedCenterCaptured = v);
        TryApply<bool>  (j, "NearbyAutoActEnabled",                   v => _config.NearbyAutoActEnabled = v);
        TryApply<string>(j, "NearbyAutoActCommandName",               v => _config.NearbyAutoActCommandName = v);
        TryApply<string>(j, "AllianceNearbyCommandName",              v => _config.AllianceNearbyCommandName = v);
        TryApply<float> (j, "NearbyAutoActTimeoutMinutes",            v => _config.NearbyAutoActTimeoutMinutes = Math.Clamp(v, 1f, 1440f));
        TryApply<float> (j, "CustomButtonPaddingH",                   v => _config.CustomButtonPaddingH = v);
        TryApply<float> (j, "CustomButtonPaddingV",                   v => _config.CustomButtonPaddingV = v);
        TryApply<float> (j, "CustomButtonFontScale",                  v => _config.CustomButtonFontScale = v);
        TryApply<bool>  (j, "CustomButtonUseMono",                    v => _config.CustomButtonUseMono = v);
        TryApply<string>(j, "SelectedFontName",                       v => _config.SelectedFontName = v);
        TryApply<ButtonBarLayout>(j, "ButtonBarLayout",               v => _config.ButtonBarLayout = v);
        TryApply<bool>  (j, "ButtonBarFixedWidth",                    v => _config.ButtonBarFixedWidth = v);
        TryApply<float> (j, "ButtonBarFixedWidthValue",               v => _config.ButtonBarFixedWidthValue = v);
        TryApply<Vector4>(j, "ButtonBarBackgroundColor",              v => _config.ButtonBarBackgroundColor = v);
        TryApply<float> (j, "InitialViewDirection",                   v => _config.InitialViewDirection = v);
        TryApply<bool>  (j, "LookEveryTime",                         v => _config.LookEveryTime = v);
        TryApply<bool>  (j, "UnlockWaitTimer",                       v => _config.UnlockWaitTimer = v);
        TryApply<float> (j, "PayoutPercent",                           v => _config.PayoutPercent = v);
        TryApply<long>  (j, "GilPerHour",                             v => _config.GilPerHour = v);
        TryApply<int>   (j, "ClipHoursMode",                          v => _config.ClipHoursMode = v);
        TryApply<bool>  (j, "NearbyAlertEnabled",                    v => _config.NearbyAlertEnabled = v);
        TryApply<WageInterval>(j, "WageIntervalMode",                v => _config.WageIntervalMode = v);
        TryApply<float> (j, "NearbyAlertVolume",                     v => _config.NearbyAlertVolume = v);
        TryApply<float> (j, "NearbyAlertCooldown",                   v => _config.NearbyAlertCooldown = v);
        TryApply<bool>  (j, "NearbyAlertSoundEntriesMigrated",       v => _config.NearbyAlertSoundEntriesMigrated = v);
        TryApply<NearbyAlertSoundMode>(j, "NearbyAlertSoundMode",     v => _config.NearbyAlertSoundMode = v);
        TryApply<bool>  (j, "NearbyAlwaysShowCircle",                v => _config.NearbyAlwaysShowCircle = v);
        TryApply<bool>  (j, "AutoContinue",                          v => _config.AutoContinue = v);
        TryApply<float> (j, "AutoContinueDelay",                     v => _config.AutoContinueDelay = v);
        TryApply<Vector4>(j, "AutoContinueBarColor",                  v => _config.AutoContinueBarColor = v);
        TryApply<float> (j, "AutoContinueBarHeight",                  v => _config.AutoContinueBarHeight = v);
        TryApply<bool>  (j, "AutoContinueBarShowText",                v => _config.AutoContinueBarShowText = v);
        TryApply<float> (j, "DrawLogicScale",                          v => _config.DrawLogicScale = v);
        TryApply<float> (j, "DrawLogicOffsetX",                        v => _config.DrawLogicOffsetX = v);
        TryApply<float> (j, "DrawLogicOffsetY",                        v => _config.DrawLogicOffsetY = v);
        TryApply<float> (j, "DrawLogicOffsetZ",                        v => _config.DrawLogicOffsetZ = v);
        TryApply<float> (j, "DrawLogicOffsetR",                        v => _config.DrawLogicOffsetR = v);
        TryApply<Vector4>(j, "DrawLogicColorSpades",                   v => _config.DrawLogicColorSpades = v);
        TryApply<Vector4>(j, "DrawLogicColorClubs",                    v => _config.DrawLogicColorClubs = v);
        TryApply<Vector4>(j, "DrawLogicColorHearts",                   v => _config.DrawLogicColorHearts = v);
        TryApply<Vector4>(j, "DrawLogicColorDiamonds",                 v => _config.DrawLogicColorDiamonds = v);
        // Keep the old integral offset as a fallback, then let the new lossless data win.
        TryApply<int>   (j, "UtcOffsetHours",                            v => _config.UtcOffsetHours = v);
        TryApply<int>   (j, "UtcOffsetMinutes",                          v => _config.UtcOffsetMinutes = v);
        TryApply<string>(j, "UtcTimeZoneName",                           v => _config.UtcTimeZoneName = v);
        TryApply<bool>  (j, "UtcSummerTime",                             v => _config.UtcSummerTime = v);
        TryApply<bool>  (j, "UtcOffsetConfigured",                       v => _config.UtcOffsetConfigured = v);
        TryApply<bool>  (j, "BetLimitEntriesMigrated",                   v => _config.BetLimitEntriesMigrated = v);
    }

    private void DoFullReplace() {
        if (_tempImportJson == null) return;

        ApplyScalars(_tempImportJson);

        if (_tempImportJson.ContainsKey("CommandGroups"))
            _config.CommandGroups = _tempImportJson["CommandGroups"]!.ToObject<List<CommandGroup>>()!;
        if (_tempImportJson.ContainsKey("CustomCommandGroups"))
            _config.CustomCommandGroups = _tempImportJson["CustomCommandGroups"]!.ToObject<List<CommandGroup>>()!;
        if (_tempImportJson.ContainsKey("MessageBatches"))
            _config.MessageBatches = _tempImportJson["MessageBatches"]!.ToObject<List<MessageBatch>>()!;
        if (_tempImportJson.ContainsKey("UserRegexes"))
            _config.UserRegexes = _tempImportJson["UserRegexes"]!.ToObject<List<UserRegexEntry>>()!;
        if (_tempImportJson.ContainsKey("VipBetTiers"))
            _config.VipBetTiers = _tempImportJson["VipBetTiers"]!.ToObject<List<VipBetTier>>()!;
        if (_tempImportJson.ContainsKey("BetLimitEntries"))
        {
            _config.BetLimitEntries = _tempImportJson["BetLimitEntries"]!.ToObject<List<BetLimitEntry>>()!;
            if (!_tempImportJson.ContainsKey("BetLimitEntriesMigrated"))
                _config.BetLimitEntriesMigrated = false;
        }
        if (_tempImportJson.ContainsKey("BettingPresets"))
            _config.BettingPresets = _tempImportJson["BettingPresets"]!.ToObject<List<BettingPreset>>()!;
        if (_tempImportJson.ContainsKey("CustomButtonOrder"))
        {
            _config.CustomButtonOrder = _tempImportJson["CustomButtonOrder"]!.ToObject<List<string>>()!;
            if (!_tempImportJson.ContainsKey("CustomButtonEntries"))
                _config.ResetCustomButtonEntriesFromLegacy();
        }
        if (_tempImportJson.ContainsKey("CustomButtonEntries"))
        {
            _config.CustomButtonEntries = _tempImportJson["CustomButtonEntries"]!.ToObject<List<CustomButtonEntry>>()!;
            _config.CustomButtonEntriesMigrated = true;
        }
        if (_tempImportJson.ContainsKey("NearbyAlertSoundFiles"))
        {
            _config.NearbyAlertSoundFiles = _tempImportJson["NearbyAlertSoundFiles"]!.ToObject<List<string>>()!;
            if (!_tempImportJson.ContainsKey("NearbyAlertSoundEntries"))
                _config.NearbyAlertSoundEntriesMigrated = false;
        }
        if (_tempImportJson.ContainsKey("NearbyAlertSoundEntries"))
            _config.NearbyAlertSoundEntries = _tempImportJson["NearbyAlertSoundEntries"]!.ToObject<List<NearbyAlertSoundEntry>>()!;
        if (_tempImportJson.ContainsKey("NearbyAutoActIgnoreList"))
            _config.NearbyAutoActIgnoreList = _tempImportJson["NearbyAutoActIgnoreList"]!.ToObject<List<string>>()!;

        _config.EnsureLayout3Migrations();

        _save();
    }

    private void MergeNamedList<T>(JObject j, string key, List<T> target, Func<T, string> nameSelector)
    {
        if (!j.ContainsKey(key)) return;
        var items = j[key]!.ToObject<List<T>>()!;
        foreach (var item in items)
        {
            var name = nameSelector(item);
            target.RemoveAll(x => nameSelector(x) == name);
            target.Add(item);
        }
    }

    private void DoMerge() {
        if (_tempImportJson == null) return;

        ApplyScalars(_tempImportJson);

        MergeNamedList(_tempImportJson, "CommandGroups",       _config.CommandGroups,       x => x.Name);
        MergeNamedList(_tempImportJson, "CustomCommandGroups", _config.CustomCommandGroups, x => x.Name);
        MergeNamedList(_tempImportJson, "MessageBatches",      _config.MessageBatches,      x => x.Name);
        MergeNamedList(_tempImportJson, "UserRegexes",         _config.UserRegexes,         x => x.Name);
        MergeNamedList(_tempImportJson, "VipBetTiers",         _config.VipBetTiers,         x => x.Name);
        if (_tempImportJson.ContainsKey("BetLimitEntries"))
        {
            _config.BetLimitEntries = _tempImportJson["BetLimitEntries"]!.ToObject<List<BetLimitEntry>>()!;
            if (!_tempImportJson.ContainsKey("BetLimitEntriesMigrated"))
                _config.BetLimitEntriesMigrated = false;
        }
        if (_tempImportJson.ContainsKey("BettingPresets"))
            _config.BettingPresets = _tempImportJson["BettingPresets"]!.ToObject<List<BettingPreset>>()!;

        if (_tempImportJson.ContainsKey("CustomButtonOrder"))
        {
            _config.CustomButtonOrder = _tempImportJson["CustomButtonOrder"]!.ToObject<List<string>>()!;
            if (!_tempImportJson.ContainsKey("CustomButtonEntries"))
                _config.ResetCustomButtonEntriesFromLegacy();
        }
        if (_tempImportJson.ContainsKey("CustomButtonEntries"))
        {
            _config.CustomButtonEntries = _tempImportJson["CustomButtonEntries"]!.ToObject<List<CustomButtonEntry>>()!;
            _config.CustomButtonEntriesMigrated = true;
        }
        if (_tempImportJson.ContainsKey("NearbyAlertSoundFiles"))
        {
            _config.NearbyAlertSoundFiles = _tempImportJson["NearbyAlertSoundFiles"]!.ToObject<List<string>>()!;
            if (!_tempImportJson.ContainsKey("NearbyAlertSoundEntries"))
                _config.NearbyAlertSoundEntriesMigrated = false;
        }
        if (_tempImportJson.ContainsKey("NearbyAlertSoundEntries"))
            _config.NearbyAlertSoundEntries = _tempImportJson["NearbyAlertSoundEntries"]!.ToObject<List<NearbyAlertSoundEntry>>()!;
        if (_tempImportJson.ContainsKey("NearbyAutoActIgnoreList"))
            _config.NearbyAutoActIgnoreList = _tempImportJson["NearbyAutoActIgnoreList"]!.ToObject<List<string>>()!;

        _config.EnsureLayout3Migrations();

        _save();
    }
}
