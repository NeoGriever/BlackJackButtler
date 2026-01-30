using System;
using System.Collections.Generic;
using System.Linq;
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
    public float CommandSpeedMultiplier = 1.0f;

    public List<CommandGroup> CommandGroups = new();
    public List<MessageBatch> MessageBatches = new();
    public List<UserRegexEntry> UserRegexes = new();

    public bool DefaultBatchesSeeded = false;
    public bool DefaultRegexSeeded = false;
    public bool DefaultCommandsSeeded = false;

    public bool AutoInitialDeal = false;
    public bool SmallResult = false;
    public bool dismissDevWarning = false;

    public UserLevel CurrentLevel = UserLevel.Beginner;

    public static string[] StandardBatchNames => DefaultsManager.GetDefaultMessages().Select(m => m.Name).ToArray();
    public static string[] StandardRegexNames => DefaultsManager.GetDefaultRegex().Select(r => r.Name).ToArray();

    public void ForceResetStandardBatches() {
        var defaults = DefaultsManager.GetDefaultMessages();
        var names = defaults.Select(d => d.Name).ToList();
        MessageBatches.RemoveAll(b => names.Contains(b.Name));
        MessageBatches.AddRange(defaults);
        DefaultBatchesSeeded = true;
    }

    public void ForceResetStandardRegexes() {
        var defaults = DefaultsManager.GetDefaultRegex();
        var names = defaults.Select(d => d.Name).ToList();
        UserRegexes.RemoveAll(r => names.Contains(r.Name));
        UserRegexes.AddRange(defaults);
        DefaultRegexSeeded = true;
    }

    public void ForceResetCommandGroups() {
        CommandGroups = DefaultsManager.GetDefaultCommands();
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
        var msg = Messages[IterativeIndex];
        IterativeIndex = (IterativeIndex + 1) % Messages.Count;
        return msg;
      case SelectionMode.Random:
      default:
        return Messages[Random.Shared.Next(Messages.Count)];
    }
  }
}
