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
        ["Welcome Messages"] =          new[] { "Welcome to BlacJack. I'm your dealer today. I wish everyone good luck." },
        ["New Player Messages"] =       new[] { "Hello <t>. Welcome to my BlackJack table." },
        ["Collecting Bets Messages"] =  new[] { "Collecting bets right now. Just trade to me." },
        ["Dealer Draw Messages"] =      new[] { "Let's get it started. The dealers first card is ..." },
        ["Player draw Messages"] =      new[] { "<t> needs another card. Your card is ..." },
        ["Hand Reaction Messages"] =    new[] { "Will it be enough for <t> to win?" },
        ["Win Messages"] =              new[] { "<t> won this round." },
        ["Push Messages"] =             new[] { "<t> got pushed." },
        ["Bust Messages"] =             new[] { "<t> busted. I'm so sorry." },
        ["Blackjack Dealer Messages"] = new[] { "Wohoo. I got a blackjack." },
        ["BlackJack Player Messages"] = new[] { "Wohoo. <t> got a blackjack." },
    };

    public static readonly List<UserRegexEntry> DefaultTradeRegexes = new()
    {
        new UserRegexEntry { Name = "Dice: Blackjack Logic",    Pattern = @"(\d+)\s*$",                                     Action = RegexAction.DiceRollValue, Mode = RegexEntryMode.Trigger },
        new UserRegexEntry { Name = "Trade: Inbound",           Pattern = @"^(.+) möchte mit dir handeln\.$",               Action = RegexAction.TradePartner,  Mode = RegexEntryMode.Trigger },
        new UserRegexEntry { Name = "Trade: Outbound",          Pattern = @"^Du hast (.+) einen Handel angeboten\.$",       Action = RegexAction.TradePartner,  Mode = RegexEntryMode.Trigger },
        new UserRegexEntry { Name = "Trade: Gil In",            Pattern = @"^Du erhältst ([\d.]+) Gil\.$",                  Action = RegexAction.TradeGilIn,    Mode = RegexEntryMode.Trigger },
        new UserRegexEntry { Name = "Trade: Gil Out",           Pattern = @"^Du überreichst ([\d.]+) Gil\.$",               Action = RegexAction.TradeGilOut,   Mode = RegexEntryMode.Trigger },
        new UserRegexEntry { Name = "Trade: Success",           Pattern = @"^Der Handel wurde abgeschlossen\.$",            Action = RegexAction.TradeCommit,   Mode = RegexEntryMode.Trigger },
        new UserRegexEntry { Name = "Trade: Cancel",            Pattern = @"^Der Handel wurde abgebrochen\.$",              Action = RegexAction.TradeCancel,   Mode = RegexEntryMode.Trigger },
        new UserRegexEntry { Name = "Party: Join",              Pattern = @"^(.+) ist der Gruppe beigetreten\.$",           Action = RegexAction.PartyJoin,     Mode = RegexEntryMode.Trigger },
        new UserRegexEntry { Name = "Party: Join Self",         Pattern = @"^Du bist der Gruppe von (.+) beigetreten\.$",   Action = RegexAction.PartyJoin,     Mode = RegexEntryMode.Trigger },
        new UserRegexEntry { Name = "Party: Leave",             Pattern = @"^(.+) hat die Gruppe verlassen\.$",             Action = RegexAction.PartyLeave,    Mode = RegexEntryMode.Trigger },
        new UserRegexEntry { Name = "Party: Removed",           Pattern = @"^(.+) wurde aus der Gruppe entfernt\.$",        Action = RegexAction.PartyLeave,    Mode = RegexEntryMode.Trigger },
        new UserRegexEntry { Name = "Party: Disband",           Pattern = @"^Deine Gruppe wurde aufgelöst\.$",              Action = RegexAction.PartyDisband,  Mode = RegexEntryMode.Trigger },
    };

    public List<MessageBatch> MessageBatches { get; set; } = new();

    public bool AllowEditingStandardRegex { get; set; } = false;

    public List<UserRegexEntry> UserRegexes { get; set; } = new();

    public bool DefaultBatchesSeeded { get; set; } = false;
    public bool DefaultRegexSeeded { get; set; } = false;

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

        public void ForceResetStandardRegexes()
        {
            var standardNames = DefaultTradeRegexes.Select(x => x.Name).ToList();
            UserRegexes.RemoveAll(r => standardNames.Contains(r.Name));

            foreach (var entry in DefaultTradeRegexes)
            {
                UserRegexes.Add(new UserRegexEntry
                {
                    Name = entry.Name,
                    Pattern = entry.Pattern,
                    Action = entry.Action,
                    Mode = entry.Mode,
                    Enabled = true
                    });
                }
                DefaultRegexSeeded = true;
            }

            public bool EnsureDefaultsOnce()
            {
                bool changed = false;

                if (!DefaultBatchesSeeded)
                {
                    ForceResetStandardBatches();
                    changed = true;
                }

                if (!DefaultRegexSeeded)
                {
                    ForceResetStandardRegexes();
                    changed = true;
                }

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
