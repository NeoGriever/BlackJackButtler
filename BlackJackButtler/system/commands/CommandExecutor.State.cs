using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;
using RRX = System.Text.RegularExpressions;
using System.Globalization;

namespace BlackJackButtler;

public static partial class CommandExecutor
{
    private static readonly RRX.Regex StackTokenRegex = new(@"#\{([^}]+)\}", RRX.RegexOptions.Compiled);
    private static readonly RRX.Regex DicePartyRegex = new(@"^/dice\s+(?:party|p|alliance|al)\s+(\d+)\s*$", RRX.RegexOptions.Compiled | RRX.RegexOptions.IgnoreCase);
    private const float MinCommandDelay = 0.05f;
    private const int MaxInternalDepth = 5;
    private static int _internalDepth = 0;
    private static volatile bool _isRunning = false;
    public static bool IsRunning => _isRunning;

    private static volatile bool _followUpPending = false;
    public static bool IsFollowUpPending => _followUpPending;
    public static void SignalFollowUpPending() => _followUpPending = true;
    public static void ClearFollowUpPending() => _followUpPending = false;

    public static event Action? OnGroupCompleted;

    private static bool _wait = false;
    private static bool _cancel = false;
    private static int _expectedDiceSides;
    private static bool _expectsPlayerDice;
    private static string _expectedDicePlayerName = string.Empty;
    private static uint _expectedDicePlayerWorldId;

    private static string _currentGroupName = string.Empty;
    private static string _currentTargetPlayer = string.Empty;
    private static bool _currentGroupHasDice = false;
    private static int _preActionSnapshotIndex = -1;
    private static CancellationTokenSource? _delayCts = null;

    public static string CurrentGroupName => _currentGroupName;
    public static string CurrentTargetPlayer => _currentTargetPlayer;
    public static bool CurrentGroupHasDice => _currentGroupHasDice;
    public static int PreActionSnapshotIndex => _preActionSnapshotIndex;
    public static bool IsCancelling => _cancel;

    private static readonly HashSet<string> StateGroupNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "StateHSDTS", "StateHSTS", "StateHSDT", "StateHST", "StateHSDS", "StateHSD", "StateHS",
    };

    public static string LastStateGroupName { get; private set; } = string.Empty;
    public static string LastStateTargetName { get; private set; } = string.Empty;
    public static DateTime LastStateFiredAt { get; private set; } = DateTime.MinValue;

    public static void ClearLastState()
    {
        LastStateGroupName = string.Empty;
        LastStateTargetName = string.Empty;
        LastStateFiredAt = DateTime.MinValue;
    }
}
