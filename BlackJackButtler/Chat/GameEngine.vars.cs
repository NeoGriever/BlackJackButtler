using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static partial class GameEngine
{
    public static GamePhase CurrentPhase = GamePhase.Waiting;

    private static bool _debugMode;

    private static readonly object _ctxLock = new();
    private static List<PlayerState>? _ctxPlayers;
    private static PlayerState? _ctxDealer;

    private static string _virtualTargetName = string.Empty;
    private static string _forcedRecipientName = string.Empty;

    public static void SetDebugMode(bool enabled) => _debugMode = enabled;

    private static bool IsHandDone(HandState h)
        => h.IsStand || h.IsBust || h.IsNaturalBlackJack;
}
