using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Configuration;
using BlackJackButtler.Regex;

namespace BlackJackButtler;
public enum UserLevel { Beginner, Advanced, Dev, Custom }
public enum BlackjackTieRule { AlwaysPush, PlayerNatBJWins, DealerNatBJWins, NatBJBeatsDirty }
public enum NearbyAlertSoundMode { Iterative, Random, FirstOnly }
public enum NearbyShapeMode { Circle, Rectangle }
public enum ButtonBarLayout { Horizontal, Vertical }
// Keep the original numeric values for MinBet/Vip so existing JSON remains valid.
public enum BetLimitEntryKind { MinBet, Vip, Normal }
public enum ShortResultDataSource { None, Winners, Pushed, Loosed, Busted }
public enum GilVisualMode { Plain, Grouped, FixedGroup }
public enum MenuStyleMode { Sidebar, BurgerMenu, TopTabs }
public enum WageInterval { Minute, FifteenMinutes, ThirtyMinutes, Hour, TwoHours }

[Serializable]
public sealed class ShortResultRule
{
    public ShortResultDataSource Data = ShortResultDataSource.None;
    public bool VisibleIfEmpty;
    public bool VisibleIfContentBeforeIsEmpty;
    public bool VisibleIfContentAfterIsEmpty;
    public bool Compress;
    public string Template = string.Empty;

    public ShortResultRule Clone() => new()
    {
        Data = Data,
        VisibleIfEmpty = VisibleIfEmpty,
        VisibleIfContentBeforeIsEmpty = VisibleIfContentBeforeIsEmpty,
        VisibleIfContentAfterIsEmpty = VisibleIfContentAfterIsEmpty,
        Compress = Compress,
        Template = Template,
    };
}

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool HideStandardBatches = true;
    public bool AllowEditingStandardRegex = false;

    public bool FirstDealThenPlay = true;
    public bool PlayerRollingForThemselves = false;
    public Dictionary<string, bool> PlayerSelfRollPreferences = new();
    public bool IdenticalSplitOnly = true;
    public bool EnableSplit = true;
    public bool EnableDoubleDown = true;
    public bool EnableTripleDown = false;
    public bool EnableDirtyBlackjack = true;
    public bool AllowDoubleDownAfterSplit = false;
    public bool AllowTripleDownAfterSplit = false;
    public bool LimitTripleDownToMaxPoints = false;
    public int TripleDownMaxPoints = 10;
    public int MaxHandsPerPlayer = 3;
    public float MultiplierNormalWin = 1.0f;
    public float MultiplierBlackjackWin = 1.5f;
    public float MultiplierDirtyBlackjackWin = 1.0f;
    public float MultiplierCharlieWin = 1.5f;
    public float MultiplierSplitWin = 1.0f;
    public float MultiplierDoubleDownWin = 1.0f;
    public float MultiplierTripleDownWin = 1.0f;
    public bool RefundFullDoubleDownOnPush = false;
    public bool RefundFullTripleDownOnPush = true;
    public BlackjackTieRule BlackjackTieRule = BlackjackTieRule.AlwaysPush;
    public bool EnableCharlie = false;
    public int CharlieCardCount = 5;
    public bool CharlieInstantWin = true;
    public bool EnableBankInput = false;
    public string AutoBetPostCommandName = "";
    public string InsufficientBetCommandName = "";
    public float CommandSpeedMultiplier = 1.0f;
    public bool UnlockWaitTimer = false;
    public bool DelaySecondSnapping = true;
    public float RecallUnlockSeconds = 20f;
    public bool EnableCompanionSync = false;
    public string CompanionServerAddress = "http://127.0.0.1:8000";
    public int CompanionTimeoutMs = 200;
    // Legacy compatibility only. Alliance routing is always enabled.
    public bool EnableAllianceSupport = true;
    public string AllianceNearbyCommandName = "";

    public List<CommandGroup> CommandGroups = new();
    public List<CommandGroup> CustomCommandGroups = new();
    public List<CustomButtonEntry> CustomButtonEntries = new();
    public bool CustomButtonEntriesMigrated = false;
    // Legacy storage retained for import/export compatibility. New entries take priority.
    public List<string> CustomButtonOrder = new();
    public List<MessageBatch> MessageBatches = new();
    public List<UserRegexEntry> UserRegexes = new();

    public List<PresetEntry> Presets = new();
    public string? ActivePresetName = null;   // null = "Default"
    public string ActivePresetId = string.Empty;

    public long MinBet = 50000;
    public long MaxBet = 500000;

    public List<VipBetTier> VipBetTiers = new();
    public List<BettingPreset> BettingPresets = new();
    public bool BetLimitEntriesMigrated = false;
    public bool ShortBetFormat = true;
    public bool HideCardSuits = false;

    public bool DefaultBatchesSeeded = false;
    public bool DefaultRegexSeeded = false;
    public bool DefaultCommandsSeeded = false;

    public bool AutoInitialDeal = false;
    public bool AutoDealerDraw = false;
    public bool AutoRun = false;
    // Controls runtime handling of newly joined group members; defaults to enabled for existing configurations.
    public bool AutoActivateTradingPlayers = true;
    public bool EnableAutomation = true;
    public bool ShowAutoDealerDrawButton = true;
    public bool ShowAutoPlayerHandButton = true;
    public bool ShowAutoContinueButton = true;
    public bool ShowAutoRunButton = true;
    public int DealerDrawsUntil = 17;
    public bool DealerSoftRule = true;
    public bool SmallResult = false;
    public string ResultTemplate = "${results}";
    public List<ShortResultRule> ShortResultRules = new();
    public bool ShortResultRulesInitialized = false;
    public bool AutostartRoundOnlyOnMultiplePlayers = true;
    public bool EnableAntiDouble = false;
    public Vector4 HighlightColor = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
    public Vector4 HighlightTextColor = new Vector4(0f, 0f, 0f, 1f);
    public Vector4 ButtonColor     = new Vector4(0.26f, 0.26f, 0.26f, 1.0f); // dark grey
    public Vector4 ButtonTextColor = new Vector4(1.0f,  1.0f,  1.0f,  1.0f); // white
    public GilVisualMode GilVisual = GilVisualMode.FixedGroup;

    public float PayoutPercent = 30f;
    public long GilPerHour = 250000;
    public WageInterval WageIntervalMode = WageInterval.Hour;
    public int ClipHoursMode = 0;
    public bool UseFixedWage = false;
    public long FixedWage = 500000;

    // UtcOffsetHours remains for compatibility with existing configuration and presets.
    // New code uses signed minutes so half- and quarter-hour time zones are lossless.
    public int UtcOffsetHours = 0;
    public int UtcOffsetMinutes = int.MinValue;
    public string UtcTimeZoneName = string.Empty;
    public bool UtcSummerTime = false;
    public bool UtcOffsetConfigured = false;

    public bool dismissDevWarning = false;
    public bool HashedStats = true;
    public bool StatsSubtractPlayerBanks = true;
    public long StatsHouseBank = 0;

    public string LastSeenVersion = "";
    public bool DisableUpdatePopup = false;

    public bool UseBurgerMenu = false;
    public MenuStyleMode MenuStyle = MenuStyleMode.Sidebar;
    public bool MenuStyleMigrated = false;
    public int MainViewVersion = 1;
    public bool MainViewV2SuperCompact = false;

    public bool ImportantNoticeAcknowledged = false;

    public string BlacklistDetectedAt = "";
    public bool BlacklistActive = false;

    public float InitialViewDirection = 0f;
    public bool LookEveryTime = false;

    public string NotepadText = "";

    public List<string> NearbyFavorites = new();
    public float NearbyDistanceCap = 2.5f;
    public bool ShowNearbyPlayers = true;
    public bool NearbySticky = false;
    public int NearbyColumns = 2;
    public bool NoAutoDequeue = false;
    public bool NearbyAlwaysShowCircle = false;
    public string NearbyQuestionCommandName = string.Empty;
    public bool NearbyShowFootNumbers = true;
    public float NearbyOffsetX = 0f;
    public float NearbyOffsetZ = 0f;
    public NearbyShapeMode NearbyShape = NearbyShapeMode.Circle;
    public float NearbyRectangleAspectRatio = 1f;
    public float NearbyRectangleRotation = 0f;
    public bool NearbyUseFixedPosition = false;
    public float NearbyFixedCenterX = 0f;
    public float NearbyFixedCenterY = 0f;
    public float NearbyFixedCenterZ = 0f;
    public bool NearbyFixedCenterCaptured = false;
    public bool NearbyAutoActEnabled = false;
    public string NearbyAutoActCommandName = string.Empty;
    public float NearbyAutoActTimeoutMinutes = 120f;
    public List<string> NearbyAutoActIgnoreList = new();

    public bool AutoContinue = false;
    public float AutoContinueDelay = 30f;

    public Vector4 AutoContinueBarColor = new Vector4(0.2f, 0.8f, 0.2f, 1.0f);
    public float AutoContinueBarHeight = 4f;
    public bool AutoContinueBarShowText = false;

    public bool NearbyAlertEnabled = false;
    // Legacy paths remain serialized for backwards-compatible import/export. Structured
    // entries take precedence once migrated because they carry enabled state and volume.
    public List<string> NearbyAlertSoundFiles = new();
    public List<NearbyAlertSoundEntry> NearbyAlertSoundEntries = new();
    public bool NearbyAlertSoundEntriesMigrated = false;
    public float NearbyAlertVolume = 50f;
    public float NearbyAlertCooldown = 0.30f;
    public NearbyAlertSoundMode NearbyAlertSoundMode = NearbyAlertSoundMode.Random;

    public float CustomButtonPaddingH = 4.0f;
    public float CustomButtonPaddingV = 2.0f;
    public float CustomButtonFontScale = 1.0f;
    public bool CustomButtonUseMono = false;

    public bool ButtonBarPopout = false;
    public bool ButtonBarNoBackground = false;
    public bool ButtonBarLocked = false;
    public ButtonBarLayout ButtonBarLayout = ButtonBarLayout.Horizontal;
    public bool ButtonBarFixedWidth = false;
    public float ButtonBarFixedWidthValue = 200f;
    public Vector4 ButtonBarBackgroundColor = new(0.1f, 0.1f, 0.1f, 1f);
    public string SelectedFontName = "Default";
    public ButtonStyleConfig GeneralButtonDefaultStyle = ButtonStyleConfig.Default();
    public ButtonStyleConfig GeneralButtonActiveStyle = ButtonStyleConfig.Active();
    public ButtonStyleConfig GeneralButtonHighlightStyle = ButtonStyleConfig.Highlight();
    public ButtonStyleConfig CustomButtonDefaultStyle = ButtonStyleConfig.Default();

    public List<DrawLogicEntry> DrawLogicEntries = new();
    public string DrawLogicStartEntry = "";
    public bool DrawLogicSeeded = false;
    public bool DotTokenMigrated = false;
    public bool GameplayRegexPatternsMigrated = false;
    public bool BankTransferRegexMigrated = false;
    public bool NotifyGroupsMigrated = false;
    public string DrawLogicScriptDir = "";

    public bool TablePopout = false;
    public bool NearbyPopout = false;
    public bool HideThanksPage = false;
    public bool PresetsMigrated = false;

    public float DrawLogicScale = 1.0f;
    public float DrawLogicOffsetX = 0.0f;
    public float DrawLogicOffsetY = 0.0f;
    public float DrawLogicOffsetZ = 0.0f;
    public float DrawLogicOffsetR = 0.0f;
    public Vector4 DrawLogicColorSpades = new(0f, 0f, 0f, 1f);
    public Vector4 DrawLogicColorClubs = new(0f, 0f, 0f, 1f);
    public Vector4 DrawLogicColorHearts = new(1f, 0f, 0f, 1f);
    public Vector4 DrawLogicColorDiamonds = new(1f, 0f, 0f, 1f);

    public UserLevel CurrentLevel = UserLevel.Beginner;
    public List<string> CustomVisiblePages = new();
    public List<BetLimitEntry> BetLimitEntries = new();

    public static string[] StandardBatchNames => DefaultsManager.GetDefaultMessages().Select(m => m.Name).ToArray();
    public static string[] StandardRegexNames => DefaultsManager.GetDefaultRegex().Select(r => r.Name).ToArray();

    public static List<ShortResultRule> CreateDefaultShortResultRules() => new()
    {
        new() { Data = ShortResultDataSource.Winners, Compress = true, Template = "Winners: <data>" },
        new() { Data = ShortResultDataSource.None, Template = " | " },
        new() { Data = ShortResultDataSource.Pushed, Compress = true, Template = "Pushed: <data>" },
        new() { Data = ShortResultDataSource.None, Template = " | " },
        new() { Data = ShortResultDataSource.Loosed, Compress = true, Template = "Lost: <data>" },
        new() { Data = ShortResultDataSource.None, Template = " | " },
        new() { Data = ShortResultDataSource.Busted, Compress = true, Template = "Busted: <data>" },
    };

    public bool EnsureShortResultRules()
    {
        if (ShortResultRulesInitialized)
            return false;

        ShortResultRules ??= new List<ShortResultRule>();
        var defaults = CreateDefaultShortResultRules();

        if (ShortResultRules.Count == 0)
        {
            ShortResultRules = defaults;
        }
        else
        {
            // Older builds initialized the field with defaults before Newtonsoft
            // populated it. Every load therefore prepended another default block.
            while (ShortResultRules.Count > defaults.Count
                && HasRulePrefix(ShortResultRules, defaults))
            {
                ShortResultRules.RemoveRange(0, defaults.Count);
            }
        }

        ShortResultRulesInitialized = true;
        return true;
    }

    private static bool HasRulePrefix(IReadOnlyList<ShortResultRule> rules, IReadOnlyList<ShortResultRule> prefix)
    {
        if (rules.Count < prefix.Count)
            return false;

        for (var i = 0; i < prefix.Count; i++)
        {
            var left = rules[i];
            var right = prefix[i];
            if (left.Data != right.Data
                || left.VisibleIfEmpty != right.VisibleIfEmpty
                || left.VisibleIfContentBeforeIsEmpty != right.VisibleIfContentBeforeIsEmpty
                || left.VisibleIfContentAfterIsEmpty != right.VisibleIfContentAfterIsEmpty
                || left.Compress != right.Compress
                || !string.Equals(left.Template, right.Template, StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    public void ForceResetStandardBatches() {
        var defaults = DefaultsMigration.GetSnapshotMessages()
                    ?? DefaultsManager.GetDefaultMessages();
        var names = defaults.Select(d => d.Name).ToList();
        MessageBatches.RemoveAll(b => names.Contains(b.Name));
        MessageBatches.AddRange(defaults);
        DefaultBatchesSeeded = true;
    }

    public void ForceResetStandardRegexes() {
        var defaults = DefaultsMigration.GetSnapshotRegex()
                    ?? DefaultsManager.GetDefaultRegex();
        var names = defaults.Select(d => d.Name).ToList();
        UserRegexes.RemoveAll(r => names.Contains(r.Name));
        UserRegexes.AddRange(defaults);
        DefaultsMigration.EnsureBankTransferRegex(this);
        DefaultRegexSeeded = true;
        RegexEngine.InvalidateCache();
    }

    public void ForceResetCommandGroups() {
        CommandGroups = DefaultsMigration.GetSnapshotCommands()
                     ?? DefaultsManager.GetDefaultCommands();
        DefaultCommandsSeeded = true;
    }

    public bool EnsureDefaultsOnce() {
        bool changed = false;
        if (!DefaultBatchesSeeded) { ForceResetStandardBatches(); changed = true; }
        if (!DefaultRegexSeeded) { ForceResetStandardRegexes(); changed = true; }
        if (!DefaultCommandsSeeded) { ForceResetCommandGroups(); changed = true; }
        return changed;
    }

    public bool EnsureDefaultBatchesOnce() => EnsureDefaultsOnce();

    public bool EnsurePresetMigrations()
    {
        bool changed = false;
        foreach (var p in Presets)
        {
            if (string.IsNullOrEmpty(p.PresetId))
            {
                p.PresetId = Guid.NewGuid().ToString("N");
                changed = true;
            }
            if (!p.CommandsCheckboxMigrated)
            {
                p.ApplyStandardCommands = p.ApplyCommands;
                p.ApplyOwnButtons = p.ApplyCommands;
                p.CommandsCheckboxMigrated = true;
                changed = true;
            }
            if (!p.SettingsCategoryMigrated)
            {
                p.ApplySettingsGeneral = p.ApplySettings;
                p.ApplySettingsAutomation = p.ApplySettings;
                p.ApplySettingsRules = p.ApplySettings;
                p.ApplySettingsBetting = p.ApplySettings;
                p.ApplySettingsTimeDelay = p.ApplySettings;
                p.ApplySettingsMessageSettings = p.ApplySettings;
                p.ApplySettingsNearbyPlayers = p.ApplySettings;
                p.ApplySettingsVisual = p.ApplySettings;
                p.ApplySettingsSystem = false;
                p.ApplyDrawLogic = false;
                p.SettingsCategoryMigrated = true;
                changed = true;
            }
            if (!p.MessagesCategoryMigrated)
            {
                p.ApplyMessagesDefault = p.ApplyMessages;
                p.ApplyMessagesCustom = p.ApplyMessages;
                p.MessagesCategoryMigrated = true;
                changed = true;
            }
        }
        if (string.IsNullOrEmpty(ActivePresetId) && !string.IsNullOrEmpty(ActivePresetName))
        {
            var match = Presets.FirstOrDefault(p => p.Name == ActivePresetName);
            if (match != null)
            {
                ActivePresetId = match.PresetId;
                changed = true;
            }
        }
        return changed;
    }

    public bool EnsureLayout3Migrations()
    {
        var changed = false;
        if (UtcOffsetMinutes == int.MinValue)
        {
            UtcOffsetMinutes = Math.Clamp(UtcOffsetHours, -12, 14) * 60;
            changed = true;
        }
        // Message settings were intentionally retired. Both behaviours are now invariant.
        if (!EnableAntiDouble)
        {
            EnableAntiDouble = true;
            changed = true;
        }
        if (!DelaySecondSnapping)
        {
            DelaySecondSnapping = true;
            changed = true;
        }
        if (!NoAutoDequeue)
        {
            NoAutoDequeue = true;
            changed = true;
        }
        changed |= EnsureBetLimitEntriesMigration();
        changed |= EnsureNearbyAlertSoundEntriesMigration();
        changed |= EnsureCustomButtonEntriesMigration();
        return changed;
    }

    public bool EnsureNearbyAlertSoundEntriesMigration()
    {
        if (NearbyAlertSoundEntriesMigrated) return false;

        if (NearbyAlertSoundEntries.Count == 0)
        {
            foreach (var path in NearbyAlertSoundFiles.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                NearbyAlertSoundEntries.Add(new NearbyAlertSoundEntry { Path = path, Enabled = true, Volume = 100f });
            }
        }

        NearbyAlertSoundEntriesMigrated = true;
        SyncLegacyNearbyAlertSoundFiles();
        return true;
    }

    public void SyncLegacyNearbyAlertSoundFiles()
    {
        NearbyAlertSoundFiles = NearbyAlertSoundEntries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Path))
            .Select(entry => entry.Path)
            .ToList();
    }

    public bool EnsureBetLimitEntriesMigration()
    {
        if (BetLimitEntriesMigrated)
        {
            var repaired = false;
            foreach (var entry in BetLimitEntries.Where(entry => entry.Kind == BetLimitEntryKind.Vip && entry.VipLevel == 0))
            {
                entry.Kind = BetLimitEntryKind.Normal;
                entry.Name = string.IsNullOrWhiteSpace(entry.Name) || entry.Name == "VIP" ? "Max" : entry.Name;
                repaired = true;
            }
            if (!BetLimitEntries.Any(entry => entry.Kind == BetLimitEntryKind.MinBet))
            {
                BetLimitEntries.Insert(0, new BetLimitEntry { Active = true, Kind = BetLimitEntryKind.MinBet, Name = "Min", Amount = MinBet });
                repaired = true;
            }
            return repaired;
        }

        if (BetLimitEntries.Count == 0)
        {
            BetLimitEntries.Add(new BetLimitEntry { Active = true, Kind = BetLimitEntryKind.MinBet, Name = "Min", Amount = MinBet });
            BetLimitEntries.Add(new BetLimitEntry { Active = true, Kind = BetLimitEntryKind.Normal, Name = "Max", Amount = MaxBet });

            if (VipBetTiers.Count == 0)
            {
                BetLimitEntries.Add(new BetLimitEntry { Active = false, Kind = BetLimitEntryKind.Vip, VipLevel = 1, Name = "VIP", Amount = 1_000_000 });
                BetLimitEntries.Add(new BetLimitEntry { Active = false, Kind = BetLimitEntryKind.Vip, VipLevel = 2, Name = "Lifetime", Amount = 2_000_000 });
            }
            else
            {
                for (var i = 0; i < VipBetTiers.Count; i++)
                {
                    var tier = VipBetTiers[i];
                    BetLimitEntries.Add(new BetLimitEntry
                    {
                        Active = true,
                        Kind = BetLimitEntryKind.Vip,
                        VipLevel = i + 1,
                        Name = string.IsNullOrWhiteSpace(tier.Name) ? $"VIP {i + 1}" : tier.Name,
                        Amount = tier.MaxBet,
                    });
                }
            }
        }
        else
        {
            foreach (var entry in BetLimitEntries)
            {
                // V2 used VIP level 0 as the normal maximum. Preserve its amount but
                // migrate it to the explicit kind before the new editor sees it.
                if (entry.Kind == BetLimitEntryKind.Vip && entry.VipLevel == 0)
                {
                    entry.Kind = BetLimitEntryKind.Normal;
                    entry.Name = string.IsNullOrWhiteSpace(entry.Name) || entry.Name == "VIP" ? "Max" : entry.Name;
                }
                if (entry.Kind == BetLimitEntryKind.MinBet)
                {
                    entry.VipLevel = 0;
                    if (string.IsNullOrWhiteSpace(entry.Name)) entry.Name = "Min";
                }
                if (entry.Kind == BetLimitEntryKind.Normal)
                {
                    entry.VipLevel = 0;
                    if (string.IsNullOrWhiteSpace(entry.Name)) entry.Name = "Max";
                }
            }
        }

        if (!BetLimitEntries.Any(entry => entry.Kind == BetLimitEntryKind.MinBet))
            BetLimitEntries.Insert(0, new BetLimitEntry { Active = true, Kind = BetLimitEntryKind.MinBet, Name = "Min", Amount = MinBet });

        BetLimitEntriesMigrated = true;
        return true;
    }

    public bool EnsureCustomButtonEntriesMigration()
    {
        var changed = false;
        foreach (var group in CustomCommandGroups)
        {
            if (!string.IsNullOrWhiteSpace(group.Id)) continue;
            group.Id = Guid.NewGuid().ToString("N");
            changed = true;
        }

        if (!CustomButtonEntriesMigrated)
        {
            if (CustomButtonEntries.Count == 0)
            {
                foreach (var legacyEntry in CustomButtonOrder)
                {
                    if (legacyEntry == "---")
                    {
                        CustomButtonEntries.Add(new CustomButtonEntry { IsBreak = true });
                        continue;
                    }

                    var group = CustomCommandGroups.FirstOrDefault(g =>
                        g.Name.Equals(legacyEntry, StringComparison.OrdinalIgnoreCase));
                    CustomButtonEntries.Add(new CustomButtonEntry
                    {
                        GroupId = group?.Id ?? string.Empty,
                        LegacyGroupName = group == null ? legacyEntry : string.Empty,
                    });
                }
            }

            // The former Unassigned section is folded into the ordered list at the end.
            foreach (var group in CustomCommandGroups)
            {
                if (CustomButtonEntries.Any(e => !e.IsBreak && e.GroupId == group.Id)) continue;
                CustomButtonEntries.Add(new CustomButtonEntry { GroupId = group.Id });
            }

            CustomButtonEntriesMigrated = true;
            changed = true;
        }

        if (changed)
            SyncLegacyCustomButtonOrder();
        return changed;
    }

    public void ResetCustomButtonEntriesFromLegacy()
    {
        CustomButtonEntries.Clear();
        CustomButtonEntriesMigrated = false;
    }

    public void SyncLegacyCustomButtonOrder()
    {
        CustomButtonOrder = CustomButtonEntries.Select(entry =>
        {
            if (entry.IsBreak) return "---";
            var group = CustomCommandGroups.FirstOrDefault(g => g.Id == entry.GroupId);
            return group?.Name ?? entry.LegacyGroupName;
        }).Where(entry => !string.IsNullOrWhiteSpace(entry)).ToList();
    }

    public int GetUtcOffsetMinutes()
    {
        var baseMinutes = UtcOffsetMinutes == int.MinValue
            ? Math.Clamp(UtcOffsetHours, -12, 14) * 60
            : UtcOffsetMinutes;
        return baseMinutes + (UtcSummerTime ? 60 : 0);
    }

    public void SetUtcBaseOffsetMinutes(int minutes, string? timeZoneName)
    {
        UtcOffsetMinutes = Math.Clamp(minutes, -12 * 60, 14 * 60);
        UtcOffsetHours = (int)MathF.Round(UtcOffsetMinutes / 60f);
        UtcTimeZoneName = timeZoneName ?? string.Empty;
        UtcOffsetConfigured = true;
    }

    public void Save()
    {
        if (PresetsMigrated && Presets.Count > 0)
        {
            var snapshot = Presets.ToList();
            Presets.Clear();
            Plugin.PluginInterface.SavePluginConfig(this);
            Presets.AddRange(snapshot);
        }
        else
        {
            Plugin.PluginInterface.SavePluginConfig(this);
        }
    }
}

public enum SelectionMode { Random, First, Iterative }

[Serializable]
public sealed class MessageBatch
{
  public string Name { get; set; } = "New Batch";
  public bool IsExpanded { get; set; } = true;
  public List<string> Messages { get; set; } = new();
  public List<bool> ADFlags { get; set; } = new();
  public SelectionMode Mode { get; set; } = SelectionMode.Random;
  public int IterativeIndex { get; set; } = 0;

  public bool GetAD(int index) => index >= 0 && index < ADFlags.Count && ADFlags[index];

  public void SetAD(int index, bool value)
  {
    while (ADFlags.Count <= index) ADFlags.Add(false);
    ADFlags[index] = value;
  }

  public string GetNextMessage()
  {
    if (Messages.Count == 0) return string.Empty;

    int selectedIndex;
    switch (Mode)
    {
      case SelectionMode.First:
        selectedIndex = 0;
        break;
      case SelectionMode.Iterative:
        if (IterativeIndex >= Messages.Count) IterativeIndex = 0;
        selectedIndex = IterativeIndex;
        IterativeIndex = (IterativeIndex + 1) % Messages.Count;
        break;
      case SelectionMode.Random:
      default:
        selectedIndex = Random.Shared.Next(Messages.Count);
        break;
    }

    return Messages[selectedIndex];
  }
}

[Serializable]
public sealed class PresetEntry
{
    public string Name = "New Preset";
    public string PresetId = string.Empty;

    // Legacy-Flags (für Migration erhalten)
    public bool ApplySettings = true;
    public bool ApplyCommands = true;
    public bool ApplyMessages = true;

    // Granulare Kategorien
    public bool ApplyRegexes = true;
    public bool ApplyMessagesDefault = true;
    public bool ApplyMessagesCustom = true;
    public bool ApplyStandardCommands = true;
    public bool ApplyOwnButtons = true;
    public bool ApplySettingsGeneral = true;
    public bool ApplySettingsAutomation = true;
    public bool ApplySettingsRules = true;
    public bool ApplySettingsBetting = true;
    public bool ApplySettingsTimeDelay = true;
    public bool ApplySettingsMessageSettings = true;
    public bool ApplySettingsNearbyPlayers = true;
    public bool ApplySettingsVisual = true;
    public bool ApplySettingsSystem = false;
    public bool ApplyDrawLogic = false;

    // Zeitstempel & Reihenfolge
    public DateTime CreatedAt = DateTime.UtcNow;
    public DateTime UpdatedAt = DateTime.UtcNow;
    public int SortOrder = 0;

    // Migrations-Flags
    public bool CommandsCheckboxMigrated = false;
    public bool SettingsCategoryMigrated = false;
    public bool MessagesCategoryMigrated = false;

    // Optionale Titelfarbe (null = automatisch aus Checkbox-Kombination berechnet)
    public Vector4? CustomTitleColor = null;

    public string SnapshotJson = "{}";
}

[Serializable]
public sealed class DrawLogicEntry
{
    public string Name { get; set; } = "New Entry";
    public string Script { get; set; } = "";
    public string ScriptPath { get; set; } = "";
    public bool IsIterate { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public bool AutoReload { get; set; } = false;
}

[Serializable]
public sealed class VipBetTier
{
    public string Name = "VIP";
    public long MaxBet = 1000000;
}

[Serializable]
public sealed class BetLimitEntry
{
    public bool Active = true;
    public BetLimitEntryKind Kind = BetLimitEntryKind.Vip;
    public int VipLevel = 0;
    public string Name = "";
    public long Amount = 250000;
}

[Serializable]
public sealed class BettingPreset
{
    public string Name = "New Preset";
    public List<BetLimitEntry> Entries = new();
    public Vector4? Color = null;
}

[Serializable]
public sealed class NearbyAlertSoundEntry
{
    public string Path = string.Empty;
    public bool Enabled = true;
    public float Volume = 100f;
}

[Serializable]
public sealed class ButtonStyleConfig
{
    public Vector4 Background = new(0.26f, 0.26f, 0.26f, 1f);
    public Vector4 Text = new(1f, 1f, 1f, 1f);
    public float FontSize = 1f;
    public int PaddingTop = 0;
    public int PaddingLeft = 0;
    public int PaddingBottom = 0;
    public int PaddingRight = 0;

    public static ButtonStyleConfig Default() => new();

    public static ButtonStyleConfig Active() => new()
    {
        Background = new Vector4(1.0f, 0.5f, 0.0f, 1f),
        Text = new Vector4(1f, 1f, 1f, 1f),
    };

    public static ButtonStyleConfig Highlight() => new()
    {
        Background = new Vector4(1.0f, 1.0f, 0.0f, 1f),
        Text = new Vector4(0f, 0f, 0f, 1f),
    };
}
