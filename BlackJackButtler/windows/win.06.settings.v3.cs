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
    private int _settingsV3TabIndex;
    private string _v3TimeZoneFilter = string.Empty;
    private bool _v3EditingCustomUtc;
    private string _v3CustomUtcInput = string.Empty;
    private string _v3ArmedBetPresetName = string.Empty;
    private DateTime _v3ArmedBetPresetUntil = DateTime.MinValue;
    private string _v3EditingBetPresetColor = string.Empty;

    private sealed class TimeZoneOption
    {
        public TimeZoneOption(int offsetMinutes, string name)
        {
            OffsetMinutes = offsetMinutes;
            Name = name;
        }

        public int OffsetMinutes { get; }
        public string Name { get; }
    }

    // These are deliberate fixed UTC offsets, not IANA zones. The separate Summer/Winter
    // toggle is the user's manual +1 hour shift and keeps the choice predictable in-game.
    private static readonly TimeZoneOption[] TimeZoneOptions = BuildTimeZoneOptions();

    private static TimeZoneOption[] BuildTimeZoneOptions()
    {
        var groups = new (int Offset, string Names)[]
        {
            (-660, "Niue|Pago Pago"),
            (-600, "Honolulu|Rarotonga|Tahiti"),
            (-570, "Marquesas"),
            (-540, "Adak|Gambier"),
            (-480, "Anchorage|Juneau|Metlakatla|Nome|Pitcairn|Sitka|Yakutat"),
            (-420, "Dawson|Dawson Creek|Fort Nelson|Hermosillo|Los Angeles|Mazatlan|Phoenix|Tijuana|Vancouver|Whitehorse"),
            (-360, "Bahia Banderas|Belize|Boise|Cambridge Bay|Chihuahua|Ciudad Juarez|Costa Rica|Denver|Easter|Edmonton|El Salvador|Galapagos|Guatemala|Inuvik|Managua|Merida|Mexico City|Monterrey|Regina|Swift Current|Tegucigalpa"),
            (-300, "Beulah|Bogota|Cancun|Center|Chicago|Eirunepe|Guayaquil|Jamaica|Knox|Lima|Matamoros|Menominee|New Salem|Ojinaga|Panama|Rankin Inlet|Resolute|Rio Branco|Tell City|Winnipeg"),
            (-240, "Barbados|Boa Vista|Campo Grande|Caracas|Cuiaba|Detroit|Grand Turk|Guyana|Havana|Indianapolis|Iqaluit|La Paz|Louisville|Manaus|Marengo|Martinique|Monticello|New York|Petersburg|Port-au-Prince|Porto Velho|Puerto Rico|Santiago|Santo Domingo|Toronto|Vevay|Vincennes|Winamac"),
            (-180, "Araguaina|Asuncion|Bahia|Belem|Bermuda|Buenos Aires|Catamarca|Cayenne|Cordoba|Coyhaique|Fortaleza|Glace Bay|Goose Bay|Halifax|Jujuy|La Rioja|Maceio|Mendoza|Moncton|Montevideo|Palmer|Paramaribo|Punta Arenas|Recife|Rio Gallegos|Rothera|Salta|San Juan|San Luis|Santarem|Sao Paulo|Stanley|Thule|Tucuman|Ushuaia"),
            (-150, "St. John's"),
            (-120, "Miquelon|Noronha|South Georgia"),
            (-60, "Cape Verde|Nuuk|Scoresbysund"),
            (0, "Abidjan|Azores|Bissau|Danmarkshavn|Monrovia|Sao Tome|UTC"),
            (60, "Algiers|Canary|Casablanca|Dublin|El Aaiun|Faroe|Lagos|Lisbon|London|Madeira|Ndjamena|Tunis"),
            (120, "Andorra|Belgrade|Berlin|Brussels|Budapest|Ceuta|Gibraltar|Johannesburg|Juba|Kaliningrad|Khartoum|Madrid|Malta|Maputo|Paris|Prague|Rome|Tirane|Tripoli|Troll|Vienna|Warsaw|Windhoek|Zurich"),
            (180, "Amman|Athens|Baghdad|Beirut|Bucharest|Cairo|Chisinau|Damascus|Famagusta|Gaza|Hebron|Helsinki|Istanbul|Jerusalem|Kirov|Kyiv|Minsk|Moscow|Nairobi|Nicosia|Qatar|Riga|Riyadh|Simferopol|Sofia|Tallinn|Vilnius|Volgograd"),
            (210, "Tehran"),
            (240, "Astrakhan|Baku|Dubai|Mauritius|Samara|Saratov|Tbilisi|Ulyanovsk|Yerevan"),
            (270, "Kabul"),
            (300, "Almaty|Aqtau|Aqtobe|Ashgabat|Atyrau|Dushanbe|Karachi|Maldives|Mawson|Oral|Qostanay|Qyzylorda|Samarkand|Tashkent|Vostok|Yekaterinburg"),
            (330, "Colombo|Kolkata"),
            (345, "Kathmandu"),
            (360, "Bishkek|Chagos|Dhaka|Omsk|Thimphu|Urumqi"),
            (390, "Yangon"),
            (420, "Bangkok|Barnaul|Davis|Ho Chi Minh|Hovd|Jakarta|Krasnoyarsk|Novokuznetsk|Novosibirsk|Pontianak|Tomsk"),
            (480, "Casey|Hong Kong|Irkutsk|Kuching|Macau|Makassar|Manila|Perth|Shanghai|Singapore|Taipei|Ulaanbaatar"),
            (525, "Eucla"),
            (540, "Chita|Dili|Jayapura|Khandyga|Palau|Pyongyang|Seoul|Tokyo|Yakutsk"),
            (570, "Adelaide|Broken Hill|Darwin"),
            (600, "Brisbane|Guam|Hobart|Lindeman|Macquarie|Melbourne|Port Moresby|Sydney|Ust-Nera|Vladivostok"),
            (630, "Lord Howe"),
            (660, "Bougainville|Efate|Guadalcanal|Kosrae|Magadan|Norfolk|Noumea|Sakhalin|Srednekolymsk"),
            (720, "Anadyr|Auckland|Fiji|Kamchatka|Kwajalein|Nauru|Tarawa"),
            (765, "Chatham"),
            (780, "Apia|Fakaofo|Kanton|Tongatapu"),
            (840, "Kiritimati"),
        };

        return groups.SelectMany(group => group.Names.Split('|')
            .Select(name => new TimeZoneOption(group.Offset, name))).ToArray();
    }

    private void DrawMainPageV3()
    {
        // V3 deliberately starts from the compact main composition. Feature-specific
        // V3 controls are added in the shared renderers so V1/V2 remain stable.
        DrawMainPageV2();
    }

    private void DrawSettingsPageV3(int level)
    {
        if (!ImGui.BeginTabBar("##settings_v3_tabs"))
            return;

        DrawSettingsV3Tab(0, "General", () => DrawSettingsV3General(level));
        DrawSettingsV3Tab(1, "Automation", DrawSettingsV3Automation);
        DrawSettingsV3Tab(2, "Rules", DrawSettingsV3Rules);
        DrawSettingsV3Tab(3, "Betting", DrawSettingsV3Betting);
        DrawSettingsV3Tab(4, "Time & Delay", DrawSettingsV3TimeDelay);
        DrawSettingsV3Tab(5, "Nearby Players", DrawSettingsV3Nearby);
        DrawSettingsV3Tab(6, "Visual", DrawSettingsV2Visual);
        DrawSettingsV3Tab(7, "Alliance", DrawSettingsAllianceBody);
        DrawSettingsV3Tab(8, "Preset Setup", DrawSettingsPresetSetupBody);
        DrawSettingsV3Tab(9, "System", () => DrawSettingsV2System(level));

        ImGui.EndTabBar();
    }

    private void DrawSettingsV3Tab(int index, string label, Action draw)
    {
        if (!ImGui.BeginTabItem(label))
            return;

        _settingsV3TabIndex = index;
        ImGui.Spacing();
        draw();
        ImGui.EndTabItem();
    }

    private void DrawSettingsV3General(int level)
    {
        ImGui.TextUnformatted("User Level");
        DrawEnumButtons("user_level_v3", ref level, new[] { "Beginner", "Advanced", "Dev", "Custom" }, idx =>
        {
            _config.CurrentLevel = (UserLevel)idx;
            _save();
        });

        ImGui.Spacing();
        ImGui.TextUnformatted("Main View");
        var mainView = 2;
        DrawEnumButtons("main_view_v3", ref mainView, new[] { "Classic", "Version 2", "Version 3" }, idx =>
        {
            _config.MainViewVersion = idx + 1;
            _save();
        });

        ImGui.Spacing();
        ImGui.TextUnformatted("Menu Style");
        var menuStyle = (int)_config.MenuStyle;
        DrawEnumButtons("menu_style_v3", ref menuStyle, new[] { "Side", "Burger", "Tabs" }, idx =>
        {
            _config.MenuStyle = (MenuStyleMode)idx;
            _save();
        });

        ImGui.Spacing();
        ImGui.TextUnformatted("Gil Display");
        var gilVisual = (int)_config.GilVisual;
        ImGui.PushFont(UiBuilder.MonoFont);
        DrawEnumButtons("gil_display_v3", ref gilVisual, new[] { "12345678", "12,345,678", ", 12,345,678" }, idx =>
        {
            _config.GilVisual = (GilVisualMode)idx;
            _save();
        });
        ImGui.PopFont();
    }

    private void DrawSettingsV3Automation()
    {
        ImGui.TextUnformatted("Enable:");

        DrawAutomationToggle("Message Reaction", ref _config.AutoRun, "message_reaction", enabled =>
        {
            _config.ShowAutoRunButton = true;
            if (enabled) _config.EnableAutomation = true;
            Plugin.Instance.ResetAutoActionState(cancelCurrentGroup: !enabled);
        }, drawAfter: () =>
        {
            if (BJBGui.Button("Insert regular Regex entries"))
                InsertRegularGameplayRegexEntries();
        });

        DrawAutomationToggle("Dealer Draw", ref _config.AutoDealerDraw, "dealer_draw", enabled =>
        {
            _config.ShowAutoDealerDrawButton = true;
            if (enabled) _config.EnableAutomation = true;
        });
        DrawAutomationToggle("Player Draw", ref _config.AutoInitialDeal, "player_draw", enabled =>
        {
            _config.ShowAutoPlayerHandButton = true;
            if (enabled) _config.EnableAutomation = true;
        });
        DrawAutomationToggle("Auto Activate Trading Players", ref _config.AutoActivateTradingPlayers,
            "auto_activate_trading_players", _ => { });
        DrawAutomationToggle("Continue after", ref _config.AutoContinue, "auto_continue", enabled =>
        {
            _config.ShowAutoContinueButton = true;
            if (enabled) _config.EnableAutomation = true;
        }, drawAfter: () =>
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100f);
            if (ImGui.InputFloat("##v3_auto_continue_delay", ref _config.AutoContinueDelay, 0f, 0f, "%.0f"))
            {
                _config.AutoContinueDelay = Math.Clamp(MathF.Round(_config.AutoContinueDelay), 10f, 300f);
                _save();
            }
            ImGui.SameLine();
            ImGui.TextUnformatted("Seconds");
        });

        ImGui.Spacing();
        DrawCommandSelector("Command after Bet-Change", ref _config.AutoBetPostCommandName);
        DrawCommandSelector("Command on insufficient Bank", ref _config.InsufficientBetCommandName);

        ImGui.TextUnformatted("Command Speed");
        ImGui.SameLine(260f);
        ImGui.SetNextItemWidth(220f);
        if (BJBGui.SliderFloat("##v3_command_speed", ref _config.CommandSpeedMultiplier, 0.1f, 4f, "%.2fx"))
        {
            _config.CommandSpeedMultiplier = Math.Clamp((float)Math.Round(_config.CommandSpeedMultiplier / 0.05f) * 0.05f, 0.1f, 4f);
            _save();
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton("Reset##v3_command_speed"))
        {
            _config.CommandSpeedMultiplier = 1f;
            _save();
        }

        ImGui.TextUnformatted("Recall Unlock");
        ImGui.SameLine(260f);
        ImGui.SetNextItemWidth(220f);
        if (BJBGui.SliderFloat("##v3_recall_unlock", ref _config.RecallUnlockSeconds, 1f, 60f, "%.0fs"))
        {
            _config.RecallUnlockSeconds = Math.Clamp(MathF.Round(_config.RecallUnlockSeconds), 1f, 60f);
            _save();
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton("Reset##v3_recall_unlock"))
        {
            _config.RecallUnlockSeconds = 20f;
            _save();
        }
    }

    private void DrawAutomationToggle(string label, ref bool value, string id, Action<bool> changed, Action? drawAfter = null)
    {
        if (BJBOnOffSwitch.Draw(id, ref value))
        {
            changed(value);
            _save();
        }
        ImGui.SameLine();
        ImGui.TextUnformatted(label);
        drawAfter?.Invoke();
    }

    private void InsertRegularGameplayRegexEntries()
    {
        var entries = new[]
        {
            CreateGameplayRegex("Draw", true, RegexChatSource.Party, RegexAction.WantHit,
                "^hit\\!*$", "^hit me\\!*$", "^h\\!*$"),
            CreateGameplayRegex("Stand", true, RegexChatSource.Party, RegexAction.WantStand,
                "^stand\\!*$", "^stando\\!*$", "^standy\\!*$", "^stay\\!*$", "^s\\!*$"),
            CreateGameplayRegex("Double Down", true, RegexChatSource.Party, RegexAction.WantDD,
                "^dd\\!*$", "^double down\\!*$"),
            CreateGameplayRegex("Split", true, RegexChatSource.Party, RegexAction.WantSplit,
                "^split\\!*$"),
            CreateGameplayRegex("Ready", false, RegexChatSource.Party | RegexChatSource.Tell | RegexChatSource.Say, RegexAction.NextRound,
                "^ready\\!*$", "^i\\'m ready\\!*$", "^r\\!*$"),
            CreateGameplayRegex("Set Bet", false, RegexChatSource.Party | RegexChatSource.Tell, RegexAction.SetBet,
                "^bet\\s+((?:\\d[\\d.,]*\\s*[km]?)|max|all|full|min)$"),
            CreateGameplayRegex("Bank Tell", false, RegexChatSource.Party | RegexChatSource.Tell | RegexChatSource.Say, RegexAction.BankTell,
                "^bank\\?*$", "^what\\'*s my bank\\?*$", "^what is my bank\\?*$"),
            CreateGameplayRegex("Withdraw", false, RegexChatSource.Party, RegexAction.Withdraw,
                "^withdraw\\s+((?:\\d[\\d.,]*\\s*[km]?)|all|everything)$"),
        };

        var changed = false;
        foreach (var entry in entries)
        {
            if (_config.UserRegexes.Any(existing => existing.Name.Equals(entry.Name, StringComparison.OrdinalIgnoreCase)))
                continue;
            _config.UserRegexes.Add(entry);
            changed = true;
        }

        if (!changed) return;
        RegexEngine.InvalidateCache();
        _save();
    }

    private static UserRegexEntry CreateGameplayRegex(string name, bool enabled, RegexChatSource sources,
        RegexAction action, params string[] patterns)
        => new()
        {
            Name = name,
            Enabled = enabled,
            CaseSensitive = false,
            Sources = sources,
            Mode = RegexEntryMode.Trigger,
            Action = action,
            Patterns = new List<string>(patterns),
        };

    private void DrawSettingsV3Rules()
    {
        Header("Dealing Behavior");
        DrawV3RuleActionButton("First Deal, then play", "first_deal", ref _config.FirstDealThenPlay);
        DrawV3RuleActionButton("Player self-rolling", "self_rolling", ref _config.PlayerRollingForThemselves);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Players roll their own required cards with /dice 13, /dice alliance 13, or the native /random command.\nDealer rolls are unchanged.");
        DrawV3RuleActionButton("Hide card suits", "hide_suits", ref _config.HideCardSuits);

        Header("Dealer Rules");
        ImGui.TextUnformatted("Dealer stands on");
        ImGui.SameLine(260f);
        ImGui.SetNextItemWidth(90f);
        if (BJBGui.InputInt("##v3_dealer_draws_until", ref _config.DealerDrawsUntil, 1))
        {
            _config.DealerDrawsUntil = Math.Clamp(_config.DealerDrawsUntil, 2, 21);
            _save();
        }
        ImGui.SameLine();
        DrawV3RuleActionButton("Soft", "soft", ref _config.DealerSoftRule);

        Header("Game Settings");
        Header("Win");
        MultiplierInput("Payout", ref _config.MultiplierNormalWin, 2f, "v3_win");

        Header("BlackJack");
        Header("Natural");
        MultiplierInput("Payout", ref _config.MultiplierBlackjackWin, 2.5f, "v3_natbj");
        Header("Dirty");
        DrawV3OnOff("Enable Dirty Blackjack", "dirty", ref _config.EnableDirtyBlackjack);
        MultiplierInput("Payout", ref _config.MultiplierDirtyBlackjackWin, 2f, "v3_dirtybj");

        Header("Charlie");
        DrawV3OnOff("Enable Charlie", "charlie", ref _config.EnableCharlie);
        DrawV3OnOff("Instant-Win", "charlie_instant", ref _config.CharlieInstantWin);
        DrawV3RuleInteger("Cards", "charlie_cards", ref _config.CharlieCardCount, 3, 9, 5);
        MultiplierInput("Payout", ref _config.MultiplierBlackjackWin, 2.5f, "v3_charlie_payout");

        Header("Split");
        DrawV3OnOff("Enable Split", "split", ref _config.EnableSplit);
        DrawV3OnOff("Identical Split only", "identical_split", ref _config.IdenticalSplitOnly);
        DrawV3RuleInteger("Max Hands", "max_hands", ref _config.MaxHandsPerPlayer, 2, 10, 2);
        MultiplierInput("Payout", ref _config.MultiplierNormalWin, 2f, "v3_split_payout");

        Header("Double Down");
        DrawV3OnOff("Enable Double Down", "double_down", ref _config.EnableDoubleDown);
        DrawV3OnOff("Allow Double-Down after Split", "double_after_split", ref _config.AllowDoubleDownAfterSplit);
        DrawV3OnOff("Refund Double Down on push", "refund_double_down", ref _config.RefundFullDoubleDownOnPush);
        var tie = (int)_config.BlackjackTieRule;
        ImGui.TextUnformatted("BlackJack Tie Rule");
        ImGui.SameLine(260f);
        DrawEnumButtons("bj_tie_v3", ref tie, new[] { "Push", "Player Nat BJ wins", "Dealer Nat BJ wins", "NatBJ > DirtyBJ" }, idx =>
        {
            _config.BlackjackTieRule = (BlackjackTieRule)idx;
            _save();
        });
        MultiplierInput("Payout", ref _config.MultiplierBlackjackWin, 2.5f, "v3_dd_payout");

        Header("Result");
        DrawV3OnOff("Short Result Messages", "short_results", ref _config.SmallResult);
        DrawShortResultRulesEditor();
    }

    private void DrawV3OnOff(string label, string id, ref bool value)
    {
        ImGui.TextUnformatted(label);
        ImGui.SameLine(260f);
        if (BJBOnOffSwitch.Draw($"v3_{id}", ref value)) _save();
    }

    private void DrawV3RuleActionButton(string label, string id, ref bool value)
    {
        // Keep rendering scope tied to the value that existed on entry. A click
        // may change the setting, but it must never decide whether this frame's
        // style colors are popped.
        var wasActive = value;
        if (wasActive)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1f, 0.5f, 0f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1f, 0.6f, 0.1f, 1f));
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0f, 0f, 0f, 1f));
        }
        var clicked = wasActive
            ? ImGui.Button($"{label}##v3_rule_{id}")
            : BJBGui.Button($"{label}##v3_rule_{id}");
        if (wasActive) ImGui.PopStyleColor(3);
        if (clicked)
        {
            value = !value;
            _save();
        }
    }

    private void DrawV3RuleInteger(string label, string id, ref int value, int min, int max, int defaultValue)
    {
        ImGui.TextUnformatted(label);
        ImGui.SameLine(260f);
        ImGui.SetNextItemWidth(90f);
        if (BJBGui.InputInt($"##v3_rule_{id}", ref value, 1))
        {
            value = Math.Clamp(value, min, max);
            _save();
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton($"Reset##v3_rule_{id}_reset"))
        {
            value = defaultValue;
            _save();
        }
    }

    private void DrawSettingsV3Betting()
    {
        if (_config.EnsureBetLimitEntriesMigration()) _save();

        if (ImGui.BeginTable("bjb_bet_entries_v3", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Active", ImGuiTableColumnFlags.WidthFixed, 96f);
            ImGui.TableSetupColumn("Kind", ImGuiTableColumnFlags.WidthFixed, 100f);
            ImGui.TableSetupColumn("VIP", ImGuiTableColumnFlags.WidthFixed, 116f);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 200f);
            ImGui.TableSetupColumn("Amount", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, 17f);
            ImGui.TableHeadersRow();

            for (var i = 0; i < _config.BetLimitEntries.Count; i++)
            {
                var entry = _config.BetLimitEntries[i];
                ImGui.PushID($"v3_bet_entry_{i}");
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                if (BJBOnOffSwitch.Draw("active", ref entry.Active, 48f)) CommitV3BetEntries();

                ImGui.TableNextColumn();
                var kind = entry.Kind switch
                {
                    BetLimitEntryKind.MinBet => 0,
                    BetLimitEntryKind.Normal => 1,
                    _ => 2,
                };
                if (entry.Kind == BetLimitEntryKind.MinBet) ImGui.BeginDisabled();
                // Grow from the original ImGui control width, rather than from
                // the table column width, so the visual gain is exactly 36 px.
                var kindComboWidth = ImGui.CalcItemWidth() + 36f;
                ImGui.SetNextItemWidth(kindComboWidth);
                if (BJBGui.Combo("##kind", ref kind, "Minimum\0Normal\0VIP\0", kindComboWidth))
                {
                    entry.Kind = kind switch
                    {
                        0 => BetLimitEntryKind.MinBet,
                        1 => BetLimitEntryKind.Normal,
                        _ => BetLimitEntryKind.Vip,
                    };
                    if (entry.Kind != BetLimitEntryKind.Vip) entry.VipLevel = 0;
                    entry.Name = entry.Kind switch
                    {
                        BetLimitEntryKind.MinBet => "Min",
                        BetLimitEntryKind.Normal => "Max",
                        _ => string.IsNullOrWhiteSpace(entry.Name) ? "VIP" : entry.Name,
                    };
                    CommitV3BetEntries();
                }
                if (entry.Kind == BetLimitEntryKind.MinBet) ImGui.EndDisabled();

                ImGui.TableNextColumn();
                if (entry.Kind != BetLimitEntryKind.Vip)
                    ImGui.TextDisabled("-");
                else
                {
                    ImGui.SetNextItemWidth(-1f);
                    if (BJBGui.InputInt("##vip_level", ref entry.VipLevel, 1))
                    {
                        entry.VipLevel = Math.Clamp(entry.VipLevel, 1, 99);
                        CommitV3BetEntries();
                    }
                }

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.InputText("##name", ref entry.Name, 64))
                {
                    if (string.IsNullOrWhiteSpace(entry.Name))
                        entry.Name = entry.Kind switch
                        {
                            BetLimitEntryKind.MinBet => "Min",
                            BetLimitEntryKind.Normal => "Max",
                            _ => "VIP",
                        };
                    CommitV3BetEntries();
                }

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1f);
                if (BJBGui.InputLongFormatted("##amount", ref entry.Amount))
                {
                    entry.Amount = Math.Clamp(entry.Amount, 1, 1_000_000_000);
                    CommitV3BetEntries();
                }

                ImGui.TableNextColumn();
                if (entry.Kind == BetLimitEntryKind.MinBet) ImGui.BeginDisabled();
                if (BJBGui.SmallButton("X##delete"))
                {
                    _config.BetLimitEntries.RemoveAt(i);
                    CommitV3BetEntries();
                    ImGui.PopID();
                    break;
                }
                if (entry.Kind == BetLimitEntryKind.MinBet) ImGui.EndDisabled();

                ImGui.PopID();
            }
            ImGui.EndTable();
        }

        if (BJBGui.Button("Add Entry##v3_add_bet_entry"))
        {
            var nextVipLevel = Math.Max(1, _config.BetLimitEntries
                .Where(entry => entry.Kind == BetLimitEntryKind.Vip)
                .Select(entry => entry.VipLevel)
                .DefaultIfEmpty(0)
                .Max() + 1);
            _config.BetLimitEntries.Add(new BetLimitEntry
            {
                Active = false,
                Kind = BetLimitEntryKind.Vip,
                VipLevel = nextVipLevel,
                Name = "VIP",
                Amount = _config.MaxBet,
            });
            CommitV3BetEntries();
        }

        Header("Betting Presets");
        foreach (var preset in _config.BettingPresets.ToList())
            DrawV3BettingPreset(preset);

        if (BJBGui.Button("Save as preset##v3_save_bet_preset"))
        {
            _config.BettingPresets.Add(new BettingPreset
            {
                Name = GetNextV3BettingPresetName(),
                Entries = _config.BetLimitEntries.Select(CloneBetEntry).ToList(),
            });
            _save();
        }
    }

    private void DrawV3BettingPreset(BettingPreset preset)
    {
        ImGui.PushID($"v3_betting_preset_{preset.Name}");
        if (preset.Color.HasValue)
            ImGui.PushStyleColor(ImGuiCol.Button, preset.Color.Value);
        if (BJBGui.Button($"{FormatV3BettingPreset(preset)}##load"))
        {
            var armed = _v3ArmedBetPresetName.Equals(preset.Name, StringComparison.Ordinal)
                && DateTime.UtcNow <= _v3ArmedBetPresetUntil;
            if (armed)
            {
                _config.BetLimitEntries = preset.Entries.Select(CloneBetEntry).ToList();
                _config.BetLimitEntriesMigrated = true;
                CommitV3BetEntries();
                _v3ArmedBetPresetName = string.Empty;
            }
            else
            {
                _v3ArmedBetPresetName = preset.Name;
                _v3ArmedBetPresetUntil = DateTime.UtcNow.AddSeconds(2);
            }
        }
        if (preset.Color.HasValue) ImGui.PopStyleColor();
        ImGui.SameLine();
        if (BJBGui.SmallButton("*##color"))
            _v3EditingBetPresetColor = _v3EditingBetPresetColor == preset.Name ? string.Empty : preset.Name;
        ImGui.SameLine();
        if (BJBGui.SmallButton("X##delete"))
        {
            _config.BettingPresets.Remove(preset);
            if (_v3ArmedBetPresetName == preset.Name) _v3ArmedBetPresetName = string.Empty;
            _save();
            ImGui.PopID();
            return;
        }

        if (_v3ArmedBetPresetName == preset.Name && DateTime.UtcNow <= _v3ArmedBetPresetUntil)
            ImGui.TextDisabled("Click again to load this preset");

        ImGui.SetNextItemWidth(180f);
        if (ImGui.InputText("Name##rename", ref preset.Name, 64)) _save();

        if (_v3EditingBetPresetColor == preset.Name)
        {
            var color = preset.Color ?? new Vector4(0.26f, 0.46f, 0.72f, 1f);
            if (ImGui.ColorEdit4("Color##edit", ref color))
            {
                color.W = 1f;
                preset.Color = color;
                _save();
            }
            ImGui.SameLine();
            if (BJBGui.SmallButton("Reset Color##reset"))
            {
                preset.Color = null;
                _save();
            }
        }
        ImGui.PopID();
    }

    private void CommitV3BetEntries()
    {
        var minimum = _config.BetLimitEntries.FirstOrDefault(entry => entry.Kind == BetLimitEntryKind.MinBet);
        if (minimum == null)
        {
            minimum = new BetLimitEntry { Active = true, Kind = BetLimitEntryKind.MinBet, Name = "Min", Amount = _config.MinBet };
            _config.BetLimitEntries.Insert(0, minimum);
        }
        minimum.VipLevel = 0;
        minimum.Name = string.IsNullOrWhiteSpace(minimum.Name) ? "Min" : minimum.Name;
        _config.MinBet = Math.Max(1, minimum.Amount);

        var normal = _config.BetLimitEntries.LastOrDefault(entry => entry.Kind == BetLimitEntryKind.Normal);
        if (normal != null)
        {
            normal.VipLevel = 0;
            normal.Name = string.IsNullOrWhiteSpace(normal.Name) ? "Max" : normal.Name;
            _config.MaxBet = Math.Max(1, normal.Amount);
        }

        _config.VipBetTiers = _config.BetLimitEntries
            .Where(entry => entry.Active && entry.Kind == BetLimitEntryKind.Vip && entry.VipLevel > 0)
            .GroupBy(entry => entry.VipLevel)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var entry = group.Last();
                return new VipBetTier { Name = string.IsNullOrWhiteSpace(entry.Name) ? "VIP" : entry.Name, MaxBet = entry.Amount };
            })
            .ToList();
        _config.BetLimitEntriesMigrated = true;
        _betDraftEntries = null;
        _save();
    }

    private string GetNextV3BettingPresetName()
    {
        const string root = "New Preset";
        if (_config.BettingPresets.All(preset => !preset.Name.Equals(root, StringComparison.OrdinalIgnoreCase)))
            return root;
        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{root} {suffix}";
            if (_config.BettingPresets.All(preset => !preset.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
        }
    }

    private static string FormatV3BettingPreset(BettingPreset preset)
    {
        var entries = preset.Entries
            .OrderBy(entry => entry.Kind == BetLimitEntryKind.MinBet ? 0 : entry.Kind == BetLimitEntryKind.Normal ? 1 : 2)
            .ThenBy(entry => entry.VipLevel)
            .Select(entry => $"{(string.IsNullOrWhiteSpace(entry.Name) ? entry.Kind.ToString() : entry.Name)}: {entry.Amount}");
        return string.Join(", ", entries);
    }

    private void DrawSettingsV3TimeDelay()
    {
        EnsureV3TimeZoneSelection();

        ImGui.TextUnformatted("Time Zone");
        ImGui.SameLine();
        ImGui.TextDisabled($"Current: {GetV3TimeZoneLabel()} ({FormatUtcOffset(_config.GetUtcOffsetMinutes())})");

        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##v3_timezone_filter", "Filter", ref _v3TimeZoneFilter, 100);
        ImGui.Spacing();

        var filter = _v3TimeZoneFilter.Trim();
        if (ImGui.BeginChild("##v3_timezone_list", new Vector2(0, 270f), true))
        {
            var lastOffset = int.MinValue;
            foreach (var option in TimeZoneOptions.Where(option => string.IsNullOrWhiteSpace(filter)
                         || option.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                         || FormatUtcOffset(option.OffsetMinutes).Contains(filter, StringComparison.OrdinalIgnoreCase)))
            {
                if (lastOffset != option.OffsetMinutes)
                {
                    if (lastOffset != int.MinValue)
                    {
                        ImGui.NewLine();
                        ImGui.Spacing();
                    }
                    ImGui.TextDisabled(FormatUtcOffset(option.OffsetMinutes));
                    lastOffset = option.OffsetMinutes;
                }

                var selected = _config.UtcTimeZoneName.Equals(option.Name, StringComparison.OrdinalIgnoreCase)
                    && _config.UtcOffsetMinutes == option.OffsetMinutes;
                if (selected)
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.26f, 0.46f, 0.72f, 1f));

                if (BJBGui.Button($"{option.Name}##v3_tz_{option.Name}"))
                {
                    _config.SetUtcBaseOffsetMinutes(option.OffsetMinutes, option.Name);
                    _v3EditingCustomUtc = false;
                    _save();
                }

                if (selected) ImGui.PopStyleColor();
                if (ImGui.GetContentRegionAvail().X > ImGui.CalcTextSize(option.Name).X + 30f)
                    ImGui.SameLine();
            }
        }
        ImGui.EndChild();

        ImGui.Spacing();
        ImGui.TextUnformatted("Summer/Winter Time");
        ImGui.SameLine(260f);
        if (BJBOnOffSwitch.Draw("v3_summer_time", ref _config.UtcSummerTime)) _save();
        ImGui.SameLine();
        ImGui.TextDisabled("On adds +1 hour to the selected base offset.");

        if (IsV3CustomUtcSelection())
        {
            ImGui.Spacing();
            if (BJBGui.Button($"Custom {FormatUtcOffset(_config.UtcOffsetMinutes)}##v3_custom_utc"))
            {
                _v3EditingCustomUtc = true;
                _v3CustomUtcInput = FormatUtcOffset(_config.UtcOffsetMinutes).Replace("UTC", string.Empty);
            }

            if (_v3EditingCustomUtc)
                DrawV3CustomUtcEditor();
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Delay");
        ImGui.SameLine(260f);
        ImGui.TextDisabled("Second snapping is always enabled.");
    }

    private void EnsureV3TimeZoneSelection()
    {
        if (_config.UtcOffsetMinutes == int.MinValue)
            _config.SetUtcBaseOffsetMinutes(_config.UtcOffsetHours * 60, null);

        if (!string.IsNullOrWhiteSpace(_config.UtcTimeZoneName)) return;
        var match = TimeZoneOptions.FirstOrDefault(option => option.OffsetMinutes == _config.UtcOffsetMinutes);
        if (match == null) return;

        // Migration rule: an old numeric setting selects the first matching city.
        _config.UtcTimeZoneName = match.Name;
        _save();
    }

    private bool IsV3CustomUtcSelection() => string.IsNullOrWhiteSpace(_config.UtcTimeZoneName)
        || TimeZoneOptions.All(option => !option.Name.Equals(_config.UtcTimeZoneName, StringComparison.OrdinalIgnoreCase)
            || option.OffsetMinutes != _config.UtcOffsetMinutes);

    private string GetV3TimeZoneLabel() => IsV3CustomUtcSelection()
        ? "Custom"
        : _config.UtcTimeZoneName;

    private void DrawV3CustomUtcEditor()
    {
        ImGui.SetNextItemWidth(120f);
        ImGui.InputText("UTC offset##v3_custom_utc_input", ref _v3CustomUtcInput, 16);
        ImGui.SameLine();
        if (BJBGui.Button("OK##v3_custom_utc_ok") && TryParseUtcOffset(_v3CustomUtcInput, out var offsetMinutes))
        {
            _config.SetUtcBaseOffsetMinutes(offsetMinutes, null);
            _v3EditingCustomUtc = false;
            _save();
        }
        ImGui.SameLine();
        if (BJBGui.Button("Cancel##v3_custom_utc_cancel"))
            _v3EditingCustomUtc = false;
        ImGui.SameLine();
        ImGui.TextDisabled("Examples: -3, +5:30, +5.75");
    }

    private static bool TryParseUtcOffset(string text, out int offsetMinutes)
    {
        text = text.Trim().Replace("UTC", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        var sign = 1;
        if (text.StartsWith('+')) text = text[1..];
        else if (text.StartsWith('-'))
        {
            sign = -1;
            text = text[1..];
        }

        var colonIndex = text.IndexOf(':');
        if (colonIndex > 0
            && text.IndexOf(':', colonIndex + 1) < 0
            && int.TryParse(text[..colonIndex], NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
            && int.TryParse(text[(colonIndex + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            && minutes >= 0 && minutes < 60)
        {
            offsetMinutes = Math.Clamp(sign * (hours * 60 + minutes), -12 * 60, 14 * 60);
            return true;
        }

        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var decimalHours))
        {
            offsetMinutes = Math.Clamp((int)MathF.Round(sign * decimalHours * 60f), -12 * 60, 14 * 60);
            return true;
        }

        offsetMinutes = 0;
        return false;
    }

    private static string FormatUtcOffset(int offsetMinutes)
    {
        var sign = offsetMinutes < 0 ? "-" : "+";
        var absolute = Math.Abs(offsetMinutes);
        return absolute % 60 == 0
            ? $"UTC{sign}{absolute / 60}"
            : $"UTC{sign}{absolute / 60}:{absolute % 60:00}";
    }

    private void DrawSettingsV3Nearby() => DrawSettingsV2Nearby();
}
