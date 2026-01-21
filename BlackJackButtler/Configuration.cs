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

  public List<MessageBatch> MessageBatches { get; set; } = new();

  public bool AllowEditingStandardRegex { get; set; } = false;

  public List<UserRegexEntry> UserRegexes { get; set; } = new();

  public void EnsureDefaults()
  {
    if (Version >= 1)
    return;

    var defaults = new[]
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
      "Dealer Blackjack Messages",
      "Player BlackJack Messages",
    };

    var existing = new HashSet<string>(
      MessageBatches.Select(b => b.Name ?? string.Empty),
      StringComparer.OrdinalIgnoreCase
    );

    foreach (var name in defaults)
    {
      if (existing.Contains(name))
      continue;

      MessageBatches.Add(new MessageBatch
      {
        Name = name,
        IsExpanded = false,
        Messages = new List<string>()
      });
    }

    Version = 1;
  }

  public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}

[Serializable]
public sealed class MessageBatch
{
  public string Name { get; set; } = "New Batch";
  public bool IsExpanded { get; set; } = true;
  public List<string> Messages { get; set; } = new() { "Hello!", "Another line..." };
}
