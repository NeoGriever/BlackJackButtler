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

    public int MaxHandsPerPlayer = 2;
    public float MultiplierNormalWin = 1.0f;
    public float MultiplierBlackjackWin = 1.5f;
    public bool RefundFullDoubleDownOnPush = true;

    public List<CommandGroup> CommandGroups { get; set; } = new();

    public List<MessageBatch> MessageBatches { get; set; } = new();
    public bool AllowEditingStandardRegex { get; set; } = false;
    public List<UserRegexEntry> UserRegexes { get; set; } = new();

    public bool DefaultBatchesSeeded { get; set; } = false;
    public bool DefaultRegexSeeded { get; set; } = false;
    public bool DefaultCommandsSeeded { get; set; } = false;

    public bool FirstDealThenPlay = true;

    public static readonly string[] StandardCommandGroupNames =
    {
        "Initial", "Hit", "Stand", "DD", "Split",
        "PlayerBJ", "PlayerDirtyBJ", "PlayerBust",
        "DealStart", "DealHit", "DealStand", "DealerBJ", "DealerBust",
        "ResultPlayerWin", "ResultPlayerPush", "ResultPlayerBusted", "ResultPlayerLost"
    };

    public static readonly IReadOnlyDictionary<string, PluginCommand[]> DefaultCommands =
        new Dictionary<string, PluginCommand[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["Initial"] = new[] { new PluginCommand { Text = "/p #{PlayerStarts}", Delay = 0.4f }, new PluginCommand { Text = "/dice party 13", Delay = 0.4f }, new PluginCommand { Text = "/dice party 13", Delay = 0.4f } },
        ["Hit"] = new[] { new PluginCommand { Text = "/p #{PlayerHits}", Delay = 0.4f }, new PluginCommand { Text = "/dice party 13", Delay = 0.4f } },
        ["Stand"] = new[] { new PluginCommand { Text = "/p #{PlayerStands}", Delay = 0.4f } },
        ["DD"] = new[] { new PluginCommand { Text = "/p #{PlayerDoubleDowns}", Delay = 0.4f }, new PluginCommand { Text = "/dice party 13", Delay = 0.4f } },
        ["Split"] = new[] { new PluginCommand { Text = "/p #{PlayerSplits}", Delay = 0.4f } },
        ["PlayerBJ"] = new[] { new PluginCommand { Text = "/p #{PlayerNatBJ}", Delay = 0.4f }, new PluginCommand { Text = "/shout #{PlayerNatBJShout}", Delay = 0.4f } },
        ["PlayerDirtyBJ"] = new[] { new PluginCommand { Text = "/p #{PlayerDirtyBJ}", Delay = 0.4f } },
        ["PlayerBust"] = new[] { new PluginCommand { Text = "/p #{PlayerBusts}", Delay = 0.4f } },
        ["DealStart"] = new[] { new PluginCommand { Text = "/p #{DealerStarts}", Delay = 0.4f }, new PluginCommand { Text = "/dice party 13", Delay = 0.4f } },
        ["DealHit"] = new[] { new PluginCommand { Text = "/p #{DealerHits}", Delay = 0.4f }, new PluginCommand { Text = "/dice party 13", Delay = 0.4f } },
        ["DealStand"] = new[] { new PluginCommand { Text = "/p #{DealerStands}", Delay = 0.4f } },
        ["DealerBJ"] = new[] { new PluginCommand { Text = "/p #{DealerBlackjack}", Delay = 0.4f } },
        ["DealerBust"] = new[] { new PluginCommand { Text = "/p #{DealerBusts}", Delay = 0.4f } },
        ["ResultPlayerWin"] = new[] { new PluginCommand { Text = "/p <t> won!", Delay = 0.1f } },
        ["ResultPlayerPush"] = new[] { new PluginCommand { Text = "/p <t> got pushed!", Delay = 0.1f } },
        ["ResultPlayerBusted"] = new[] { new PluginCommand { Text = "/p <t> busted!", Delay = 0.1f } },
        ["ResultPlayerLost"] = new[] { new PluginCommand { Text = "/p <t> lost!", Delay = 0.1f } },
    };


    public void ForceResetStandardBatches()
    {
        MessageBatches.RemoveAll(b => !string.IsNullOrWhiteSpace(b.Name) && StandardBatchNames.Contains(b.Name!, StringComparer.OrdinalIgnoreCase));
        foreach (var name in StandardBatchNames)
        {
            var messages = DefaultMessages.TryGetValue(name, out var defaults) ? defaults.ToList() : new List<string>();
            MessageBatches.Add(new MessageBatch { Name = name, Messages = messages });
        }
        DefaultBatchesSeeded = true;
    }

    public void ForceResetStandardRegexes()
    {
        var standardNames = DefaultTradeRegexes.Select(x => x.Name).ToList();
        UserRegexes.RemoveAll(r => standardNames.Contains(r.Name));
        foreach (var entry in DefaultTradeRegexes)
        {
            UserRegexes.Add(new UserRegexEntry {
                Name = entry.Name,
                Patterns = new List<string>(entry.Patterns),
                Action = entry.Action,
                Mode = entry.Mode,
                Enabled = true
                });
            }
            DefaultRegexSeeded = true;
        }

    public void ForceResetCommandGroups()
    {
        CommandGroups.Clear();

        foreach (var name in StandardCommandGroupNames)
        {
            var group = new CommandGroup { Name = name };

            if (DefaultCommands.TryGetValue(name, out var defaults) && defaults != null)
            {
                group.Commands.AddRange(defaults.Select(c => new PluginCommand
                {
                    Text = c.Text,
                    Delay = c.Delay
                }));
            }
            else
            {
                group.Commands.Add(new PluginCommand
                {
                    Text = $"/p [BJB] Executing {name} logic for <t>...",
                    Delay = 0.2f
                });
            }

            CommandGroups.Add(group);
        }

        DefaultCommandsSeeded = true;
    }

    public bool EnsureDefaultsOnce()
    {
        bool changed = false;
        if (!DefaultBatchesSeeded) { ForceResetStandardBatches(); changed = true; }
        if (!DefaultRegexSeeded) { ForceResetStandardRegexes(); changed = true; }
        if (!DefaultCommandsSeeded) { ForceResetCommandGroups(); changed = true; }
        return changed;
    }

    public bool EnsureDefaultBatchesOnce() => EnsureDefaultsOnce();
    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);

    public static readonly string[] StandardBatchNames = {
        "PlayerStarts",
        "PlayerHits",
        "PlayerStands",
        "PlayerDoubleDowns",
        "PlayerSplits",
        "PlayerNatBJ",
        "PlayerDirtyBJ",
        "PlayerBusts",
        "DealerStarts",
        "DealerHits",
        "DealerStands",
        "DealerBlackjack",
        "DealerBusts",
    };

    public static readonly IReadOnlyDictionary<string, string[]> DefaultMessages = new Dictionary<string, string[]> {
        ["PlayerStarts"] = new[] { "Player Starts Message" },
        ["PlayerHits"] = new[] { "Player Hits Message" },
        ["PlayerStands"] = new[] { "Player Stands Message" },
        ["PlayerDoubleDowns"] = new[] { "Player Double Down Message" },
        ["PlayerSplits"] = new[] { "Player Splits Message" },
        ["PlayerNatBJ"] = new[] { "Player Nat BJ Message" },
        ["PlayerDirtyBJ"] = new[] { "Player D BJ Message" },
        ["PlayerBusts"] = new[] { "Player Busts" },
        ["DealerStarts"] = new[] { "Dealer Start" },
        ["DealerHits"] = new[] { "Dealer Hit" },
        ["DealerStands"] = new[] { "Dealer Stand" },
        ["DealerBlackjack"] = new[] { "Dealer BJ" },
        ["DealerBusts"] = new[] { "Dealer Bust" },
    };

    public static readonly List<UserRegexEntry> DefaultTradeRegexes = new() {
        new UserRegexEntry { Name = "Trade: Inbound",           Patterns = new() { @"^(.+) möchte mit dir handeln\.$" },            Action = RegexAction.TradePartner,  Mode = RegexEntryMode.Trigger },
        new UserRegexEntry { Name = "Trade: Outbound",          Patterns = new() { @"^Du hast (.+) einen Handel angeboten\.$" },    Action = RegexAction.TradePartner,  Mode = RegexEntryMode.Trigger },
        new UserRegexEntry { Name = "Trade: Gil In",            Patterns = new() { @"^Du erhältst ([\d.]+) Gil\.$" },               Action = RegexAction.TradeGilIn,    Mode = RegexEntryMode.Trigger },
        new UserRegexEntry { Name = "Trade: Gil Out",           Patterns = new() { @"^Du überreichst ([\d.]+) Gil\.$" },            Action = RegexAction.TradeGilOut,   Mode = RegexEntryMode.Trigger },
        new UserRegexEntry { Name = "Trade: Success",           Patterns = new() { @"^Der Handel wurde abgeschlossen\.$" },         Action = RegexAction.TradeCommit,   Mode = RegexEntryMode.Trigger },
        new UserRegexEntry { Name = "Trade: Cancel",            Patterns = new() { @"^Der Handel wurde abgebrochen\.$" },           Action = RegexAction.TradeCancel,   Mode = RegexEntryMode.Trigger },
        new UserRegexEntry { Name = "Dice: Blackjack Logic",    Patterns = new() { @"(\d+)\s*$" },                                  Action = RegexAction.DiceRollValue, Mode = RegexEntryMode.Trigger },
      };
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
