using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static partial class GameEngine
{
    public static GamePhase CurrentPhase = GamePhase.Waiting;

    private static bool _debugMode;
    private static int _payoutOutputComplete = 1;

    private static readonly object _ctxLock = new();
    private static List<PlayerState>? _ctxPlayers;
    private static PlayerState? _ctxDealer;

    internal static int _payoutGuard = 0;

    private static string _virtualTargetName = string.Empty;
    private static string _forcedRecipientName = string.Empty;

    public static void SetDebugMode(bool enabled) => _debugMode = enabled;

    public static bool IsPayoutOutputComplete => Volatile.Read(ref _payoutOutputComplete) != 0;

    public static void BeginPayoutOutput()
    {
        CurrentPhase = GamePhase.Payout;
        Volatile.Write(ref _payoutOutputComplete, 0);
    }

    public static void MarkPayoutOutputComplete()
        => Volatile.Write(ref _payoutOutputComplete, 1);

    public static bool CanAcceptInterRoundDetectors()
        => CurrentPhase == GamePhase.Waiting
           || (CurrentPhase == GamePhase.Payout && IsPayoutOutputComplete);

    private static bool IsHandDone(HandState h)
        => h.IsStand || h.IsBust || h.IsNaturalBlackJack || h.IsCharlie;
}
