using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private static readonly string[] SettingsFields =
    {
        "FirstDealThenPlay", "IdenticalSplitOnly", "AllowDoubleDownAfterSplit",
        "MaxHandsPerPlayer", "MultiplierNormalWin", "MultiplierBlackjackWin",
        "MultiplierDirtyBlackjackWin", "RefundFullDoubleDownOnPush", "BlackjackTieRule",
        "EnableCharlie", "CharlieCardCount", "EnableBankInput", "CommandSpeedMultiplier",
        "OpenDropboxInsteadOfTrade", "MinBet", "MaxBet", "ShortBetFormat",
        "AutoInitialDeal", "AutoDealerDraw", "AutoRun", "DealerDrawsUntil",
        "SmallResult", "AutostartRoundOnlyOnMultiplePlayers", "HighlightColor",
        "HighlightTextColor", "ButtonColor", "ButtonTextColor", "NearbyAlertEnabled",
        "NearbyAlertSoundFiles", "NearbyAlertVolume", "NearbyAlertCooldown",
        "VipBetTiers", "NearbyAlwaysShowCircle", "AutoContinue", "AutoContinueDelay",
    };

    private static readonly string[] CommandFields = { "CommandGroups", "CustomCommandGroups" };
    private static readonly string[] MessageFields = { "MessageBatches" };
    private static readonly string[] RegexFields = { "UserRegexes" };
    private static readonly string[] WebhookFields = { "Webhooks" };

    private void DrawPresetsPage()
    {
        if (_openPresetImportConfirm)
        {
            _showPresetImportModal = true;
            ImGui.OpenPopup("preset_import_confirm");
            _openPresetImportConfirm = false;
        }

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

        ImGui.SameLine();
        if (ImGui.Button("Import Preset"))
        {
            _fileDialogManager.OpenFileDialog(
                "Import Preset JSON", "JSON Files{.json}",
                (ok, path) =>
                {
                    if (!ok || string.IsNullOrWhiteSpace(path)) return;
                    try
                    {
                        var json = System.IO.File.ReadAllText(path);
                        var arr = JArray.Parse(json);
                        if (arr.Count == 0) return;

                        JObject? cumulative = null;

                        for (int j = 0; j < arr.Count; j++)
                        {
                            var entry = (JObject)arr[j];
                            var snapshot = (JObject)entry["Snapshot"]!;
                            bool isDelta = entry["IsDelta"]?.Value<bool>() ?? false;

                            if (j == 0 || !isDelta)
                                cumulative = (JObject)snapshot.DeepClone();
                            else
                                ApplyDelta(cumulative!, snapshot);

                            _config.Presets.Add(new PresetEntry
                            {
                                Name = entry["Name"]?.Value<string>() ?? "Imported Preset",
                                ApplySettings = entry["ApplySettings"]?.Value<bool>() ?? true,
                                ApplyCommands = entry["ApplyCommands"]?.Value<bool>() ?? true,
                                ApplyMessages = entry["ApplyMessages"]?.Value<bool>() ?? true,
                                ApplyRegexes = entry["ApplyRegexes"]?.Value<bool>() ?? true,
                                ApplyWebhooks = entry["ApplyWebhooks"]?.Value<bool>() ?? true,
                                SnapshotJson = ((JObject)cumulative!.DeepClone()).ToString(Formatting.None),
                            });
                        }
                        _save();
                    }
                    catch { }
                });
        }

        if (_config.Presets.Count > 0)
        {
            ImGui.SameLine();
            if (ImGui.Button("Export All"))
                ExportAllPresets();
        }

        ImGui.Spacing();

        for (int i = _config.Presets.Count - 1; i >= 0; i--)
        {
            var preset = _config.Presets[i];
            bool isActive = preset.Name == _config.ActivePresetName;

            if (isActive)
            {
                ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.05f, 0.25f, 0.05f, 1f));
                ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.08f, 0.35f, 0.08f, 1f));
                ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.04f, 0.20f, 0.04f, 1f));
            }

            bool expanded = ImGui.TreeNodeEx($"##pn_{i}", ImGuiTreeNodeFlags.AllowItemOverlap, preset.Name);

            if (isActive)
                ImGui.PopStyleColor(3);

            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.15f, 0.55f, 0.15f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.20f, 0.70f, 0.20f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.10f, 0.40f, 0.10f, 1f));
            if (ImGui.SmallButton($"Apply##apply_{i}"))
                ApplyPreset(preset);
            ImGui.PopStyleColor(3);

            ImGui.SameLine();
            bool ctrl = ImGui.GetIO().KeyCtrl;
            ImGui.BeginDisabled(!ctrl);
            if (ImGui.SmallButton($"X##del_{i}") && ctrl)
            {
                if (_config.ActivePresetName == preset.Name)
                {
                    _config.ActivePresetName = null;
                    _presetChangeCount = 0;
                }
                _config.Presets.RemoveAt(i);
                _save();
            }
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && !ctrl)
                ImGui.SetTooltip("Hold CTRL to delete");

            if (isActive && _presetChangeCount > 0)
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), $"{_presetChangeCount} Changes");
            }

            ImGui.SameLine();
            ImGui.PushFont(UiBuilder.IconFont);
            if (BJBGui.SmallButton(FontAwesomeIcon.FileExport.ToIconString() + $"##export_{i}"))
                ExportSinglePreset(preset);
            ImGui.PopFont();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Export preset to file");

            ImGui.SameLine();
            ImGui.PushFont(UiBuilder.IconFont);
            if (BJBGui.SmallButton(FontAwesomeIcon.FileImport.ToIconString() + $"##importonto_{i}"))
            {
                int targetIdx = i;
                _fileDialogManager.OpenFileDialog(
                    "Import Preset", "JSON Files{.json}",
                    (ok, path) =>
                    {
                        if (!ok || string.IsNullOrWhiteSpace(path)) return;
                        try
                        {
                            _presetImportJson = System.IO.File.ReadAllText(path);
                            _presetImportTargetIndex = targetIdx;
                            _openPresetImportConfirm = true;
                        }
                        catch { }
                    });
            }
            ImGui.PopFont();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Import preset from file (overwrites snapshot)");

            if (expanded)
            {
                ImGui.Indent();

                var nameBuf = preset.Name;
                ImGui.SetNextItemWidth(300);
                if (ImGui.InputText($"Name##pname_{i}", ref nameBuf, 128))
                {
                    if (isActive)
                        _config.ActivePresetName = nameBuf;
                    preset.Name = nameBuf;
                    _save();
                }

                ImGui.Spacing();

                bool applySettings = preset.ApplySettings;
                if (ImGui.Checkbox($"##pset_{i}", ref applySettings))
                { preset.ApplySettings = applySettings; _save(); }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Settings");

                ImGui.SameLine();
                bool applyCommands = preset.ApplyCommands;
                if (ImGui.Checkbox($"##pcmd_{i}", ref applyCommands))
                { preset.ApplyCommands = applyCommands; _save(); }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Commands & Own Buttons");

                ImGui.SameLine();
                bool applyMessages = preset.ApplyMessages;
                if (ImGui.Checkbox($"##pmsg_{i}", ref applyMessages))
                { preset.ApplyMessages = applyMessages; _save(); }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Messages");

                ImGui.SameLine();
                bool applyRegexes = preset.ApplyRegexes;
                if (ImGui.Checkbox($"##prx_{i}", ref applyRegexes))
                { preset.ApplyRegexes = applyRegexes; _save(); }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Regexes");

                ImGui.SameLine();
                bool applyWebhooks = preset.ApplyWebhooks;
                if (ImGui.Checkbox($"##pwh_{i}", ref applyWebhooks))
                { preset.ApplyWebhooks = applyWebhooks; _save(); }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Webhooks");

                ImGui.Spacing();

                if (!isActive || _presetChangeCount > 0)
                {
                    if (ImGui.Button($"Update Snapshot##psnap_{i}"))
                    {
                        var snapObj = JObject.FromObject(_config);
                        snapObj.Remove("Presets");
                        preset.SnapshotJson = snapObj.ToString(Formatting.None);
                        _save();
                        if (isActive)
                        {
                            _presetChangeCount = 0;
                            _presetDirty = false;
                        }
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Overwrite this preset's snapshot with the current settings.");
                }

                ImGui.Unindent();
                ImGui.TreePop();
            }

            ImGui.Separator();
        }

        if (ImGui.BeginPopupModal("preset_import_confirm", ref _showPresetImportModal, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextUnformatted("Overwrite this preset's snapshot with the imported data?");
            ImGui.Spacing();
            if (BJBGui.Button("Yes##preset_import_yes"))
            {
                ApplyPresetImport();
                _showPresetImportModal = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (BJBGui.Button("No##preset_import_no"))
            {
                _presetImportJson = null;
                _presetImportTargetIndex = null;
                _showPresetImportModal = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
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
        _save();
        _presetChangeCount = 0;
        _presetDirty = false;
    }

    private void RecomputePresetChangeCount()
    {
        _presetChangeCount = 0;
        var preset = _config.Presets.FirstOrDefault(p => p.Name == _config.ActivePresetName);
        if (preset == null) return;

        JObject current, snapshot;
        try
        {
            current = JObject.FromObject(_config);
            current.Remove("Presets");
            current.Remove("ActivePresetName");
            snapshot = JObject.Parse(preset.SnapshotJson);
            snapshot.Remove("ActivePresetName");
        }
        catch { return; }

        int count = 0;
        if (preset.ApplySettings)  count += CountDiffs(current, snapshot, SettingsFields);
        if (preset.ApplyCommands)  count += CountDiffs(current, snapshot, CommandFields);
        if (preset.ApplyMessages)  count += CountDiffs(current, snapshot, MessageFields);
        if (preset.ApplyRegexes)   count += CountDiffs(current, snapshot, RegexFields);
        if (preset.ApplyWebhooks)  count += CountDiffs(current, snapshot, WebhookFields);
        _presetChangeCount = count;
    }

    private static int CountDiffs(JObject current, JObject snapshot, string[] fields)
    {
        int count = 0;
        foreach (var field in fields)
        {
            var a = current[field];
            var b = snapshot[field];
            if (a == null && b == null) continue;
            if (a == null || b == null || !JToken.DeepEquals(a, b))
                count++;
        }
        return count;
    }

    private void ExportSinglePreset(PresetEntry preset)
    {
        var exportArr = new JArray
        {
            new JObject
            {
                ["Name"] = preset.Name,
                ["ApplySettings"] = preset.ApplySettings,
                ["ApplyCommands"] = preset.ApplyCommands,
                ["ApplyMessages"] = preset.ApplyMessages,
                ["ApplyRegexes"] = preset.ApplyRegexes,
                ["ApplyWebhooks"] = preset.ApplyWebhooks,
                ["Snapshot"] = JObject.Parse(preset.SnapshotJson),
                ["IsDelta"] = false,
            }
        };
        var json = exportArr.ToString(Formatting.Indented);
        _fileDialogManager.SaveFileDialog(
            "Export Preset", "JSON Files{.json}", $"bjb_preset_{preset.Name}", ".json",
            (ok, path) =>
            {
                if (ok && !string.IsNullOrWhiteSpace(path))
                    System.IO.File.WriteAllText(path, json);
            });
    }

    private void ExportAllPresets()
    {
        var exportArr = new JArray();
        JObject? cumulative = null;

        for (int j = 0; j < _config.Presets.Count; j++)
        {
            var p = _config.Presets[j];
            JObject snapshot;
            try { snapshot = JObject.Parse(p.SnapshotJson); }
            catch { snapshot = new JObject(); }

            var entry = new JObject
            {
                ["Name"] = p.Name,
                ["ApplySettings"] = p.ApplySettings,
                ["ApplyCommands"] = p.ApplyCommands,
                ["ApplyMessages"] = p.ApplyMessages,
                ["ApplyRegexes"] = p.ApplyRegexes,
                ["ApplyWebhooks"] = p.ApplyWebhooks,
            };

            if (cumulative == null)
            {
                entry["Snapshot"] = snapshot;
                entry["IsDelta"] = false;
                cumulative = (JObject)snapshot.DeepClone();
            }
            else
            {
                var delta = new JObject();
                foreach (var prop in snapshot.Properties())
                {
                    var existing = cumulative[prop.Name];
                    if (existing == null || !JToken.DeepEquals(existing, prop.Value))
                        delta[prop.Name] = prop.Value.DeepClone();
                }
                foreach (var prop in cumulative.Properties().ToList())
                {
                    if (snapshot[prop.Name] == null)
                        delta[prop.Name] = JValue.CreateNull();
                }
                entry["Snapshot"] = delta;
                entry["IsDelta"] = true;

                foreach (var prop in snapshot.Properties())
                    cumulative[prop.Name] = prop.Value.DeepClone();
            }

            exportArr.Add(entry);
        }

        var json = exportArr.ToString(Formatting.Indented);
        _fileDialogManager.SaveFileDialog(
            "Export All Presets", "JSON Files{.json}", "bjb_presets_all", ".json",
            (ok, path) =>
            {
                if (ok && !string.IsNullOrWhiteSpace(path))
                    System.IO.File.WriteAllText(path, json);
            });
    }

    private void ApplyPresetImport()
    {
        if (_presetImportJson == null || _presetImportTargetIndex == null) return;
        if (_presetImportTargetIndex.Value < 0 || _presetImportTargetIndex.Value >= _config.Presets.Count) return;

        try
        {
            var arr = JArray.Parse(_presetImportJson);
            if (arr.Count == 0) return;

            JObject? cumulative = null;

            for (int j = 0; j < arr.Count; j++)
            {
                var entry = (JObject)arr[j];
                var snapshot = (JObject)entry["Snapshot"]!;
                bool isDelta = entry["IsDelta"]?.Value<bool>() ?? false;

                if (j == 0 || !isDelta)
                    cumulative = (JObject)snapshot.DeepClone();
                else
                    ApplyDelta(cumulative!, snapshot);
            }

            var preset = _config.Presets[_presetImportTargetIndex.Value];
            var lastEntry = (JObject)arr[arr.Count - 1];

            if (arr.Count == 1)
            {
                if (lastEntry.ContainsKey("Name")) preset.Name = lastEntry["Name"]!.Value<string>() ?? preset.Name;
                if (lastEntry.ContainsKey("ApplySettings")) preset.ApplySettings = lastEntry["ApplySettings"]!.Value<bool>();
                if (lastEntry.ContainsKey("ApplyCommands")) preset.ApplyCommands = lastEntry["ApplyCommands"]!.Value<bool>();
                if (lastEntry.ContainsKey("ApplyMessages")) preset.ApplyMessages = lastEntry["ApplyMessages"]!.Value<bool>();
                if (lastEntry.ContainsKey("ApplyRegexes")) preset.ApplyRegexes = lastEntry["ApplyRegexes"]!.Value<bool>();
                if (lastEntry.ContainsKey("ApplyWebhooks")) preset.ApplyWebhooks = lastEntry["ApplyWebhooks"]!.Value<bool>();
            }

            preset.SnapshotJson = cumulative!.ToString(Formatting.None);
            _save();
        }
        catch { }
        finally
        {
            _presetImportJson = null;
            _presetImportTargetIndex = null;
        }
    }

    private static void ApplyDelta(JObject cumulative, JObject delta)
    {
        foreach (var prop in delta.Properties())
        {
            if (prop.Value.Type == JTokenType.Null)
                cumulative.Remove(prop.Name);
            else
                cumulative[prop.Name] = prop.Value.DeepClone();
        }
    }
}
