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
        TryApply<bool>  (j, "EnableTripleDown",                       v => _config.EnableTripleDown = v);
        TryApply<bool>  (j, "EnableDirtyBlackjack",                   v => _config.EnableDirtyBlackjack = v);
        TryApply<bool>  (j, "AllowDoubleDownAfterSplit",               v => _config.AllowDoubleDownAfterSplit = v);
        TryApply<bool>  (j, "AllowTripleDownAfterSplit",               v => _config.AllowTripleDownAfterSplit = v);
        TryApply<bool>  (j, "LimitTripleDownToMaxPoints",              v => _config.LimitTripleDownToMaxPoints = v);
        TryApply<int>   (j, "TripleDownMaxPoints",                     v => _config.TripleDownMaxPoints = Math.Clamp(v, 2, 21));
        TryApply<int>   (j, "MaxHandsPerPlayer",                      v => _config.MaxHandsPerPlayer = v);
        TryApply<float> (j, "MultiplierNormalWin",                    v => _config.MultiplierNormalWin = v);
        TryApply<float> (j, "MultiplierBlackjackWin",                 v => _config.MultiplierBlackjackWin = v);
        TryApply<float> (j, "MultiplierDirtyBlackjackWin",            v => _config.MultiplierDirtyBlackjackWin = v);
        TryApply<float> (j, "MultiplierCharlieWin",                    v => _config.MultiplierCharlieWin = v);
        TryApply<float> (j, "MultiplierSplitWin",                      v => _config.MultiplierSplitWin = v);
        TryApply<float> (j, "MultiplierDoubleDownWin",                 v => _config.MultiplierDoubleDownWin = v);
        TryApply<float> (j, "MultiplierTripleDownWin",                 v => _config.MultiplierTripleDownWin = v);
        TryApply<bool>  (j, "RefundFullDoubleDownOnPush",             v => _config.RefundFullDoubleDownOnPush = v);
        TryApply<bool>  (j, "RefundFullTripleDownOnPush",             v => _config.RefundFullTripleDownOnPush = v);
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
        TryApply<PartyNearbyJCommandMode>(j, "PartyNearbyJCommand",   v => _config.PartyNearbyJCommand = v);
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
        TryApply<int>   (j, "AutoContinueMinimumPlayers",            v => _config.AutoContinueMinimumPlayers = Math.Clamp(v, 1, 4));
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
        TryApply<int>   (j, "UtcOffsetHours",                            v => _config.UtcOffsetHours = v);
        TryApply<int>   (j, "UtcOffsetMinutes",                          v => _config.UtcOffsetMinutes = v);
        TryApply<string>(j, "UtcTimeZoneName",                           v => _config.UtcTimeZoneName = v);
        TryApply<bool>  (j, "UtcSummerTime",                             v => _config.UtcSummerTime = v);
        TryApply<bool>  (j, "UtcOffsetConfigured",                       v => _config.UtcOffsetConfigured = v);
        TryApply<bool>  (j, "BetLimitEntriesMigrated",                   v => _config.BetLimitEntriesMigrated = v);
        TryApply<bool>  (j, "MoneyRingEnabled",                          v => _config.MoneyRingEnabled = v);
        TryApply<bool>  (j, "MoneyRingInitialized",                      v => _config.MoneyRingInitialized = v);

        if (j.ContainsKey("AutoContinueMinimumPlayers"))
        {
            _config.AutoContinueMinimumPlayersMigrated = true;
        }
        else if (j.TryGetValue("AutostartRoundOnlyOnMultiplePlayers", out var legacyMinimumToken))
        {
            _config.AutoContinueMinimumPlayers = legacyMinimumToken.ToObject<bool>() ? 2 : 1;
            _config.AutoContinueMinimumPlayersMigrated = true;
        }
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
        if (_tempImportJson.ContainsKey("MoneyRingEntries"))
        {
            _config.MoneyRingEntries = _tempImportJson["MoneyRingEntries"]!.ToObject<List<MoneyRingEntry>>()
                ?? new List<MoneyRingEntry>();
            _config.MoneyRingInitialized = true;
        }
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
        if (_tempImportJson.ContainsKey("MoneyRingEntries"))
        {
            _config.MoneyRingEntries = _tempImportJson["MoneyRingEntries"]!.ToObject<List<MoneyRingEntry>>()
                ?? new List<MoneyRingEntry>();
            _config.MoneyRingInitialized = true;
        }

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
