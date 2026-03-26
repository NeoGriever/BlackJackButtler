using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawPresetsPage()
    {
        ImGui.TextUnformatted("Presets");
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button("+ Create Preset"))
        {
            var obj = JObject.FromObject(_config);
            obj.Remove("Presets");
            _config.Presets.Add(new PresetEntry { SnapshotJson = obj.ToString(Formatting.None) });
            _save();
        }

        ImGui.Spacing();

        for (int i = _config.Presets.Count - 1; i >= 0; i--)
        {
            var preset = _config.Presets[i];

            bool expanded = ImGui.TreeNodeEx($"##pn_{i}", ImGuiTreeNodeFlags.AllowItemOverlap, preset.Name);

            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0.15f, 0.55f, 0.15f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.20f, 0.70f, 0.20f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive,  new Vector4(0.10f, 0.40f, 0.10f, 1f));
            if (ImGui.SmallButton($"▶ Apply##apply_{i}"))
            {
                ApplyPreset(preset);
            }
            ImGui.PopStyleColor(3);

            ImGui.SameLine();
            bool ctrl = ImGui.GetIO().KeyCtrl;
            ImGui.BeginDisabled(!ctrl);
            if (ImGui.SmallButton($"X##del_{i}") && ctrl)
            {
                if (_config.ActivePresetName == preset.Name)
                    _config.ActivePresetName = null;
                _config.Presets.RemoveAt(i);
                _save();
            }
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && !ctrl)
                ImGui.SetTooltip("Hold CTRL to delete");

            if (expanded)
            {
                ImGui.Indent();

                // Name input
                var nameBuf = preset.Name;
                ImGui.SetNextItemWidth(300);
                if (ImGui.InputText($"Name##pname_{i}", ref nameBuf, 128))
                {
                    preset.Name = nameBuf;
                    _save();
                }

                ImGui.Spacing();
                ImGui.TextDisabled("Apply categories:");

                bool applySettings = preset.ApplySettings;
                if (ImGui.Checkbox($"Settings##pset_{i}", ref applySettings))
                { preset.ApplySettings = applySettings; _save(); }

                ImGui.SameLine();
                bool applyCommands = preset.ApplyCommands;
                if (ImGui.Checkbox($"Commands & Own Buttons##pcmd_{i}", ref applyCommands))
                { preset.ApplyCommands = applyCommands; _save(); }

                bool applyMessages = preset.ApplyMessages;
                if (ImGui.Checkbox($"Messages##pmsg_{i}", ref applyMessages))
                { preset.ApplyMessages = applyMessages; _save(); }

                ImGui.SameLine();
                bool applyRegexes = preset.ApplyRegexes;
                if (ImGui.Checkbox($"Regexes##prx_{i}", ref applyRegexes))
                { preset.ApplyRegexes = applyRegexes; _save(); }

                ImGui.SameLine();
                bool applyWebhooks = preset.ApplyWebhooks;
                if (ImGui.Checkbox($"Webhooks##pwh_{i}", ref applyWebhooks))
                { preset.ApplyWebhooks = applyWebhooks; _save(); }

                ImGui.Spacing();

                if (ImGui.Button($"Update Snapshot##psnap_{i}"))
                {
                    var snapObj = JObject.FromObject(_config);
                    snapObj.Remove("Presets");
                    preset.SnapshotJson = snapObj.ToString(Formatting.None);
                    _save();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Overwrite this preset's snapshot with the current settings.");

                ImGui.Unindent();
                ImGui.TreePop();
            }

            ImGui.Separator();
        }
    }

    private void ApplyPreset(PresetEntry preset)
    {
        var snap = JsonConvert.DeserializeObject<Configuration>(preset.SnapshotJson);
        if (snap == null) return;

        if (preset.ApplySettings)
        {
            _config.FirstDealThenPlay                  = snap.FirstDealThenPlay;
            _config.IdenticalSplitOnly                 = snap.IdenticalSplitOnly;
            _config.AllowDoubleDownAfterSplit           = snap.AllowDoubleDownAfterSplit;
            _config.MaxHandsPerPlayer                  = snap.MaxHandsPerPlayer;
            _config.MultiplierNormalWin                = snap.MultiplierNormalWin;
            _config.MultiplierBlackjackWin             = snap.MultiplierBlackjackWin;
            _config.MultiplierDirtyBlackjackWin        = snap.MultiplierDirtyBlackjackWin;
            _config.RefundFullDoubleDownOnPush         = snap.RefundFullDoubleDownOnPush;
            _config.BlackjackTieRule                    = snap.BlackjackTieRule;
            _config.EnableCharlie                      = snap.EnableCharlie;
            _config.CharlieCardCount                   = snap.CharlieCardCount;
            _config.EnableBankInput                    = snap.EnableBankInput;
            _config.CommandSpeedMultiplier             = snap.CommandSpeedMultiplier;
            _config.OpenDropboxInsteadOfTrade          = snap.OpenDropboxInsteadOfTrade;
            _config.MinBet                             = snap.MinBet;
            _config.MaxBet                             = snap.MaxBet;
            _config.ShortBetFormat                     = snap.ShortBetFormat;
            _config.AutoInitialDeal                    = snap.AutoInitialDeal;
            _config.AutoDealerDraw                     = snap.AutoDealerDraw;
            _config.AutoRun                            = snap.AutoRun;
            _config.DealerDrawsUntil                   = snap.DealerDrawsUntil;
            _config.SmallResult                        = snap.SmallResult;
            _config.AutostartRoundOnlyOnMultiplePlayers = snap.AutostartRoundOnlyOnMultiplePlayers;
            _config.HighlightColor                     = snap.HighlightColor;
            _config.HighlightTextColor                 = snap.HighlightTextColor;
            _config.ButtonColor                        = snap.ButtonColor;
            _config.ButtonTextColor                    = snap.ButtonTextColor;
            _config.NearbyAlertEnabled                 = snap.NearbyAlertEnabled;
            _config.NearbyAlertSoundFiles              = snap.NearbyAlertSoundFiles;
            _config.NearbyAlertVolume                  = snap.NearbyAlertVolume;
            _config.NearbyAlertCooldown                = snap.NearbyAlertCooldown;
            _config.VipBetTiers                        = snap.VipBetTiers;
            _config.NearbyAlwaysShowCircle              = snap.NearbyAlwaysShowCircle;
            _config.AutoContinue                       = snap.AutoContinue;
            _config.AutoContinueDelay                  = snap.AutoContinueDelay;
        }

        if (preset.ApplyCommands)
        {
            _config.CommandGroups       = snap.CommandGroups;
            _config.CustomCommandGroups = snap.CustomCommandGroups;
        }

        if (preset.ApplyMessages) _config.MessageBatches = snap.MessageBatches;
        if (preset.ApplyRegexes)  _config.UserRegexes    = snap.UserRegexes;
        if (preset.ApplyWebhooks) _config.Webhooks       = snap.Webhooks;

        _config.ActivePresetName = preset.Name;
        _save();           // wrapper setzt _presetDirty = true
        _presetDirty = false;  // sofort zurücksetzen
    }
}
