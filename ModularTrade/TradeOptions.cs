namespace ModularTrade;

/// <summary>Limits and non-blocking action intervals used by a <see cref="TradeEngine"/>.</summary>
public sealed record TradeOptions
{
    public int MaxGilPerTrade { get; init; } = 1_000_000;
    public int MaxTradeCount { get; init; } = 10_000;
    public TimeSpan FindTargetTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan OpenTradeTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan UiTimeout { get; init; } = TimeSpan.FromSeconds(12);
    public TimeSpan OutcomeTimeout { get; init; } = TimeSpan.FromSeconds(45);
    public TimeSpan BetweenTradesDelay { get; init; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan CommandCooldown { get; init; } = TimeSpan.FromSeconds(3);
    public TimeSpan ButtonCooldown { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan DialogCooldown { get; init; } = TimeSpan.FromSeconds(2);
}
