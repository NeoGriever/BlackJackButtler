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
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Active: If a player has DD and got pushed, the DD bet gets pushed too.\nInactive: If a player has DD and got pushed, the DD bet is loosed.");

        ImGui.Spacing();
        if (ImGui.Checkbox("Small Result Message", ref _config.SmallResult)) _save();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Active: Collects all results and sends a single compressed message.\nInactive: Sends individual result messages for every player hand.");

        ImGui.Spacing();
        DrawMultiplierInput("Normal Win Multiplier", ref _config.MultiplierNormalWin);

        ImGui.Spacing();
        DrawMultiplierInput("Natural BJ Multiplier (2 Cards)", ref _config.MultiplierBlackjackWin);

        ImGui.Spacing();
        DrawMultiplierInput("Dirty BJ Multiplier (3+ Cards)", ref _config.MultiplierDirtyBlackjackWin);

        ImGui.Spacing();
        ImGui.TextUnformatted("Max Hands per Player (Splits)");
        ImGui.SameLine(300f);
        ImGui.SetNextItemWidth(200f);
        if (ImGui.InputInt("##max_hands", ref _config.MaxHandsPerPlayer, 1))
        {
            _config.MaxHandsPerPlayer = Math.Clamp(_config.MaxHandsPerPlayer, 2, 10);
            _save();
        }

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
