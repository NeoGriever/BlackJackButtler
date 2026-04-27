using System;
using System.Collections.Generic;
using System.Linq;
using BlackJackButtler.Regex;

namespace BlackJackButtler;

public static class DefaultsManagerV2
{
    internal static DefaultsManager.DefaultsContainer? GetRawContainer()
    {
        return DefaultsManager.GetRawContainer();
    }

    public static List<MessageBatch> GetDefaultMessages()
    {
        var data = GetRawContainer();
        var dict = data?.Messages ?? new Dictionary<string, List<string>>();
        return dict.Select(kv => new MessageBatch
        {
            Name = kv.Key,
            Messages = new List<string>(kv.Value)
        }).ToList();
    }

    public static List<UserRegexEntry> GetDefaultRegex()
    {
        var data = GetRawContainer();
        var list = data?.TradeRegex ?? new List<DefaultsManager.TradeRegexDto>();
        return list.Select(r => new UserRegexEntry
        {
            Name = r.Name ?? "",
            Patterns = r.Patterns ?? new List<string>(),
            Action = Enum.TryParse<RegexAction>(r.Action, out var act) ? act : RegexAction.None,
            Mode = RegexEntryMode.Trigger,
            Enabled = true
        }).ToList();
    }

    public static List<CommandGroup> GetDefaultCommands()
    {
        var data = GetRawContainer();
        var dict = data?.Commands ?? new Dictionary<string, List<DefaultsManager.CommandDto>>();
        return dict.Select(kv =>
        {
            var g = new CommandGroup { Name = kv.Key };
            g.Commands.AddRange(kv.Value.Select(c => new PluginCommand
            {
                Text = c.Text ?? "",
                Delay = c.Delay
            }));
            return g;
        }).ToList();
    }
}
