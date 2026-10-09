namespace ModularTrade;

/// <summary>
/// Framework-tick driven transfer state machine. It performs at most one outgoing game action per tick.
/// A closed trade window is deliberately never considered proof of payment on its own.
/// </summary>
public sealed class TradeEngine
{
    private readonly ITradeGameAdapter _io;
    private readonly TradeOptions _options;
    private readonly Action<TradeEventType, TradeSnapshot> _notify;

    private TradeSession? _session;
    private TradeState _state = TradeState.Idle;
    private DateTimeOffset _stateEntered;
    private DateTimeOffset _nextActionAt;
    private GameTradeOutcome _observedOutcome;
    private bool _paymentMayHaveBeenCommitted;

    public TradeEngine(
        ITradeGameAdapter io,
        TradeOptions options,
        Action<TradeEventType, TradeSnapshot> notify)
    {
        _io = io ?? throw new ArgumentNullException(nameof(io));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _notify = notify ?? throw new ArgumentNullException(nameof(notify));
    }

    public bool IsBusy => _session is not null;
    public TradeSnapshot? CurrentSnapshot => _session?.Snapshot(_state);

    public Guid Start(string recipient, long amount)
    {
        if (IsBusy)
            throw new InvalidOperationException("A transfer is already active.");
        if (string.IsNullOrWhiteSpace(recipient))
            throw new ArgumentException("A recipient is required.", nameof(recipient));

        var plan = TradePlanner.CreatePlan(amount, _options.MaxGilPerTrade, _options.MaxTradeCount);
        _session = new TradeSession(recipient.Trim(), amount, plan);
        _observedOutcome = GameTradeOutcome.None;
        _paymentMayHaveBeenCommitted = false;
        _io.ResetObservedOutcome();
        Enter(TradeState.FindingTarget);
        _io.Log($"Started session {_session.Id}: recipient={_session.Recipient}, total={amount}, plan=[{string.Join(',', plan)}]");
        Emit(TradeEventType.Start);
        return _session.Id;
    }

    public void Tick()
    {
        if (_session is null)
            return;

        try
        {
            ObserveOutcome();
            if (CheckForTerminalOutcome())
                return;

            if (HasTimedOut())
            {
                End(
                    TradeEventType.Cancel,
                    TradeState.Failed,
                    _paymentMayHaveBeenCommitted
                        ? TradeFailureReason.UnknownOutcome
                        : TradeFailureReason.Timeout);
                return;
            }

            // Cooldowns throttle writes only. Outcome observation and timeout checks keep running.
            if (_io.Now < _nextActionAt)
                return;

            var amount = _session.CurrentTradeAmount;
            switch (_state)
            {
                case TradeState.FindingTarget:
                    if (_io.FindAndTargetPlayer(_session.Recipient))
                        Enter(TradeState.Targeting);
                    break;

                case TradeState.Targeting:
                    if (!_io.IsCorrectRecipient(_session.Recipient))
                    {
                        Enter(TradeState.FindingTarget);
                        break;
                    }

                    if (_io.TrySendTradeRequest())
                    {
                        Enter(TradeState.OpeningTrade);
                        Gate(_options.CommandCooldown);
                    }
                    break;

                case TradeState.OpeningTrade:
                    if (_io.IsTradeOpen)
                        Enter(TradeState.WaitingForTrade);
                    break;

                case TradeState.WaitingForTrade:
                    if (!_io.IsTradeUiReady)
                        break;
                    if (!_io.IsCorrectRecipient(_session.Recipient))
                    {
                        End(TradeEventType.Cancel, TradeState.Failed, TradeFailureReason.TargetChanged);
                        break;
                    }
                    if (!_io.HasSufficientGil(amount))
                    {
                        End(TradeEventType.Cancel, TradeState.Failed, TradeFailureReason.InsufficientGil);
                        break;
                    }
                    Enter(TradeState.OpeningGilInput);
                    break;

                case TradeState.OpeningGilInput:
                    if (_io.TryOpenGilInput())
                        Enter(TradeState.WaitingForNumericInput);
                    break;

                case TradeState.WaitingForNumericInput:
                    if (_io.IsNumericInputOpen)
                        Enter(TradeState.SettingGil);
                    break;

                case TradeState.SettingGil:
                    if (_io.TrySetGil(amount))
                        Enter(TradeState.VerifyingOffer);
                    break;

                case TradeState.VerifyingOffer:
                    // The current client does not publish the locally entered Gil amount in
                    // TradeItemsLocal. Dropbox likewise relies on the InputNumeric callback
                    // and waits for the dialog to close; the authoritative verification is the
                    // subsequent localized system message, before another trade may begin.
                    if (!_io.IsNumericInputOpen)
                        Enter(TradeState.ConfirmingTrade);
                    break;

                case TradeState.ConfirmingTrade:
                    if (!PreConfirmChecks())
                        break;
                    if (_io.IsTradeButtonEnabled && _io.TryClickTradeButton())
                    {
                        // The server may accept from this point onwards. Never retry this amount
                        // after a subsequent timeout or ambiguous window close.
                        _paymentMayHaveBeenCommitted = true;
                        Enter(TradeState.ConfirmingFinalDialog);
                        Gate(_options.ButtonCooldown);
                    }
                    break;

                case TradeState.ConfirmingFinalDialog:
                    if (!PreConfirmChecks())
                        break;
                    if (_io.IsFinalTradeDialogVisible && _io.TryConfirmFinalDialog())
                    {
                        Enter(TradeState.AwaitingOutcome);
                        Gate(_options.DialogCooldown);
                    }
                    break;

                case TradeState.AwaitingOutcome:
                    // CheckForTerminalOutcome handles terminal failures before this switch.
                    if (!_io.IsTradeOpen && _observedOutcome == GameTradeOutcome.Succeeded)
                    {
                        _session.MarkCurrentTradeVerified();
                        Emit(TradeEventType.Progress, reportCompletedStep: true);

                        if (_session.IsComplete)
                        {
                            End(TradeEventType.Success, TradeState.Completed, TradeFailureReason.None);
                        }
                        else
                        {
                            _observedOutcome = GameTradeOutcome.None;
                            _paymentMayHaveBeenCommitted = false;
                            _io.ResetObservedOutcome();
                            Enter(TradeState.BetweenTrades);
                            Gate(_options.BetweenTradesDelay);
                        }
                    }
                    break;

                case TradeState.BetweenTrades:
                    Enter(TradeState.FindingTarget);
                    break;
            }
        }
        catch (Exception ex)
        {
            _io.Log("Trade engine error", ex);
            End(
                TradeEventType.Cancel,
                TradeState.Failed,
                _paymentMayHaveBeenCommitted
                    ? TradeFailureReason.UnknownOutcome
                    : TradeFailureReason.AdapterError);
        }
    }

    public void Cancel()
    {
        if (_session is null)
            return;

        _io.Log($"Cancellation requested for session {_session.Id}; paymentMayHaveBeenCommitted={_paymentMayHaveBeenCommitted}.");
        End(
            TradeEventType.Cancel,
            TradeState.Cancelled,
            _paymentMayHaveBeenCommitted
                ? TradeFailureReason.UnknownOutcome
                : TradeFailureReason.UserCancelled);
    }

    private void ObserveOutcome()
    {
        if (_state != TradeState.AwaitingOutcome)
            return;

        var outcome = _io.ConsumeOutcome();
        if (outcome != GameTradeOutcome.None)
            _observedOutcome = outcome;
    }

    private bool CheckForTerminalOutcome()
    {
        if (_state != TradeState.AwaitingOutcome)
            return false;

        if (_observedOutcome == GameTradeOutcome.Cancelled)
        {
            End(TradeEventType.Cancel, TradeState.Cancelled, TradeFailureReason.RemoteCancelled);
            return true;
        }
        if (_observedOutcome == GameTradeOutcome.Unknown)
        {
            End(TradeEventType.Cancel, TradeState.Failed, TradeFailureReason.UnknownOutcome);
            return true;
        }
        return false;
    }

    private bool PreConfirmChecks()
    {
        if (_session is null || !_io.IsTradeOpen || !_io.IsCorrectRecipient(_session.Recipient))
        {
            End(TradeEventType.Cancel, TradeState.Failed, TradeFailureReason.TargetChanged);
            return false;
        }
        return true;
    }

    private bool HasTimedOut()
    {
        var timeout = _state switch
        {
            TradeState.FindingTarget => _options.FindTargetTimeout,
            TradeState.OpeningTrade => _options.OpenTradeTimeout,
            TradeState.WaitingForTrade or
            TradeState.OpeningGilInput or
            TradeState.WaitingForNumericInput or
            TradeState.SettingGil or
            TradeState.VerifyingOffer or
            TradeState.ConfirmingTrade or
            TradeState.ConfirmingFinalDialog => _options.UiTimeout,
            TradeState.AwaitingOutcome => _options.OutcomeTimeout,
            _ => TimeSpan.Zero,
        };
        return timeout > TimeSpan.Zero && _io.Now - _stateEntered > timeout;
    }

    private void Enter(TradeState state)
    {
        var previous = _state;
        _state = state;
        _stateEntered = _io.Now;
        _nextActionAt = _io.Now;
        _io.Log($"State {previous} -> {state}.");
    }

    private void Gate(TimeSpan delay) => _nextActionAt = _io.Now + delay;

    private void Emit(TradeEventType type, bool reportCompletedStep = false)
    {
        if (_session is not null)
            _notify(type, _session.Snapshot(_state, reportCompletedStep: reportCompletedStep));
    }

    private void End(TradeEventType type, TradeState finalState, TradeFailureReason reason)
    {
        if (_session is null)
            return;

        var finished = _session.Snapshot(finalState, reason);
        _io.Log($"Terminal event={type}, state={finalState}, reason={reason}, confirmed={finished.ConfirmedAmount}/{finished.TotalAmount}.");

        // Before the offer was locked, a failed or cancelled session can close
        // its own still-open Trade window safely. After clicking the trade
        // button, the server outcome may already be pending; leave it untouched
        // and report UnknownOutcome instead of risking an incorrect conclusion.
        if (finalState is TradeState.Failed or TradeState.Cancelled && !_paymentMayHaveBeenCommitted)
        {
            try
            {
                _io.AbortOpenTrade();
            }
            catch (Exception ex)
            {
                _io.Log("Could not close the failed trade window", ex);
            }
        }

        _state = finalState;
        _session = null;
        _notify(type, finished);
    }
}
