using System;
using System.Collections.Generic;
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

    private static readonly string[] SettingsV2Tabs =
    {
        "General", "Automation", "Rules", "Betting", "Time & Delay", "Message settings",
        "Nearby Players", "Visual", "Alliance", "Preset Setup", "System"
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
            DrawSettingsV2Tab(5, "Message settings", DrawSettingsV2Messages);
            DrawSettingsV2Tab(6, "Nearby Players", DrawSettingsV2Nearby);
            DrawSettingsV2Tab(7, "Visual", DrawSettingsV2Visual);
            DrawSettingsV2Tab(8, "Alliance", DrawSettingsAllianceBody);
            DrawSettingsV2Tab(9, "Preset Setup", DrawSettingsPresetSetupBody);
            DrawSettingsV2Tab(10, "System", () => DrawSettingsV2System(level));
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
        DrawEnumButtons("user_level_v2", ref level, new[] { "Beginner", "Advanced", "Dev", "Custom" }, idx =>
        {
            _config.CurrentLevel = (UserLevel)idx;
            _save();
        });

        ImGui.Spacing();
        ImGui.TextUnformatted("Main View");
        int mainView = _config.MainViewVersion == 2 ? 1 : 0;
        DrawEnumButtons("main_view_v2", ref mainView, new[] { "Classic", "Version 2" }, idx =>
        {
            _config.MainViewVersion = idx == 1 ? 2 : 1;
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
        DrawEnumButtons("menu_style_v2", ref menuMode, new[] { "Sidebar", "Burger Menu", "Top Tabs (experimental)" }, idx =>
        {
            _config.MenuStyle = (MenuStyleMode)idx;
            _save();
        });

        ImGui.Spacing();
        ImGui.TextUnformatted("Gil visual");
        int gilVisual = (int)_config.GilVisual;
        ImGui.PushFont(UiBuilder.MonoFont);
        DrawEnumButtons("gil_visual_v2", ref gilVisual, new[] { "12345678", "     12,345,678", "   , 12,345,678" }, idx =>
        {
            _config.GilVisual = (GilVisualMode)idx;
            _save();
        });
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

        ImGui.Spacing();
        ImGui.TextUnformatted("Player Ready Start");
        bool requireMultipleParticipants = _config.AutostartRoundOnlyOnMultiplePlayers;
        if (requireMultipleParticipants)
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1f, 0.5f, 0f, 1f));
        if (BJBGui.SmallButton("Only at 2 or more participants##v2_ready_start_multiple") && !requireMultipleParticipants)
        {
            _config.AutostartRoundOnlyOnMultiplePlayers = true;
            _save();
        }
        if (requireMultipleParticipants)
            ImGui.PopStyleColor();

        ImGui.SameLine();
        if (!requireMultipleParticipants)
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1f, 0.5f, 0f, 1f));
        if (BJBGui.SmallButton("Every time##v2_ready_start_every_time") && requireMultipleParticipants)
        {
            _config.AutostartRoundOnlyOnMultiplePlayers = false;
            _save();
        }
        if (!requireMultipleParticipants)
            ImGui.PopStyleColor();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Counts active participants only; the dealer is excluded.\nThis does not affect the Auto-Continue timer.");

        ImGui.Spacing();
        Header("Initial Rotation");
        var currentRotation = Plugin.ObjectTable.LocalPlayer?.Rotation;
        var currentText = currentRotation.HasValue
            ? FormatRotationLabel(currentRotation.Value)
            : "n/a";
        var targetText = FormatRotationLabel(_config.InitialViewDirection);

        ImGui.TextUnformatted("Current Rotation");
        ImGui.SameLine(260f);
        ImGui.TextUnformatted(currentText);

        ImGui.TextUnformatted("Target Rotation");
        ImGui.SameLine(260f);
        ImGui.TextUnformatted(targetText);
        ImGui.SameLine();
        if (BJBGui.SmallButton("To current rotation##v2_settings_initial_rotation_current"))
        {
            ViewDirectionManager.CaptureCurrentRotation(_config);
            _save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Sets the initial round rotation to your current facing direction.");
    }

    private static string FormatRotationLabel(float radians)
    {
        var degrees = radians * (180f / MathF.PI);
        degrees %= 360f;
        if (degrees < 0f)
            degrees += 360f;
        return $"{degrees:0.0}° / {radians:0.0000} rad";
    }

    private void DrawSettingsV2Rules()
    {
        Header("Dealing Behavior");
        Indent(() =>
        {
        CheckSave("First Deal, then play", ref _config.FirstDealThenPlay);
        CheckSave("Player rolling for themselves", ref _config.PlayerRollingForThemselves);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Players roll their own required cards with /dice 13, /dice alliance 13, or the native /random command.\nDealer rolls are unchanged.");
        CheckSave("Hide card suits", ref _config.HideCardSuits);
        });

        Header("Dealer Rules");
        Indent(() =>
        {
        ImGui.TextUnformatted("Dealer Stands on:");
        ImGui.SameLine(260f);
        CheckSave("Soft##v2_dealer_soft", ref _config.DealerSoftRule);
        ImGui.SameLine(0f, 18f);
        ImGui.SetNextItemWidth(120f);
        if (BJBGui.InputInt("##v2_dealer_draws_until", ref _config.DealerDrawsUntil, 1))
        {
            _config.DealerDrawsUntil = Math.Clamp(_config.DealerDrawsUntil, 2, 21);
            _save();
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton("Reset##v2_dealer_draws_until_reset"))
        {
            _config.DealerDrawsUntil = 17;
            _save();
        }
        });

        Header("Game settings");
        Indent(() =>
        {
        Header("Win");
        Indent(() =>
        {
            MultiplierInput("Payout", ref _config.MultiplierNormalWin, 2f, "v2_win");
        });

        Header("BlackJack");
        Indent(() =>
        {
        Header("Natural");
        Indent(() =>
        {
        MultiplierInput("Payout", ref _config.MultiplierBlackjackWin, 2.5f, "v2_natbj");
        });

        Header("Dirty");
        Indent(() =>
        {
        CheckSave("Enable Dirty Blackjack##v2_dirty", ref _config.EnableDirtyBlackjack);
        MultiplierInput("Payout", ref _config.MultiplierDirtyBlackjackWin, 2f, "v2_dirtybj");
        });
        });

        Header("Charlie");
        Indent(() =>
        {
        CheckSave("Enable Charlie", ref _config.EnableCharlie);
        CheckSave("Instant-Win", ref _config.CharlieInstantWin);
        IntInputSave("Cards##v2_charlie_cards", ref _config.CharlieCardCount, 3, 9, 1, 5);
        MultiplierInput("Payout", ref _config.MultiplierBlackjackWin, 2.5f, "v2_charlie_payout");
        });

        Header("Split");
        Indent(() =>
        {
        CheckSave("Enable Split", ref _config.EnableSplit);
        CheckSave("Identical Split only", ref _config.IdenticalSplitOnly);
        IntInputSave("Max Hands##v2_max_hands", ref _config.MaxHandsPerPlayer, 2, 10, 1, 2);
        MultiplierInput("Payout", ref _config.MultiplierNormalWin, 2f, "v2_split_payout");
        });

        Header("Double Down");
        Indent(() =>
        {
        CheckSave("Enable Double Down", ref _config.EnableDoubleDown);
        CheckSave("Allow Double-Down after Split", ref _config.AllowDoubleDownAfterSplit);
        CheckSave("Refund Double Down on push", ref _config.RefundFullDoubleDownOnPush);
        int tie = (int)_config.BlackjackTieRule;
        ImGui.TextUnformatted("BlackJack Tie Rule:");
        ImGui.SameLine(260f);
        DrawEnumButtons("bj_tie_v2", ref tie, new[] { "Always Push", "Player NatBJ wins", "Dealer NatBJ wins", "NatBJ beats Dirty BJ" }, idx =>
        {
            _config.BlackjackTieRule = (BlackjackTieRule)idx;
            _save();
        });
        MultiplierInput("Payout", ref _config.MultiplierBlackjackWin, 2.5f, "v2_dd_payout");
        });
        });

        Header("Result");
        Indent(() =>
        {
        CheckSave("Small Result Messages", ref _config.SmallResult);
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

            if (ImGui.Checkbox("Visible if empty", ref rule.VisibleIfEmpty)) ruleEdited = true;
            if (ImGui.Checkbox("Visible if content before is empty", ref rule.VisibleIfContentBeforeIsEmpty)) ruleEdited = true;
            if (ImGui.Checkbox("Visible if content after is empty", ref rule.VisibleIfContentAfterIsEmpty)) ruleEdited = true;
            if (ImGui.Checkbox("Compress", ref rule.Compress)) ruleEdited = true;

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
        var winners = new[] { "Alice Winner", "Bob Winner" };
        var pushed = new[] { "Cara Push", "Dorian Push" };
        var loosed = new[] { "Eve Lost", "Finn Lost" };
        var busted = new[] { "Gina Bust", "Hugo Bust" };
        var ruleOutput = ShortResultFormatter.Render(_config, winners, pushed, loosed, busted);
        var outerTemplate = string.IsNullOrWhiteSpace(_config.ResultTemplate)
            ? "${results}"
            : _config.ResultTemplate;
        var preview = outerTemplate
            .Replace("${results}", ruleOutput)
            .Replace("<results>", ruleOutput)
            .Replace("${winners}", $"Winners: {string.Join(", ", winners)}")
            .Replace("${pushed}", $"Pushed: {string.Join(", ", pushed)}")
            .Replace("${loosers}", $"Lost: {string.Join(", ", loosed)}")
            .Replace("${busted}", $"Busted: {string.Join(", ", busted)}");
        ImGui.InputTextMultiline(
            "##short_result_example_output",
            ref preview,
            4096,
            new Vector2(-1f, 180f),
            ImGuiInputTextFlags.ReadOnly);

        ImGui.EndTable();
    }

    private static List<ShortResultRule> CloneShortResultRules(IEnumerable<ShortResultRule> rules)
        => rules.Select(rule => rule.Clone()).ToList();

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
                int kind = entry.Kind == BetLimitEntryKind.MinBet ? 0 : 1;
                if (BJBGui.Combo("##kind", ref kind, "Min-Bet\0VIP\0"))
                {
                    entry.Kind = kind == 0 ? BetLimitEntryKind.MinBet : BetLimitEntryKind.Vip;
                    _betDraftDirty = true;
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

        if (BJBGui.SmallButton("+ Add VIP")) AddBetDraft(new BetLimitEntry { Kind = BetLimitEntryKind.Vip, VipLevel = 0, Name = GetDefaultVipName(0), Amount = _config.MaxBet });
        ImGui.SameLine();
        if (BJBGui.SmallButton("+ Add Min-Bet")) AddBetDraft(new BetLimitEntry { Kind = BetLimitEntryKind.MinBet, Amount = _config.MinBet });

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

    private void DrawSettingsV2TimeDelay()
    {
        ImGui.TextUnformatted("UTC Offset");
        ImGui.SameLine(260f);
        int utc = _config.UtcOffsetHours;
        ImGui.SetNextItemWidth(80f);
        if (BJBGui.InputInt("##v2_utc", ref utc, 1))
        {
            _config.UtcOffsetHours = Math.Clamp(utc, -12, 14);
            _config.UtcOffsetConfigured = true;
            _save();
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton("+##utc_plus")) { _config.UtcOffsetHours = Math.Clamp(_config.UtcOffsetHours + 1, -12, 14); _save(); }
        ImGui.SameLine();
        if (BJBGui.SmallButton("-##utc_minus")) { _config.UtcOffsetHours = Math.Clamp(_config.UtcOffsetHours - 1, -12, 14); _save(); }
        ImGui.SameLine();
        if (BJBGui.SmallButton("Reset##utc_reset")) { _config.UtcOffsetHours = 0; _config.UtcOffsetConfigured = false; _save(); }

        SliderSave("Command Speed", ref _config.CommandSpeedMultiplier, 0.1f, 2f, 0.1f, 1f, "x");
        SliderSave("Recall Unlock", ref _config.RecallUnlockSeconds, 1f, 60f, 1f, 20f, "s");
    }

    private void DrawSettingsV2Messages()
    {
        CheckSave("Avoid double Messages", ref _config.EnableAntiDouble);
        CheckSave("Seconds snapping delay input field", ref _config.DelaySecondSnapping);
    }

    private void DrawSettingsV2Nearby()
    {
        CheckSave("Enable nearby Players Feature", ref _config.ShowNearbyPlayers);
        IntInputSave("Nearby Player Columns", ref _config.NearbyColumns, 1, 5, 1, 2);
        CheckSave("No auto dequeue", ref _config.NoAutoDequeue);
        CheckSave("Always show range circle", ref _config.NearbyAlwaysShowCircle);
        DrawCommandSelector("Nearby Player Custom Command Button", ref _config.NearbyQuestionCommandName);

        Header("Sound");
        Indent(() =>
        {
        CheckSave("Enable Sound on player enter range", ref _config.NearbyAlertEnabled);
        ImGui.TextUnformatted("Volume");
        ImGui.SameLine(260f);
        float volume01 = _config.NearbyAlertVolume / 100f;
        ImGui.SetNextItemWidth(220f);
        if (BJBGui.SliderFloat("##v2_sound_volume", ref volume01, 0.01f, 1f, "%.2f"))
        {
            volume01 = (float)(Math.Round(volume01 / 0.01f) * 0.01f);
            _config.NearbyAlertVolume = Math.Clamp(volume01, 0.01f, 1f) * 100f;
            _save();
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton("Reset##v2_sound_volume_reset"))
        {
            _config.NearbyAlertVolume = 50f;
            _save();
        }
        SliderSave("Cooldown", ref _config.NearbyAlertCooldown, 0.1f, 30f, 0.1f, 0.3f, "s");
        int mode = (int)_config.NearbyAlertSoundMode;
        DrawEnumButtons("sound_mode_v2", ref mode, new[] { "Iterative", "Random", "First only" }, idx =>
        {
            _config.NearbyAlertSoundMode = (NearbyAlertSoundMode)idx;
            _save();
        });

        Header("Files");
        Indent(() =>
        {
            DrawSoundFileListV2();
        });
        });
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
        CheckSave("Lock Position", ref _config.ButtonBarLocked);
        int layout = (int)_config.ButtonBarLayout;
        DrawEnumButtons("bar_layout_v2", ref layout, new[] { "Horizontal", "Vertical" }, idx =>
        {
            _config.ButtonBarLayout = (ButtonBarLayout)idx;
            _save();
        });
        CheckSave("Fixed width buttons", ref _config.ButtonBarFixedWidth);
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
            DrawButtonStyleEditor("Default##custom", _config.CustomButtonDefaultStyle, ButtonStyleConfig.Default(), applyToCustom: true);
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
        CheckSave("Disable Update Popup", ref _config.DisableUpdatePopup);
        if (BJBGui.Button("Open Changelog"))
            Plugin.Instance.OpenChangelog();
        CheckSave("Hide Thanks page", ref _config.HideThanksPage);
        var allowZeroBet = GameEngine.AllowZeroBetForSession;
        if (ImGui.Checkbox("Allow 0 bet", ref allowZeroBet))
            GameEngine.AllowZeroBetForSession = allowZeroBet;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Session-only setting. Resets to disabled when the plugin reloads.");

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

        if (BJBGui.Button("Reset default config file")) DefaultsMigration.ResetSnapshotFile();

        ImGui.Spacing();
        ImGui.TextUnformatted("Wait-Range expanded:");
        int waitRange = _config.UnlockWaitTimer ? 1 : 0;
        DrawEnumButtons("wait_range_v2", ref waitRange, new[] { "Default", "Expanded" }, idx =>
        {
            _config.UnlockWaitTimer = idx == 1;
            _save();
        });

        if (_config.HashedStats)
        {
            bool hashed = _config.HashedStats;
            if (ImGui.Checkbox("Hashed Stats", ref hashed))
                _openHashedStatsConfirm = true;
        }
        else
        {
            CheckSave("Hashed Stats", ref _config.HashedStats);
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

    private void DisableAllAutomationStates()
    {
        _config.AutoDealerDraw = false;
        _config.AutoInitialDeal = false;
        _config.AutoContinue = false;
        _config.AutoRun = false;
        _save();
    }

    private void DrawEnumButtons(string id, ref int value, string[] labels, Action<int> onChanged)
    {
        for (int i = 0; i < labels.Length; i++)
        {
            if (i > 0) ImGui.SameLine();
            bool active = value == i;
            if (active) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1f, 0.5f, 0f, 1f));
            if (BJBGui.Button($"{labels[i]}##{id}_{i}"))
            {
                value = i;
                onChanged(i);
            }
            if (active) ImGui.PopStyleColor();
        }
    }

    private void MultiplierInput(string label, ref float value, float defaultValue, string id)
    {
        ImGui.TextUnformatted(label);
        ImGui.SameLine(260f);
        ImGui.SetNextItemWidth(180f);
        if (BJBGui.InputFloat($"##{id}", ref value, 0.05f, 0.1f, "%.2fx"))
        {
            value = (float)(Math.Round(value / 0.05f) * 0.05f);
            value = Math.Clamp(value, 1f, 3f);
            _save();
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton($"Reset##{id}_reset"))
        {
            value = defaultValue;
            _save();
        }
    }

    private void IntInputSave(string label, ref int value, int min, int max, int step, int defaultValue)
    {
        ImGui.TextUnformatted(label.Split("##")[0]);
        ImGui.SameLine(260f);
        ImGui.SetNextItemWidth(120f);
        if (BJBGui.InputInt(label, ref value, step))
        {
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

    private void FloatInputSave(string label, ref float value, float min, float max, float defaultValue)
    {
        ImGui.TextUnformatted(label.Split("##")[0]);
        ImGui.SameLine(260f);
        ImGui.SetNextItemWidth(120f);
        if (BJBGui.InputFloat($"##{label}", ref value, 1f, 10f, "%.2f"))
        {
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
        changed |= ColorComponent("R", ref color.X, label);
        ImGui.SameLine();
        changed |= ColorComponent("G", ref color.Y, label);
        ImGui.SameLine();
        changed |= ColorComponent("B", ref color.Z, label);
        color.W = 1f;
        if (changed) _save();
        ImGui.SameLine();
        if (BJBGui.SmallButton($"Reset##{label}_reset"))
        {
            color = defaultValue;
            _save();
        }
    }

    private bool ColorComponent(string component, ref float value, string id)
    {
        ImGui.SetNextItemWidth(70f);
        if (BJBGui.InputFloat($"##{id}_{component}", ref value, 0.01f, 0.1f, "%.2f"))
        {
            value = Math.Clamp(value, 0f, 1f);
            return true;
        }
        return false;
    }

    private void DrawButtonStyleEditor(string label, ButtonStyleConfig style, ButtonStyleConfig defaults,
        bool applyToGlobal = false, bool applyToCustom = false, bool applyToHighlight = false)
    {
        Header(label);
        ColorRgbInputs($"Background##{label}", ref style.Background, defaults.Background);
        ColorRgbInputs($"Text##{label}", ref style.Text, defaults.Text);
        SliderSave($"Font-Size##{label}", ref style.FontSize, 0.25f, 3.5f, 0.25f, defaults.FontSize, "x");
        PaddingInput("Padding top", ref style.PaddingTop, defaults.PaddingTop, label);
        PaddingInput("Padding left", ref style.PaddingLeft, defaults.PaddingLeft, label);
        PaddingInput("Padding bottom", ref style.PaddingBottom, defaults.PaddingBottom, label);
        PaddingInput("Padding right", ref style.PaddingRight, defaults.PaddingRight, label);

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
        if (BJBGui.InputInt($"##{id}_{text}", ref value, 1))
        {
            value = Math.Clamp(value, 0, 25);
            _save();
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton($"Reset##{id}_{text}_reset"))
        {
            value = defaultValue;
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
            new() { Active = true, Kind = BetLimitEntryKind.MinBet, Amount = _config.MinBet },
            new() { Active = true, Kind = BetLimitEntryKind.Vip, VipLevel = 0, Name = GetDefaultVipName(0), Amount = _config.MaxBet }
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
            .OrderBy(e => e.Kind == BetLimitEntryKind.MinBet ? 0 : 1)
            .ThenBy(e => e.Kind == BetLimitEntryKind.Vip ? e.VipLevel : -1)
            .ThenBy(e => e.Amount)
            .Select(CloneBetEntry)
            .ToList();

        _config.BetLimitEntries = sorted;
        var min = sorted.FirstOrDefault(e => e.Active && e.Kind == BetLimitEntryKind.MinBet);
        if (min != null) _config.MinBet = min.Amount;
        var vip0 = sorted.LastOrDefault(e => e.Active && e.Kind == BetLimitEntryKind.Vip && e.VipLevel == 0);
        if (vip0 != null) _config.MaxBet = vip0.Amount;
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
        ImGui.TextUnformatted(label);
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
    }

    private void DrawSoundFileListV2()
    {
        int removeIdx = -1;
        for (int i = 0; i < _config.NearbyAlertSoundFiles.Count; i++)
        {
            var path = _config.NearbyAlertSoundFiles[i];
            ImGui.TextUnformatted(System.IO.Path.GetFileName(path));
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(path);
            ImGui.SameLine();
            if (BJBGui.SmallButton($"X##v2_del_sound_{i}")) removeIdx = i;
        }
        if (removeIdx >= 0)
        {
            _config.NearbyAlertSoundFiles.RemoveAt(removeIdx);
            _save();
        }

        if (BJBGui.SmallButton("+ Add Sound##v2_add_sound"))
        {
            _fileDialogManager.OpenFileDialog("Add Sound File", "Audio{.wav,.mp3,.ogg}",
                (ok, path) =>
                {
                    if (ok && !string.IsNullOrWhiteSpace(path) && !_config.NearbyAlertSoundFiles.Contains(path))
                    {
                        _config.NearbyAlertSoundFiles.Add(path);
                        _save();
                    }
                });
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton("Test##v2_test_sound")) NearbyAlertManager.PlayTestSound(_config);
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
