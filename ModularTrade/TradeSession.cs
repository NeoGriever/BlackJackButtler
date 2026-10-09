namespace ModularTrade;

internal sealed class TradeSession
{
    public TradeSession(string recipient, long totalAmount, IReadOnlyList<int> plan)
    {
        Recipient = recipient;
        TotalAmount = totalAmount;
        Plan = plan.Count > 0 ? plan : throw new ArgumentException("A transfer plan cannot be empty.", nameof(plan));
    }

    public Guid Id { get; } = Guid.NewGuid();
    public string Recipient { get; }
    public long TotalAmount { get; }
    public IReadOnlyList<int> Plan { get; }
    public int CompletedSteps { get; private set; }
    public long ConfirmedAmount { get; private set; }
    public bool IsComplete => CompletedSteps == Plan.Count;
    public int CurrentTradeAmount => !IsComplete
        ? Plan[CompletedSteps]
        : throw new InvalidOperationException("The transfer is already complete.");

    public void MarkCurrentTradeVerified()
    {
        if (IsComplete)
            throw new InvalidOperationException("The transfer is already complete.");

        ConfirmedAmount = checked(ConfirmedAmount + Plan[CompletedSteps]);
        CompletedSteps++;
    }

    public TradeSnapshot Snapshot(
        TradeState state,
        TradeFailureReason reason = TradeFailureReason.None,
        bool reportCompletedStep = false)
    {
        var currentStep = reportCompletedStep || IsComplete
            ? CompletedSteps
            : CompletedSteps + 1;
        return new TradeSnapshot(
            Id,
            Recipient,
            state,
            currentStep,
            Plan.Count,
            ConfirmedAmount,
            TotalAmount,
            reason);
    }
}
