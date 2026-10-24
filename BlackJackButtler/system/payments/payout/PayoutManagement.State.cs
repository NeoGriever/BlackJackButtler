using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using BlackJackButtler.Chat;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.ChatMethods;
using ECommons.DalamudServices;
using ECommons.Reflection;
using FFXIVClientStructs.FFXIV.Component.GUI;
using static ECommons.GenericHelpers;

namespace BlackJackButtler;

public static partial class PayoutManagement
{
    private sealed record QueuedPayout(string PlayerName, uint WorldId, long Amount);

    private const long MaxGilPerTrade = 1_000_000;
    private const double TargetSettleSeconds = 0.45;
    private const double DropboxTradeOpenTimeoutSeconds = 30.0;
    private const double TradeClosedSettleSeconds = 1.0;

    private enum PayoutState
    {
        Idle,
        Targeting,
        OpeningTrade,
        WaitingTradeOpen,
        WaitingTradeClose,
        TradeClosedSettle,
    }

    private enum TradeExecutor
    {
        None,
        ModularTrade,
        Dropbox,
    }

    public enum PayoutStartResult
    {
        Started,
        InvalidAmount,
        InsufficientFunds,
        AlreadyActive,
    }

    private static PayoutState _state = PayoutState.Idle;
    private static DateTime _stateEnteredAt = DateTime.UtcNow;
    private static string _sourceName = string.Empty;
    private static uint _sourceWorldId;
    private static string _targetName = string.Empty;
    private static uint _targetWorldId;
    private static long _startAmount;
    private static long _remainingAmount;
    private static long _lastKnownBank;
    private static long _currentChunk;
    private static bool _cancelRequested;
    private static bool _openedDropboxUiForPayout;
    private static long _bankBeforeCurrentTrade;
    private static TradeExecutor _executor = TradeExecutor.None;
    private static readonly Queue<QueuedPayout> QueuedPayouts = new();
#if DEBUG
    private static bool _isResetting;
#endif
    private static bool _modularTradeInitialized;

    public readonly record struct DropboxStatus(bool Installed, bool Active);

    public static bool IsActive => _state != PayoutState.Idle;
    public static string CurrentTargetName => _targetName;
}
