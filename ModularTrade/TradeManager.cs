namespace ModularTrade;

/// <summary>
/// Static convenience facade for hosts that have one client and one framework update loop.
/// Call <see cref="Update"/> once per framework tick after <see cref="Initialize"/>.
/// </summary>
public static class TradeManager
{
    private static TradeEngine? _engine;
    private static ITradeGameAdapter? _adapter;
    private static TradeSnapshot? _lastResult;

    public static event Action<int, long[]>? onTradeChange;
    public static event Action<int, long[]>? onStateChange
    {
        add => onTradeChange += value;
        remove => onTradeChange -= value;
    }
    public static event Action<TradeEventType, TradeSnapshot>? TradeChanged;

    public static bool IsInitialized => _engine is not null;
    public static bool IsBusy => _engine?.IsBusy ?? false;
    public static TradeSnapshot? Current => _engine?.CurrentSnapshot;
    public static TradeSnapshot? LastResult => _lastResult;

    public static void Initialize(ITradeGameAdapter adapter, TradeOptions? options = null)
    {
        if (_engine is not null)
            throw new InvalidOperationException("ModularTrade is already initialized.");

        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _engine = new TradeEngine(adapter, options ?? new TradeOptions(), Emit);
    }

    public static Guid Trade(string recipient, long amount)
    {
        EnsureInitialized();
        return _engine!.Start(recipient, amount);
    }

    public static void Update()
    {
        _engine?.Tick();
    }

    public static void Cancel()
    {
        EnsureInitialized();
        _engine!.Cancel();
    }

    public static void Dispose()
    {
        if (_engine is null)
            return;
        if (_engine.IsBusy)
            throw new InvalidOperationException("Cancel and settle the active transfer before disposing ModularTrade.");

        _adapter?.Dispose();
        _engine = null;
        _adapter = null;
        _lastResult = null;
        onTradeChange = null;
        TradeChanged = null;
    }

    private static void Emit(TradeEventType type, TradeSnapshot snapshot)
    {
        _lastResult = snapshot;
        var data = type == TradeEventType.Progress
            ? new long[] { snapshot.CurrentStep, snapshot.TotalSteps, snapshot.ConfirmedAmount, snapshot.TotalAmount }
            : Array.Empty<long>();

        InvokeSafely(onTradeChange, handler => handler((int)type, (long[])data.Clone()));
        InvokeSafely(TradeChanged, handler => handler(type, snapshot));
    }

    private static void InvokeSafely<TDelegate>(TDelegate? callbacks, Action<TDelegate> invoke)
        where TDelegate : Delegate
    {
        if (callbacks is null)
            return;

        foreach (var callback in callbacks.GetInvocationList().Cast<TDelegate>())
        {
            try
            {
                invoke(callback);
            }
            catch (Exception ex)
            {
                _adapter?.Log("Trade event handler failed", ex);
            }
        }
    }

    private static void EnsureInitialized()
    {
        if (_engine is null)
            throw new InvalidOperationException("Call Initialize() before using ModularTrade.");
    }
}
