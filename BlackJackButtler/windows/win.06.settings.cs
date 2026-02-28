using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawSettingsPage()
    {
        if (_openImportConfirmPopup) {
            ImGui.OpenPopup("import_confirm_popup");
            _openImportConfirmPopup = false;
        }

        int level = (int)_config.CurrentLevel;

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
    }

    private void DrawSettingsTab_General(int level)
    {
        if (ImGui.BeginTabItem("General"))
        {
            ImGui.Spacing();
            ImGui.TextUnformatted("User Level");
            ImGui.SameLine(300f);
            ImGui.SetNextItemWidth(200f);
            if (BJBGui.Combo("##user_level", ref level, "Beginner\0Advanced\0Dev\0")) {
                _config.CurrentLevel = (UserLevel)level;
                _save();
            }

            ImGui.Spacing();
            ImGui.TextUnformatted("Command Speed");
            ImGui.SameLine(300f);
            ImGui.SetNextItemWidth(200f);
            if (BJBGui.SliderFloat("##cmd_speed", ref _config.CommandSpeedMultiplier, 0.1f, 4.0f, "%.2fx"))
            {
                _config.CommandSpeedMultiplier = (float)(Math.Round(_config.CommandSpeedMultiplier / 0.05) * 0.05);
                _config.CommandSpeedMultiplier = Math.Clamp(_config.CommandSpeedMultiplier, 0.1f, 4.0f);
                _save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Multiplier applied to all command delays at execution time.\n1.00x = normal speed, 0.50x = twice as fast, 2.00x = twice as slow.\nMinimum effective delay is always 0.3s.");

            ImGui.Separator();
            ImGui.Spacing();
            if (ImGui.Checkbox("Show Nearby Players", ref _config.ShowNearbyPlayers)) _save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Show a list of nearby players below the player table.");
            if (_config.ShowNearbyPlayers)
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100f);
                if (BJBGui.DragInt("Columns##nearby_columns", ref _config.NearbyColumns, 0.1f, 1, 5, "%d"))
                {
                    _config.NearbyColumns = Math.Clamp(_config.NearbyColumns, 1, 5);
                    _save();
                }
            }

            ImGui.EndTabItem();
        }
    }

    private void DrawSettingsTab_Gameplay(int level)
    {
        if (ImGui.BeginTabItem("Gameplay"))
        {
            ImGui.Spacing();
            ImGui.TextUnformatted("Rules");
            ImGui.Separator();

            ImGui.Spacing();
            if (ImGui.Checkbox("First Deal, then Play", ref _config.FirstDealThenPlay)) _save();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Active: First deal every player their hands.\nInactive: Deal hand and direct play per player.");

            ImGui.Spacing();
            if (ImGui.Checkbox("Identical Split Only", ref _config.IdenticalSplitOnly)) _save();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Active: Only same cards (e.g. J+J) can split.\nInactive: Same score (e.g. J+K) can split.");

            ImGui.Spacing();
            if (ImGui.Checkbox("Allow Double Down after Split", ref _config.AllowDoubleDownAfterSplit)) _save();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Active: Allows the player to Double Down on hands that resulted from a split.\nInactive: Splitted hands don't allow to Double Down.");

            ImGui.Spacing();
            if (ImGui.Checkbox("Refund DD on push", ref _config.RefundFullDoubleDownOnPush)) _save();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Active: If a player has DD and got pushed, the DD bet gets pushed too.\nInactive: If a player has DD and got pushed, the DD bet is lost.");

            ImGui.Spacing();
            if (ImGui.Checkbox("Player BJ wins on tie", ref _config.PlayerBJWinsOnTie)) _save();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Active: If the player has a Blackjack (natural or dirty) and the dealer also has 21, the player wins.\nInactive: Both having 21 results in a push.");

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextUnformatted("Round Behavior");
            ImGui.Separator();

            ImGui.Spacing();
            if (ImGui.Checkbox("Autostart round only on multiple players", ref _config.AutostartRoundOnlyOnMultiplePlayers)) _save();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Active: NextRound trigger only auto-starts when 2+ active players voted.\nWith only 1 player, it highlights the button instead.\nInactive: NextRound auto-starts regardless of player count.");

            bool dropboxDetected = DropboxIntegration.IsDropboxAvailable();
            if (dropboxDetected)
            {
                ImGui.Spacing();
                if (ImGui.Checkbox("Open Dropbox instead of trade", ref _config.OpenDropboxInsteadOfTrade)) _save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Use the Dropbox plugin for payouts instead of manual trade.\nDropbox plugin detected and loaded.");
            }

            ImGui.Spacing();
            if (ImGui.Checkbox("Small Result Message", ref _config.SmallResult)) _save();
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
                ImGui.Spacing();
                ImGui.TextUnformatted("View Direction");
                ImGui.Separator();

                ImGui.Spacing();
                float degrees = _config.InitialViewDirection * (180f / MathF.PI);
                if (degrees < 0) degrees += 360f;
                ImGui.TextUnformatted("Facing Direction");
                ImGui.SameLine(300f);
                ImGui.SetNextItemWidth(200f);
                if (BJBGui.SliderFloat("##view_dir", ref degrees, 0f, 360f, "%.1f\u00b0"))
                {
                    _config.InitialViewDirection = degrees * (MathF.PI / 180f);
                    _save();
                    ViewDirectionManager.ApplyViewDirection(_config);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Character facing direction.\nAuto-captured when Group Detector activates.\nChanges apply immediately.");

                ImGui.Spacing();
                if (ImGui.Checkbox("Auto-Rotate on phase change", ref _config.LookEveryTime))
                    _save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Active: Face the configured direction when entering Waiting, Deal, Dealer, or Payout phase.\nInactive: Only at round start or via /initialviewdirection command.");
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

    private void DrawSettingsTab_System(int level)
    {
        if (ImGui.BeginTabItem("System"))
        {
            ImGui.Spacing();
            ImGui.TextUnformatted("Defaults");
            ImGui.Separator();
            ImGui.Spacing();

            var io = ImGui.GetIO();
            bool keysDown = io.KeyCtrl && io.KeyShift;

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
                    ImGui.SetTooltip("Erhöht die maximale Wait-Zeit auf 30 s.\nBeim Deaktivieren werden alle Einträge über 12 s auf 12 s gesetzt.");

                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.TextUnformatted("Config File");
                ImGui.Separator();
                ImGui.Spacing();

                if (BJBGui.Button("Export##cfg")) {
                    var json = JsonConvert.SerializeObject(_config, Formatting.Indented);
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
                                var imported = JsonConvert.DeserializeObject<Configuration>(json);
                                if (imported != null) {
                                    _tempImportConfig = imported;
                                    _openImportConfirmPopup = true;
                                }
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

    private void DoFullReplace() {
        if (_tempImportConfig == null) return;

        _config.MessageBatches = _tempImportConfig.MessageBatches;
        _config.UserRegexes = _tempImportConfig.UserRegexes;
        _config.CommandGroups = _tempImportConfig.CommandGroups;
        _config.CustomCommandGroups = _tempImportConfig.CustomCommandGroups;

        _config.MultiplierNormalWin = _tempImportConfig.MultiplierNormalWin;
        _config.MultiplierBlackjackWin = _tempImportConfig.MultiplierBlackjackWin;
        _config.MultiplierDirtyBlackjackWin = _tempImportConfig.MultiplierDirtyBlackjackWin;
        _config.MaxHandsPerPlayer = _tempImportConfig.MaxHandsPerPlayer;
        _config.MinBet = _tempImportConfig.MinBet;
        _config.MaxBet = _tempImportConfig.MaxBet;
        _config.ShortBetFormat = _tempImportConfig.ShortBetFormat;

        _save();
    }

    private void DoMerge() {
        if (_tempImportConfig == null) return;

        foreach (var b in _tempImportConfig.MessageBatches) {
            _config.MessageBatches.RemoveAll(x => x.Name == b.Name);
            _config.MessageBatches.Add(b);
        }

        foreach (var r in _tempImportConfig.UserRegexes) {
            _config.UserRegexes.RemoveAll(x => x.Name == r.Name);
            _config.UserRegexes.Add(r);
        }

        foreach (var c in _tempImportConfig.CommandGroups) {
            _config.CommandGroups.RemoveAll(x => x.Name == c.Name);
            _config.CommandGroups.Add(c);
        }

        foreach (var c in _tempImportConfig.CustomCommandGroups) {
            _config.CustomCommandGroups.RemoveAll(x => x.Name == c.Name);
            _config.CustomCommandGroups.Add(c);
        }

        _save();
    }
}
