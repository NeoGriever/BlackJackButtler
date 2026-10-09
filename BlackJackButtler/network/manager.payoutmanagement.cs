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

public static class PayoutManagement
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
    // The ledger player may be an imaginary player; the target remains the real player that
    // receives the trade in the game client.
    private static string _sourceName = string.Empty;
    private static uint _sourceWorldId;
    private static string _targetName = string.Empty;
    private static uint _targetWorldId;
    private static long _startAmount;
    // Tracks the requested payout independently of the player's remaining bank.  This lets a
    // withdrawal settle after its requested amount even when the bank still contains Gil.
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

    public static void Initialize()
    {
        if (_modularTradeInitialized)
            return;

        try
        {
            global::ModularTrade.TradeManager.Initialize(
                new ModularTrade.BjbDalamudTradeAdapter());
            global::ModularTrade.TradeManager.TradeChanged += OnModularTradeChanged;
            _modularTradeInitialized = true;
            Trace("Initialized integrated ModularTrade executor.");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "[PayoutManagement] Failed to initialize integrated ModularTrade executor.");
        }
    }

    public static void Dispose()
    {
        if (!_modularTradeInitialized)
            return;

        global::ModularTrade.TradeManager.TradeChanged -= OnModularTradeChanged;
        if (global::ModularTrade.TradeManager.IsBusy)
            global::ModularTrade.TradeManager.Cancel();
        global::ModularTrade.TradeManager.Dispose();
        _modularTradeInitialized = false;
        Trace("Disposed integrated ModularTrade executor.");
    }

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

        var players = Plugin.Instance.GetMainWindow().GetPlayers();
        var recipient = PlayerIdentityManager.GetReferencedPlayer(players, p) ?? p;
        _sourceName = p.Name;
        _sourceWorldId = p.WorldId;
        _targetName = recipient.Name;
        _targetWorldId = recipient.WorldId;
        _startAmount = amount;
        _remainingAmount = amount;
        _lastKnownBank = p.Bank;
        _currentChunk = Math.Min(amount, MaxGilPerTrade);
        _cancelRequested = false;
        _openedDropboxUiForPayout = false;
        _bankBeforeCurrentTrade = p.Bank;
        _executor = TradeExecutor.None;

        SetState(PayoutState.Targeting,
            $"Started for {p.DisplayName}, payout={amount:N0}, bank={p.Bank:N0}, chunk={_currentChunk:N0}");
        return PayoutStartResult.Started;
    }

    public static PayoutStartResult TryStartPayoutSequence(IEnumerable<PlayerState> players)
    {
        if (IsActive)
            return PayoutStartResult.AlreadyActive;

        var requests = players
            .Where(player => player.Bank > 0)
            .Select(player => new QueuedPayout(player.Name, player.WorldId, player.Bank))
            .ToList();
        if (requests.Count == 0)
            return PayoutStartResult.InvalidAmount;

        QueuedPayouts.Clear();
        var firstRequest = requests[0];
        var firstPlayer = Plugin.Instance.GetMainWindow().GetPlayers().FirstOrDefault(player =>
            player.Name.Equals(firstRequest.PlayerName, StringComparison.OrdinalIgnoreCase)
            && (firstRequest.WorldId == 0 || player.WorldId == firstRequest.WorldId));
        if (firstPlayer == null)
            return PayoutStartResult.InvalidAmount;

        var startResult = TryStartPayout(firstPlayer, firstRequest.Amount);
        if (startResult != PayoutStartResult.Started)
            return startResult;

        foreach (var request in requests.Skip(1))
            QueuedPayouts.Enqueue(request);

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
        if (_modularTradeInitialized)
            global::ModularTrade.TradeManager.Update();
        if (!IsActive) return;

        if (_cancelRequested)
        {
            Reset("Cancelled");
            return;
        }

        var player = GetCurrentPlayer();
        var recipient = GetPayoutRecipient();
        if (player == null || recipient == null)
        {
            Reset($"Payout player not found: {_sourceName}");
            return;
        }

        if (_remainingAmount <= 0)
        {
            Reset("Payout complete");
            return;
        }

        var settlementPending = _state is PayoutState.WaitingTradeClose
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
                TargetPayoutPlayer(recipient);
                SetState(PayoutState.OpeningTrade, "Target/focus target set");
                break;

            case PayoutState.OpeningTrade:
                if (StateElapsed < TargetSettleSeconds) return;
                if (!IsPayoutTargetSelected())
                {
                    TargetPayoutPlayer(recipient);
                    return;
                }

                _currentChunk = Math.Min(_remainingAmount, Math.Min(player.Bank, MaxGilPerTrade));
                if (_currentChunk <= 0)
                {
                    Reset("No Gil available for remaining payout");
                    return;
                }
                _bankBeforeCurrentTrade = player.Bank;

                var dropbox = GetDropboxStatus();
                if (ShouldUseDropbox(dropbox))
                {
                    if (TryStartDropboxPayout(recipient, (int)_currentChunk))
                    {
                        _executor = TradeExecutor.Dropbox;
                        SetState(PayoutState.WaitingTradeOpen, "Dropbox payout task queued");
                        return;
                    }

                    Reset("Dropbox is active but its trade task could not be started");
                    return;
                }

                if (TryStartModularTradePayout(recipient, (int)_currentChunk))
                {
                    _executor = TradeExecutor.ModularTrade;
                    SetState(PayoutState.WaitingTradeOpen, "Integrated ModularTrade payout started");
                    return;
                }
                Reset("Integrated ModularTrade executor could not start");
                break;

            case PayoutState.WaitingTradeOpen:
                if (_executor == TradeExecutor.ModularTrade)
                    return; // ModularTrade owns its state and reports its verified outcome via its event.

                if (IsTradeOpen())
                {
                    SetState(PayoutState.WaitingTradeClose, "Dropbox opened trade");
                    return;
                }
                if (StateElapsed >= DropboxTradeOpenTimeoutSeconds)
                    Reset("Timed out waiting for Trade addon");
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
#if DEBUG
        if (_isResetting)
            return;
        _isResetting = true;
#endif
        try
        {
        if (IsActive)
            DebugLog($"[PayoutManagement] {reason}");

        Trace($"Reset: reason='{reason}', executor={_executor}, remaining={_remainingAmount}, chunk={_currentChunk}");

        if (_executor == TradeExecutor.ModularTrade && global::ModularTrade.TradeManager.IsBusy)
            global::ModularTrade.TradeManager.Cancel();

        var startNextQueuedPayout = reason.Equals("Payout complete", StringComparison.OrdinalIgnoreCase)
            && QueuedPayouts.Count > 0;

        _state = PayoutState.Idle;
        _sourceName = string.Empty;
        _sourceWorldId = 0;
        _targetName = string.Empty;
        _targetWorldId = 0;
        _startAmount = 0;
        _remainingAmount = 0;
        _lastKnownBank = 0;
        _currentChunk = 0;
        _cancelRequested = false;
        _openedDropboxUiForPayout = false;
        _bankBeforeCurrentTrade = 0;
        _executor = TradeExecutor.None;
        _stateEnteredAt = DateTime.UtcNow;

        if (startNextQueuedPayout)
        {
            StartNextQueuedPayout();
        }
        else if (!reason.Equals("Payout complete", StringComparison.OrdinalIgnoreCase))
        {
            QueuedPayouts.Clear();
        }
        }
        finally
        {
#if DEBUG
            _isResetting = false;
#endif
        }
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
        DebugLog($"[PayoutManagement] {state}: {log}");
        Trace($"State={state}, executor={_executor}, target={_targetName}@{_targetWorldId}, chunk={_currentChunk}, remaining={_remainingAmount}: {log}");
    }

    private static PlayerState? GetCurrentPlayer()
    {
        return GetPayoutSource(Plugin.Instance.GetMainWindow().GetPlayers());
    }

    private static void StartNextQueuedPayout()
    {
        var players = Plugin.Instance.GetMainWindow().GetPlayers();
        while (QueuedPayouts.TryDequeue(out var request))
        {
            var player = players.FirstOrDefault(candidate =>
                candidate.Name.Equals(request.PlayerName, StringComparison.OrdinalIgnoreCase)
                && (request.WorldId == 0 || candidate.WorldId == request.WorldId));
            if (player == null || player.Bank <= 0)
            {
                DebugLog(
                    $"[PayoutManagement] Skipped queued payout for {request.PlayerName}: player missing or bank empty");
                continue;
            }

            var amount = Math.Min(request.Amount, player.Bank);
            if (TryStartPayout(player, amount) == PayoutStartResult.Started)
                return;

            DebugLog(
                $"[PayoutManagement] Skipped queued payout for {player.DisplayName}: could not start payout");
        }
    }

    private static PlayerState? GetPayoutSource(IEnumerable<PlayerState> players)
    {
        return players.FirstOrDefault(x =>
            x.Name.Equals(_sourceName, StringComparison.OrdinalIgnoreCase)
            && (_sourceWorldId == 0 || x.WorldId == _sourceWorldId));
    }

    private static PlayerState? GetPayoutRecipient()
    {
        return GetPayoutRecipient(Plugin.Instance.GetMainWindow().GetPlayers());
    }

    private static PlayerState? GetPayoutRecipient(IEnumerable<PlayerState> players)
    {
        return players.FirstOrDefault(x =>
            x.Name.Equals(_targetName, StringComparison.OrdinalIgnoreCase)
            && (_targetWorldId == 0 || x.WorldId == _targetWorldId));
    }

    public static PlayerState? ResolveActivePayoutLedgerPlayer(string partnerName, List<PlayerState> players)
    {
        if (!IsActive || string.IsNullOrWhiteSpace(partnerName))
            return null;

        var recipient = GetPayoutRecipient(players);
        var detectedPartner = TradeManager.ResolvePlayer(partnerName, players);
        return recipient != null && ReferenceEquals(detectedPartner, recipient)
            ? GetPayoutSource(players)
            : null;
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

    private static bool TryStartModularTradePayout(PlayerState player, int gil)
    {
        var isBusy = global::ModularTrade.TradeManager.IsBusy;
        if (!_modularTradeInitialized || isBusy)
        {
            Trace($"Integrated ModularTrade unavailable: initialized={_modularTradeInitialized}, busy={isBusy}");
            return false;
        }

        if (FindPlayerObject(player.Name, player.WorldId) is not IPlayerCharacter character)
        {
            Trace($"Integrated ModularTrade could not resolve recipient {player.DisplayName}.");
            return false;
        }

        try
        {
            var recipient = new Sender(character).ToString();
            Trace($"Starting integrated ModularTrade recipient={recipient}, gil={gil}.");
            global::ModularTrade.TradeManager.Trade(recipient, gil);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "[PayoutManagement] Integrated ModularTrade could not start.");
            return false;
        }
    }

    private static bool ShouldUseDropbox(DropboxStatus status)
    {
#if DEBUG
        return Plugin.Instance.Configuration.DebugPayoutExecutor switch
        {
            DebugPayoutExecutorVariant.ModularTrade => false,
            DebugPayoutExecutorVariant.Dropbox => status.Active,
            _ => status.Installed && status.Active,
        };
#else
        return status.Installed && status.Active;
#endif
    }

    /// <summary>Queries Dalamud's installed-plugin list and its loaded-plugin registry separately.</summary>
    public static DropboxStatus GetDropboxStatus()
    {
        var active = false;
        var installed = false;

        try
        {
            active = DalamudReflector.TryGetDalamudPlugin(
                "Dropbox", out _, out _, suppressErrors: true, ignoreCache: true);
        }
        catch (Exception ex)
        {
            Trace($"Dropbox active-state query failed: {ex.GetBaseException().Message}");
        }

        try
        {
            var getPluginManager = typeof(DalamudReflector).GetMethod(
                "GetPluginManager", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var pluginManager = getPluginManager?.Invoke(null, null);
            var installedPlugins = pluginManager?.GetType().GetProperty(
                "InstalledPlugins", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(pluginManager)
                as IEnumerable;

            if (installedPlugins is not null)
            {
                foreach (var plugin in installedPlugins)
                {
                    if (plugin is not null && IsDropboxPlugin(plugin))
                    {
                        installed = true;
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Trace($"Dropbox installation-state query failed: {ex.GetBaseException().Message}");
        }

        // A loaded plugin is necessarily installed, even if a future Dalamud build changes
        // the internal installed-plugin collection accessed above.
        return new DropboxStatus(installed || active, active);
    }

    private static bool IsDropboxPlugin(object plugin)
    {
        var type = plugin.GetType();
        var internalName = type.GetProperty("InternalName")?.GetValue(plugin) as string
            ?? type.GetField("InternalName")?.GetValue(plugin) as string;
        if (string.Equals(internalName, "Dropbox", StringComparison.OrdinalIgnoreCase))
            return true;

        var manifest = type.GetProperty("Manifest")?.GetValue(plugin);
        if (manifest is null)
            return false;

        var manifestType = manifest.GetType();
        internalName = manifestType.GetProperty("InternalName")?.GetValue(manifest) as string
            ?? manifestType.GetField("InternalName")?.GetValue(manifest) as string;
        return string.Equals(internalName, "Dropbox", StringComparison.OrdinalIgnoreCase);
    }

    private static void OnModularTradeChanged(
        global::ModularTrade.TradeEventType type,
        global::ModularTrade.TradeSnapshot snapshot)
    {
        Trace($"ModularTrade event={type}, state={snapshot.State}, step={snapshot.CurrentStep}/{snapshot.TotalSteps}, confirmed={snapshot.ConfirmedAmount}/{snapshot.TotalAmount}, reason={snapshot.FailureReason}");
        if (!IsActive || _executor != TradeExecutor.ModularTrade)
            return;

        switch (type)
        {
            case global::ModularTrade.TradeEventType.Success:
                SetState(PayoutState.TradeClosedSettle,
                    $"Integrated ModularTrade verified {_currentChunk:N0} Gil");
                break;

            case global::ModularTrade.TradeEventType.Cancel:
                Reset($"Integrated ModularTrade stopped: {snapshot.FailureReason}; verified={snapshot.ConfirmedAmount:N0}");
                break;
        }
    }

    private static void Trace(string message)
    {
#if DEBUG
        if (Plugin.Instance.Configuration.DebugTradeLogging)
            Plugin.Log.Debug($"[PayoutManagement] {message}");
#endif
    }

    private static void DebugLog(string message)
    {
#if DEBUG
        if (Plugin.Instance.Configuration.DebugTradeLogging)
            Plugin.Instance.GetMainWindow().AddDebugLog(message);
#endif
    }

    private static bool TryStartDropboxPayout(PlayerState player, int gil)
    {
        Trace($"Trying external Dropbox executor for {player.DisplayName}, gil={gil}.");
        if (gil < 1 || gil > MaxGilPerTrade) return false;

        try
        {
            // Dropbox exposes TaskAddItemsToTrade.Enqueue(IEnumerable<QueueEntry>, int).
            // Keep both reflected types from the same assembly: an old plugin load context may
            // otherwise supply QueueEntry while a newer one supplies the task type, causing
            // Invoke to reject the argument and unnecessarily fall back to local automation.
            var taskApis = GetDropboxTaskApis().ToList();
            if (taskApis.Count == 0)
            {
                Trace("External Dropbox task API was not found.");
                DebugLog("[PayoutManagement] Dropbox task API not found");
                return false;
            }

            // The command only opens Dropbox's UI. Its registration is not a reliable test that
            // the task API is available, especially when Dropbox was loaded after BJB.
            if (IsDropboxCommandRegistered())
                OpenDropboxWindow();

            if (!TargetPayoutPlayerImmediate(player))
            {
                DebugLog("[PayoutManagement] Dropbox target/focus target could not be set");
                return false;
            }

            foreach (var (queueEntryType, enqueue) in taskApis)
            {
                try
                {
                    var emptyEntries = Array.CreateInstance(queueEntryType, 0);
                    enqueue.Invoke(null, new object[] { emptyEntries, gil });
                    DebugLog(
                        $"[PayoutManagement] Dropbox task invoked directly ({enqueue.DeclaringType?.Assembly.GetName().Name})");
                    Trace($"External Dropbox task invocation succeeded: assembly={enqueue.DeclaringType?.Assembly.GetName().Name}.");
                    return true;
                }
                catch (Exception ex)
                {
                    DebugLog(
                        $"[PayoutManagement] Dropbox task invocation failed ({enqueue.DeclaringType?.Assembly.GetName().Name}): {ex.GetBaseException().Message}");
                    Trace($"External Dropbox task invocation failed: {ex.GetBaseException().Message}");
                }
            }

            DebugLog("[PayoutManagement] All Dropbox task APIs failed");
            return false;
        }
        catch (Exception ex)
        {
            DebugLog($"[PayoutManagement] Dropbox start failed: {ex.GetBaseException().Message}");
            Trace($"External Dropbox start failed: {ex.GetBaseException().Message}");
            return false;
        }
    }

    private static IEnumerable<(Type QueueEntryType, MethodInfo Enqueue)> GetDropboxTaskApis()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var queueEntryType = assembly.GetType("Dropbox.QueueEntry", false);
            var taskType = assembly.GetType("Dropbox.TaskAddItemsToTrade", false);
            if (queueEntryType == null || taskType == null)
                continue;

            var queueEntryArrayType = queueEntryType.MakeArrayType();
            var enqueue = taskType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method =>
                {
                    if (!method.Name.Equals("Enqueue", StringComparison.Ordinal))
                        return false;

                    var parameters = method.GetParameters();
                    return parameters.Length == 2
                        && parameters[0].ParameterType.IsAssignableFrom(queueEntryArrayType)
                        && parameters[1].ParameterType == typeof(int);
                });

            if (enqueue != null)
                yield return (queueEntryType, enqueue);
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
            DebugLog($"[PayoutManagement] /dropbox command failed: {ex.Message}");
        }

        DebugLog(opened
            ? "[PayoutManagement] /dropbox command dispatched"
            : "[PayoutManagement] /dropbox command was registered but dispatch returned false");
        _openedDropboxUiForPayout = opened;
    }

    private static unsafe bool IsTradeOpen()
    {
        return Svc.Condition[ConditionFlag.TradeOpen]
            || (TryGetAddonByName<AtkUnitBase>("Trade", out var addon) && IsAddonReady(addon));
    }

}
