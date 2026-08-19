using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using BlackJackButtler.Chat;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    // Granulare Settings-Felder nach Kategorie
    private static readonly string[] SettingsGeneralFields = {
        "EnableBankInput", "CommandSpeedMultiplier", "WageIntervalMode",
        "SmallResult", "ResultTemplate", "ShortResultRules", "AutostartRoundOnlyOnMultiplePlayers",
        "MainViewV2SuperCompact",
    };
    private static readonly string[] SettingsAutomationFields = {
        "EnableAutomation", "ShowAutoDealerDrawButton", "ShowAutoPlayerHandButton",
        "ShowAutoContinueButton", "ShowAutoRunButton",
        "AutoInitialDeal", "AutoDealerDraw", "AutoRun", "AutoActivateTradingPlayers", "AutoContinue", "AutoContinueDelay", "AutoContinueMinimumPlayers",
    };
    private static readonly string[] SettingsRulesFields = {
        "FirstDealThenPlay", "PlayerRollingForThemselves", "IdenticalSplitOnly", "AllowDoubleDownAfterSplit",
        "AllowTripleDownAfterSplit", "LimitTripleDownToMaxPoints", "TripleDownMaxPoints",
        "EnableSplit", "EnableDoubleDown", "EnableTripleDown", "EnableDirtyBlackjack",
        "MaxHandsPerPlayer", "MultiplierNormalWin", "MultiplierBlackjackWin",
        "MultiplierDirtyBlackjackWin", "MultiplierCharlieWin", "MultiplierSplitWin",
        "MultiplierDoubleDownWin", "MultiplierTripleDownWin", "RefundFullDoubleDownOnPush",
        "RefundFullTripleDownOnPush", "BlackjackTieRule",
        "EnableCharlie", "CharlieCardCount", "CharlieInstantWin",
        "DealerDrawsUntil", "DealerSoftRule",
    };
    private static readonly string[] SettingsBettingFields = {
        "MinBet", "MaxBet", "ShortBetFormat", "VipBetTiers", "BetLimitEntries", "BetLimitEntriesMigrated", "BettingPresets",
    };
    private static readonly string[] SettingsTimeDelayFields = {
        "RecallUnlockSeconds", "DelaySecondSnapping", "UtcOffsetHours", "UtcOffsetMinutes", "UtcTimeZoneName", "UtcSummerTime",
    };
    private static readonly string[] SettingsMessageSettingsFields = {
        "EnableAntiDouble",
    };
    private static readonly string[] SettingsNearbyPlayersFields = {
        "NearbyAlertEnabled", "NearbyAlertSoundFiles", "NearbyAlertSoundEntries", "NearbyAlertSoundEntriesMigrated", "NearbyAlertVolume", "NearbyAlertCooldown",
        "NearbyAlertSoundMode", "NearbyAlwaysShowCircle", "NearbyQuestionCommandName",
        "NearbyShowFootNumbers", "NearbyOffsetX", "NearbyOffsetZ", "NearbyShape",
        "NearbyRectangleAspectRatio", "NearbyRectangleRotation", "NearbyUseFixedPosition",
        "NearbyFixedCenterX", "NearbyFixedCenterY", "NearbyFixedCenterZ", "NearbyFixedCenterCaptured",
        "NearbyAutoActEnabled", "NearbyAutoActCommandName", "NearbyAutoActTimeoutMinutes",
        "NearbyAutoActIgnoreList", "NearbyDistanceCap", "NearbyColumns",
    };
    private static readonly string[] SettingsVisualFields = {
        "HighlightColor", "HighlightTextColor", "ButtonColor", "ButtonTextColor",
        "HideCardSuits", "SelectedFontName",
        "DrawLogicScale", "DrawLogicOffsetX", "DrawLogicOffsetY", "DrawLogicOffsetZ", "DrawLogicOffsetR",
        "DrawLogicColorSpades", "DrawLogicColorClubs", "DrawLogicColorHearts", "DrawLogicColorDiamonds",
        "CustomButtonPaddingH", "CustomButtonPaddingV", "CustomButtonFontScale", "CustomButtonUseMono",
        "ButtonBarLayout", "ButtonBarFixedWidth", "ButtonBarFixedWidthValue",
        "GeneralButtonDefaultStyle", "GeneralButtonActiveStyle", "GeneralButtonHighlightStyle",
        "CustomButtonDefaultStyle",
    };
    private static readonly string[] SettingsSystemFields = {
        "EnableCompanionSync", "CompanionServerAddress", "CompanionTimeoutMs",
        "DisableUpdatePopup", "HashedStats", "AllianceNearbyCommandName",
    };
    private static readonly string[] DrawLogicFields = {
        "DrawLogicEntries", "DrawLogicStartEntry",
    };

    private static readonly string[] SettingsFields = SettingsGeneralFields
        .Concat(SettingsAutomationFields).Concat(SettingsRulesFields).Concat(SettingsBettingFields)
        .Concat(SettingsTimeDelayFields).Concat(SettingsMessageSettingsFields)
        .Concat(SettingsNearbyPlayersFields).Concat(SettingsVisualFields).Concat(SettingsSystemFields)
        .ToArray();

    private static readonly string[] StandardCommandFields = { "CommandGroups" };
    private static readonly string[] OwnButtonFields = { "CustomCommandGroups", "CustomButtonEntries", "CustomButtonEntriesMigrated", "CustomButtonOrder" };
    private static readonly string[] MessageFields = { "MessageBatches" };
    private static readonly string[] RegexFields = { "UserRegexes" };

    // Regexes für Preview-Simulation
    private static readonly System.Text.RegularExpressions.Regex _pvSeRegex =
        new(
            @"<se\.[^>]*>",
            System.Text.RegularExpressions.RegexOptions.Compiled |
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    private static readonly System.Text.RegularExpressions.Regex _pvBatchRegex =
        new(@"#\{([^}]+)\}", System.Text.RegularExpressions.RegexOptions.Compiled);

    // Preset UI State
    private string?  _presetPendingUpdateId;
    private DateTime _presetUpdateConfirmOpenedAt;
    private string?  _presetPendingApplyId;
    private string?  _presetPendingDuplicateId;
    private bool     _presetMigrationPending;
    private string?  _presetMigrationBackupPath;
    private string?  _presetMigrationStatus;
    private readonly HashSet<string>           _presetPreviewOpen  = new();
    private readonly Dictionary<string, string> _presetPreviewCache = new();
    private readonly List<string> _presetPreviewStack = new();
    private bool _showPresetAssignmentRuleOptions;
    // Deferred popup flags (OpenPopup und BeginPopupModal müssen im selben Fensterkontext sein)
    private bool _triggerApplyPopup;
    private bool _triggerUpdatePopup;
    private bool _triggerDupPopup;

    // ─── DrawPresetsPage ───────────────────────────────────────────────────────

    private void DrawPresetsPage()
    {
        _presetNavHoverPage = null;

        // Migration-Trigger
        if (_config.Presets.Count > 0 && !_config.PresetsMigrated && !_presetMigrationPending)
            _presetMigrationPending = true;
        if (_presetMigrationPending)
            ImGui.OpenPopup("preset_migration");
        DrawPresetMigrationPopup();

        // Import-Confirm öffnen
        if (_openPresetImportConfirm)
        {
            _showPresetImportModal = true;
            ImGui.OpenPopup("preset_import_confirm");
            _openPresetImportConfirm = false;
        }

        // ── Popup: Import-Confirm (vor BeginChild, da dort geöffnet) ─────────────
        if (ImGui.BeginPopupModal("preset_import_confirm", ref _showPresetImportModal,
            ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextUnformatted("Overwrite this preset's snapshot with the imported data?");
            ImGui.Spacing();
            if (BJBGui.Button("Yes##pi_y")) { ApplyPresetImport(); _showPresetImportModal = false; ImGui.CloseCurrentPopup(); }
            ImGui.SameLine();
            if (BJBGui.Button("No##pi_n"))
            { _presetImportJson = null; _presetImportTargetIndex = null; _showPresetImportModal = false; ImGui.CloseCurrentPopup(); }
            ImGui.EndPopup();
        }

        // ── Kopfzeile ─────────────────────────────────────────────────────────
        ImGui.TextUnformatted("Presets");
        ImGui.SameLine();
        ImGui.TextDisabled($"({_config.Presets.Count})");
        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button("+ Create"))
        {
            var obj = JObject.FromObject(_config);
            obj.Remove("Presets");
            var now2 = DateTime.UtcNow;
            int ns = _config.Presets.Count > 0 ? _config.Presets.Max(p => p.SortOrder) + 1 : 0;
            _config.Presets.Add(new PresetEntry
            {
                PresetId = Guid.NewGuid().ToString("N"),
                CommandsCheckboxMigrated = true,
                SettingsCategoryMigrated = true,
                MessagesCategoryMigrated = true,
                CreatedAt = now2, UpdatedAt = now2,
                SortOrder = ns,
                SnapshotJson = obj.ToString(Formatting.None),
            });
            if (!_config.PresetsMigrated) _config.PresetsMigrated = true;
            PresetStorage.Save(_config.Presets);
            _save();
        }
        ImGui.SameLine();
        if (ImGui.Button("Import"))
        {
            _fileDialogManager.OpenFileDialog("Import Preset JSON", "JSON Files{.json}",
                (ok, path) =>
                {
                    if (!ok || string.IsNullOrWhiteSpace(path)) return;
                    try
                    {
                        var json = System.IO.File.ReadAllText(path);
                        var arr  = JArray.Parse(json);
                        if (arr.Count == 0) return;
                        JObject? cum = null;
                        var now3 = DateTime.UtcNow;
                        int ns2 = _config.Presets.Count > 0 ? _config.Presets.Max(p => p.SortOrder) + 1 : 0;
                        for (int j = 0; j < arr.Count; j++)
                        {
                            var e = (JObject)arr[j];
                            var sn = (JObject)e["Snapshot"]!;
                            bool isDelta = e["IsDelta"]?.Value<bool>() ?? false;
                            if (j == 0 || !isDelta) cum = (JObject)sn.DeepClone();
                            else ApplyDelta(cum!, sn);
                            var impId = e["PresetId"]?.Value<string>();
                            if (string.IsNullOrEmpty(impId) || _config.Presets.Any(pp => pp.PresetId == impId))
                                impId = Guid.NewGuid().ToString("N");
                            var lCmd = e["ApplyCommands"]?.Value<bool>() ?? true;
                            var lSet = e["ApplySettings"]?.Value<bool>() ?? true;
                            var lMsg = e["ApplyMessages"]?.Value<bool>() ?? true;
                            _config.Presets.Add(new PresetEntry
                            {
                                Name = e["Name"]?.Value<string>() ?? "Imported",
                                PresetId = impId,
                                ApplyRegexes = e["ApplyRegexes"]?.Value<bool>() ?? true,
                                ApplyMessagesDefault = e["ApplyMessagesDefault"]?.Value<bool>() ?? lMsg,
                                ApplyMessagesCustom  = e["ApplyMessagesCustom"]?.Value<bool>()  ?? lMsg,
                                ApplyStandardCommands = e["ApplyStandardCommands"]?.Value<bool>() ?? lCmd,
                                ApplyOwnButtons = e["ApplyOwnButtons"]?.Value<bool>() ?? lCmd,
                                ApplySettingsGeneral = e["ApplySettingsGeneral"]?.Value<bool>() ?? lSet,
                                ApplySettingsAutomation = e["ApplySettingsAutomation"]?.Value<bool>() ?? lSet,
                                ApplySettingsRules = e["ApplySettingsRules"]?.Value<bool>() ?? lSet,
                                ApplySettingsBetting = e["ApplySettingsBetting"]?.Value<bool>() ?? lSet,
                                ApplySettingsTimeDelay = e["ApplySettingsTimeDelay"]?.Value<bool>() ?? lSet,
                                ApplySettingsMessageSettings = e["ApplySettingsMessageSettings"]?.Value<bool>() ?? lSet,
                                ApplySettingsNearbyPlayers = e["ApplySettingsNearbyPlayers"]?.Value<bool>() ?? lSet,
                                ApplySettingsVisual = e["ApplySettingsVisual"]?.Value<bool>() ?? lSet,
                                ApplySettingsSystem = e["ApplySettingsSystem"]?.Value<bool>() ?? false,
                                ApplyDrawLogic = e["ApplyDrawLogic"]?.Value<bool>() ?? false,
                                CommandsCheckboxMigrated = true, SettingsCategoryMigrated = true, MessagesCategoryMigrated = true,
                                CreatedAt = e["CreatedAt"]?.Value<DateTime>() ?? now3,
                                UpdatedAt = now3,
                                SortOrder = ns2 + j,
                                SnapshotJson = ((JObject)cum!.DeepClone()).ToString(Formatting.None),
                            });
                        }
                        PresetStorage.Save(_config.Presets);
                        _save();
                    }
                    catch { }
                });
        }
        if (_config.Presets.Count > 0)
        {
            ImGui.SameLine();
            if (ImGui.Button("Export All")) ExportAllPresets();
        }

        ImGui.Spacing();
        ImGui.Checkbox("Show Assigment Rule Options", ref _showPresetAssignmentRuleOptions);
        ImGui.Spacing();

        if (ImGui.BeginTable("##preset_layout", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingStretchProp))
        {
        ImGui.TableSetupColumn("Presets", ImGuiTableColumnFlags.WidthStretch, 0.58f);
        ImGui.TableSetupColumn("Preview", ImGuiTableColumnFlags.WidthStretch, 0.42f);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        // ── Preset-Liste ──────────────────────────────────────────────────────
        ImGui.BeginChild("##preset_scroll", new Vector2(0, ImGui.GetContentRegionAvail().Y), false);

        var sorted = _config.Presets.OrderBy(p => p.SortOrder).ToList();

        // Normalize SortOrders (fix duplicates / gaps)
        bool needsNorm = false;
        for (int ni = 0; ni < sorted.Count; ni++)
            if (sorted[ni].SortOrder != ni) { needsNorm = true; break; }
        if (needsNorm)
        {
            for (int ni = 0; ni < sorted.Count; ni++) sorted[ni].SortOrder = ni;
            PresetStorage.Save(_config.Presets);
            _save();
        }

        for (int si = 0; si < sorted.Count; si++)
        {
            var preset = sorted[si];
            bool isActive = !string.IsNullOrEmpty(preset.PresetId)
                            && preset.PresetId == _config.ActivePresetId;

            ImGui.PushID($"p_{preset.PresetId}");

            // Sortier-Pfeile
            if (si == 0) ImGui.BeginDisabled();
            if (ImGui.ArrowButton("##up", ImGuiDir.Up))
            {
                sorted[si].SortOrder = si - 1;
                sorted[si - 1].SortOrder = si;
                PresetStorage.Save(_config.Presets);
                _save();
            }
            if (si == 0) ImGui.EndDisabled();
            ImGui.SameLine(0, 2);
            if (si >= sorted.Count - 1) ImGui.BeginDisabled();
            if (ImGui.ArrowButton("##dn", ImGuiDir.Down))
            {
                sorted[si].SortOrder = si + 1;
                sorted[si + 1].SortOrder = si;
                PresetStorage.Save(_config.Presets);
                _save();
            }
            if (si >= sorted.Count - 1) ImGui.EndDisabled();
            ImGui.SameLine(0, 4);

            // Use-Button (Apply mit Sicherheitsabfrage)
            ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0.10f, 0.38f, 0.10f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.15f, 0.52f, 0.15f, 1f));
            if (ImGui.Button("Use##use", new Vector2(38f, 0)))
            {
                _presetPendingApplyId = preset.PresetId;
                _triggerApplyPopup = true;
            }
            ImGui.PopStyleColor(2);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Apply this preset");
            ImGui.SameLine(0, 5);

            bool inPreview = _presetPreviewStack.Contains(preset.PresetId);
            if (ImGui.Button(inPreview ? "<##preview" : ">##preview", new Vector2(24f, 0)))
            {
                if (inPreview)
                    _presetPreviewStack.Remove(preset.PresetId);
                else
                    _presetPreviewStack.Add(preset.PresetId);
                _presetPreviewCache.Clear();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(inPreview ? "Remove from Preview" : "Use for Preview");
            if (inPreview)
            {
                ImGui.SameLine(0, 3);
                ImGui.TextDisabled($"{_presetPreviewStack.IndexOf(preset.PresetId) + 1}");
            }
            ImGui.SameLine(0, 5);

            if (_showPresetAssignmentRuleOptions)
            {
                DrawPresetAssignmentCheckboxes(preset);
                ImGui.SameLine(0, 5);
            }

            // Titelfarbe: Custom oder aus Checkbox-Kombination berechnet
            var titleColor = preset.CustomTitleColor ?? ComputePresetColor(preset);

            if (isActive)
                ImGui.PushStyleColor(ImGuiCol.Header,        new Vector4(0.06f, 0.30f, 0.10f, 0.70f));
            if (isActive)
                ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.08f, 0.38f, 0.12f, 0.85f));
            ImGui.PushStyleColor(ImGuiCol.Text, titleColor);

            bool expanded = ImGui.TreeNodeEx("##node",
                ImGuiTreeNodeFlags.FramePadding | ImGuiTreeNodeFlags.SpanAvailWidth,
                preset.Name);

            ImGui.PopStyleColor(); // Text-Farbe
            if (isActive) ImGui.PopStyleColor(2);

            bool deletedThisFrame = false;
            if (expanded)
            {
                ImGui.Indent(22f);
                ImGui.Spacing();

                // Name-Eingabe
                var nameBuf = preset.Name;
                ImGui.SetNextItemWidth(240f);
                if (ImGui.InputText("##pname", ref nameBuf, 128))
                {
                    preset.Name = nameBuf;
                    if (isActive) _config.ActivePresetName = nameBuf;
                    _presetPreviewCache.Clear();
                    PresetStorage.Save(_config.Presets);
                    _save();
                }

                // Farb-Kästchen (custom oder berechnet)
                ImGui.SameLine(0, 6);
                var colVal = preset.CustomTitleColor ?? ComputePresetColor(preset);
                if (ImGui.ColorEdit4("##colpick", ref colVal,
                    ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
                {
                    preset.CustomTitleColor = colVal;
                    PresetStorage.Save(_config.Presets);
                    _save();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(preset.CustomTitleColor.HasValue
                        ? "Custom color — click to change"
                        : "Auto color (from checkboxes) — click to override");

                if (preset.CustomTitleColor.HasValue)
                {
                    ImGui.SameLine(0, 4);
                    if (ImGui.SmallButton("Reset color##rcol"))
                    {
                        preset.CustomTitleColor = null;
                        PresetStorage.Save(_config.Presets);
                        _save();
                    }
                }

                // Änderungs-Indikator
                if (isActive && _presetChangeCount > 0)
                {
                    ImGui.SameLine(0, 8);
                    ImGui.TextColored(new Vector4(1f, 0.7f, 0.2f, 1f), $"*{_presetChangeCount} unsaved");
                }

                ImGui.Spacing();

                // ── Aktions-Buttons ───────────────────────────────────────────
                const float bw = 44f;
                if (ImGui.Button("Upd##upd", new Vector2(bw, 0)))
                {
                    _presetPendingUpdateId = preset.PresetId;
                    _presetUpdateConfirmOpenedAt = DateTime.Now;
                    _triggerUpdatePopup = true;
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Update snapshot with current config");

                ImGui.SameLine(0, 10);
                ImGui.PushFont(UiBuilder.IconFont);
                if (BJBGui.SmallButton(FontAwesomeIcon.FileExport.ToIconString() + "##exp"))
                    ExportSinglePreset(preset);
                ImGui.PopFont();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Export preset");

                ImGui.SameLine(0, 4);
                ImGui.PushFont(UiBuilder.IconFont);
                if (BJBGui.SmallButton(FontAwesomeIcon.FileImport.ToIconString() + "##imp"))
                {
                    int tIdx = _config.Presets.IndexOf(preset);
                    _fileDialogManager.OpenFileDialog("Import Preset", "JSON Files{.json}",
                        (ok, path) =>
                        {
                            if (!ok || string.IsNullOrWhiteSpace(path)) return;
                            try
                            {
                                _presetImportJson = System.IO.File.ReadAllText(path);
                                _presetImportTargetIndex = tIdx;
                                _openPresetImportConfirm = true;
                            }
                            catch { }
                        });
                }
                ImGui.PopFont();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Import onto this preset");

                ImGui.SameLine(0, 10);
                if (ImGui.Button("Dup##dup", new Vector2(bw, 0)))
                {
                    _presetPendingDuplicateId = preset.PresetId;
                    _triggerDupPopup = true;
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Duplicate this preset");

                ImGui.SameLine(0, 14);
                bool ctrl = ImGui.GetIO().KeyCtrl;
                if (!ctrl) ImGui.BeginDisabled();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.55f, 0.08f, 0.08f, 1f));
                if (ImGui.SmallButton("X##del") && ctrl)
                {
                    if (_config.ActivePresetId == preset.PresetId)
                    { _config.ActivePresetId = string.Empty; _config.ActivePresetName = null; _presetChangeCount = 0; }
                    _config.Presets.Remove(preset);
                    _presetPreviewOpen.Remove(preset.PresetId);
                    _presetPreviewStack.Remove(preset.PresetId);
                    _presetPreviewCache.Clear();
                    PresetStorage.Save(_config.Presets);
                    _save();
                    deletedThisFrame = true;
                }
                ImGui.PopStyleColor();
                if (!ctrl) ImGui.EndDisabled();
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip(ctrl ? "Delete preset" : "Hold CTRL to delete");

                ImGui.Spacing();

                // Fußnoten: Zeitstempel
                ImGui.TextColored(new Vector4(0.42f, 0.42f, 0.42f, 1f),
                    $"Created: {preset.CreatedAt.ToLocalTime():dd.MM.yy HH:mm}  ·  " +
                    $"Updated: {preset.UpdatedAt.ToLocalTime():dd.MM.yy HH:mm}");

                ImGui.Unindent(22f);
                ImGui.Spacing();
                ImGui.TreePop();
            }

            ImGui.PopID();

            if (deletedThisFrame) break;
        }

        ImGui.EndChild();

        ImGui.TableNextColumn();
        DrawPresetPreviewColumn();
        ImGui.EndTable();
        }

        // Deferred OpenPopup — muss im selben Fensterkontext wie BeginPopupModal sein
        if (_triggerApplyPopup)  { ImGui.OpenPopup("preset_apply_confirm");  _triggerApplyPopup  = false; }
        if (_triggerUpdatePopup) { ImGui.OpenPopup("preset_update_confirm"); _triggerUpdatePopup = false; }
        if (_triggerDupPopup)    { ImGui.OpenPopup("preset_dup_confirm");    _triggerDupPopup    = false; }

        // ── Popup: Apply ──────────────────────────────────────────────────────
        bool applyOpen = true;
        if (ImGui.BeginPopupModal("preset_apply_confirm", ref applyOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            var ap = _config.Presets.FirstOrDefault(p => p.PresetId == _presetPendingApplyId);
            ImGui.TextUnformatted($"Apply preset \"{ap?.Name ?? "?"}\"?");
            ImGui.TextColored(new Vector4(1f, 0.7f, 0.3f, 1f), "Current settings will be overwritten.");
            ImGui.Spacing();
            if (ImGui.Button("Yes##pac_y", new Vector2(80f, 0)))
            {
                if (ap != null) ApplyPreset(ap);
                _presetPendingApplyId = null;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("No##pac_n", new Vector2(70f, 0)))
            { _presetPendingApplyId = null; ImGui.CloseCurrentPopup(); }
            ImGui.EndPopup();
        }

        // ── Popup: Update (2s Delay) ──────────────────────────────────────────
        bool updateOpen = true;
        if (ImGui.BeginPopupModal("preset_update_confirm", ref updateOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            var up = _config.Presets.FirstOrDefault(p => p.PresetId == _presetPendingUpdateId);
            ImGui.TextUnformatted($"Update \"{up?.Name ?? "?"}\" with current settings?");
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Only activated categories will be updated.");
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "This cannot be undone.");
            ImGui.Spacing();
            double elapsed   = (DateTime.Now - _presetUpdateConfirmOpenedAt).TotalSeconds;
            bool   canYes    = elapsed >= 2.0;
            int    remaining = Math.Max(0, 2 - (int)elapsed);
            if (!canYes) ImGui.BeginDisabled();
            if (ImGui.Button(canYes ? "Yes, update##puc_y" : $"Yes ({remaining}s)##puc_y", new Vector2(130f, 0)))
            {
                if (up != null)
                {
                    UpdatePresetSnapshot(up);
                    _presetPreviewCache.Clear();
                    PresetStorage.Save(_config.Presets);
                    _save();
                    if (up.PresetId == _config.ActivePresetId) { _presetChangeCount = 0; _presetDirty = false; }
                }
                _presetPendingUpdateId = null;
                ImGui.CloseCurrentPopup();
            }
            if (!canYes) ImGui.EndDisabled();
            ImGui.SameLine();
            if (ImGui.Button("Cancel##puc_c"))
            { _presetPendingUpdateId = null; ImGui.CloseCurrentPopup(); }
            ImGui.EndPopup();
        }

        // ── Popup: Duplicate ─────────────────────────────────────────────────
        bool dupOpen = true;
        if (ImGui.BeginPopupModal("preset_dup_confirm", ref dupOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            var dp = _config.Presets.FirstOrDefault(p => p.PresetId == _presetPendingDuplicateId);
            ImGui.TextUnformatted($"Duplicate \"{dp?.Name ?? "?"}\"?");
            ImGui.Spacing();
            if (ImGui.Button("Yes##pdc_y", new Vector2(70f, 0)))
            {
                if (dp != null)
                {
                    int ns3 = _config.Presets.Count > 0 ? _config.Presets.Max(p => p.SortOrder) + 1 : 0;
                    var now4 = DateTime.UtcNow;
                    _config.Presets.Add(new PresetEntry
                    {
                        Name = dp.Name + " (Copy)",
                        PresetId = Guid.NewGuid().ToString("N"),
                        SnapshotJson = dp.SnapshotJson,
                        ApplyRegexes = dp.ApplyRegexes,
                        ApplyMessagesDefault = dp.ApplyMessagesDefault,
                        ApplyMessagesCustom = dp.ApplyMessagesCustom,
                        ApplyStandardCommands = dp.ApplyStandardCommands,
                        ApplyOwnButtons = dp.ApplyOwnButtons,
                        ApplySettingsGeneral = dp.ApplySettingsGeneral,
                        ApplySettingsAutomation = dp.ApplySettingsAutomation,
                        ApplySettingsRules = dp.ApplySettingsRules,
                        ApplySettingsBetting = dp.ApplySettingsBetting,
                        ApplySettingsTimeDelay = dp.ApplySettingsTimeDelay,
                        ApplySettingsMessageSettings = dp.ApplySettingsMessageSettings,
                        ApplySettingsNearbyPlayers = dp.ApplySettingsNearbyPlayers,
                        ApplySettingsVisual = dp.ApplySettingsVisual,
                        ApplySettingsSystem = dp.ApplySettingsSystem,
                        ApplyDrawLogic = dp.ApplyDrawLogic,
                        CommandsCheckboxMigrated = true,
                        SettingsCategoryMigrated = true,
                        MessagesCategoryMigrated = true,
                        CreatedAt = now4, UpdatedAt = now4,
                        SortOrder = ns3,
                    });
                    PresetStorage.Save(_config.Presets);
                    _save();
                }
                _presetPendingDuplicateId = null;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("No##pdc_n", new Vector2(70f, 0)))
            { _presetPendingDuplicateId = null; ImGui.CloseCurrentPopup(); }
            ImGui.EndPopup();
        }
    }

    // ─── Command Preview Simulation ────────────────────────────────────────────

    private void DrawPresetAssignmentCheckboxes(PresetEntry preset)
    {
        void CChk(string tip, ref bool val, Vector4 col, Page hoverPage)
        {
            var bg = val
                ? new Vector4(col.X * 0.22f, col.Y * 0.22f, col.Z * 0.22f, 0.90f)
                : new Vector4(0.13f, 0.13f, 0.13f, 0.80f);
            ImGui.PushStyleColor(ImGuiCol.CheckMark, col);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, bg);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered,
                new Vector4(col.X * 0.35f, col.Y * 0.35f, col.Z * 0.35f, 1f));
            if (ImGui.Checkbox($"##cc_{preset.PresetId}_{tip.GetHashCode()}", ref val))
            {
                _presetPreviewCache.Clear();
                PresetStorage.Save(_config.Presets);
                _save();
            }
            ImGui.PopStyleColor(3);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(tip);
                _presetNavHoverPage = hoverPage;
            }
        }

        var cBlue = new Vector4(0.35f, 0.65f, 1.00f, 1f);
        var cPurp = new Vector4(0.75f, 0.45f, 1.00f, 1f);
        var cGold = new Vector4(1.00f, 0.80f, 0.20f, 1f);
        var cRed = new Vector4(1.00f, 0.35f, 0.35f, 1f);
        var cGreen = new Vector4(0.35f, 1.00f, 0.55f, 1f);
        var sepCol = new Vector4(0.40f, 0.40f, 0.40f, 0.60f);

        CChk("Regex", ref preset.ApplyRegexes, cBlue, Page.Regexes); ImGui.SameLine(0, 2);
        CChk("Messages Default", ref preset.ApplyMessagesDefault, cBlue, Page.Messages); ImGui.SameLine(0, 2);
        CChk("Messages Custom", ref preset.ApplyMessagesCustom, cBlue, Page.Messages); ImGui.SameLine(0, 2);
        CChk("Commands", ref preset.ApplyStandardCommands, cPurp, Page.Commands); ImGui.SameLine(0, 2);
        CChk("Own Buttons", ref preset.ApplyOwnButtons, cPurp, Page.Commands);
        ImGui.SameLine(0, 4); ImGui.TextColored(sepCol, "|"); ImGui.SameLine(0, 4);
        CChk("Settings > General", ref preset.ApplySettingsGeneral, cGold, Page.Settings); ImGui.SameLine(0, 2);
        CChk("Settings > Automation", ref preset.ApplySettingsAutomation, cGold, Page.Settings); ImGui.SameLine(0, 2);
        CChk("Settings > Rules", ref preset.ApplySettingsRules, cGold, Page.Settings); ImGui.SameLine(0, 2);
        CChk("Settings > Betting", ref preset.ApplySettingsBetting, cGold, Page.Settings); ImGui.SameLine(0, 2);
        CChk("Settings > Time & Delay", ref preset.ApplySettingsTimeDelay, cGold, Page.Settings); ImGui.SameLine(0, 2);
        CChk("Settings > Nearby Players", ref preset.ApplySettingsNearbyPlayers, cGold, Page.Settings); ImGui.SameLine(0, 2);
        CChk("Settings > Visual", ref preset.ApplySettingsVisual, cGold, Page.Settings);
        ImGui.SameLine(0, 4); ImGui.TextColored(sepCol, "|"); ImGui.SameLine(0, 4);
        CChk("Settings > System", ref preset.ApplySettingsSystem, cRed, Page.Settings); ImGui.SameLine(0, 2);
        CChk("Draw Logic", ref preset.ApplyDrawLogic, cGreen, Page.DrawLogic);
    }

    private void DrawPresetPreviewColumn()
    {
        if (_presetPreviewStack.Count > 0)
        {
            for (int i = 0; i < _presetPreviewStack.Count; i++)
            {
                var preset = _config.Presets.FirstOrDefault(p => p.PresetId == _presetPreviewStack[i]);
                if (preset == null) continue;
                ImGui.TextDisabled($"{i + 1}. {preset.Name}");
            }
            ImGui.Spacing();
        }

        var pvText = BuildPresetStackPreview();
        ImGui.InputTextMultiline("##preset_stack_preview", ref pvText, 65536,
            new Vector2(ImGui.GetContentRegionAvail().X, Math.Max(180f, ImGui.GetContentRegionAvail().Y - 36f)),
            ImGuiInputTextFlags.ReadOnly);

        if (BJBGui.Button("Back to current##preset_preview_current", new Vector2(130f, 0)))
        {
            _presetPreviewStack.Clear();
            _presetPreviewCache.Clear();
        }
        ImGui.SameLine();
        if (_presetPreviewStack.Count == 0) ImGui.BeginDisabled();
        if (BJBGui.Button("Use as shown##preset_preview_apply", new Vector2(130f, 0)))
        {
            var stack = _presetPreviewStack
                .Select(id => _config.Presets.FirstOrDefault(p => p.PresetId == id))
                .Where(p => p != null)
                .Cast<PresetEntry>()
                .ToList();
            foreach (var preset in stack)
                ApplyPreset(preset);
            _presetPreviewStack.Clear();
            _presetPreviewCache.Clear();
        }
        if (_presetPreviewStack.Count == 0) ImGui.EndDisabled();
    }

    private string BuildPresetStackPreview()
    {
        var previewConfig = JsonConvert.DeserializeObject<Configuration>(
            JObject.FromObject(_config).ToString(Formatting.None)) ?? _config;
        previewConfig.Presets = new List<PresetEntry>();

        foreach (var id in _presetPreviewStack)
        {
            var preset = _config.Presets.FirstOrDefault(p => p.PresetId == id);
            if (preset == null) continue;
            Configuration snap;
            try { snap = JsonConvert.DeserializeObject<Configuration>(preset.SnapshotJson) ?? previewConfig; }
            catch { continue; }

            if (preset.ApplyStandardCommands && snap.CommandGroups.Count > 0)
                previewConfig.CommandGroups = snap.CommandGroups;
            if (preset.ApplyOwnButtons && snap.CustomCommandGroups.Count > 0)
            {
                previewConfig.CustomCommandGroups = snap.CustomCommandGroups;
                previewConfig.CustomButtonEntries = snap.CustomButtonEntries;
                previewConfig.CustomButtonEntriesMigrated = snap.CustomButtonEntriesMigrated;
                previewConfig.CustomButtonOrder = snap.CustomButtonOrder;
            }
            if (preset.ApplyMessagesDefault || preset.ApplyMessagesCustom)
            {
                var standardNames = Configuration.StandardBatchNames.ToHashSet();
                if (preset.ApplyMessagesDefault)
                {
                    previewConfig.MessageBatches.RemoveAll(b => standardNames.Contains(b.Name));
                    previewConfig.MessageBatches.AddRange(snap.MessageBatches.Where(b => standardNames.Contains(b.Name)));
                }
                if (preset.ApplyMessagesCustom)
                {
                    previewConfig.MessageBatches.RemoveAll(b => !standardNames.Contains(b.Name));
                    previewConfig.MessageBatches.AddRange(snap.MessageBatches.Where(b => !standardNames.Contains(b.Name)));
                }
            }
        }

        return BuildLogicalChatPreview(previewConfig);
    }

    private string BuildLogicalChatPreview(Configuration src)
    {
        var previewPlayer = new PlayerState
        {
            Name = "Demo Player",
            Alias = "Demo Player",
            IsActivePlayer = true,
            CurrentBet = 100000,
            Hands = new List<HandState> { new(100000) },
            CurrentHandIndex = 0
        };
        var previewDealer = new PlayerState
        {
            Name = "Dealer",
            IsDealer = true,
            IsActivePlayer = true,
            Hands = new List<HandState> { new(0) },
            CurrentHandIndex = 0
        };
        PlayerState currentTarget = previewDealer;
        string previewWinners = string.Empty;
        string previewPushed = string.Empty;
        string previewLoosers = string.Empty;
        string previewBusted = string.Empty;
        string previewResults = string.Empty;
        string? lastPreviewGroupChatMessage = null;

        string ResolveBatch(string raw)
            => _pvBatchRegex.Replace(raw, m =>
            {
                var bn = m.Groups[1].Value.Trim();
                var b = src.MessageBatches.FirstOrDefault(
                    x => x.Name.Equals(bn, StringComparison.OrdinalIgnoreCase));
                return b?.Messages.Count > 0 ? b.Messages[0] : $"[{bn}]";
            });

        static int BestScore(PlayerState state)
        {
            if (state.Hands.Count == 0) return 0;
            var (min, max) = state.CalculatePoints(state.CurrentHandIndex);
            return (max.HasValue && max.Value <= 21) ? max.Value : min;
        }

        static string PointsText(PlayerState state)
        {
            if (state.Hands.Count == 0) return "0";
            var (min, max) = state.CalculatePoints(state.CurrentHandIndex);
            return max.HasValue ? $"{min}/{max}" : $"{min}";
        }

        static string CardsText(PlayerState state)
            => state.Hands.Count == 0 ? string.Empty : state.GetCardsString(state.CurrentHandIndex);

        static string ResultCategory(List<string> names, string singular, string plural)
        {
            if (names.Count == 0) return string.Empty;
            return $"{(names.Count == 1 ? singular : plural)}: {string.Join(", ", names.Distinct())}";
        }

        void RecomputePreviewResults()
        {
            var winList = new List<string>();
            var pushList = new List<string>();
            var lossList = new List<string>();
            var bustList = new List<string>();

            var hand = previewPlayer.Hands.Count > 0 ? previewPlayer.Hands[previewPlayer.CurrentHandIndex] : null;
            var pScore = BestScore(previewPlayer);
            var dealerScore = BestScore(previewDealer);
            var dealerHand = previewDealer.Hands.Count > 0 ? previewDealer.Hands[previewDealer.CurrentHandIndex] : null;
            var dealerBust = dealerHand?.IsBust == true || dealerScore > 21;

            if (hand == null)
            {
                // no-op
            }
            else if (hand.IsBust || pScore > 21)
            {
                bustList.Add(previewPlayer.DisplayName);
            }
            else if (dealerBust || pScore > dealerScore)
            {
                winList.Add(previewPlayer.DisplayName);
            }
            else if (pScore == dealerScore)
            {
                pushList.Add(previewPlayer.DisplayName);
            }
            else
            {
                lossList.Add(previewPlayer.DisplayName);
            }

            previewWinners = ResultCategory(winList, "Winner", "Winners");
            previewPushed = ResultCategory(pushList, "Pushed", "Pushed");
            previewLoosers = ResultCategory(lossList, "Lost", "Lost");
            previewBusted = ResultCategory(bustList, "Busted", "Busted");

            var defaultResults = ShortResultFormatter.Render(src, winList, pushList, lossList, bustList);
            var resultTemplate = string.IsNullOrWhiteSpace(src.ResultTemplate) ? "${results}" : src.ResultTemplate;
            previewResults = resultTemplate
                .Replace("${results}", defaultResults)
                .Replace("<results>", defaultResults)
                .Replace("${winners}", previewWinners)
                .Replace("${pushed}", previewPushed)
                .Replace("${loosers}", previewLoosers)
                .Replace("${busted}", previewBusted);
        }

        string GetPreviewResultGroup()
        {
            var hand = previewPlayer.Hands.Count > 0 ? previewPlayer.Hands[previewPlayer.CurrentHandIndex] : null;
            if (hand == null) return "ResultPlayerLost";

            var pScore = BestScore(previewPlayer);
            var dealerScore = BestScore(previewDealer);
            var dealerHand = previewDealer.Hands.Count > 0 ? previewDealer.Hands[previewDealer.CurrentHandIndex] : null;
            var dealerBust = dealerHand?.IsBust == true || dealerScore > 21;

            if (hand.IsBust || pScore > 21) return "ResultPlayerBusted";
            if (dealerBust || pScore > dealerScore) return "ResultPlayerWin";
            if (pScore == dealerScore) return "ResultPlayerPush";
            return "ResultPlayerLost";
        }

        string Process(string raw, string playerName)
        {
            RecomputePreviewResults();
            raw = ResolveBatch(raw);
            raw = _pvSeRegex.Replace(raw, "");
            var currentCards = CardsText(currentTarget);
            var dealerCards = CardsText(previewDealer);
            return raw
                .Replace("<points>", PointsText(currentTarget))
                .Replace("<cards>", currentCards)
                .Replace("<dealerHand>", dealerCards)
                .Replace("${playerCards}", currentCards)
                .Replace("${dealerpoints}", BestScore(previewDealer).ToString())
                .Replace("${dealerHand}", dealerCards)
                .Replace("<winners>", previewWinners)
                .Replace("<pushed>", previewPushed)
                .Replace("<loosers>", previewLoosers)
                .Replace("<busted>", previewBusted)
                .Replace("<results>", previewResults)
                .Replace("${winners}", previewWinners)
                .Replace("${pushed}", previewPushed)
                .Replace("${loosers}", previewLoosers)
                .Replace("${busted}", previewBusted)
                .Replace("${results}", previewResults)
                .Replace("${HandIndex}", "")
                .Replace("<t>", playerName)
                .Replace("<.>", playerName)
                .Trim();
        }

        static string ExtractChat(string text)
        {
            var t = text.TrimStart();
            if (ChatCommandRouter.TryGetAntiDoubleComparisonKey(t, out var groupMessage))
                return groupMessage;
            if (t.StartsWith("/e ", StringComparison.OrdinalIgnoreCase)) return $"[/e] {t[3..].Trim()}";
            if (t.StartsWith("/s ", StringComparison.OrdinalIgnoreCase)) return $"[/s] {t[3..].Trim()}";
            if (t.StartsWith("/sh ", StringComparison.OrdinalIgnoreCase)) return $"[/sh] {t[4..].Trim()}";
            return string.Empty;
        }

        bool IsDiceCommand(string text)
        {
            var t = text.TrimStart();
            return t.StartsWith("/dice ", StringComparison.OrdinalIgnoreCase);
        }

        var dice = GetPreviewDiceRolls();
        int diceIndex = 0;
        string NextDiceLine()
        {
            var roll = dice.Count == 0 ? 0 : dice[Math.Min(diceIndex, dice.Count - 1)];
            diceIndex++;
            return $"SYSTEM: Random! You roll a {roll}.";
        }

        void ApplyPreviewRoll()
        {
            var roll = dice.Count == 0 ? 0 : dice[Math.Min(diceIndex - 1, dice.Count - 1)];
            if (roll <= 0) return;
            if (currentTarget.Hands.Count == 0)
                currentTarget.Hands.Add(new HandState(currentTarget.CurrentBet));
            currentTarget.Hands[currentTarget.CurrentHandIndex].Cards.Add(new DeckCard
            {
                Value = GameEngine.MapDice13ToCardValue(roll),
                Suit = CardSuit.Spades,
                DrawnAt = DateTime.UtcNow
            });
            var hand = currentTarget.Hands[currentTarget.CurrentHandIndex];
            var (min, max) = currentTarget.CalculatePoints(currentTarget.CurrentHandIndex);
            hand.IsBust = min > 21 && (!max.HasValue || max.Value > 21);
        }

        CommandGroup? FindGroup(string grpName)
            => src.CommandGroups.FirstOrDefault(g => g.Name.Equals(grpName, StringComparison.OrdinalIgnoreCase))
               ?? src.CustomCommandGroups.FirstOrDefault(g => g.Name.Equals(grpName, StringComparison.OrdinalIgnoreCase));

        PluginCommand? PickPreviewLineGroupCommand(CommandGroup grp, int groupId)
            => grp.Commands.FirstOrDefault(c => c.GroupId == groupId
                                                && c.Enabled
                                                && (!string.IsNullOrWhiteSpace(c.Text)
                                                    || (c.IsCommandRef && !string.IsNullOrWhiteSpace(c.CommandRefName))));

        void AppendCommand(StringBuilder sb, PluginCommand cmd, string playerName, int depth)
        {
            if (depth > 5) return;

            if (cmd.IsCommandRef && !string.IsNullOrWhiteSpace(cmd.CommandRefName))
            {
                AppendGroup(sb, cmd.CommandRefName, currentTarget, depth + 1);
                return;
            }

            if (!cmd.Enabled || string.IsNullOrWhiteSpace(cmd.Text)) return;

            var processed = Process(cmd.Text, playerName);
            if (IsDiceCommand(processed))
            {
                sb.AppendLine(NextDiceLine());
                ApplyPreviewRoll();
                return;
            }

            if (ChatCommandRouter.TryGetAntiDoubleComparisonKey(processed, out var comparisonKey))
            {
                if (cmd.NonDoubled
                    && comparisonKey.Equals(lastPreviewGroupChatMessage, StringComparison.Ordinal))
                    return;

                // Every generated Party/Alliance line refreshes history. Only AD lines compare it.
                lastPreviewGroupChatMessage = comparisonKey;
            }

            var chat = ExtractChat(processed);
            if (string.IsNullOrWhiteSpace(chat))
                return;

            sb.AppendLine(chat);
        }

        void AppendGroup(StringBuilder sb, string grpName, PlayerState target, int depth = 0)
        {
            var grp = FindGroup(grpName);
            if (grp == null) return;
            currentTarget = target;
            var playerName = target.DisplayName;

            var processedGroups = new HashSet<int>();
            foreach (var rawCmd in grp.Commands)
            {
                PluginCommand? effectiveCmd;
                if (rawCmd.GroupId == 0)
                {
                    effectiveCmd = rawCmd;
                }
                else
                {
                    if (!processedGroups.Add(rawCmd.GroupId))
                        continue;
                    effectiveCmd = PickPreviewLineGroupCommand(grp, rawCmd.GroupId);
                    if (effectiveCmd == null) continue;
                }

                AppendCommand(sb, effectiveCmd, playerName, depth);
            }
        }

        var sb = new StringBuilder();

        // Dealer opening draw.
        AppendGroup(sb, "DealStart", previewDealer);

        // Player opening draw.
        AppendGroup(sb, "Initial", previewPlayer);

        // Player state prompt followed by the selected preview action: Stand.
        var playerHand = previewPlayer.Hands[previewPlayer.CurrentHandIndex];
        var stateGroup = GameEngine.GetStatePromptGroup(previewPlayer, src);
        if (!string.IsNullOrWhiteSpace(stateGroup))
            AppendGroup(sb, stateGroup, previewPlayer);
        playerHand.IsStand = true;
        playerHand.ActionLog.Add("Stand");
        AppendGroup(sb, "Stand", previewPlayer);

        // Dealer turn using the same hard/soft threshold rule as Auto Dealer Draw.
        const int maxDealerPreviewTurns = 20;
        for (var turn = 0; turn < maxDealerPreviewTurns; turn++)
        {
            var dealerHand = previewDealer.Hands[previewDealer.CurrentHandIndex];
            var (min, max) = previewDealer.CalculatePoints(previewDealer.CurrentHandIndex);
            var dealerScore = max.HasValue && max.Value <= 21 ? max.Value : min;
            var dealerBust = dealerHand.IsBust || dealerScore > 21;
            if (dealerBust)
            {
                dealerHand.IsBust = true;
                AppendGroup(sb, "DealerBust", previewDealer);
                break;
            }

            var isSoft = max.HasValue && max.Value <= 21 && max.Value != min;
            var shouldHit = dealerScore < src.DealerDrawsUntil
                            || (src.DealerSoftRule && isSoft && dealerScore == src.DealerDrawsUntil);
            if (!shouldHit)
            {
                dealerHand.IsStand = true;
                dealerHand.ActionLog.Add("Stand");
                AppendGroup(sb, "DealStand", previewDealer);
                break;
            }

            var cardsBefore = dealerHand.Cards.Count;
            dealerHand.ActionLog.Add("Hit");
            AppendGroup(sb, "DealHit", previewDealer);

            // A custom DealHit group without /dice cannot advance the simulated hand.
            if (dealerHand.Cards.Count == cardsBefore)
            {
                dealerHand.IsStand = true;
                AppendGroup(sb, "DealStand", previewDealer);
                break;
            }
        }

        RecomputePreviewResults();
        AppendGroup(sb, src.SmallResult ? "ResultSmall" : GetPreviewResultGroup(), src.SmallResult ? previewDealer : previewPlayer);

        return sb.ToString().TrimEnd();
    }

    private static List<int> GetPreviewDiceRolls()
    {
        var rolls = Plugin.DebugDiceSequence
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => int.TryParse(token, out var roll) ? roll : (int?)null)
            .Where(roll => roll.HasValue)
            .Select(roll => roll!.Value)
            .ToList();
        return rolls.Count > 0 ? rolls : new List<int> { 7, 10, 9, 4, 4, 8, 3, 3, 10, 6, 5, 4, 3, 2 };
    }

    private string BuildDealerDrawPreview(PresetEntry preset)
    {
        if (_presetPreviewCache.TryGetValue(preset.PresetId, out var cached))
            return cached;

        Configuration snap;
        try { snap = JsonConvert.DeserializeObject<Configuration>(preset.SnapshotJson) ?? _config; }
        catch { return "(Error: could not parse snapshot)"; }

        // Befehlsquelle: Snapshot wenn aktiviert, sonst aktuelle Config als Fallback
        var cmdSrc = (preset.ApplyStandardCommands && snap.CommandGroups.Count > 0) ? snap : _config;

        // Nachrichtenquelle: Snapshot wenn aktiviert, sonst aktuelle Config als Fallback
        var msgSrc = (preset.ApplyMessagesDefault || preset.ApplyMessagesCustom) ? snap : _config;

        // Auflösung von #{BatchName} → erste Nachricht des Batches
        string ResolveBatch(string raw)
            => _pvBatchRegex.Replace(raw, m =>
            {
                var bn = m.Groups[1].Value.Trim();
                var b  = msgSrc.MessageBatches.FirstOrDefault(
                    x => x.Name.Equals(bn, StringComparison.OrdinalIgnoreCase));
                return b?.Messages.Count > 0 ? b.Messages[0] : $"[{bn}]";
            });

        // Variable-Ersetzung + Bereinigung (Dealer + Spieler)
        string Process(string raw, int pts, string cards, string playerName, int dealerPts)
        {
            raw = _pvSeRegex.Replace(raw, "");
            raw = ResolveBatch(raw);
            return raw
                .Replace("<points>",        pts.ToString())
                .Replace("<cards>",         cards)
                .Replace("<dealerHand>",    "6S 5C 6D")
                .Replace("${playerCards}",  cards)
                .Replace("${dealerpoints}", dealerPts.ToString())
                .Replace("${dealerHand}",   "6S 5C 6D")
                .Replace("${HandIndex}",    "")
                .Replace("<t>",             playerName)
                .Replace("<.>",             playerName)
                .Trim();
        }

        // Chat-Prefix entfernen
        static string ExtractChat(string text)
        {
            var t = text.TrimStart();
            if (t.StartsWith("/p ", StringComparison.OrdinalIgnoreCase)) return t[3..].Trim();
            if (t.StartsWith("/e ", StringComparison.OrdinalIgnoreCase)) return $"[/e] {t[3..].Trim()}";
            return string.Empty;
        }

        // Gruppe ausgeben: gibt true zurück wenn mindestens eine Zeile ausgegeben wurde
        bool AppendGroup(StringBuilder sb, string grpName, int pts, string cards, string playerName, int dealerPts)
        {
            var grp = cmdSrc.CommandGroups.FirstOrDefault(
                g => g.Name.Equals(grpName, StringComparison.OrdinalIgnoreCase));
            if (grp == null) return false;
            bool any = false;
            foreach (var cmd in grp.Commands)
            {
                if (!cmd.Enabled || string.IsNullOrWhiteSpace(cmd.Text)) continue;
                var chat = ExtractChat(Process(cmd.Text, pts, cards, playerName, dealerPts));
                if (!string.IsNullOrWhiteSpace(chat)) { sb.AppendLine(chat); any = true; }
            }
            return any;
        }

        var sb = new StringBuilder();

        // ── Abschnitt 1: Dealer Draw ──────────────────────────────────────────
        // Demo: 6♠ → 9♦ → 5♣ = 20 pts → Stand
        sb.AppendLine("━━ Dealer Draw  (6♠ + 9♦ + 5♣ = 20 → Stand) ━━");
        sb.AppendLine();

        var dealerSeq = new (string Group, string? Card, int Pts, string Cards)[]
        {
            ("DealStart", "6♠",  6,  "6♠"),
            ("DealHit",   "9♦", 15, "6♠ 9♦"),
            ("DealHit",   "5♣", 20, "6♠ 9♦ 5♣"),
            ("DealStand",  null, 20, "6♠ 9♦ 5♣"),
        };
        foreach (var (grpName, card, pts, cards) in dealerSeq)
        {
            if (card != null) sb.AppendLine($"── {card} drawn ({pts} pts) ──");
            AppendGroup(sb, grpName, pts, cards, "Dealer", pts);
            sb.AppendLine();
        }

        // ── Abschnitt 2: Player Draw ──────────────────────────────────────────
        // Demo: "Demo Player" / 7♠ 8♣ = 15 pts / dealer shows 6
        sb.AppendLine("━━ Player Draw  (Demo Player: 7♠ + 8♣ = 15, Dealer: 6) ━━");
        sb.AppendLine();

        // Fallback-Kette: alle State-Varianten mit optionalem Triple Down.
        string[] playerDrawGroups = { "StateHSDTS", "StateHSTS", "StateHSDT", "StateHST", "StateHSD", "StateHS", "StateHSDS" };
        string? pdGroup = playerDrawGroups.FirstOrDefault(g =>
            cmdSrc.CommandGroups.Any(cg => cg.Name.Equals(g, StringComparison.OrdinalIgnoreCase)));

        if (pdGroup != null)
        {
            sb.AppendLine($"── 7♠ 8♣ (15 pts) — options from \"{pdGroup}\" ──");
            AppendGroup(sb, pdGroup, 15, "7♠ 8♣", "Demo Player", 6);
        }
        else
        {
            sb.AppendLine("(no player-state command group found)");
        }
        sb.AppendLine();

        // ── Abschnitt 3: Player Hit ───────────────────────────────────────────
        // Demo: "Demo Player" / 7♠ 8♣ 5♦ = 20 pts / dealer shows 6
        sb.AppendLine("━━ Player Hit  (Demo Player: 7♠ + 8♣ + 5♦ = 20, Dealer: 6) ━━");
        sb.AppendLine();

        sb.AppendLine("── 5♦ drawn → 7♠ 8♣ 5♦ (20 pts) ──");
        if (!AppendGroup(sb, "StateHS", 20, "7♠ 8♣ 5♦", "Demo Player", 6))
            sb.AppendLine("(no StateHS group found)");
        sb.AppendLine();

        var result = sb.ToString().TrimEnd();
        _presetPreviewCache[preset.PresetId] = result;
        return result;
    }

    // ─── Snapshot selektiv aktualisieren (Checkboxen beachten) ───────────────

    private void UpdatePresetSnapshot(PresetEntry preset)
    {
        var current = JObject.FromObject(_config);
        current.Remove("Presets");

        JObject snap;
        try { snap = JObject.Parse(preset.SnapshotJson); }
        catch { snap = (JObject)current.DeepClone(); }

        void CopyFields(string[] fields)
        {
            foreach (var f in fields)
            {
                if (current[f] != null) snap[f] = current[f]!.DeepClone();
                else snap.Remove(f);
            }
        }

        if (preset.ApplySettingsGeneral)         CopyFields(SettingsGeneralFields);
        if (preset.ApplySettingsAutomation)      CopyFields(SettingsAutomationFields);
        if (preset.ApplySettingsRules)           CopyFields(SettingsRulesFields);
        if (preset.ApplySettingsBetting)         CopyFields(SettingsBettingFields);
        if (preset.ApplySettingsTimeDelay)       CopyFields(SettingsTimeDelayFields);
        if (preset.ApplySettingsMessageSettings) CopyFields(SettingsMessageSettingsFields);
        if (preset.ApplySettingsNearbyPlayers)   CopyFields(SettingsNearbyPlayersFields);
        if (preset.ApplySettingsVisual)          CopyFields(SettingsVisualFields);
        if (preset.ApplySettingsSystem)          CopyFields(SettingsSystemFields);
        if (preset.ApplyDrawLogic)               CopyFields(DrawLogicFields);
        if (preset.ApplyStandardCommands)        CopyFields(StandardCommandFields);
        if (preset.ApplyOwnButtons)              CopyFields(OwnButtonFields);
        if (preset.ApplyRegexes)                 CopyFields(RegexFields);

        if (preset.ApplyMessagesDefault || preset.ApplyMessagesCustom)
        {
            var curBatches  = current["MessageBatches"] as JArray ?? new JArray();
            var snapBatches = snap["MessageBatches"]    as JArray ?? new JArray();
            var stdNames    = Configuration.StandardBatchNames.ToHashSet();

            if (preset.ApplyMessagesDefault && preset.ApplyMessagesCustom)
            {
                snap["MessageBatches"] = curBatches.DeepClone();
            }
            else
            {
                var merged = new JArray();
                // Behalte Batches aus dem Snapshot, die NICHT aktualisiert werden sollen
                foreach (var b in snapBatches)
                {
                    bool isStd = stdNames.Contains(b["Name"]?.Value<string>() ?? "");
                    if (isStd  && !preset.ApplyMessagesDefault) merged.Add(b.DeepClone());
                    if (!isStd && !preset.ApplyMessagesCustom)  merged.Add(b.DeepClone());
                }
                // Füge aktuelle Batches hinzu, die aktualisiert werden sollen
                foreach (var b in curBatches)
                {
                    bool isStd = stdNames.Contains(b["Name"]?.Value<string>() ?? "");
                    if (isStd  && preset.ApplyMessagesDefault) merged.Add(b.DeepClone());
                    if (!isStd && preset.ApplyMessagesCustom)  merged.Add(b.DeepClone());
                }
                snap["MessageBatches"] = merged;
            }
        }

        preset.SnapshotJson = snap.ToString(Formatting.None);
        preset.UpdatedAt    = DateTime.UtcNow;
    }

    // ─── Migration-Popup ──────────────────────────────────────────────────────

    private void DrawPresetMigrationPopup()
    {
        bool migOpen = _presetMigrationPending;
        if (!ImGui.BeginPopupModal("preset_migration", ref migOpen, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        ImGui.TextColored(new Vector4(1f, 0.85f, 0.3f, 1f), "Preset Migration");
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextUnformatted("Your presets will be moved to a separate file (presets.json).");
        ImGui.TextUnformatted("A backup of your config will be created first.");
        if (_presetMigrationStatus != null)
        {
            ImGui.Spacing();
            bool ok = _presetMigrationStatus.StartsWith("OK");
            ImGui.TextColored(ok ? new Vector4(0.3f, 1f, 0.3f, 1f) : new Vector4(1f, 0.4f, 0.4f, 1f),
                _presetMigrationStatus);
        }
        ImGui.Spacing();

        if (ImGui.Button("Migrate now##pmig_yes", new Vector2(150f, 0)))
        {
            try
            {
                _presetMigrationBackupPath = PresetStorage.WriteBackup(_config);
                PresetStorage.Save(_config.Presets);
                var loaded  = PresetStorage.Load();
                bool verified = PresetStorage.VerifyMigration(_config.Presets, loaded);
                if (verified)
                {
                    _config.Presets.Clear();
                    _config.PresetsMigrated = true;
                    _save();
                    var reloaded = PresetStorage.Load();
                    _config.Presets.AddRange(reloaded);
                    _presetMigrationPending = false;
                    _presetMigrationStatus = $"OK — Migrated. Backup: {System.IO.Path.GetFileName(_presetMigrationBackupPath)}";
                    ImGui.CloseCurrentPopup();
                }
                else
                {
                    _presetMigrationStatus = "ERROR — Verification failed. Presets remain in config.";
                }
            }
            catch (Exception ex)
            {
                _presetMigrationStatus = $"ERROR — {ex.Message}";
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Later##pmig_no", new Vector2(80f, 0)))
        {
            _presetMigrationPending = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    // ─── Preset anwenden ──────────────────────────────────────────────────────

    private void ApplyPreset(PresetEntry preset)
    {
        var snap = JsonConvert.DeserializeObject<Configuration>(preset.SnapshotJson);
        if (snap == null) return;

        if (preset.ApplySettingsGeneral)
        {
            _config.EnableBankInput                     = snap.EnableBankInput;
            _config.CommandSpeedMultiplier              = snap.CommandSpeedMultiplier;
            _config.SmallResult                         = snap.SmallResult;
            _config.ResultTemplate                      = snap.ResultTemplate;
            _config.ShortResultRules                    = snap.ShortResultRules?.Select(r => r.Clone()).ToList()
                ?? Configuration.CreateDefaultShortResultRules();
            _config.AutostartRoundOnlyOnMultiplePlayers = snap.AutostartRoundOnlyOnMultiplePlayers;
            _config.MainViewV2SuperCompact              = snap.MainViewV2SuperCompact;
        }

        if (preset.ApplySettingsAutomation)
        {
            _config.EnableAutomation             = snap.EnableAutomation;
            _config.ShowAutoDealerDrawButton     = snap.ShowAutoDealerDrawButton;
            _config.ShowAutoPlayerHandButton     = snap.ShowAutoPlayerHandButton;
            _config.ShowAutoContinueButton       = snap.ShowAutoContinueButton;
            _config.ShowAutoRunButton            = snap.ShowAutoRunButton;
            _config.AutoInitialDeal              = snap.AutoInitialDeal;
            _config.AutoDealerDraw               = snap.AutoDealerDraw;
            _config.AutoRun                      = snap.AutoRun;
            _config.AutoActivateTradingPlayers   = snap.AutoActivateTradingPlayers;
            _config.AutoContinue                 = snap.AutoContinue;
            _config.AutoContinueDelay            = snap.AutoContinueDelay;
            _config.AutoContinueMinimumPlayers   = snap.AutoContinueMinimumPlayersMigrated
                ? Math.Clamp(snap.AutoContinueMinimumPlayers, 1, 4)
                : (snap.AutostartRoundOnlyOnMultiplePlayers ? 2 : 1);
        }

        if (preset.ApplySettingsRules)
        {
            _config.FirstDealThenPlay            = snap.FirstDealThenPlay;
            _config.PlayerRollingForThemselves   = snap.PlayerRollingForThemselves;
            _config.IdenticalSplitOnly           = snap.IdenticalSplitOnly;
            _config.EnableSplit                  = snap.EnableSplit;
            _config.EnableDoubleDown             = snap.EnableDoubleDown;
            _config.EnableTripleDown             = snap.EnableTripleDown;
            _config.EnableDirtyBlackjack         = snap.EnableDirtyBlackjack;
            _config.AllowDoubleDownAfterSplit    = snap.AllowDoubleDownAfterSplit;
            _config.AllowTripleDownAfterSplit    = snap.AllowTripleDownAfterSplit;
            _config.LimitTripleDownToMaxPoints   = snap.LimitTripleDownToMaxPoints;
            _config.TripleDownMaxPoints          = snap.TripleDownMaxPoints;
            _config.MaxHandsPerPlayer            = snap.MaxHandsPerPlayer;
            _config.MultiplierNormalWin          = snap.MultiplierNormalWin;
            _config.MultiplierBlackjackWin       = snap.MultiplierBlackjackWin;
            _config.MultiplierDirtyBlackjackWin  = snap.MultiplierDirtyBlackjackWin;
            _config.MultiplierCharlieWin         = snap.MultiplierCharlieWin;
            _config.MultiplierSplitWin           = snap.MultiplierSplitWin;
            _config.MultiplierDoubleDownWin      = snap.MultiplierDoubleDownWin;
            _config.MultiplierTripleDownWin      = snap.MultiplierTripleDownWin;
            _config.RefundFullDoubleDownOnPush   = snap.RefundFullDoubleDownOnPush;
            _config.RefundFullTripleDownOnPush   = snap.RefundFullTripleDownOnPush;
            _config.BlackjackTieRule             = snap.BlackjackTieRule;
            _config.EnableCharlie                = snap.EnableCharlie;
            _config.CharlieCardCount             = snap.CharlieCardCount;
            _config.CharlieInstantWin            = snap.CharlieInstantWin;
            _config.DealerDrawsUntil             = snap.DealerDrawsUntil;
            _config.DealerSoftRule               = snap.DealerSoftRule;
        }

        if (preset.ApplySettingsBetting)
        {
            _config.MinBet           = snap.MinBet;
            _config.MaxBet           = snap.MaxBet;
            _config.ShortBetFormat   = snap.ShortBetFormat;
            _config.VipBetTiers      = snap.VipBetTiers;
            _config.BetLimitEntries  = snap.BetLimitEntries;
            _config.BetLimitEntriesMigrated = snap.BetLimitEntriesMigrated;
            _config.BettingPresets   = snap.BettingPresets;
        }

        if (preset.ApplySettingsTimeDelay)
        {
            _config.RecallUnlockSeconds    = snap.RecallUnlockSeconds;
            _config.UtcOffsetHours         = snap.UtcOffsetHours;
            _config.UtcOffsetMinutes       = snap.UtcOffsetMinutes;
            _config.UtcTimeZoneName        = snap.UtcTimeZoneName;
            _config.UtcSummerTime          = snap.UtcSummerTime;
        }

        if (preset.ApplySettingsNearbyPlayers)
        {
            _config.NearbyAlertEnabled          = snap.NearbyAlertEnabled;
            _config.NearbyAlertSoundFiles       = snap.NearbyAlertSoundFiles;
            _config.NearbyAlertSoundEntries     = snap.NearbyAlertSoundEntries;
            _config.NearbyAlertSoundEntriesMigrated = snap.NearbyAlertSoundEntriesMigrated;
            _config.NearbyAlertVolume           = snap.NearbyAlertVolume;
            _config.NearbyAlertCooldown         = snap.NearbyAlertCooldown;
            _config.NearbyAlertSoundMode        = snap.NearbyAlertSoundMode;
            _config.NearbyAlwaysShowCircle      = snap.NearbyAlwaysShowCircle;
            _config.NearbyQuestionCommandName   = snap.NearbyQuestionCommandName;
            _config.NearbyShowFootNumbers       = snap.NearbyShowFootNumbers;
            _config.NearbyOffsetX               = snap.NearbyOffsetX;
            _config.NearbyOffsetZ               = snap.NearbyOffsetZ;
            _config.NearbyShape                 = snap.NearbyShape;
            _config.NearbyRectangleAspectRatio  = snap.NearbyRectangleAspectRatio;
            _config.NearbyRectangleRotation     = snap.NearbyRectangleRotation;
            _config.NearbyUseFixedPosition      = snap.NearbyUseFixedPosition;
            _config.NearbyFixedCenterX          = snap.NearbyFixedCenterX;
            _config.NearbyFixedCenterY          = snap.NearbyFixedCenterY;
            _config.NearbyFixedCenterZ          = snap.NearbyFixedCenterZ;
            _config.NearbyFixedCenterCaptured   = snap.NearbyFixedCenterCaptured;
            _config.NearbyAutoActEnabled        = snap.NearbyAutoActEnabled;
            _config.NearbyAutoActCommandName    = snap.NearbyAutoActCommandName;
            _config.NearbyAutoActTimeoutMinutes = snap.NearbyAutoActTimeoutMinutes;
            _config.NearbyAutoActIgnoreList     = snap.NearbyAutoActIgnoreList;
            _config.NearbyDistanceCap           = snap.NearbyDistanceCap;
            _config.NearbyColumns               = snap.NearbyColumns;
        }

        if (preset.ApplySettingsVisual)
        {
            _config.HighlightColor              = snap.HighlightColor;
            _config.HighlightTextColor          = snap.HighlightTextColor;
            _config.ButtonColor                 = snap.ButtonColor;
            _config.ButtonTextColor             = snap.ButtonTextColor;
            _config.HideCardSuits               = snap.HideCardSuits;
            _config.SelectedFontName            = snap.SelectedFontName;
            _config.DrawLogicScale              = snap.DrawLogicScale;
            _config.DrawLogicOffsetX            = snap.DrawLogicOffsetX;
            _config.DrawLogicOffsetY            = snap.DrawLogicOffsetY;
            _config.DrawLogicOffsetZ            = snap.DrawLogicOffsetZ;
            _config.DrawLogicOffsetR            = snap.DrawLogicOffsetR;
            _config.DrawLogicColorSpades        = snap.DrawLogicColorSpades;
            _config.DrawLogicColorClubs         = snap.DrawLogicColorClubs;
            _config.DrawLogicColorHearts        = snap.DrawLogicColorHearts;
            _config.DrawLogicColorDiamonds      = snap.DrawLogicColorDiamonds;
            _config.CustomButtonPaddingH        = snap.CustomButtonPaddingH;
            _config.CustomButtonPaddingV        = snap.CustomButtonPaddingV;
            _config.CustomButtonFontScale       = snap.CustomButtonFontScale;
            _config.CustomButtonUseMono         = snap.CustomButtonUseMono;
            _config.ButtonBarLayout             = snap.ButtonBarLayout;
            _config.ButtonBarFixedWidth         = snap.ButtonBarFixedWidth;
            _config.ButtonBarFixedWidthValue    = snap.ButtonBarFixedWidthValue;
            _config.GeneralButtonDefaultStyle   = snap.GeneralButtonDefaultStyle;
            _config.GeneralButtonActiveStyle    = snap.GeneralButtonActiveStyle;
            _config.GeneralButtonHighlightStyle = snap.GeneralButtonHighlightStyle;
            _config.CustomButtonDefaultStyle    = snap.CustomButtonDefaultStyle;
        }

        if (preset.ApplySettingsSystem)
        {
            _config.EnableCompanionSync      = snap.EnableCompanionSync;
            _config.CompanionServerAddress   = snap.CompanionServerAddress;
            _config.CompanionTimeoutMs       = snap.CompanionTimeoutMs;
            _config.DisableUpdatePopup       = snap.DisableUpdatePopup;
            _config.AllianceNearbyCommandName = snap.AllianceNearbyCommandName;
        }

        if (preset.ApplyDrawLogic)
        {
            _config.DrawLogicEntries    = snap.DrawLogicEntries;
            _config.DrawLogicStartEntry = snap.DrawLogicStartEntry;
        }

        if (preset.ApplyStandardCommands) _config.CommandGroups = snap.CommandGroups;
        if (preset.ApplyOwnButtons)
        {
            _config.CustomCommandGroups = snap.CustomCommandGroups;
            _config.CustomButtonEntries = snap.CustomButtonEntries;
            _config.CustomButtonEntriesMigrated = snap.CustomButtonEntriesMigrated;
            _config.CustomButtonOrder   = snap.CustomButtonOrder;
            _config.EnsureCustomButtonEntriesMigration();
        }

        if (preset.ApplyMessagesDefault || preset.ApplyMessagesCustom)
        {
            var standardNames = Configuration.StandardBatchNames.ToHashSet();
            if (preset.ApplyMessagesDefault)
            {
                _config.MessageBatches.RemoveAll(b => standardNames.Contains(b.Name));
                _config.MessageBatches.AddRange(snap.MessageBatches.Where(b => standardNames.Contains(b.Name)));
            }
            if (preset.ApplyMessagesCustom)
            {
                _config.MessageBatches.RemoveAll(b => !standardNames.Contains(b.Name));
                _config.MessageBatches.AddRange(snap.MessageBatches.Where(b => !standardNames.Contains(b.Name)));
            }
        }

        if (preset.ApplyRegexes) _config.UserRegexes = snap.UserRegexes;

        _config.EnsureLayout3Migrations();

        if (string.IsNullOrEmpty(preset.PresetId))
            preset.PresetId = Guid.NewGuid().ToString("N");
        _config.ActivePresetId   = preset.PresetId;
        _config.ActivePresetName = preset.Name;
        preset.UpdatedAt = DateTime.UtcNow;
        _presetPreviewCache.Clear();
        PresetStorage.Save(_config.Presets);
        _save();
        _presetChangeCount = 0;
        _presetDirty = false;
    }

    // ─── Änderungs-Zähler ────────────────────────────────────────────────────

    private void RecomputePresetChangeCount()
    {
        _presetChangeCount = 0;
        PresetEntry? preset = null;
        if (!string.IsNullOrEmpty(_config.ActivePresetId))
            preset = _config.Presets.FirstOrDefault(p => p.PresetId == _config.ActivePresetId);
        if (preset == null) return;

        JObject current, snapshot;
        try
        {
            current = JObject.FromObject(_config);
            current.Remove("Presets");
            current.Remove("ActivePresetName");
            current.Remove("ActivePresetId");
            snapshot = JObject.Parse(preset.SnapshotJson);
            snapshot.Remove("ActivePresetName");
            snapshot.Remove("ActivePresetId");
        }
        catch { return; }

        int count = 0;
        if (preset.ApplySettingsGeneral)         count += CountDiffs(current, snapshot, SettingsGeneralFields);
        if (preset.ApplySettingsAutomation)      count += CountDiffs(current, snapshot, SettingsAutomationFields);
        if (preset.ApplySettingsRules)           count += CountDiffs(current, snapshot, SettingsRulesFields);
        if (preset.ApplySettingsBetting)         count += CountDiffs(current, snapshot, SettingsBettingFields);
        if (preset.ApplySettingsTimeDelay)       count += CountDiffs(current, snapshot, SettingsTimeDelayFields);
        if (preset.ApplySettingsMessageSettings) count += CountDiffs(current, snapshot, SettingsMessageSettingsFields);
        if (preset.ApplySettingsNearbyPlayers)   count += CountDiffs(current, snapshot, SettingsNearbyPlayersFields);
        if (preset.ApplySettingsVisual)          count += CountDiffs(current, snapshot, SettingsVisualFields);
        if (preset.ApplySettingsSystem)          count += CountDiffs(current, snapshot, SettingsSystemFields);
        if (preset.ApplyDrawLogic)               count += CountDiffs(current, snapshot, DrawLogicFields);
        if (preset.ApplyStandardCommands)        count += CountDiffs(current, snapshot, StandardCommandFields);
        if (preset.ApplyOwnButtons)              count += CountDiffs(current, snapshot, OwnButtonFields);
        if (preset.ApplyMessagesDefault || preset.ApplyMessagesCustom)
                                                 count += CountDiffs(current, snapshot, MessageFields);
        if (preset.ApplyRegexes)                 count += CountDiffs(current, snapshot, RegexFields);
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
            if (a == null || b == null || !JToken.DeepEquals(a, b)) count++;
        }
        return count;
    }

    // ─── Export / Import ──────────────────────────────────────────────────────

    private JObject BuildPresetExportObject(PresetEntry preset) => new JObject
    {
        ["Name"]                        = preset.Name,
        ["PresetId"]                    = preset.PresetId,
        ["CreatedAt"]                   = preset.CreatedAt,
        ["UpdatedAt"]                   = preset.UpdatedAt,
        ["ApplyRegexes"]                = preset.ApplyRegexes,
        ["ApplyMessagesDefault"]        = preset.ApplyMessagesDefault,
        ["ApplyMessagesCustom"]         = preset.ApplyMessagesCustom,
        ["ApplyStandardCommands"]       = preset.ApplyStandardCommands,
        ["ApplyOwnButtons"]             = preset.ApplyOwnButtons,
        ["ApplySettingsGeneral"]        = preset.ApplySettingsGeneral,
        ["ApplySettingsAutomation"]     = preset.ApplySettingsAutomation,
        ["ApplySettingsRules"]          = preset.ApplySettingsRules,
        ["ApplySettingsBetting"]        = preset.ApplySettingsBetting,
        ["ApplySettingsTimeDelay"]      = preset.ApplySettingsTimeDelay,
        ["ApplySettingsMessageSettings"]= preset.ApplySettingsMessageSettings,
        ["ApplySettingsNearbyPlayers"]  = preset.ApplySettingsNearbyPlayers,
        ["ApplySettingsVisual"]         = preset.ApplySettingsVisual,
        ["ApplySettingsSystem"]         = preset.ApplySettingsSystem,
        ["ApplyDrawLogic"]              = preset.ApplyDrawLogic,
    };

    private void ExportSinglePreset(PresetEntry preset)
    {
        var obj = BuildPresetExportObject(preset);
        obj["Snapshot"] = JObject.Parse(preset.SnapshotJson);
        obj["IsDelta"]  = false;
        var json = new JArray { obj }.ToString(Formatting.Indented);
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
        var exportArr  = new JArray();
        JObject? cumul = null;

        foreach (var p in _config.Presets.OrderBy(x => x.SortOrder))
        {
            JObject snap2;
            try { snap2 = JObject.Parse(p.SnapshotJson); }
            catch { snap2 = new JObject(); }

            var entry = BuildPresetExportObject(p);

            if (cumul == null)
            {
                entry["Snapshot"] = snap2;
                entry["IsDelta"]  = false;
                cumul = (JObject)snap2.DeepClone();
            }
            else
            {
                var delta = new JObject();
                foreach (var prop in snap2.Properties())
                {
                    var existing = cumul[prop.Name];
                    if (existing == null || !JToken.DeepEquals(existing, prop.Value))
                        delta[prop.Name] = prop.Value.DeepClone();
                }
                foreach (var prop in cumul.Properties().ToList())
                    if (snap2[prop.Name] == null)
                        delta[prop.Name] = JValue.CreateNull();
                entry["Snapshot"] = delta;
                entry["IsDelta"]  = true;
                foreach (var prop in snap2.Properties())
                    cumul[prop.Name] = prop.Value.DeepClone();
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
                var entry    = (JObject)arr[j];
                var snapshot = (JObject)entry["Snapshot"]!;
                bool isDelta = entry["IsDelta"]?.Value<bool>() ?? false;
                if (j == 0 || !isDelta) cumulative = (JObject)snapshot.DeepClone();
                else                    ApplyDelta(cumulative!, snapshot);
            }

            var preset    = _config.Presets[_presetImportTargetIndex.Value];
            var lastEntry = (JObject)arr[arr.Count - 1];

            if (lastEntry.ContainsKey("Name"))
                preset.Name = lastEntry["Name"]!.Value<string>() ?? preset.Name;

            var lSet = lastEntry["ApplySettings"]?.Value<bool>() ?? true;
            var lCmd = lastEntry["ApplyCommands"]?.Value<bool>() ?? true;
            var lMsg = lastEntry["ApplyMessages"]?.Value<bool>() ?? true;

            preset.ApplyRegexes                = lastEntry["ApplyRegexes"]?.Value<bool>()                ?? preset.ApplyRegexes;
            preset.ApplyMessagesDefault         = lastEntry["ApplyMessagesDefault"]?.Value<bool>()         ?? lMsg;
            preset.ApplyMessagesCustom          = lastEntry["ApplyMessagesCustom"]?.Value<bool>()          ?? lMsg;
            preset.ApplyStandardCommands        = lastEntry["ApplyStandardCommands"]?.Value<bool>()        ?? lCmd;
            preset.ApplyOwnButtons              = lastEntry["ApplyOwnButtons"]?.Value<bool>()              ?? lCmd;
            preset.ApplySettingsGeneral         = lastEntry["ApplySettingsGeneral"]?.Value<bool>()         ?? lSet;
            preset.ApplySettingsAutomation      = lastEntry["ApplySettingsAutomation"]?.Value<bool>()      ?? lSet;
            preset.ApplySettingsRules           = lastEntry["ApplySettingsRules"]?.Value<bool>()           ?? lSet;
            preset.ApplySettingsBetting         = lastEntry["ApplySettingsBetting"]?.Value<bool>()         ?? lSet;
            preset.ApplySettingsTimeDelay       = lastEntry["ApplySettingsTimeDelay"]?.Value<bool>()       ?? lSet;
            preset.ApplySettingsMessageSettings = lastEntry["ApplySettingsMessageSettings"]?.Value<bool>() ?? lSet;
            preset.ApplySettingsNearbyPlayers   = lastEntry["ApplySettingsNearbyPlayers"]?.Value<bool>()   ?? lSet;
            preset.ApplySettingsVisual          = lastEntry["ApplySettingsVisual"]?.Value<bool>()          ?? lSet;
            preset.ApplySettingsSystem          = lastEntry["ApplySettingsSystem"]?.Value<bool>()          ?? false;
            preset.ApplyDrawLogic               = lastEntry["ApplyDrawLogic"]?.Value<bool>()               ?? false;
            preset.CommandsCheckboxMigrated  = true;
            preset.SettingsCategoryMigrated  = true;
            preset.MessagesCategoryMigrated  = true;

            preset.SnapshotJson = cumulative!.ToString(Formatting.None);
            _presetPreviewCache.Clear();
            _save();
        }
        catch { }
        finally
        {
            _presetImportJson        = null;
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

    // ─── Farbe aus Checkbox-Kombination berechnen ─────────────────────────────

    private static Vector4 ComputePresetColor(PresetEntry p)
    {
        int mask = (p.ApplyRegexes                ? 1     : 0)
                 | (p.ApplyMessagesDefault         ? 2     : 0)
                 | (p.ApplyMessagesCustom          ? 4     : 0)
                 | (p.ApplyStandardCommands        ? 8     : 0)
                 | (p.ApplyOwnButtons              ? 16    : 0)
                 | (p.ApplySettingsGeneral         ? 32    : 0)
                 | (p.ApplySettingsAutomation      ? 64    : 0)
                 | (p.ApplySettingsRules           ? 128   : 0)
                 | (p.ApplySettingsBetting         ? 256   : 0)
                 | (p.ApplySettingsTimeDelay       ? 512   : 0)
                 | (p.ApplySettingsMessageSettings ? 1024  : 0)
                 | (p.ApplySettingsNearbyPlayers   ? 2048  : 0)
                 | (p.ApplySettingsVisual          ? 4096  : 0)
                 | (p.ApplySettingsSystem          ? 8192  : 0)
                 | (p.ApplyDrawLogic               ? 16384 : 0);
        // Goldener-Schnitt-Multiplikator für gute Verteilung über den Farbkreis
        float hue = (mask * 137.508f) % 360f / 360f;
        return HsvToRgba(hue, 0.60f, 0.90f);
    }

    private static Vector4 HsvToRgba(float h, float s, float v)
    {
        float c  = v * s;
        float x  = c * (1f - MathF.Abs(h * 6f % 2f - 1f));
        float m  = v - c;
        float r, g, b;
        switch ((int)(h * 6f) % 6)
        {
            case 0:  r = c; g = x; b = 0; break;
            case 1:  r = x; g = c; b = 0; break;
            case 2:  r = 0; g = c; b = x; break;
            case 3:  r = 0; g = x; b = c; break;
            case 4:  r = x; g = 0; b = c; break;
            default: r = c; g = 0; b = x; break;
        }
        return new Vector4(r + m, g + m, b + m, 1f);
    }
}
