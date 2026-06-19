using System;
using System.Collections.Generic;
using System.Linq;

namespace BlackJackButtler.Chat;

public static class ShortResultFormatter
{
    public static string Render(
        Configuration cfg,
        IReadOnlyList<string> winners,
        IReadOnlyList<string> pushed,
        IReadOnlyList<string> loosed,
        IReadOnlyList<string> busted)
    {
        var rules = cfg.ShortResultRules;
        if (rules == null || rules.Count == 0)
            rules = Configuration.CreateDefaultShortResultRules();

        var rows = rules.Select(rule => BuildRow(rule, winners, pushed, loosed, busted)).ToList();
        var visible = rows.Select(row => row.BaseVisible).ToArray();

        // The before/after predicates can affect one another. Three fixed-point passes
        // make the requested chained fallback rules deterministic without recursion.
        for (var pass = 0; pass < 3; pass++)
        {
            var next = new bool[rows.Count];
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!row.BaseVisible)
                    continue;

                var hasDataBefore = HasVisibleData(rows, visible, 0, i);
                var hasDataAfter = HasVisibleData(rows, visible, i + 1, rows.Count);

                if (row.Rule.VisibleIfContentBeforeIsEmpty && hasDataBefore)
                    continue;
                if (row.Rule.VisibleIfContentAfterIsEmpty && hasDataAfter)
                    continue;

                // A plain Data=None row is a separator. It is useful only between
                // two data-producing rows. Conditional Data=None rows are fallbacks.
                if (row.Rule.Data == ShortResultDataSource.None
                    && !row.Rule.VisibleIfContentBeforeIsEmpty
                    && !row.Rule.VisibleIfContentAfterIsEmpty
                    && (!hasDataBefore || !hasDataAfter))
                    continue;

                next[i] = true;
            }
            visible = next;
        }

        return string.Concat(rows.Where((_, i) => visible[i]).Select(row => row.Content));
    }

    private static ResultRow BuildRow(
        ShortResultRule rule,
        IReadOnlyList<string> winners,
        IReadOnlyList<string> pushed,
        IReadOnlyList<string> loosed,
        IReadOnlyList<string> busted)
    {
        IReadOnlyList<string> source = rule.Data switch
        {
            ShortResultDataSource.Winners => winners,
            ShortResultDataSource.Pushed => pushed,
            ShortResultDataSource.Loosed => loosed,
            ShortResultDataSource.Busted => busted,
            _ => Array.Empty<string>(),
        };

        var values = rule.Compress
            ? source.Distinct(StringComparer.OrdinalIgnoreCase)
            : source;
        var data = string.Join(", ", values);
        var isDataRow = rule.Data != ShortResultDataSource.None;
        var baseVisible = !isDataRow || data.Length > 0 || rule.VisibleIfEmpty;
        return new ResultRow(rule, baseVisible, isDataRow && baseVisible,
            (rule.Template ?? string.Empty).Replace("<data>", data));
    }

    private static bool HasVisibleData(IReadOnlyList<ResultRow> rows, IReadOnlyList<bool> visible, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (visible[i] && rows[i].ProducesDataContent)
                return true;
        }
        return false;
    }

    private sealed record ResultRow(
        ShortResultRule Rule,
        bool BaseVisible,
        bool ProducesDataContent,
        string Content);
}
