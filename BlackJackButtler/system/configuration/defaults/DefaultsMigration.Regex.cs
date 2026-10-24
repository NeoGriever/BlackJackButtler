using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlackJackButtler.Regex;
using Newtonsoft.Json;

namespace BlackJackButtler;

public static partial class DefaultsMigration
{
    private static void FixGilRegexPatterns(Configuration config)
    {
        var names = new[] { "Trade: Gil In", "Trade: Gil Out" };
        foreach (var name in names)
        {
            var entry = config.UserRegexes.FirstOrDefault(x => x.Name == name);
            if (entry == null) continue;

            for (int i = 0; i < entry.Patterns.Count; i++)
            {
                if (entry.Patterns[i].Contains(@"[\d.]") && entry.Patterns[i].Contains("gil"))
                {
                    entry.Patterns[i] = entry.Patterns[i].Replace(@"[\d.]", @"[\d,]");
                    Plugin.Log.Information($"[DefaultsMigration] Fixed Gil pattern (period->comma) in: {name}");
                }
            }
        }
    }

    internal static bool EnsureGameplayRegexPatterns(Configuration config)
    {
        if (config.GameplayRegexPatternsMigrated)
            return false;

        var changed = false;
        changed |= EnsureRegexPatterns(
            config,
            RegexAction.BankTell,
            "Bank Tell",
            "^bank\\?$",
            "^bank please\\.?$",
            "^what does my bank say\\??$"
        );
        changed |= EnsureRegexPatterns(
            config,
            RegexAction.NextRound,
            "New Round",
            "^ready\\s*[!?.]*$",
            "^rdy$",
            "^let'?s go[!?.]*$"
        );

        config.GameplayRegexPatternsMigrated = true;
        return true;
    }

    private static bool EnsureRegexPatterns(
        Configuration config,
        RegexAction action,
        string fallbackName,
        params string[] patterns
    )
    {
        var entry = config.UserRegexes.FirstOrDefault(x => x.Action == action)
            ?? config.UserRegexes.FirstOrDefault(x => x.Name.Equals(fallbackName, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
            return false;

        entry.Patterns ??= new List<string>();
        var changed = false;
        foreach (var pattern in patterns)
        {
            if (entry.Patterns.Any(x => x.Equals(pattern, StringComparison.Ordinal)))
                continue;

            entry.Patterns.Add(pattern);
            changed = true;
            Plugin.Log.Information($"[DefaultsMigration] Added gameplay regex pattern to {entry.Name}: {pattern}");
        }

        return changed;
    }

    internal static bool EnsureBankTransferRegex(Configuration config)
    {
        var entry = config.UserRegexes.FirstOrDefault(
            x =>
                x.Action == RegexAction.BankTransfer
                || x.Name.Equals("Bank Transfer", StringComparison.OrdinalIgnoreCase)
        );
        var changed = false;
        if (entry == null)
        {
            config.UserRegexes.Add(
                new UserRegexEntry
                {
                    Name = "Bank Transfer",
                    Patterns = new List<string>
                    {
                        @"^transfer\s+(-?(?:(?:\d[\d.,]*\s*[km]?)|half|50%|min|max))\s*$"
                    },
                    Action = RegexAction.BankTransfer,
                    Mode = RegexEntryMode.Trigger,
                    Enabled = true,
                }
            );
            changed = true;
            Plugin.Log.Information("[DefaultsMigration] Added Bank Transfer regex entry");
        }

        if (!config.BankTransferRegexMigrated)
        {
            config.BankTransferRegexMigrated = true;
            changed = true;
        }

        return changed;
    }
}
