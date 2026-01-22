using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Configuration;
using BlackJackButtler.Regex;

namespace BlackJackButtler;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
  public int Version { get; set; } = 0;
  public bool HideStandardBatches { get; set; } = true;

  public static readonly string[] StandardBatchNames =
  {
    "Welcome Messages",
    "New Player Messages",
    "Collecting Bets Messages",
    "Dealer Draw Messages",
    "Player draw Messages",
    "Hand Reaction Messages",
    "Win Messages",
    "Push Messages",
    "Bust Messages",
    "Blackjack Dealer Messages",
    "BlackJack Player Messages",
  };

  public static readonly IReadOnlyDictionary<string, string[]> DefaultMessages = new Dictionary<string, string[]>
  {
    ["Welcome Messages"] = new[] { "Welcome to BlacJack. I'm your dealer today. I wish everyone good luck." },
    ["New Player Messages"] = new[] { "Hello <t>. Welcome to my BlackJack table." },
    ["Collecting Bets Messages"] = new[] { "Collecting bets right now. Just trade to me." },
    ["Dealer Draw Messages"] = new[] { "Let's get it started. The dealers first card is ..." },
    ["Player draw Messages"] = new[] { "<t> needs another card. Your card is ..." },
    ["Hand Reaction Messages"] = new[] { "Will it be enough for <t> to win?" },
    ["Win Messages"] = new[] { "<t> won this round." },
    ["Push Messages"] = new[] { "<t> got pushed." },
    ["Bust Messages"] = new[] { "<t> busted. I'm so sorry." },
    ["Blackjack Dealer Messages"] = new[] { "Wohoo. I got a blackjack." },
    ["BlackJack Player Messages"] = new[] { "Wohoo. <t> got a blackjack." },
  };

  public List<MessageBatch> MessageBatches { get; set; } = new();

  public bool AllowEditingStandardRegex { get; set; } = false;

  public List<UserRegexEntry> UserRegexes { get; set; } = new();

  public bool DefaultBatchesSeeded { get; set; } = false;

  public void ForceResetStandardBatches()
  {
    MessageBatches.RemoveAll(
    b => !string.IsNullOrWhiteSpace(b.Name) &&
    StandardBatchNames.Contains(b.Name!, StringComparer.OrdinalIgnoreCase)
    );

    foreach (var name in StandardBatchNames)
    {
      var messages = DefaultMessages.TryGetValue(name, out var defaults)
      ? defaults.ToList()
      : new List<string>();

      MessageBatches.Add(new MessageBatch
      {
        Name = name,
        IsExpanded = false,
        Messages = messages,
        Mode = SelectionMode.Random
      });
    }
    DefaultBatchesSeeded = true;
  }

  public bool EnsureDefaultBatchesOnce()
  {
    if (DefaultBatchesSeeded) return false;
    ForceResetStandardBatches();
    return true;
  }

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
