namespace ModularTrade;

/// <summary>Compatibility event codes exposed by <see cref="TradeManager"/>.</summary>
public enum TradeEventType
{
    Cancel = 0,
    Success = 1,
    Start = 2,
    Progress = 3,
}

public enum TradeState
{
    Idle,
    FindingTarget,
    Targeting,
    OpeningTrade,
    WaitingForTrade,
    OpeningGilInput,
    WaitingForNumericInput,
    SettingGil,
    VerifyingOffer,
    ConfirmingTrade,
    ConfirmingFinalDialog,
    AwaitingOutcome,
    BetweenTrades,
    Completed,
    Cancelled,
    Failed,
}

public enum TradeFailureReason
{
    None,
    UserCancelled,
    RemoteCancelled,
    TargetUnavailable,
    TargetChanged,
    InsufficientGil,
    UnexpectedOffer,
    UnexpectedDialog,
    Timeout,
    AdapterError,
    UnknownOutcome,
}

/// <summary>Only an adapter that can correlate a result to its active session may return success.</summary>
public enum GameTradeOutcome
{
    None,
    Succeeded,
    Cancelled,
    Unknown,
}
