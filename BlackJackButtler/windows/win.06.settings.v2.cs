using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using BlackJackButtler.Chat;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private List<BetLimitEntry>? _betDraftEntries;
    private bool _betDraftDirty;
    private int _settingsV2TabIndex;
    private int _settingsV2PreviousTabIndex;
    private int? _settingsV2ReturnTab;
    private Page? _settingsV2PendingPage;
    private bool _settingsV2DiscardPopupOpen;
    private readonly List<List<ShortResultRule>> _shortResultUndoHistory = new();
    private string _shortResultImportStatus = string.Empty;
    private bool _nearbyCooldownEditMode;
    private string _nearbyCooldownInput = string.Empty;
    private string _nearbySoundTestStatus = string.Empty;
    private bool _nearbySoundTestFailed;

    private static readonly string[] SettingsV2Tabs =
    {
        "General", "Automation", "Rules", "Betting", "Time & Delay",
        "Nearby Players", "Visual", "System"
    };

    private void DrawSettingsPageV2(int level)
    {
        if (ImGui.BeginTabBar("##settings_v2_tabs"))
        {
            DrawSettingsV2Tab(0, "General", () => DrawSettingsV2General(level));
            DrawSettingsV2Tab(1, "Automation", DrawSettingsV2Automation);
            DrawSettingsV2Tab(2, "Rules", DrawSettingsV2Rules);
            DrawSettingsV2Tab(3, "Betting", DrawSettingsV2Betting);
            DrawSettingsV2Tab(4, "Time & Delay", DrawSettingsV2TimeDelay);
            DrawSettingsV2Tab(5, "Nearby Players", DrawSettingsV2Nearby);
            DrawSettingsV2Tab(6, "Visual", DrawSettingsV2Visual);
            DrawSettingsV2Tab(7, "System", () => DrawSettingsV2System(level));
            ImGui.EndTabBar();
        }

        if (_betDraftDirty && _settingsV2PreviousTabIndex == 3 && _settingsV2TabIndex != 3 && !_settingsV2DiscardPopupOpen)
        {
            _settingsV2DiscardPopupOpen = true;
            _settingsV2PendingPage = null;
            ImGui.OpenPopup("bjb_settings_v2_betting_unsaved");
        }

        DrawSettingsV2UnsavedBettingPopup();
        _settingsV2PreviousTabIndex = _settingsV2TabIndex;
    }

    private void DrawSettingsV2Tab(int index, string label, Action draw)
    {
        var flags = _settingsV2ReturnTab == index ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        if (ImGui.BeginTabItem(label, flags))
        {
            _settingsV2TabIndex = index;
            if (_settingsV2ReturnTab == index)
                _settingsV2ReturnTab = null;
            ImGui.Spacing();
            draw();
            ImGui.EndTabItem();
        }
    }

    private void DrawSettingsV2General(int level)
    {
        ImGui.TextUnformatted("User-Level");
        DrawEnumButtons("user_level_v2", ref level, new[] { "Beginner", "Advanced", "Profi", "Custom" }, idx =>
        {
            _config.CurrentLevel = (UserLevel)idx;
            _save();
        }, separateBeforeIndex: 3);

        ImGui.Spacing();
        ImGui.TextUnformatted("Main View");
        int mainView = _config.MainViewVersion switch { 2 => 1, 3 => 2, _ => 0 };
        DrawEnumButtons("main_view_v2", ref mainView, new[] { "Classic", "Compacted", "Modern" }, idx =>
        {
            _config.MainViewVersion = idx + 1;
            _save();
        });

        if (_config.MainViewVersion == 2)
        {
            ImGui.Spacing();
            ImGui.TextUnformatted("Version 2 Density");
            int compact = _config.MainViewV2SuperCompact ? 1 : 0;
            DrawEnumButtons("main_view_v2_compact", ref compact, new[] { "Normal", "Compact" }, idx =>
            {
                _config.MainViewV2SuperCompact = idx == 1;
                _save();
            });
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Menu Style");
        int menuMode = (int)_config.MenuStyle;
        DrawEnumButtons("menu_style_v2", ref menuMode, new[] { "Side", "Burger", "Tabs" }, idx =>
        {
            _config.MenuStyle = (MenuStyleMode)idx;
            _save();
        });

        ImGui.Spacing();
        ImGui.TextUnformatted("Gil Display");
        int gilVisual = (int)_config.GilVisual;
        ImGui.PushFont(UiBuilder.MonoFont);
        DrawEnumButtons("gil_visual_v2", ref gilVisual, new[] { "12345678", "     12,345,678", "   , 12,345,678" }, idx =>
        {
            _config.GilVisual = (GilVisualMode)idx;
            _save();
        }, joined: false);
        ImGui.PopFont();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("How Gil amounts are displayed in all Gil input fields.");
    }

    private void DrawSettingsV2Automation()
    {
        if (CheckSaveChanged("Enable Automation", ref _config.EnableAutomation) && !_config.EnableAutomation)
            DisableAllAutomationStates();
        if (CheckSaveChanged("Enable Auto Dealer Draw", ref _config.ShowAutoDealerDrawButton) && !_config.ShowAutoDealerDrawButton)
            _config.AutoDealerDraw = false;
        if (CheckSaveChanged("Enable Auto Player Hand", ref _config.ShowAutoPlayerHandButton) && !_config.ShowAutoPlayerHandButton)
            _config.AutoInitialDeal = false;
        if (CheckSaveChanged("Enable Auto Continue", ref _config.ShowAutoContinueButton) && !_config.ShowAutoContinueButton)
            _config.AutoContinue = false;

        ImGui.Spacing();
        ImGui.TextUnformatted("Auto-Continue Delay");
        ImGui.SameLine(260f);
        ImGui.SetNextItemWidth(220f);
        if (BJBGui.SliderFloat("##v2_auto_continue_delay", ref _config.AutoContinueDelay, 10f, 180f, "%.0fs"))
        {
            _config.AutoContinueDelay = MathF.Round(_config.AutoContinueDelay);
            _config.AutoContinueDelay = Math.Clamp(_config.AutoContinueDelay, 10f, 180f);
            _save();
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton("Reset##v2_ac_reset"))
        {
            _config.AutoContinueDelay = 30f;
            _save();
        }
        ImGui.SameLine();
        CheckSave("Show remaining seconds", ref _config.AutoContinueBarShowText);

        if (CheckSaveChanged("Enable Auto Run", ref _config.ShowAutoRunButton) && !_config.ShowAutoRunButton)
            _config.AutoRun = false;

        CheckSave("Auto Activate Trading Players", ref _config.AutoActivateTradingPlayers);
        DrawSharedAutomationTimingControls("v2");

        ImGui.Spacing();
        DrawAutoContinueMinimumPlayersSelector("v2_auto_continue_min_players");

    }

    private void DrawSettingsV2Rules()
    {
        Header("Dealing Behavior");
        Indent(() =>
        {
        DrawDealingOrderSelector("v2_dealing_order");
        DrawV3OnOff("Player Self Rolling", "v2_self_rolling", ref _config.PlayerRollingForThemselves);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Players roll their own required cards with /dice 13, /dice alliance 13, or the native /random command.\nDealer rolls are unchanged.");
        DrawV3OnOff("Hide Card Suits", "v2_hide_suits", ref _config.HideCardSuits);
        });

        Header("Dealer Rules");
        Indent(() =>
        {
        ImGui.TextUnformatted("Dealer Stands on:");
        ImGui.SameLine(260f);
        DrawV3RuleActionButton("Soft", "v2_dealer_soft", ref _config.DealerSoftRule);
        ImGui.SameLine(0f, 18f);
        ImGui.SetNextItemWidth(120f);
        if (BJBGui.InputInt("##v2_dealer_draws_until", ref _config.DealerDrawsUntil, 1, defaultValue: 17))
        {
            _config.DealerDrawsUntil = Math.Clamp(_config.DealerDrawsUntil, 2, 21);
            _save();
        }
        });

        Header("Game Settings");
        Indent(() =>
        {
        Header("Win");
        Indent(() =>
        {
            MultiplierInput("Payout", ref _config.MultiplierNormalWin, 1f, "v2_win");
        });

        Header("BlackJack");
        Indent(() =>
        {
        int tie = (int)_config.BlackjackTieRule;
        ImGui.TextUnformatted("BlackJack Priority");
        ImGui.SameLine(260f);
        DrawEnumButtons("bj_tie_v2", ref tie, new[] { "Push", "Player", "Dealer", "Regular" }, idx =>
        {
            _config.BlackjackTieRule = (BlackjackTieRule)idx;
            _save();
        });

        Header("Natural");
        Indent(() =>
        {
        MultiplierInput("Payout", ref _config.MultiplierBlackjackWin, 1.5f, "v2_natbj");
        });

        Header("Dirty");
        Indent(() =>
        {
        CheckRuleSave("Enable Dirty Blackjack##v2_dirty", ref _config.EnableDirtyBlackjack);
        MultiplierInput("Payout", ref _config.MultiplierDirtyBlackjackWin, 1f, "v2_dirtybj");
        });
        });

        Header("Charlie");
        Indent(() =>
        {
        CheckRuleSave("Enable Charlie", ref _config.EnableCharlie);
        CheckRuleSave("Instant-Win", ref _config.CharlieInstantWin);
        IntInputSave("Cards##v2_charlie_cards", ref _config.CharlieCardCount, 3, 9, 1, 5);
        MultiplierInput("Payout", ref _config.MultiplierCharlieWin, 1.5f, "v2_charlie_payout");
        });

        Header("Split");
        Indent(() =>
        {
        CheckRuleSave("Enable Split", ref _config.EnableSplit);
        CheckRuleSave("Identical Split only", ref _config.IdenticalSplitOnly);
        IntInputSave("Max Hands##v2_max_hands", ref _config.MaxHandsPerPlayer, 2, 10, 1, 3);
        MultiplierInput("Payout", ref _config.MultiplierSplitWin, 1f, "v2_split_payout");
        });

        Header("Double Down");
        Indent(() =>
        {
        CheckRuleSave("Enabled##v2_double_down", ref _config.EnableDoubleDown);
        CheckRuleSave("Allow DD after Split", ref _config.AllowDoubleDownAfterSplit);
        CheckRuleSave("Refund DD on Push", ref _config.RefundFullDoubleDownOnPush);
        MultiplierInput("Payout", ref _config.MultiplierDoubleDownWin, 1f, "v2_dd_payout");
        });

        Header("Triple Down");
        Indent(() =>
        {
        CheckRuleSave("Enabled##v2_triple_down", ref _config.EnableTripleDown);
        DrawV3TripleDownPointsLimit();
        CheckRuleSave("Allow TD after Split", ref _config.AllowTripleDownAfterSplit);
        CheckRuleSave("Refund TD on Push", ref _config.RefundFullTripleDownOnPush);
        MultiplierInput("Payout", ref _config.MultiplierTripleDownWin, 1f, "v2_td_payout");
        });
        });

        Header("Result");
        Indent(() =>
        {
        CheckRuleSave("Short Result Messages", ref _config.SmallResult);
        DrawShortResultRulesEditor();
        });
    }

    private void DrawShortResultRulesEditor()
    {
        if (!ImGui.CollapsingHeader("Short-Result configuration##short_result_configuration"))
            return;

        ImGui.Indent(18f);
        DrawShortResultRulesEditorBody();
        ImGui.Unindent(18f);
    }

    private void DrawShortResultRulesEditorBody()
    {
        _config.ShortResultRules ??= Configuration.CreateDefaultShortResultRules();
        var rules = _config.ShortResultRules;
        ImGui.TextWrapped("Builds ${results} from top to bottom. <data> is replaced by the selected result data.");

        if (!ImGui.BeginTable("bjb_short_result_editor_layout", 2,
            ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInnerV))
            return;

        ImGui.TableSetupColumn("Rules", ImGuiTableColumnFlags.WidthStretch, 1.7f);
        ImGui.TableSetupColumn("Example output", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        var canUndoShortResult = _shortResultUndoHistory.Count > 0;
        if (!canUndoShortResult) ImGui.BeginDisabled();
        if (BJBGui.SmallButton($"Undo ({_shortResultUndoHistory.Count})##short_result_undo"))
        {
            var last = _shortResultUndoHistory[^1];
            _shortResultUndoHistory.RemoveAt(_shortResultUndoHistory.Count - 1);
            _config.ShortResultRules = CloneShortResultRules(last);
            _save();
        }
        if (!canUndoShortResult) ImGui.EndDisabled();

        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            ImGui.PushID($"short_result_rule_{i}");
            ImGui.Separator();
            var changedOrder = false;
            var beforeEdit = CloneShortResultRules(rules);
            var ruleEdited = false;
            var summary = $"{i + 1}. {rule.Data}: {rule.Template}";

            if (BJBGui.SmallButton("X##delete"))
            {
                PushShortResultUndo(beforeEdit);
                rules.RemoveAt(i);
                changedOrder = true;
            }
            ImGui.SameLine();
            if (i == 0) ImGui.BeginDisabled();
            if (BJBGui.SmallButton("↑##move_up"))
            {
                PushShortResultUndo(beforeEdit);
                (rules[i - 1], rules[i]) = (rules[i], rules[i - 1]);
                changedOrder = true;
            }
            if (i == 0) ImGui.EndDisabled();
            ImGui.SameLine();
            if (i == rules.Count - 1) ImGui.BeginDisabled();
            if (BJBGui.SmallButton("↓##move_down"))
            {
                PushShortResultUndo(beforeEdit);
                (rules[i + 1], rules[i]) = (rules[i], rules[i + 1]);
                changedOrder = true;
            }
            if (i == rules.Count - 1) ImGui.EndDisabled();
            ImGui.SameLine();
            if (BJBGui.SmallButton("⧉##duplicate"))
            {
                PushShortResultUndo(beforeEdit);
                rules.Insert(i + 1, rule.Clone());
                changedOrder = true;
            }
            ImGui.SameLine();

            if (!changedOrder && ImGui.CollapsingHeader($"{summary}###short_result_rule_header"))
            {

            var source = (int)rule.Data;
            ImGui.SetNextItemWidth(180f);
            if (BJBGui.Combo("Data", ref source, "None (no data)\0Winners\0Pushed\0Loosed\0Busted\0"))
            {
                rule.Data = (ShortResultDataSource)source;
                ruleEdited = true;
            }

            if (DrawShortResultRuleToggle("Visible if empty", ref rule.VisibleIfEmpty)) ruleEdited = true;
            if (DrawShortResultRuleToggle("Visible if content before is empty", ref rule.VisibleIfContentBeforeIsEmpty)) ruleEdited = true;
            if (DrawShortResultRuleToggle("Visible if content after is empty", ref rule.VisibleIfContentAfterIsEmpty)) ruleEdited = true;
            if (DrawShortResultRuleToggle("Compress", ref rule.Compress)) ruleEdited = true;

            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("Template", ref rule.Template, 512)) ruleEdited = true;
            }

            ImGui.PopID();
            if (changedOrder)
            {
                _save();
                break;
            }
            if (ruleEdited)
            {
                PushShortResultUndo(beforeEdit);
                _save();
            }
        }

        ImGui.Separator();
        if (BJBGui.SmallButton("Add result rule"))
        {
            PushShortResultUndo(CloneShortResultRules(rules));
            rules.Add(new ShortResultRule());
            _save();
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton("Reset result rules"))
        {
            PushShortResultUndo(CloneShortResultRules(rules));
            _config.ShortResultRules = Configuration.CreateDefaultShortResultRules();
            _save();
        }

        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText("Outer result template", ref _config.ResultTemplate, 512))
            _save();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Optional wrapper around the rule output. Use ${results}; default is ${results}.");

        ImGui.Separator();
        ImGui.TextUnformatted("Rule-list import / export");
        if (BJBGui.SmallButton("Export rules"))
        {
            var json = JsonConvert.SerializeObject(_config.ShortResultRules, Formatting.Indented);
            _fileDialogManager.SaveFileDialog(
                "Export Short-Result Rules",
                "JSON Files{.json}",
                "bjb_short_result_rules",
                ".json",
                (ok, path) =>
                {
                    if (!ok || string.IsNullOrWhiteSpace(path))
                        return;
                    try
                    {
                        System.IO.File.WriteAllText(path, json);
                        _shortResultImportStatus = $"Exported rules to {path}";
                    }
                    catch (Exception ex)
                    {
                        _shortResultImportStatus = $"Export failed: {ex.Message}";
                    }
                });
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton("Import rules"))
        {
            _fileDialogManager.OpenFileDialog(
                "Import Short-Result Rules",
                "JSON Files{.json}",
                (ok, path) =>
                {
                    if (!ok || string.IsNullOrWhiteSpace(path))
                        return;
                    try
                    {
                        var json = System.IO.File.ReadAllText(path);
                        var imported = JsonConvert.DeserializeObject<List<ShortResultRule>>(json)
                            ?? throw new JsonException("The JSON file does not contain a rule list.");
                        if (imported.Any(rule => rule == null))
                            throw new JsonException("The rule list contains null entries.");

                        PushShortResultUndo(CloneShortResultRules(_config.ShortResultRules));
                        _config.ShortResultRules = CloneShortResultRules(imported);
                        _config.ShortResultRulesInitialized = true;
                        _shortResultImportStatus = $"Imported {imported.Count} rules from {path}";
                        _save();
                    }
                    catch (Exception ex)
                    {
                        _shortResultImportStatus = $"Import failed: {ex.Message}";
                    }
                });
        }
        if (!string.IsNullOrWhiteSpace(_shortResultImportStatus))
            ImGui.TextWrapped(_shortResultImportStatus);

        ImGui.TableNextColumn();
        ImGui.TextUnformatted("Example output");
        var examples = new[]
        {
            RenderShortResultExample(
                new[] { "Alice Winner", "Bob Winner" },
                new[] { "Cara Push", "Dorian Push" },
                new[] { "Eve Lost", "Finn Lost" },
                new[] { "Gina Bust", "Hugo Bust" }),
            RenderShortResultExample(
                Array.Empty<string>(),
                new[] { "Cara Push", "Dorian Push" },
                Array.Empty<string>(),
                new[] { "Gina Bust", "Hugo Bust" }),
            RenderShortResultExample(
                new[] { "Alice Winner" },
                new[] { "Cara Push" },
                Array.Empty<string>(),
                new[] { "Gina Bust", "Hugo Bust" }),
            RenderShortResultExample(
                new[] { "Gina Bust", "Hugo Bust" },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>()),
        };
        var preview = string.Join("\n---\n", examples);
        ImGui.InputTextMultiline(
            "##short_result_example_output",
            ref preview,
            4096,
            new Vector2(-1f, 260f),
            ImGuiInputTextFlags.ReadOnly);

        ImGui.EndTable();
    }

    private string RenderShortResultExample(string[] winners, string[] pushed, string[] loosed, string[] busted)
    {
        var ruleOutput = ShortResultFormatter.Render(_config, winners, pushed, loosed, busted);
        var outerTemplate = string.IsNullOrWhiteSpace(_config.ResultTemplate)
            ? "${results}"
            : _config.ResultTemplate;
        return outerTemplate
            .Replace("${results}", ruleOutput)
            .Replace("<results>", ruleOutput)
            .Replace("${winners}", $"Winners: {FormatShortResultPreviewData(winners)}")
            .Replace("${pushed}", $"Pushed: {FormatShortResultPreviewData(pushed)}")
            .Replace("${loosers}", $"Lost: {FormatShortResultPreviewData(loosed)}")
            .Replace("${busted}", $"Busted: {FormatShortResultPreviewData(busted)}");
    }

    private static string FormatShortResultPreviewData(IEnumerable<string> values)
    {
        var joined = string.Join(", ", values);
        return string.IsNullOrEmpty(joined) ? "~" : joined;
    }

    private static List<ShortResultRule> CloneShortResultRules(IEnumerable<ShortResultRule> rules)
        => rules.Select(rule => rule.Clone()).ToList();

    private static bool DrawShortResultRuleToggle(string label, ref bool value)
    {
        ImGui.TextUnformatted(label);
        ImGui.SameLine(260f);
        return BJBOnOffSwitch.Draw($"short_result_{label}", ref value);
    }

    private void PushShortResultUndo(List<ShortResultRule> snapshot)
    {
        _shortResultUndoHistory.Add(snapshot);
        if (_shortResultUndoHistory.Count > 100)
            _shortResultUndoHistory.RemoveAt(0);
    }

    private void DrawSettingsV2Betting()
    {
        EnsureBetDraft();

        ImGui.TextWrapped("Edit bet entries here. Sorting and runtime application happen only after Save.");
        ImGui.Spacing();

        if (ImGui.BeginTable("bjb_bet_entries_v2", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Active", ImGuiTableColumnFlags.WidthFixed, 55);
            ImGui.TableSetupColumn("Kind", ImGuiTableColumnFlags.WidthFixed, 90);
            ImGui.TableSetupColumn("VIP", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("Amount", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("Delete", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableHeadersRow();

            for (int i = 0; i < _betDraftEntries!.Count; i++)
            {
                var entry = _betDraftEntries[i];
                ImGui.PushID($"bet_entry_{i}");
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                if (ImGui.Checkbox("##active", ref entry.Active)) _betDraftDirty = true;

                ImGui.TableNextColumn();
                if (entry.Kind == BetLimitEntryKind.MinBet)
                {
                    ImGui.TextDisabled("Minimum");
                }
                else
                {
                    var normal = entry.Kind == BetLimitEntryKind.Normal;
                    var previousVipLevel = entry.VipLevel;
                    if (BJBOnOffSwitch.Draw("kind", ref normal, "NRM", "VIP", 42f))
                    {
                        if (normal)
                        {
                            var previousDefault = GetDefaultVipName(previousVipLevel);
                            entry.Kind = BetLimitEntryKind.Normal;
                            entry.VipLevel = 0;
                            if (string.IsNullOrWhiteSpace(entry.Name)
                                || entry.Name.Equals("VIP", StringComparison.OrdinalIgnoreCase)
                                || entry.Name.Equals(previousDefault, StringComparison.OrdinalIgnoreCase))
                                entry.Name = "Max";
                        }
                        else
                        {
                            entry.Kind = BetLimitEntryKind.Vip;
                            entry.VipLevel = GetNextVipLevel(_betDraftEntries);
                            if (string.IsNullOrWhiteSpace(entry.Name)
                                || entry.Name.Equals("Max", StringComparison.OrdinalIgnoreCase))
                                entry.Name = GetDefaultVipName(entry.VipLevel);
                        }
                        _betDraftDirty = true;
                    }
                }

                ImGui.TableNextColumn();
                if (entry.Kind == BetLimitEntryKind.MinBet)
                    ImGui.TextDisabled("-");
                else
                {
                    int oldLevel = entry.VipLevel;
                    string oldDefaultName = GetDefaultVipName(oldLevel);
                    DraftInt("##vip", ref entry.VipLevel, 0, 9);
                    if (entry.VipLevel != oldLevel && (string.IsNullOrWhiteSpace(entry.Name) || entry.Name == oldDefaultName))
                        entry.Name = GetDefaultVipName(entry.VipLevel);
                }

                ImGui.TableNextColumn();
                if (entry.Kind == BetLimitEntryKind.MinBet)
                    ImGui.TextDisabled("-");
                else
                    DraftString("##vip_name", ref entry.Name, GetDefaultVipName(entry.VipLevel));

                ImGui.TableNextColumn();
                DraftLong("##amount", ref entry.Amount, 1, 1_000_000_000);

                ImGui.TableNextColumn();
                if (BJBGui.SmallButton("X##delete"))
                {
                    _betDraftEntries.RemoveAt(i);
                    _betDraftDirty = true;
                    ImGui.PopID();
                    break;
                }

                ImGui.PopID();
            }
            ImGui.EndTable();
        }

        if (BJBGui.SmallButton("+ Add Entry"))
            AddBetDraft(CreateNextBetLimitEntry(_betDraftEntries!));

        Header("Auto-Bet Detection");
        Indent(() =>
        {
            DrawAutoBetPostCommandSelector("v2");
            DrawInsufficientBetCommandSelector("v2");
        });

        ImGui.Spacing();
        if (_betDraftDirty
            ? BJBGui.ButtonHighlighted("Save Betting Changes##v2_bet_save", _config.HighlightColor, _config.HighlightTextColor)
            : BJBGui.Button("Save Betting Changes##v2_bet_save"))
            SaveBetDraft();
        ImGui.SameLine();
        if (BJBGui.Button("Discard##v2_bet_discard"))
            ResetBetDraft();
    }

    private void DrawSettingsV2TimeDelay() => DrawSettingsV3TimeDelay();

    private void DrawSettingsV2Nearby()
    {
        DrawV3OnOff("Enabled", "nearby_enabled", ref _config.ShowNearbyPlayers);
        IntInputSave("Columns##nearby_columns", ref _config.NearbyColumns, 1, 5, 1, 2);
        if (_config.MainViewVersion != 3)
        {
            DrawV3OnOff("Always show distance circle", "nearby_always_show", ref _config.NearbyAlwaysShowCircle);
            DrawPartyNearbyJCommandSelector(260f);
        }

        Header("Sound");
        Indent(() => DrawNearbySoundSettings("v2"));
    }

    private void DrawSettingsV2Visual()
    {
        Header("Font");
        Indent(() =>
        {
        int fontIdx = _config.SelectedFontName == "Mono" ? 1 : 0;
        ImGui.SetNextItemWidth(200f);
        if (BJBGui.Combo("Selected Font##v2_font", ref fontIdx, "Default\0Mono\0"))
        {
            _config.SelectedFontName = fontIdx == 1 ? "Mono" : "Default";
            _config.CustomButtonUseMono = fontIdx == 1;
            _save();
        }
        });

        Header("Poput-Bar");
        Indent(() =>
        {
        DrawV3OnOff("Lock Position", "visual_button_bar_locked", ref _config.ButtonBarLocked);
        int layout = (int)_config.ButtonBarLayout;
        DrawEnumButtons("bar_layout_v2", ref layout, new[] { "Horizontal", "Vertical" }, idx =>
        {
            _config.ButtonBarLayout = (ButtonBarLayout)idx;
            _save();
        });
        DrawV3OnOff("Fixed width buttons", "visual_button_bar_fixed_width", ref _config.ButtonBarFixedWidth);
        FloatInputSave("Fixed width##v2_bar_width", ref _config.ButtonBarFixedWidthValue, 50f, 600f, 200f);
        ColorRgbInputs("Background##v2_bar_bg", ref _config.ButtonBarBackgroundColor, new Vector4(0.1f, 0.1f, 0.1f, 1f));
        });

        Header("General Buttons");
        Indent(() =>
        {
        DrawButtonStyleEditor("Default", _config.GeneralButtonDefaultStyle, ButtonStyleConfig.Default(), applyToGlobal: true);
        DrawButtonStyleEditor("Active", _config.GeneralButtonActiveStyle, ButtonStyleConfig.Active(), applyToGlobal: false);
        DrawButtonStyleEditor("Highlight", _config.GeneralButtonHighlightStyle, ButtonStyleConfig.Highlight(), applyToHighlight: true);
        });

        Header("Custom Buttons");
        Indent(() =>
        {
            DrawButtonStyleEditor("Default", _config.CustomButtonDefaultStyle, ButtonStyleConfig.Default(),
                applyToCustom: true, styleId: "custom");
        });

        Header("Ingame Drawer");
        Indent(() =>
        {
        SliderSave("Scale", ref _config.DrawLogicScale, 0.05f, 3f, 0.05f, 1f, "x");
        FloatInputSave("Offset X", ref _config.DrawLogicOffsetX, -100f, 100f, 0f);
        FloatInputSave("Offset Y", ref _config.DrawLogicOffsetY, -100f, 100f, 0f);
        FloatInputSave("Offset Z", ref _config.DrawLogicOffsetZ, -100f, 100f, 0f);
        FloatInputSave("Offset Rotation", ref _config.DrawLogicOffsetR, 0f, 359.99f, 0f);
        });

        Header("Suits");
        Indent(() =>
        {
        ColorRgbInputs("Spades", ref _config.DrawLogicColorSpades, new Vector4(0f, 0f, 0f, 1f));
        ColorRgbInputs("Clubs", ref _config.DrawLogicColorClubs, new Vector4(0f, 0f, 0f, 1f));
        ColorRgbInputs("Hearts", ref _config.DrawLogicColorHearts, new Vector4(1f, 0f, 0f, 1f));
        ColorRgbInputs("Diamonds", ref _config.DrawLogicColorDiamonds, new Vector4(1f, 0f, 0f, 1f));
        });
    }

    private void DrawSettingsV2System(int level)
    {
        DrawV3OnOff("Disable Update Popup", "system_disable_update_popup", ref _config.DisableUpdatePopup);
        if (BJBGui.Button("Open Changelog"))
            Plugin.Instance.OpenChangelog();
        DrawV3OnOff("Hide Thanks page", "system_hide_thanks", ref _config.HideThanksPage);
        var allowZeroBet = GameEngine.AllowZeroBetForSession;
        ImGui.TextUnformatted("Allow 0 bet");
        ImGui.SameLine(260f);
        if (BJBOnOffSwitch.Draw("system_allow_zero_bet", ref allowZeroBet))
            GameEngine.AllowZeroBetForSession = allowZeroBet;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Session-only setting. Resets to disabled when the plugin reloads.");

        /* Card-Companion App UI is intentionally hidden, but retained for later reuse.
        Header("Card-Companion App");
        Indent(() =>
        {
            CheckSave("Enable Companion Synchronization", ref _config.EnableCompanionSync);

            ImGui.TextUnformatted("Server Address");
            ImGui.SameLine(260f);
            ImGui.SetNextItemWidth(260f);
            if (ImGui.InputText("##v2_companion_server", ref _config.CompanionServerAddress, 255))
                _save();

            IntInputSave("Timeout (ms)##v2_companion_timeout", ref _config.CompanionTimeoutMs, 1, 1000, 10, 200);
        });
        */

        // Retained but deliberately not exposed in the System UI.
        // if (BJBGui.Button("Reset default config file")) DefaultsMigration.ResetSnapshotFile();

        ImGui.Spacing();
        DrawV3OnOff("Wait Range Expanded", "system_wait_range_expanded", ref _config.UnlockWaitTimer);

        ImGui.TextUnformatted("Hashed Stats");
        ImGui.SameLine(260f);
        var hashedStats = _config.HashedStats;
        if (BJBOnOffSwitch.Draw("system_hashed_stats", ref hashedStats))
        {
            if (!hashedStats)
                _openHashedStatsConfirm = true;
            else
            {
                _config.HashedStats = true;
                _save();
            }
        }

        ImGui.Spacing();
        if (BJBGui.Button("Export Config File as JSON")) ExportConfigV2();
        ImGui.SameLine();
        if (BJBGui.Button("Import Config File as JSON")) ImportConfigV2();
    }

    private void DrawSettingsV2UnsavedBettingPopup()
    {
        if (!ImGui.BeginPopupModal("bjb_settings_v2_betting_unsaved", ref _settingsV2DiscardPopupOpen, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        ImGui.TextWrapped("Betting changes are not saved. Save them before leaving?");
        ImGui.Spacing();
        if (BJBGui.Button("Save"))
        {
            SaveBetDraft();
            if (_settingsV2PendingPage.HasValue) _page = _settingsV2PendingPage.Value;
            _settingsV2PendingPage = null;
            _settingsV2DiscardPopupOpen = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (BJBGui.Button("No"))
        {
            ResetBetDraft();
            if (_settingsV2PendingPage.HasValue) _page = _settingsV2PendingPage.Value;
            _settingsV2PendingPage = null;
            _settingsV2DiscardPopupOpen = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (BJBGui.Button("Back"))
        {
            _settingsV2ReturnTab = 3;
            _settingsV2PendingPage = null;
            _settingsV2DiscardPopupOpen = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    private void Header(string text)
    {
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.4f, 0.8f, 1f, 1f), text);
        ImGui.Separator();
    }

    private void Indent(Action draw)
    {
        ImGui.Indent(20f);
        draw();
        ImGui.Unindent(20f);
    }

    private void CheckSave(string label, ref bool value)
    {
        if (ImGui.Checkbox(label, ref value)) _save();
    }

    private bool CheckSaveChanged(string label, ref bool value)
    {
        if (!ImGui.Checkbox(label, ref value)) return false;
        _save();
        return true;
    }

    private void CheckRuleSave(string label, ref bool value)
    {
        var separator = label.IndexOf("##", StringComparison.Ordinal);
        var visibleLabel = separator < 0 ? label : label[..separator];
        var id = separator < 0 ? label : label[(separator + 2)..];

        ImGui.TextUnformatted(visibleLabel);
        ImGui.SameLine(260f);
        if (BJBOnOffSwitch.Draw($"rule_{id}", ref value)) _save();
    }

    private void DisableAllAutomationStates()
    {
        _config.AutoDealerDraw = false;
        _config.AutoInitialDeal = false;
        _config.AutoContinue = false;
        _config.AutoRun = false;
        _save();
    }

    private void DrawSharedAutomationTimingControls(string id)
    {
        ImGui.Spacing();
        ImGui.TextUnformatted("Command Speed");
        ImGui.SameLine(260f);
        ImGui.SetNextItemWidth(220f);
        if (BJBGui.SliderFloat($"##{id}_command_speed", ref _config.CommandSpeedMultiplier, 0.1f, 4f, "%.2fx"))
        {
            _config.CommandSpeedMultiplier = Math.Clamp(
                (float)Math.Round(_config.CommandSpeedMultiplier / 0.05f) * 0.05f, 0.1f, 4f);
            _save();
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton($"Reset##{id}_command_speed"))
        {
            _config.CommandSpeedMultiplier = 1f;
            _save();
        }

        ImGui.TextUnformatted("Recall Unlock");
        ImGui.SameLine(260f);
        ImGui.SetNextItemWidth(220f);
        if (BJBGui.SliderFloat($"##{id}_recall_unlock", ref _config.RecallUnlockSeconds, 1f, 60f, "%.0fs"))
        {
            _config.RecallUnlockSeconds = Math.Clamp(MathF.Round(_config.RecallUnlockSeconds), 1f, 60f);
            _save();
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton($"Reset##{id}_recall_unlock"))
        {
            _config.RecallUnlockSeconds = 20f;
            _save();
        }
    }

    private void DrawEnumButtons(string id, ref int value, string[] labels, Action<int> onChanged,
        int separateBeforeIndex = -1, bool joined = true)
    {
        if (!joined)
        {
            for (var index = 0; index < labels.Length; index++)
            {
                if (index > 0) ImGui.SameLine();
                var active = value == index;
                if (active) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1f, 0.5f, 0f, 1f));
                if (BJBGui.Button($"{labels[index]}##{id}_{index}",
                        active ? BJBGui.OrangeHighlightTextColor : BJBGui.ButtonTextColor))
                {
                    value = index;
                    onChanged(index);
                }
                if (active) ImGui.PopStyleColor();
            }
            return;
        }

        var style = ImGui.GetStyle();
        var height = ImGui.GetFrameHeight();
        var rounding = Math.Min(Math.Max(4f, style.FrameRounding), height * 0.5f);
        var drawList = ImGui.GetWindowDrawList();

        for (int i = 0; i < labels.Length; i++)
        {
            var startsGroup = i == 0 || i == separateBeforeIndex;
            var endsGroup = i == labels.Length - 1 || i == separateBeforeIndex - 1;
            if (i > 0) ImGui.SameLine(0f, startsGroup ? style.ItemSpacing.X : 0f);

            var textSize = ImGui.CalcTextSize(labels[i]);
            var size = new Vector2(textSize.X + style.FramePadding.X * 2f, height);
            var position = ImGui.GetCursorScreenPos();
            var clicked = ImGui.InvisibleButton($"##{id}_{i}", size);
            var hovered = ImGui.IsItemHovered();
            var held = ImGui.IsItemActive();
            var active = value == i;
            var color = active
                ? held
                    ? new Vector4(0.9f, 0.4f, 0f, 1f)
                    : hovered
                        ? new Vector4(1f, 0.6f, 0.1f, 1f)
                        : new Vector4(1f, 0.5f, 0f, 1f)
                : style.Colors[(int)(held ? ImGuiCol.ButtonActive : hovered ? ImGuiCol.ButtonHovered : ImGuiCol.Button)];
            var maximum = position + size;

            drawList.AddRectFilled(position, maximum, ImGui.GetColorU32(color),
                startsGroup || endsGroup ? rounding : 0f);
            if (startsGroup && !endsGroup)
                drawList.AddRectFilled(new Vector2(maximum.X - rounding, position.Y), maximum, ImGui.GetColorU32(color));
            else if (!startsGroup && endsGroup)
                drawList.AddRectFilled(position, new Vector2(position.X + rounding, maximum.Y), ImGui.GetColorU32(color));

            var textPosition = position + (size - textSize) * 0.5f;
            drawList.AddText(textPosition,
                ImGui.GetColorU32(active ? BJBGui.OrangeHighlightTextColor : BJBGui.ButtonTextColor), labels[i]);

            if (clicked && value != i)
            {
                value = i;
                onChanged(i);
            }
        }
    }

    private void DrawAutoContinueMinimumPlayersSelector(string id)
    {
        ImGui.TextUnformatted("Minimum players for auto continue:");
        ImGui.SameLine();

        const float width = 48f;
        const float height = 27f;
        const float rounding = 5f;
        var labels = new[] { "1+", "2+", "3+", "4+" };
        var drawList = ImGui.GetWindowDrawList();

        for (var index = 0; index < labels.Length; index++)
        {
            if (index > 0) ImGui.SameLine(0f, 0f);

            var position = ImGui.GetCursorScreenPos();
            var selected = _config.AutoContinueMinimumPlayers == index + 1;
            var hovered = false;
            if (ImGui.InvisibleButton($"##{id}_{index}", new Vector2(width, height)))
            {
                _config.AutoContinueMinimumPlayers = index + 1;
                _save();
            }
            hovered = ImGui.IsItemHovered();

            var color = selected
                ? new Vector4(1f, 0.5f, 0f, 1f)
                : ImGui.GetStyle().Colors[(int)(hovered ? ImGuiCol.ButtonHovered : ImGuiCol.Button)];
            var colorU32 = ImGui.GetColorU32(color);
            var maximum = position + new Vector2(width, height);

            if (index == 0 || index == labels.Length - 1)
            {
                drawList.AddRectFilled(position, maximum, colorU32, rounding);
                if (index == 0)
                    drawList.AddRectFilled(position + new Vector2(rounding, 0f), maximum, colorU32);
                else
                    drawList.AddRectFilled(position, maximum - new Vector2(rounding, 0f), colorU32);
            }
            else
            {
                drawList.AddRectFilled(position, maximum, colorU32);
            }

            var textSize = ImGui.CalcTextSize(labels[index]);
            drawList.AddText(
                position + new Vector2((width - textSize.X) / 2f, (height - textSize.Y) / 2f),
                ImGui.GetColorU32(selected ? BJBGui.OrangeHighlightTextColor : BJBGui.ButtonTextColor),
                labels[index]);
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Counts active, non-paused players only. The dealer is excluded.");
    }

    private void MultiplierInput(string label, ref float value, float defaultValue, string id)
    {
        ImGui.TextUnformatted(label);
        ImGui.SameLine(260f);
        ImGui.SetNextItemWidth(90f);
        if (BJBGui.InputFloat($"##{id}", ref value, 0.05f, 0.1f, "%.2fx", defaultValue: defaultValue))
        {
            value = (float)(Math.Round(value / 0.05f) * 0.05f);
            value = Math.Clamp(value, 1f, 3f);
            _save();
        }
    }

    private void IntInputSave(string label, ref int value, int min, int max, int step, int defaultValue)
    {
        var idSeparator = label.IndexOf("##", StringComparison.Ordinal);
        var visibleLabel = idSeparator >= 0 ? label[..idSeparator] : label;
        var inputId = idSeparator >= 0 ? $"##{label[(idSeparator + 2)..]}" : $"##{label}";
        ImGui.TextUnformatted(visibleLabel);
        ImGui.SameLine(260f);
        ImGui.SetNextItemWidth(120f);
        if (BJBGui.InputInt(inputId, ref value, step, defaultValue: defaultValue))
        {
            value = Math.Clamp(value, min, max);
            _save();
        }
    }

    private void FloatInputSave(string label, ref float value, float min, float max, float defaultValue)
    {
        ImGui.TextUnformatted(label.Split("##")[0]);
        ImGui.SameLine(260f);
        ImGui.SetNextItemWidth(120f);
        if (BJBGui.InputFloat($"##{label}", ref value, 1f, 10f, "%.2f", defaultValue: defaultValue))
        {
            value = Math.Clamp(value, min, max);
            _save();
        }
    }

    private void SliderSave(string label, ref float value, float min, float max, float step, float defaultValue, string suffix)
    {
        ImGui.TextUnformatted(label);
        ImGui.SameLine(260f);
        ImGui.SetNextItemWidth(220f);
        if (BJBGui.SliderFloat($"##{label}", ref value, min, max, suffix.Length > 0 ? $"%.2f{suffix}" : "%.2f"))
        {
            value = (float)(Math.Round(value / step) * step);
            value = Math.Clamp(value, min, max);
            _save();
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton($"Reset##{label}_reset"))
        {
            value = defaultValue;
            _save();
        }
    }

    private void ColorRgbInputs(string label, ref Vector4 color, Vector4 defaultValue)
    {
        ImGui.TextUnformatted(label.Split("##")[0]);
        ImGui.SameLine(260f);
        bool changed = false;
        changed |= ColorComponent("R", ref color.X, defaultValue.X, label);
        ImGui.SameLine();
        changed |= ColorComponent("G", ref color.Y, defaultValue.Y, label);
        ImGui.SameLine();
        changed |= ColorComponent("B", ref color.Z, defaultValue.Z, label);
        color.W = 1f;
        if (changed) _save();
        ImGui.SameLine();
        if (BJBGui.SmallButton($"Reset##{label}_reset"))
        {
            color = defaultValue;
            _save();
        }
    }

    private bool ColorComponent(string component, ref float value, float defaultValue, string id)
    {
        ImGui.SetNextItemWidth(70f);
        if (BJBGui.InputFloat($"##{id}_{component}", ref value, 0.01f, 0.1f, "%.2f", defaultValue: defaultValue))
        {
            value = Math.Clamp(value, 0f, 1f);
            return true;
        }
        return false;
    }

    private void DrawButtonStyleEditor(string label, ButtonStyleConfig style, ButtonStyleConfig defaults,
        bool applyToGlobal = false, bool applyToCustom = false, bool applyToHighlight = false, string? styleId = null)
    {
        var id = styleId ?? label;
        Header(label);
        ColorRgbInputs($"Background##{id}", ref style.Background, defaults.Background);
        ColorRgbInputs($"Text##{id}", ref style.Text, defaults.Text);
        SliderSave($"Font-Size##{id}", ref style.FontSize, 0.25f, 3.5f, 0.25f, defaults.FontSize, "x");
        PaddingInput("Padding top", ref style.PaddingTop, defaults.PaddingTop, id);
        PaddingInput("Padding left", ref style.PaddingLeft, defaults.PaddingLeft, id);
        PaddingInput("Padding bottom", ref style.PaddingBottom, defaults.PaddingBottom, id);
        PaddingInput("Padding right", ref style.PaddingRight, defaults.PaddingRight, id);

        if (applyToGlobal)
        {
            _config.ButtonColor = style.Background;
            _config.ButtonTextColor = style.Text;
        }
        if (applyToCustom)
        {
            _config.CustomButtonFontScale = style.FontSize;
            _config.CustomButtonPaddingH = (style.PaddingLeft + style.PaddingRight) * 0.5f;
            _config.CustomButtonPaddingV = (style.PaddingTop + style.PaddingBottom) * 0.5f;
        }
        if (applyToHighlight)
        {
            _config.HighlightColor = style.Background;
            _config.HighlightTextColor = style.Text;
        }
    }

    private void PaddingInput(string text, ref int value, int defaultValue, string id)
    {
        ImGui.TextUnformatted(text);
        ImGui.SameLine(260f);
        ImGui.SetNextItemWidth(90f);
        if (BJBGui.InputInt($"##{id}_{text}", ref value, 1, defaultValue: defaultValue))
        {
            value = Math.Clamp(value, 0, 25);
            _save();
        }
    }

    private void DraftInt(string label, ref int value, int min, int max)
    {
        ImGui.SetNextItemWidth(-1);
        if (BJBGui.InputInt(label, ref value, 1))
        {
            value = Math.Clamp(value, min, max);
            _betDraftDirty = true;
        }
    }

    private void DraftLong(string label, ref long value, long min, long max)
    {
        ImGui.SetNextItemWidth(-1);
        if (BJBGui.InputLong(label, ref value, 10000, 100000))
        {
            value = Math.Clamp(value, min, max);
            _betDraftDirty = true;
        }
    }

    private void DraftString(string label, ref string value, string defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            value = defaultValue;

        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputText(label, ref value, 64))
        {
            if (string.IsNullOrWhiteSpace(value))
                value = defaultValue;
            _betDraftDirty = true;
        }
    }

    private static string GetDefaultVipName(int level) => level <= 0 ? "VIP" : $"VIP {level}";

    private void AddBetDraft(BetLimitEntry entry)
    {
        EnsureBetDraft();
        _betDraftEntries!.Add(entry);
        _betDraftDirty = true;
    }

    private void EnsureBetDraft()
    {
        if (_betDraftEntries != null) return;
        if (_config.EnsureBetLimitEntriesMigration()) _save();
        var source = _config.BetLimitEntries.Count > 0
            ? _config.BetLimitEntries
            : BuildBetEntriesFromLegacy();
        _betDraftEntries = source.Select(CloneBetEntry).ToList();
        _betDraftDirty = false;
    }

    private List<BetLimitEntry> BuildBetEntriesFromLegacy()
    {
        var list = new List<BetLimitEntry>
        {
            new() { Active = true, Kind = BetLimitEntryKind.MinBet, Name = "Min", Amount = _config.MinBet },
            new() { Active = true, Kind = BetLimitEntryKind.Normal, Name = "Max", Amount = _config.MaxBet }
        };
        for (int i = 0; i < _config.VipBetTiers.Count && i < 9; i++)
            list.Add(new BetLimitEntry { Active = true, Kind = BetLimitEntryKind.Vip, VipLevel = i + 1, Name = _config.VipBetTiers[i].Name, Amount = _config.VipBetTiers[i].MaxBet });
        return list;
    }

    private static BetLimitEntry CloneBetEntry(BetLimitEntry e) => new()
    {
        Active = e.Active,
        Kind = e.Kind,
        VipLevel = e.VipLevel,
        Name = e.Name,
        Amount = e.Amount,
    };

    private void SaveBetDraft()
    {
        EnsureBetDraft();
        var sorted = _betDraftEntries!
            .OrderBy(e => e.Kind == BetLimitEntryKind.MinBet ? 0 : e.Kind == BetLimitEntryKind.Normal ? 1 : 2)
            .ThenBy(e => e.Kind == BetLimitEntryKind.Vip ? e.VipLevel : -1)
            .ThenBy(e => e.Amount)
            .Select(CloneBetEntry)
            .ToList();

        _config.BetLimitEntries = sorted;
        var min = sorted.FirstOrDefault(e => e.Active && e.Kind == BetLimitEntryKind.MinBet);
        if (min != null) _config.MinBet = min.Amount;
        var normal = sorted.LastOrDefault(e => e.Active && (e.Kind == BetLimitEntryKind.Normal
            || (e.Kind == BetLimitEntryKind.Vip && e.VipLevel == 0)));
        if (normal != null) _config.MaxBet = normal.Amount;
        _config.VipBetTiers = sorted
            .Where(e => e.Active && e.Kind == BetLimitEntryKind.Vip && e.VipLevel > 0)
            .GroupBy(e => e.VipLevel)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var last = g.Last();
                return new VipBetTier
                {
                    Name = string.IsNullOrWhiteSpace(last.Name) ? GetDefaultVipName(g.Key) : last.Name.Trim(),
                    MaxBet = last.Amount
                };
            })
            .ToList();

        _betDraftEntries = sorted.Select(CloneBetEntry).ToList();
        _betDraftDirty = false;
        _save();
    }

    private void ResetBetDraft()
    {
        _betDraftEntries = null;
        EnsureBetDraft();
        _betDraftDirty = false;
    }

    private void DrawCommandSelector(string label, ref string value)
    {
        ImGui.PushID(label);
        var idSeparator = label.IndexOf("##", StringComparison.Ordinal);
        var visibleLabel = idSeparator < 0 ? label : label[..idSeparator];
        ImGui.TextUnformatted(visibleLabel);
        ImGui.SameLine(260f);
        var commands = _config.CommandGroups.Select(g => "Commands/" + g.Name)
            .Concat(_config.CustomCommandGroups.Select(g => "Custom Buttons/" + g.Name))
            .Append("Actions/Payout")
            .ToArray();
        var labels = new[] { "None" }.Concat(commands).ToArray();
        int selected = 0;
        for (int i = 0; i < commands.Length; i++)
        {
            var raw = commands[i].Split('/').Last();
            if (raw.Equals(value, StringComparison.OrdinalIgnoreCase)) selected = i + 1;
        }
        ImGui.SetNextItemWidth(260f);
        if (BJBGui.Combo("##v2_command_selector", ref selected, labels, labels.Length))
        {
            value = selected == 0 ? string.Empty : commands[selected - 1].Split('/').Last();
            _save();
        }
        ImGui.PopID();
    }

    private void DrawSoundFileListV2()
    {
        DrawNearbySoundEntries("v2");
    }

    private void DrawNearbySoundSettings(string id)
    {
        if (_config.EnsureNearbyAlertSoundEntriesMigration()) _save();

        DrawV3OnOff("Player entering area sound trigger", $"{id}_sound_enabled", ref _config.NearbyAlertEnabled);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Play a sound when a new player enters your nearby area.");

        ImGui.TextUnformatted("Volume");
        ImGui.SameLine(260f);
        ImGui.SetNextItemWidth(220f);
        if (BJBGui.SliderFloat($"##{id}_sound_volume", ref _config.NearbyAlertVolume, 0f, 100f, "%.0f%%"))
        {
            _config.NearbyAlertVolume = Math.Clamp(_config.NearbyAlertVolume, 0f, 100f);
            _save();
        }

        DrawNearbyCooldownEditor(id);

        ImGui.TextUnformatted("Mode");
        ImGui.SameLine(260f);
        var mode = (int)_config.NearbyAlertSoundMode;
        DrawEnumButtons($"sound_mode_{id}", ref mode, new[] { "Iterative", "Random", "First only" }, index =>
        {
            _config.NearbyAlertSoundMode = (NearbyAlertSoundMode)index;
            _save();
        });

        Header("Files");
        DrawNearbySoundEntries(id);
    }

    private void DrawNearbyCooldownEditor(string id)
    {
        ImGui.TextUnformatted("Cooldown");
        ImGui.SameLine(260f);
        if (_nearbyCooldownEditMode)
        {
            ImGui.SetNextItemWidth(110f);
            ImGui.InputText($"##{id}_cooldown_input", ref _nearbyCooldownInput, 16,
                ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.EnterReturnsTrue);
            var confirm = ImGui.IsItemDeactivatedAfterEdit() || BJBGui.SmallButton($"OK##{id}_cooldown_ok");
            if (confirm && float.TryParse(_nearbyCooldownInput, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                _config.NearbyAlertCooldown = Math.Clamp(value, 0.02f, 30f);
                _nearbyCooldownEditMode = false;
                _save();
            }
            ImGui.SameLine();
            if (BJBGui.SmallButton($"Cancel##{id}_cooldown_cancel")) _nearbyCooldownEditMode = false;
            return;
        }

        ImGui.SetNextItemWidth(220f);
        if (BJBGui.SliderFloat($"##{id}_sound_cooldown", ref _config.NearbyAlertCooldown, 0.02f, 30f, "%.2fs"))
        {
            _config.NearbyAlertCooldown = Math.Clamp(_config.NearbyAlertCooldown, 0.02f, 30f);
            _save();
        }
        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            _nearbyCooldownInput = _config.NearbyAlertCooldown.ToString("0.##", CultureInfo.InvariantCulture);
            _nearbyCooldownEditMode = true;
        }
    }

    private void DrawNearbySoundEntries(string id)
    {
        if (_config.EnsureNearbyAlertSoundEntriesMigration()) _save();

        var removeIndex = -1;
        for (var i = 0; i < _config.NearbyAlertSoundEntries.Count; i++)
        {
            var entry = _config.NearbyAlertSoundEntries[i];
            ImGui.PushID($"{id}_sound_{i}");
            if (BJBGui.SmallButton("X##delete")) removeIndex = i;
            ImGui.SameLine();
            if (ImGui.Checkbox("##enabled", ref entry.Enabled)) SaveNearbySoundEntries();
            ImGui.SameLine();
            ImGui.SetNextItemWidth(104f);
            if (BJBGui.SliderFloat("##volume", ref entry.Volume, 0f, 100f, "%.0f%%"))
            {
                entry.Volume = Math.Clamp(entry.Volume, 0f, 100f);
                SaveNearbySoundEntries();
            }
            ImGui.SameLine();
            ImGui.TextUnformatted(entry.Path);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(entry.Path);
            ImGui.PopID();
        }

        if (removeIndex >= 0)
        {
            _config.NearbyAlertSoundEntries.RemoveAt(removeIndex);
            SaveNearbySoundEntries();
        }

        if (BJBGui.SmallButton($"Select file ...##{id}_add_sound"))
        {
            _fileDialogManager.OpenFileDialog("Select Sound File", "Audio{.wav,.mp3,.ogg}", (ok, path) =>
            {
                if (!ok || string.IsNullOrWhiteSpace(path)
                    || _config.NearbyAlertSoundEntries.Any(entry => entry.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
                    return;
                _config.NearbyAlertSoundEntries.Add(new NearbyAlertSoundEntry { Path = path, Enabled = true, Volume = 100f });
                SaveNearbySoundEntries();
            });
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton($"Test##{id}_test_sound"))
        {
            var played = NearbyAlertManager.PlayTestSound(_config, out var status);
            _nearbySoundTestStatus = status;
            _nearbySoundTestFailed = !played;
        }
        if (!string.IsNullOrEmpty(_nearbySoundTestStatus))
        {
            ImGui.SameLine();
            ImGui.TextColored(_nearbySoundTestFailed
                ? new Vector4(1f, 0.4f, 0.35f, 1f)
                : new Vector4(0.45f, 0.9f, 0.5f, 1f), _nearbySoundTestStatus);
        }
    }

    private void SaveNearbySoundEntries()
    {
        _config.NearbyAlertSoundEntriesMigrated = true;
        _config.SyncLegacyNearbyAlertSoundFiles();
        _save();
    }

    private void ExportConfigV2()
    {
        var exportObj = JObject.FromObject(_config);
        exportObj.Remove("Presets");
        var json = exportObj.ToString(Newtonsoft.Json.Formatting.Indented);
        _fileDialogManager.SaveFileDialog("Export Config", "JSON Files{.json}", "bjb_config", ".json",
            (ok, path) =>
            {
                if (ok && !string.IsNullOrWhiteSpace(path))
                    System.IO.File.WriteAllText(path, json);
            });
    }

    private void ImportConfigV2()
    {
        _fileDialogManager.OpenFileDialog("Import Config", "JSON Files{.json}",
            (ok, path) =>
            {
                if (!ok || string.IsNullOrWhiteSpace(path)) return;
                try
                {
                    var json = System.IO.File.ReadAllText(path);
                    _tempImportJson = JObject.Parse(json);
                    if (!_tempImportJson.ContainsKey("UnlockWaitTimer")
                        || !_tempImportJson.ContainsKey("EnableBankInput")
                        || !_tempImportJson.ContainsKey("NotepadText"))
                        DoMerge();
                    else
                        _openImportConfirmPopup = true;
                }
                catch { }
            });
    }

    private bool TryLeaveSettingsV2(Page targetPage)
    {
        if (!_betDraftDirty) return true;
        _settingsV2PendingPage = targetPage;
        _settingsV2DiscardPopupOpen = true;
        ImGui.OpenPopup("bjb_settings_v2_betting_unsaved");
        return false;
    }

    private void DiscardSettingsV2Drafts()
    {
        _betDraftEntries = null;
        _betDraftDirty = false;
        _settingsV2PendingPage = null;
        _settingsV2DiscardPopupOpen = false;
    }
}
