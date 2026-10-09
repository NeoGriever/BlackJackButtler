namespace ModularTrade;

/// <summary>
/// The only game-dependent boundary. Implementations must be called from the host's framework thread
/// and must never report <see cref="GameTradeOutcome.Succeeded"/> without session-level correlation.
/// </summary>
public interface ITradeGameAdapter : IDisposable
{
    DateTimeOffset Now { get; }
    bool IsTradeOpen { get; }
    bool IsNumericInputOpen { get; }
    bool IsTradeUiReady { get; }
    bool IsTradeButtonEnabled { get; }
    bool IsFinalTradeDialogVisible { get; }

    bool HasSufficientGil(int amount);
    bool IsCorrectRecipient(string recipient);
    bool FindAndTargetPlayer(string recipient);

    bool TrySendTradeRequest();
    bool TryOpenGilInput();
    bool TrySetGil(int amount);
    bool TryClickTradeButton();
    bool TryConfirmFinalDialog();
    void AbortOpenTrade();

    GameTradeOutcome ConsumeOutcome();
    void ResetObservedOutcome();
    void Log(string message, Exception? error = null);
}
