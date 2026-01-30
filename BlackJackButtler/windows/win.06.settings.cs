using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawSettingsPage()
    {
        ImGui.TextUnformatted("Gameplay Settings");
        ImGui.Separator();

        ImGui.TextUnformatted("User Level");
        ImGui.SetNextItemWidth(200f);
        int level = (int)_config.CurrentLevel;
        if (ImGui.Combo("##user_level", ref level, "Beginner\0Advanced\0Dev\0")) {
            _config.CurrentLevel = (UserLevel)level;
            _save();
        }
        ImGui.Spacing();
        ImGui.TextUnformatted("Command Speed");
        ImGui.SameLine(300f);
        ImGui.SetNextItemWidth(200f);
        if (ImGui.SliderFloat("##cmd_speed", ref _config.CommandSpeedMultiplier, 0.1f, 4.0f, "%.2fx"))
        {
            _config.CommandSpeedMultiplier = (float)(Math.Round(_config.CommandSpeedMultiplier / 0.05) * 0.05);
            _config.CommandSpeedMultiplier = Math.Clamp(_config.CommandSpeedMultiplier, 0.1f, 4.0f);
            _save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Multiplier applied to all command delays at execution time.\n1.00x = normal speed, 0.50x = twice as fast, 2.00x = twice as slow.\nMinimum effective delay is always 0.3s.");

        ImGui.Separator();

        ImGui.TextUnformatted("Gameplay Rules");
        if(level >= (int)UserLevel.Advanced)
        {
            ImGui.Spacing();
            if (ImGui.Checkbox("First Deal, then Play", ref _config.FirstDealThenPlay)) _save();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Active: First deal every player their hands.\nInactive: Deal hand and direct play per player.");
        } else if (!_config.FirstDealThenPlay) {
            _config.FirstDealThenPlay = true;
            _save();
        }

        if(level >= (int)UserLevel.Advanced)
        {
            ImGui.Spacing();
            if (ImGui.Checkbox("Identical Split Only", ref _config.IdenticalSplitOnly)) _save();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Active: Only same cards (e.g. J+J) can split.\nInactive: Same score (e.g. J+K) can split.");
        } else if (!_config.IdenticalSplitOnly) {
            _config.IdenticalSplitOnly = true;
            _save();
        }

        if(level >= (int)UserLevel.Advanced)
        {
            ImGui.Spacing();
            if (ImGui.Checkbox("Allow Double Down after Split", ref _config.AllowDoubleDownAfterSplit)) _save();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Active: Allows the player to Double Down on hands that resulted from a split.\nInactive: Splitted hands don't allow to Double Down.");
        } else if (_config.AllowDoubleDownAfterSplit) {
            _config.AllowDoubleDownAfterSplit = false;
            _save();
        }

        if(level >= (int)UserLevel.Advanced)
        {
            ImGui.Spacing();
            if (ImGui.Checkbox("Refund DD on push", ref _config.RefundFullDoubleDownOnPush)) _save();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Active: If a player has DD and got pushed, the DD bet gets pushed too.\nInactive: If a player has DD and got pushed, the DD bet is loosed.");
        } else if (_config.RefundFullDoubleDownOnPush) {
            _config.RefundFullDoubleDownOnPush = false;
            _save();
        }

        if(level >= (int)UserLevel.Advanced)
        {
            ImGui.Spacing();
            ImGui.TextUnformatted("Max Hands per Player (Splits)");
            ImGui.SameLine(300f);
            ImGui.SetNextItemWidth(200f);
            if (ImGui.InputInt("##max_hands", ref _config.MaxHandsPerPlayer, 1))
            {
                _config.MaxHandsPerPlayer = Math.Clamp(_config.MaxHandsPerPlayer, 2, 10);
                _save();
            }
        } else if (_config.MaxHandsPerPlayer != 2) {
            _config.MaxHandsPerPlayer = 2;
            _save();
        }

        ImGui.Separator();

        ImGui.TextUnformatted("UI");
        if(level >= (int)UserLevel.Advanced)
        {
            ImGui.Spacing();
            if (ImGui.Checkbox("Small Result Message", ref _config.SmallResult)) _save();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Active: Collects all results and sends a single compressed message.\nInactive: Sends individual result messages for every player hand.");
        } else if (!_config.SmallResult) {
            _config.SmallResult = true;
            _save();
        }

        if(level >= (int)UserLevel.Advanced)
        {
            ImGui.Spacing();
            ImGui.TextUnformatted("Highlight Color");
            ImGui.SameLine(300f);
            ImGui.SetNextItemWidth(200f);
            if (ImGui.ColorEdit4("##highlight_color", ref _config.HighlightColor, ImGuiColorEditFlags.NoAlpha))
            {
                _config.HighlightColor.W = 1.0f;
                _save();
            }
        }

        ImGui.Separator();

        ImGui.TextUnformatted("Multipliers");
        if(level >= (int)UserLevel.Advanced)
        {
            ImGui.Spacing();
            DrawMultiplierInput("Normal Win Multiplier", ref _config.MultiplierNormalWin);
        } else if (_config.MultiplierNormalWin != 1.0f) {
            _config.MultiplierNormalWin = 1.0f;
            _save();
        }

        ImGui.Spacing();
        DrawMultiplierInput("Natural BJ Multiplier (2 Cards)", ref _config.MultiplierBlackjackWin);

        if(level >= (int)UserLevel.Advanced)
        {
            ImGui.Spacing();
            DrawMultiplierInput("Dirty BJ Multiplier (3+ Cards)", ref _config.MultiplierDirtyBlackjackWin);
        } else if (_config.MultiplierDirtyBlackjackWin != 1.0f) {
            _config.MultiplierDirtyBlackjackWin = 1.0f;
            _save();
        }

        ImGui.Separator();

        ImGui.TextUnformatted("Bet Limits");
        ImGui.Spacing();
        ImGui.TextUnformatted("Min Bet");
        ImGui.SameLine(300f);
        ImGui.SetNextItemWidth(200f);
        if (_pendingSettingsFocus == "min_bet")
        {
            ImGui.SetKeyboardFocusHere();
            _pendingSettingsFocus = null;
        }
        if (ImGui.InputLong("##min_bet", ref _config.MinBet, 100, 1000))
        {
            _config.MinBet = Math.Clamp(_config.MinBet, 1, _config.MaxBet);
            _save();
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Max Bet");
        ImGui.SameLine(300f);
        ImGui.SetNextItemWidth(200f);
        if (_pendingSettingsFocus == "max_bet")
        {
            ImGui.SetKeyboardFocusHere();
            _pendingSettingsFocus = null;
        }
        if (ImGui.InputLong("##max_bet", ref _config.MaxBet, 1000, 10000))
        {
            _config.MaxBet = Math.Max(_config.MaxBet, _config.MinBet);
            _save();
        }

        if(level >= (int)UserLevel.Dev)
        {
            ImGui.Separator();

            ImGui.TextUnformatted("Clipboard: ");
            ImGui.SameLine();
            if (ImGui.Button("Export")) {
                var json = JsonConvert.SerializeObject(_config, Formatting.Indented);
                ImGui.SetClipboardText(json);
            }

            ImGui.SameLine();

            if (ImGui.Button("Import")) {
                try {
                    var json = ImGui.GetClipboardText();
                    var imported = JsonConvert.DeserializeObject<Configuration>(json);
                    if (imported != null) {
                        _tempImportConfig = imported;
                        ImGui.OpenPopup("import_confirm_popup");
                    }
                } catch { /* Log Error */ }
            }

            ImGui.Spacing();
            ImGui.TextUnformatted("Export and Import is in beta phase");
        }

        if (ImGui.BeginPopupModal("import_confirm_popup", ref _showImportModal, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("How do you want to import?");
            if (ImGui.Button("Full Replace (Wipe current)")) {
                DoFullReplace();
                _showImportModal = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Merge (Keep custom items)")) {
                DoMerge();
                _showImportModal = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    private void DrawMultiplierInput(string label, ref float value)
    {
        ImGui.TextUnformatted(label);
        ImGui.SameLine(300f);
        ImGui.SetNextItemWidth(200f);
        if (ImGui.InputFloat($"##input_{label}", ref value, 0.25f, 0.5f, "%.2fx"))
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

        _config.MultiplierNormalWin = _tempImportConfig.MultiplierNormalWin;
        _config.MultiplierBlackjackWin = _tempImportConfig.MultiplierBlackjackWin;
        _config.MultiplierDirtyBlackjackWin = _tempImportConfig.MultiplierDirtyBlackjackWin;
        _config.MaxHandsPerPlayer = _tempImportConfig.MaxHandsPerPlayer;
        _config.MinBet = _tempImportConfig.MinBet;
        _config.MaxBet = _tempImportConfig.MaxBet;

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

        _save();
    }
}
