namespace ModularTrade;

/// <summary>An immutable view of one transfer. RemainingAmount is not safe to retry after UnknownOutcome.</summary>
public sealed record TradeSnapshot(
    Guid SessionId,
    string Recipient,
    TradeState State,
    int CurrentStep,
    int TotalSteps,
    long ConfirmedAmount,
    long TotalAmount,
    TradeFailureReason FailureReason = TradeFailureReason.None)
{
    public long RemainingAmount => TotalAmount - ConfirmedAmount;
}

public sealed record TradeResult(bool Succeeded, TradeSnapshot FinalSnapshot);
