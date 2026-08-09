using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using BlackJackButtler.Chat;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using static ECommons.GenericHelpers;

namespace BlackJackButtler;

public static class PayoutManagement
{
    private const long MaxGilPerTrade = 1_000_000;
    private const double TargetSettleSeconds = 0.45;
    private const double TradeOpenTimeoutSeconds = 8.0;
    private const double DropboxTradeOpenTimeoutSeconds = 30.0;
    private const double AddonStepTimeoutSeconds = 5.0;
    private const double TradeClosedSettleSeconds = 1.0;
    private const double ActionThrottleSeconds = 0.20;

    private enum PayoutState
    {
        Idle,
        Targeting,
        OpeningTrade,
        WaitingTradeOpen,
        OpeningGilInput,
        SettingGil,
        ConfirmingTrade,
        WaitingTradeClose,
        TradeClosedSettle,
    }

    private enum TradeExecutor
    {
        None,
        Dropbox,
        LocalClone,
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
    private static string _targetName = string.Empty;
    private static uint _targetWorldId;
    private static long _startAmount;
    // Tracks the requested payout independently of the player's remaining bank.  This lets a
    // withdrawal settle after its requested amount even when the bank still contains Gil.
    private static long _remainingAmount;
    private static long _lastKnownBank;
    private static long _currentChunk;
    private static bool _cancelRequested;
    private static bool _confirmAllowed;
    private static bool _sentTradeCommand;
    private static bool _openedDropboxUiForPayout;
    private static bool _currentTradeAutoConfirm;
    private static long _bankBeforeCurrentTrade;
    private static DateTime _lastTradeActionAt = DateTime.MinValue;
    private static TradeExecutor _executor = TradeExecutor.None;

    public static bool IsActive => _state != PayoutState.Idle;
    public static string CurrentTargetName => _targetName;

    public static void StartPayout(PlayerState p)
    {
        _ = TryStartPayout(p, p.Bank);
    }

    public static PayoutStartResult TryStartPayout(PlayerState p, long amount)
    {
        if (amount <= 0)
            return PayoutStartResult.InvalidAmount;
        if (amount > p.Bank)
            return PayoutStartResult.InsufficientFunds;
        if (IsActive)
            return PayoutStartResult.AlreadyActive;

        _targetName = p.Name;
        _targetWorldId = p.WorldId;
        _startAmount = amount;
        _remainingAmount = amount;
        _lastKnownBank = p.Bank;
        _currentChunk = Math.Min(amount, MaxGilPerTrade);
        _cancelRequested = false;
        _confirmAllowed = false;
        _sentTradeCommand = false;
        _openedDropboxUiForPayout = false;
        _currentTradeAutoConfirm = false;
        _bankBeforeCurrentTrade = p.Bank;
        _lastTradeActionAt = DateTime.MinValue;
        _executor = TradeExecutor.None;

        SetState(PayoutState.Targeting,
            $"Started for {p.DisplayName}, payout={amount:N0}, bank={p.Bank:N0}, chunk={_currentChunk:N0}");
        return PayoutStartResult.Started;
    }

    public static bool TryParseWithdrawAmount(string rawAmount, long availableBank, out long amount)
    {
        amount = 0;
        var text = (rawAmount ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(text) || text.Contains('-'))
            return false;

        if (text is "all" or "everything")
        {
            amount = availableBank;
            return amount > 0;
        }

        var multiplier = 1L;
        var hasSuffix = false;
        if (text.EndsWith("k", StringComparison.Ordinal))
        {
            multiplier = 1_000L;
            hasSuffix = true;
            text = text[..^1].Trim();
        }
        else if (text.EndsWith("m", StringComparison.Ordinal))
        {
            multiplier = 1_000_000L;
            hasSuffix = true;
            text = text[..^1].Trim();
        }

        if (text.Any(ch => !char.IsDigit(ch) && ch != '.' && ch != ',' && !char.IsWhiteSpace(ch))
            || !TryParsePayoutNumber(text, hasSuffix, out var parsed))
            return false;

        amount = SafeMultiply(parsed, multiplier);
        return amount > 0;
    }

    public static void Tick()
    {
        if (!IsActive) return;

        if (_cancelRequested)
        {
            Reset("Cancelled");
            return;
        }

        var player = GetCurrentPlayer();
        if (player == null)
        {
            Reset($"Player not found: {_targetName}");
            return;
        }

        if (_remainingAmount <= 0)
        {
            Reset("Payout complete");
            return;
        }

        var settlementPending = _state is PayoutState.ConfirmingTrade
            or PayoutState.WaitingTradeClose
            or PayoutState.TradeClosedSettle;
        if (player.Bank <= 0 && !settlementPending)
        {
            Reset("Bank reached 0 before requested payout completed");
            return;
        }

        _lastKnownBank = player.Bank;

        switch (_state)
        {
            case PayoutState.Targeting:
                TargetPayoutPlayer(player);
                SetState(PayoutState.OpeningTrade, "Target/focus target set");
                break;

            case PayoutState.OpeningTrade:
                if (StateElapsed < TargetSettleSeconds) return;
                if (!IsPayoutTargetSelected())
                {
                    TargetPayoutPlayer(player);
                    return;
                }

                _currentChunk = Math.Min(_remainingAmount, Math.Min(player.Bank, MaxGilPerTrade));
                if (_currentChunk <= 0)
                {
                    Reset("No Gil available for remaining payout");
                    return;
                }
                _confirmAllowed = false;
                _bankBeforeCurrentTrade = player.Bank;
                _currentTradeAutoConfirm = false;

                if (TryStartDropboxPayout(player, (int)_currentChunk))
                {
                    _executor = TradeExecutor.Dropbox;
                    _sentTradeCommand = true;
                    SetState(PayoutState.WaitingTradeOpen, "Dropbox payout task queued");
                    return;
                }

                _executor = TradeExecutor.LocalClone;
                _currentTradeAutoConfirm = true;
                _sentTradeCommand = true;
                ChatCommandRouter.Send("/trade", Plugin.Instance.Configuration, "PayoutManagement");
                SetState(PayoutState.WaitingTradeOpen, "Local Dropbox clone trade command sent");
                break;

            case PayoutState.WaitingTradeOpen:
                if (IsTradeOpen())
                {
                    if (_executor == TradeExecutor.Dropbox)
                    {
                        SetState(PayoutState.WaitingTradeClose, "Dropbox opened trade");
                        return;
                    }

                    SetState(PayoutState.OpeningGilInput, "Trade opened");
                    return;
                }
                if (StateElapsed >= (_executor == TradeExecutor.Dropbox ? DropboxTradeOpenTimeoutSeconds : TradeOpenTimeoutSeconds))
                    Reset("Timed out waiting for Trade addon");
                break;

            case PayoutState.OpeningGilInput:
                if (!IsTradeOpen())
                {
                    RetryOrResetAfterUnexpectedTradeClose(player);
                    return;
                }
                if (TryOpenGilInput())
                    SetState(PayoutState.SettingGil, "Gil input opened");
                else if (StateElapsed >= AddonStepTimeoutSeconds)
                    Reset("Timed out opening Gil input");
                break;

            case PayoutState.SettingGil:
                if (!IsTradeOpen())
                {
                    RetryOrResetAfterUnexpectedTradeClose(player);
                    return;
                }
                if (TrySetNumericInput((int)_currentChunk))
                {
                    _confirmAllowed = true;
                    if (_currentTradeAutoConfirm)
                        SetState(PayoutState.ConfirmingTrade, $"Gil set: {_currentChunk:N0}");
                    else
                        SetState(PayoutState.WaitingTradeClose, $"Gil set, waiting for manual confirmation: {_currentChunk:N0}");
                }
                else if (StateElapsed >= AddonStepTimeoutSeconds)
                {
                    Reset("Timed out setting Gil amount");
                }
                break;

            case PayoutState.ConfirmingTrade:
                if (!IsTradeOpen())
                {
                    SetState(PayoutState.TradeClosedSettle, "Trade closed after confirm");
                    return;
                }
                TryConfirmTrade();
                TryConfirmYesNo();
                break;

            case PayoutState.WaitingTradeClose:
                if (!IsTradeOpen())
                    SetState(PayoutState.TradeClosedSettle, "Trade closed");
                break;

            case PayoutState.TradeClosedSettle:
                if (StateElapsed < TradeClosedSettleSeconds) return;
                var transferred = _bankBeforeCurrentTrade - player.Bank;
                if (transferred <= 0)
                {
                    Reset("Trade cancelled or closed without payout");
                    return;
                }
                if (transferred != _currentChunk)
                {
                    Reset($"Unexpected payout amount: expected {_currentChunk:N0}, received {transferred:N0}");
                    return;
                }

                _remainingAmount -= _currentChunk;
                if (_remainingAmount <= 0)
                {
                    Reset("Payout complete");
                    return;
                }
                if (player.Bank <= 0)
                {
                    Reset("Bank reached 0 before requested payout completed");
                    return;
                }
                _sentTradeCommand = false;
                _confirmAllowed = false;
                _currentTradeAutoConfirm = false;
                _executor = TradeExecutor.None;
                SetState(PayoutState.Targeting,
                    $"Remaining payout={_remainingAmount:N0}, bank={player.Bank:N0}, continuing");
                break;
        }
    }

    public static void DrawHelperWindow()
    {
        if (!IsActive) return;

        var mainWindow = Plugin.Instance.GetMainWindow();
        var rect = mainWindow.GetWindowRect();
        if (rect.Size.X > 0)
            ImGui.SetNextWindowPos(new Vector2(rect.Pos.X + rect.Size.X + 10, rect.Pos.Y), ImGuiCond.Appearing);

        var open = true;
        ImGui.SetNextWindowSize(new Vector2(300, 220), ImGuiCond.FirstUseEver);
        if (ImGui.Begin($"Payout Management: {_targetName}###bjb_payout_management", ref open, ImGuiWindowFlags.NoCollapse))
        {
            var player = GetCurrentPlayer();
            var currentBank = player?.Bank ?? _lastKnownBank;

            ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), $"Remaining payout: {_remainingAmount:N0} Gil");
            ImGui.TextDisabled($"Player bank: {currentBank:N0} Gil");
            ImGui.TextUnformatted($"Current chunk: {_currentChunk:N0} Gil");
            ImGui.TextDisabled($"State: {_state}");
            ImGui.Spacing();

            if (_startAmount > 0)
            {
                var progress = 1.0f - ((float)Math.Max(0, _remainingAmount) / _startAmount);
                ImGui.ProgressBar(Math.Clamp(progress, 0f, 1f), new Vector2(-1, 0), $"{(int)(progress * 100)}%");
            }

            if (ImGui.Button("Cancel Payout", new Vector2(-1, 0)))
                Cancel();

            ImGui.End();
        }

        if (!open)
            Cancel();
    }

    public static void Cancel()
    {
        _cancelRequested = true;
    }

    public static void Reset(string reason = "Reset")
    {
        if (IsActive)
            Plugin.Instance.GetMainWindow().AddDebugLog($"[PayoutManagement] {reason}");

        _state = PayoutState.Idle;
        _targetName = string.Empty;
        _targetWorldId = 0;
        _startAmount = 0;
        _remainingAmount = 0;
        _lastKnownBank = 0;
        _currentChunk = 0;
        _cancelRequested = false;
        _confirmAllowed = false;
        _sentTradeCommand = false;
        _openedDropboxUiForPayout = false;
        _currentTradeAutoConfirm = false;
        _bankBeforeCurrentTrade = 0;
        _lastTradeActionAt = DateTime.MinValue;
        _executor = TradeExecutor.None;
        _stateEnteredAt = DateTime.UtcNow;
    }

    public static void NotifyTradeCancelled()
    {
        if (IsActive)
            Reset("Recipient cancelled trade");
    }

    private static double StateElapsed => (DateTime.UtcNow - _stateEnteredAt).TotalSeconds;

    private static void SetState(PayoutState state, string log)
    {
        _state = state;
        _stateEnteredAt = DateTime.UtcNow;
        Plugin.Instance.GetMainWindow().AddDebugLog($"[PayoutManagement] {state}: {log}");
    }

    private static PlayerState? GetCurrentPlayer()
    {
        return Plugin.Instance.GetMainWindow().GetPlayers().FirstOrDefault(x =>
            x.Name.Equals(_targetName, StringComparison.OrdinalIgnoreCase)
            && (_targetWorldId == 0 || x.WorldId == _targetWorldId));
    }

    private static void RetryOrResetAfterUnexpectedTradeClose(PlayerState player)
    {
        if (!_sentTradeCommand)
        {
            Reset("Trade closed before command was sent");
            return;
        }

        if (_remainingAmount <= 0)
            Reset("Payout complete");
        else
            SetState(PayoutState.TradeClosedSettle, "Trade closed before payout completed");
    }

    private static bool TryParsePayoutNumber(string text, bool hasSuffix, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (!hasSuffix)
        {
            var digits = new string(text.Where(char.IsDigit).ToArray());
            return !string.IsNullOrWhiteSpace(digits)
                && decimal.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        var normalized = NormalizeSuffixedPayoutNumber(text);
        return !string.IsNullOrWhiteSpace(normalized)
            && decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    private static string NormalizeSuffixedPayoutNumber(string text)
    {
        var token = new string(text.Where(ch => char.IsDigit(ch) || ch == '.' || ch == ',').ToArray());
        if (string.IsNullOrWhiteSpace(token))
            return string.Empty;

        var lastDot = token.LastIndexOf('.');
        var lastComma = token.LastIndexOf(',');
        var separatorIndex = Math.Max(lastDot, lastComma);
        if (separatorIndex < 0)
            return new string(token.Where(char.IsDigit).ToArray());

        var hasMixedSeparators = lastDot >= 0 && lastComma >= 0;
        var fractionLength = token.Length - separatorIndex - 1;
        var treatAsDecimal = hasMixedSeparators || fractionLength is > 0 and <= 2;
        if (!treatAsDecimal)
            return new string(token.Where(char.IsDigit).ToArray());

        var whole = new string(token[..separatorIndex].Where(char.IsDigit).ToArray());
        var fraction = new string(token[(separatorIndex + 1)..].Where(char.IsDigit).ToArray());
        return string.IsNullOrEmpty(whole) ? $"0.{fraction}" : $"{whole}.{fraction}";
    }

    private static long SafeMultiply(decimal value, long multiplier)
    {
        try
        {
            var result = value * multiplier;
            return result > long.MaxValue ? long.MaxValue : decimal.ToInt64(decimal.Truncate(result));
        }
        catch (OverflowException)
        {
            return long.MaxValue;
        }
    }

    private static void TargetPayoutPlayer(PlayerState player)
    {
        GameEngine.TargetPlayer(player.Name);

        if (Plugin.IsDebugMode) return;

        Svc.Framework.RunOnTick(() =>
        {
            var obj = FindPlayerObject(player.Name, player.WorldId);
            if (obj == null) return;

            Plugin.TargetManager.Target = obj;
            Plugin.TargetManager.FocusTarget = obj;
        });
    }

    private static bool TargetPayoutPlayerImmediate(PlayerState player)
    {
        GameEngine.TargetPlayer(player.Name);

        if (Plugin.IsDebugMode)
            return true;

        var obj = FindPlayerObject(player.Name, player.WorldId);
        if (obj == null)
            return false;

        Plugin.TargetManager.Target = obj;
        Plugin.TargetManager.FocusTarget = obj;
        return true;
    }

    private static bool IsPayoutTargetSelected()
    {
        if (Plugin.IsDebugMode) return true;
        var target = Plugin.TargetManager.Target;
        return target != null && target.Name.TextValue.Equals(_targetName, StringComparison.OrdinalIgnoreCase);
    }

    private static IGameObject? FindPlayerObject(string name, uint worldId)
    {
        return Plugin.ObjectTable.FirstOrDefault(o =>
            o is IPlayerCharacter pc
            && pc.IsTargetable
            && pc.Name.TextValue.Equals(name, StringComparison.OrdinalIgnoreCase)
            && (worldId == 0 || pc.HomeWorld.RowId == worldId));
    }

    private static bool TryStartDropboxPayout(PlayerState player, int gil)
    {
        if (Plugin.IsDebugMode) return false;
        if (gil < 1 || gil > MaxGilPerTrade) return false;

        try
        {
            if (!IsDropboxCommandRegistered())
            {
                Plugin.Instance.GetMainWindow().AddDebugLog("[PayoutManagement] Dropbox not loaded; using local Dropbox clone");
                return false;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var queueEntryType = assemblies
                .Select(a => a.GetType("Dropbox.QueueEntry", false))
                .FirstOrDefault(t => t != null);
            var taskType = assemblies
                .Select(a => a.GetType("Dropbox.TaskAddItemsToTrade", false))
                .FirstOrDefault(t => t != null);
            var enqueue = taskType?.GetMethod("Enqueue", BindingFlags.Public | BindingFlags.Static);

            if (queueEntryType == null || enqueue == null)
            {
                Plugin.Instance.GetMainWindow().AddDebugLog("[PayoutManagement] Dropbox task API not found; using local Dropbox clone");
                return false;
            }

            OpenDropboxWindow();
            if (!TargetPayoutPlayerImmediate(player))
            {
                Plugin.Instance.GetMainWindow().AddDebugLog("[PayoutManagement] Dropbox target/focus target could not be set; using local Dropbox clone");
                return false;
            }

            var emptyEntries = Array.CreateInstance(queueEntryType, 0);
            enqueue.Invoke(null, new object[] { emptyEntries, gil });
            Plugin.Instance.GetMainWindow().AddDebugLog("[PayoutManagement] Dropbox task invoked directly");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Instance.GetMainWindow().AddDebugLog($"[PayoutManagement] Dropbox start failed; using local Dropbox clone: {ex.GetBaseException().Message}");
            return false;
        }
    }

    private static bool IsDropboxCommandRegistered()
    {
        return Plugin.CommandManager.Commands.Keys.Any(k => k.Equals("/dropbox", StringComparison.OrdinalIgnoreCase));
    }

    private static void OpenDropboxWindow()
    {
        if (_openedDropboxUiForPayout)
            return;

        var opened = false;
        try
        {
            opened = Plugin.CommandManager.ProcessCommand("/dropbox");
        }
        catch (Exception ex)
        {
            Plugin.Instance.GetMainWindow().AddDebugLog($"[PayoutManagement] /dropbox command failed: {ex.Message}");
        }

        Plugin.Instance.GetMainWindow().AddDebugLog(opened
            ? "[PayoutManagement] /dropbox command dispatched"
            : "[PayoutManagement] /dropbox command was registered but dispatch returned false");
        _openedDropboxUiForPayout = opened;
    }

    private static unsafe bool IsTradeOpen()
    {
        return Svc.Condition[ConditionFlag.TradeOpen]
            || (TryGetAddonByName<AtkUnitBase>("Trade", out var addon) && IsAddonReady(addon));
    }

    private static unsafe bool TryOpenGilInput()
    {
        if (!TryGetAddonByName<AtkUnitBase>("Trade", out var addon) || !IsAddonReady(addon))
            return false;

        try
        {
            if (!CanRunTradeAction()) return false;
            ECommons.Automation.Callback.Fire(addon, true, 2, ECommons.Automation.Callback.ZeroAtkValue);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Instance.GetMainWindow().AddDebugLog($"[PayoutManagement] OpenGilInput failed: {ex.Message}");
            return false;
        }
    }

    private static unsafe bool TrySetNumericInput(int amount)
    {
        if (amount < 1 || amount > MaxGilPerTrade) return false;
        if (!TryGetAddonByName<AtkUnitBase>("InputNumeric", out var addon) || !IsAddonReady(addon))
            return false;

        try
        {
            if (!CanRunTradeAction()) return false;
            ECommons.Automation.Callback.Fire(addon, true, amount);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Instance.GetMainWindow().AddDebugLog($"[PayoutManagement] SetNumericInput failed: {ex.Message}");
            return false;
        }
    }

    private static unsafe bool TryConfirmTrade()
    {
        if (!_confirmAllowed) return false;
        if (!TryGetAddonByName<AtkUnitBase>("Trade", out var addon) || !IsAddonReady(addon))
            return false;

        try
        {
            if (!CanRunTradeAction()) return false;
            ECommons.Automation.Callback.Fire(addon, true, 0, ECommons.Automation.Callback.ZeroAtkValue);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Instance.GetMainWindow().AddDebugLog($"[PayoutManagement] Trade confirm failed: {ex.Message}");
            return false;
        }
    }

    private static unsafe bool TryConfirmYesNo()
    {
        if (!_confirmAllowed) return false;
        if (!TryGetAddonByName<AtkUnitBase>("SelectYesno", out var addon) || !IsAddonReady(addon))
            return false;

        try
        {
            if (!CanRunTradeAction()) return false;
            ECommons.Automation.Callback.Fire(addon, true, 0);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Instance.GetMainWindow().AddDebugLog($"[PayoutManagement] Yes/No confirm failed: {ex.Message}");
            return false;
        }
    }

    private static bool CanRunTradeAction()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastTradeActionAt).TotalSeconds < ActionThrottleSeconds)
            return false;

        _lastTradeActionAt = now;
        return true;
    }
}
