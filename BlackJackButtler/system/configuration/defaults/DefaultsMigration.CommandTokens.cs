using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlackJackButtler.Regex;
using Newtonsoft.Json;

namespace BlackJackButtler;

public static partial class DefaultsMigration
{
    internal static bool MigrateTellDotToken(Configuration config)
    {
        if (config.DotTokenMigrated) return false;
        config.DotTokenMigrated = true;

        foreach (var group in config.CommandGroups.Concat(config.CustomCommandGroups))
        {
            foreach (var cmd in group.Commands)
            {
                if (string.IsNullOrWhiteSpace(cmd.Text)) continue;
                var trimmed = cmd.Text.TrimStart();
                if (!trimmed.StartsWith("/tell <t>", StringComparison.OrdinalIgnoreCase) &&
                    !trimmed.StartsWith("/t <t>", StringComparison.OrdinalIgnoreCase))
                    continue;

                int idx = cmd.Text.IndexOf("<t>", StringComparison.Ordinal);
                if (idx >= 0)
                    cmd.Text = string.Concat(cmd.Text.AsSpan(0, idx), "<.>", cmd.Text.AsSpan(idx + 3));
            }
        }

        return true;
    }
}
