namespace ModularTrade;

/// <summary>Pure, overflow-safe planning for a Gil transfer.</summary>
public static class TradePlanner
{
    public const int GameMaximumGilPerTrade = 1_000_000;

    public static IReadOnlyList<int> CreatePlan(
        long totalGil,
        int maxGilPerTrade = GameMaximumGilPerTrade,
        int maxTradeCount = 10_000)
    {
        if (totalGil <= 0)
            throw new ArgumentOutOfRangeException(nameof(totalGil));
        if (maxGilPerTrade is <= 0 or > GameMaximumGilPerTrade)
            throw new ArgumentOutOfRangeException(nameof(maxGilPerTrade));
        if (maxTradeCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTradeCount));

        // Do not calculate (totalGil + maxGilPerTrade - 1): that can overflow long.
        var stepCount = 1 + (totalGil - 1) / maxGilPerTrade;
        if (stepCount > maxTradeCount)
            throw new ArgumentOutOfRangeException(
                nameof(totalGil),
                $"Transfer requires {stepCount} trades; configured maximum is {maxTradeCount}.");

        var plan = new List<int>(checked((int)stepCount));
        for (var remaining = totalGil; remaining > 0; remaining -= Math.Min(remaining, maxGilPerTrade))
            plan.Add(checked((int)Math.Min(remaining, maxGilPerTrade)));

        return plan;
    }
}
