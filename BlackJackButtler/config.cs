using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Configuration;
using BlackJackButtler.Regex;

namespace BlackJackButtler;
public enum UserLevel { Beginner, Advanced, Dev, Custom }
public enum BlackjackTieRule { AlwaysPush, PlayerNatBJWins, DealerNatBJWins, NatBJBeatsDirty }

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool HideStandardBatches = true;
    public bool AllowEditingStandardRegex = false;

    public bool FirstDealThenPlay = true;
    public bool IdenticalSplitOnly = true;
    public bool AllowDoubleDownAfterSplit = false;
    public int MaxHandsPerPlayer = 2;
    public float MultiplierNormalWin = 1.0f;
    public float MultiplierBlackjackWin = 1.5f;
    public float MultiplierDirtyBlackjackWin = 1.0f;
    public bool RefundFullDoubleDownOnPush = false;
    public BlackjackTieRule BlackjackTieRule = BlackjackTieRule.AlwaysPush;
    public bool EnableCharlie = false;
    public int CharlieCardCount = 5;
    public bool EnableBankInput = false;
    public float CommandSpeedMultiplier = 1.0f;
    public bool UnlockWaitTimer = false;
    public bool OpenDropboxInsteadOfTrade = true;

    public List<CommandGroup> CommandGroups = new();
    public List<CommandGroup> CustomCommandGroups = new();
    public List<string> CustomButtonOrder = new();
    public List<MessageBatch> MessageBatches = new();
    public List<UserRegexEntry> UserRegexes = new();
    public List<WebhookEntry> Webhooks = new();

    public List<PresetEntry> Presets = new();
    public string? ActivePresetName = null;   // null = "Default"

    public long MinBet = 50000;
    public long MaxBet = 500000;

    public List<VipBetTier> VipBetTiers = new();
    public bool ShortBetFormat = true;
    public bool HideCardSuits = false;

    public bool DefaultBatchesSeeded = false;
    public bool DefaultRegexSeeded = false;
    public bool DefaultCommandsSeeded = false;

    public bool AutoInitialDeal = false;
    public bool AutoDealerDraw = false;
    public bool AutoRun = false;
    public int DealerDrawsUntil = 17;
    public bool SmallResult = false;
    public bool AutostartRoundOnlyOnMultiplePlayers = true;
    public bool EnableAntiDouble = false;
    public Vector4 HighlightColor = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
    public Vector4 HighlightTextColor = new Vector4(0f, 0f, 0f, 1f);
    public Vector4 ButtonColor     = new Vector4(0.26f, 0.26f, 0.26f, 1.0f); // dark grey
    public Vector4 ButtonTextColor = new Vector4(1.0f,  1.0f,  1.0f,  1.0f); // white

    public float PayoutPercent = 30f;
    public long GilPerHour = 250000;
    public int ClipHoursMode = 0;
    public bool UseFixedWage = false;
    public long FixedWage = 500000;

    public int UtcOffsetHours = 0;
    public bool UtcOffsetConfigured = false;

    public bool dismissDevWarning = false;

    public string LastSeenVersion = "";
    public bool DisableUpdatePopup = false;

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

    public bool AutoContinue = false;
    public float AutoContinueDelay = 30f;

    public Vector4 AutoContinueBarColor = new Vector4(0.2f, 0.8f, 0.2f, 1.0f);
    public float AutoContinueBarHeight = 4f;
    public bool AutoContinueBarShowText = false;

    public bool NearbyAlertEnabled = false;
    public List<string> NearbyAlertSoundFiles = new();
    public float NearbyAlertVolume = 50f;
    public float NearbyAlertCooldown = 0.30f;

    public float CustomButtonPaddingH = 4.0f;
    public float CustomButtonPaddingV = 2.0f;
    public float CustomButtonFontScale = 1.0f;
    public bool CustomButtonUseMono = false;

    public bool ButtonBarPopout = false;
    public bool ButtonBarNoBackground = false;
    public bool ButtonBarLocked = false;

    public List<DrawLogicEntry> DrawLogicEntries = new();
    public string DrawLogicStartEntry = "";
    public bool DrawLogicSeeded = false;
    public bool DotTokenMigrated = false;
    public string DrawLogicScriptDir = "";

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

    public static string[] StandardBatchNames => DefaultsManager.GetDefaultMessages().Select(m => m.Name).ToArray();
    public static string[] StandardRegexNames => DefaultsManager.GetDefaultRegex().Select(r => r.Name).ToArray();

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

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
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
  [Newtonsoft.Json.JsonIgnore] public string LastSelected = string.Empty;

  public bool GetAD(int index) => index >= 0 && index < ADFlags.Count && ADFlags[index];

  public void SetAD(int index, bool value)
  {
    while (ADFlags.Count <= index) ADFlags.Add(false);
    ADFlags[index] = value;
  }

  public string GetNextMessage(bool enableAntiDouble = false)
  {
    if (Messages.Count == 0) return string.Empty;

    int startIndex;
    switch (Mode)
    {
      case SelectionMode.First:
        startIndex = 0;
        break;
      case SelectionMode.Iterative:
        if (IterativeIndex >= Messages.Count) IterativeIndex = 0;
        startIndex = IterativeIndex;
        IterativeIndex = (IterativeIndex + 1) % Messages.Count;
        break;
      case SelectionMode.Random:
      default:
        startIndex = Random.Shared.Next(Messages.Count);
        break;
    }

    string picked = Messages[startIndex];

    if (enableAntiDouble && GetAD(startIndex) && picked == LastSelected && Messages.Count > 1)
    {
      int nextIndex = (startIndex + 1) % Messages.Count;
      picked = Messages[nextIndex];
      if (Mode == SelectionMode.Iterative)
        IterativeIndex = (nextIndex + 1) % Messages.Count;
    }

    LastSelected = picked;
    return picked;
  }
}

[Serializable]
public sealed class WebhookEntry
{
    public string Name = "New Webhook";
    public string Url = string.Empty;
    public bool ShowBetAmounts = true;
    public bool Enabled = true;
}

[Serializable]
public sealed class PresetEntry
{
    public string Name = "New Preset";

    // Welche Kategorien werden beim Anwenden überschrieben
    public bool ApplySettings = true;
    public bool ApplyCommands = true;   // CommandGroups + CustomCommandGroups
    public bool ApplyMessages = true;
    public bool ApplyRegexes  = true;
    public bool ApplyWebhooks = true;

    // Vollständiger Config-Snapshot als JSON-String
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
