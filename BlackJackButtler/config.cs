using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Configuration;
using BlackJackButtler.Regex;

namespace BlackJackButtler;
public enum UserLevel { Beginner, Advanced, Dev }

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
    public bool PlayerBJWinsOnTie = false;
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

    public bool DefaultBatchesSeeded = false;
    public bool DefaultRegexSeeded = false;
    public bool DefaultCommandsSeeded = false;

    public bool AutoInitialDeal = false;
    public bool AutoDealerDraw = false;
    public bool AutoRun = false;
    public int DealerDrawsUntil = 17;
    public bool SmallResult = false;
    public bool AutostartRoundOnlyOnMultiplePlayers = true;
    public Vector4 HighlightColor = new Vector4(1.0f, 1.0f, 0.0f, 1.0f);
    public Vector4 HighlightTextColor = new Vector4(0f, 0f, 0f, 1f);
    public Vector4 ButtonColor     = new Vector4(0.26f, 0.26f, 0.26f, 1.0f); // dark grey
    public Vector4 ButtonTextColor = new Vector4(1.0f,  1.0f,  1.0f,  1.0f); // white

    public long OverallIncome = 0;
    public long OverallExpense = 0;
    public int OverallRounds = 0;

    public bool dismissDevWarning = false;

    public float InitialViewDirection = 0f;
    public bool LookEveryTime = false;

    public string NotepadText = "";

    public List<string> NearbyFavorites = new();
    public float NearbyDistanceCap = 2.5f;
    public bool ShowNearbyPlayers = true;
    public bool NearbySticky = false;

    public UserLevel CurrentLevel = UserLevel.Beginner;

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
  public SelectionMode Mode { get; set; } = SelectionMode.Random;
  public int IterativeIndex { get; set; } = 0;

  public string GetNextMessage()
  {
    if (Messages.Count == 0) return string.Empty;

    switch (Mode)
    {
      case SelectionMode.First:
        return Messages[0];
      case SelectionMode.Iterative:
        if (IterativeIndex >= Messages.Count) IterativeIndex = 0;
        var msg = Messages[IterativeIndex];
        IterativeIndex = (IterativeIndex + 1) % Messages.Count;
        return msg;
      case SelectionMode.Random:
      default:
        return Messages[Random.Shared.Next(Messages.Count)];
    }
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
public sealed class VipBetTier
{
    public string Name = "VIP";
    public long MaxBet = 1000000;
}
