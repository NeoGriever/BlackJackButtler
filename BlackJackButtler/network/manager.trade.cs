using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using RRX = System.Text.RegularExpressions;

namespace BlackJackButtler.Chat;

public static class TradeManager
{
    private static string? _currentPartner;
    private static long _buffer;
    private static bool _isTradeActive;
    private static bool _committed;
    private static DateTime? _closedAtUtc;

    public static bool IsTradeActive => _isTradeActive;
    public static string? CurrentPartner => _currentPartner;

    // --- Regex-driven methods (called from RegexEngine) ---

    public static void SetPartner(string name)
    {
        _currentPartner = name.Trim();
        _buffer = 0;
        _closedAtUtc = null;
    }

    public static void AddGil(string rawAmount, bool isPositive)
    {
        long amount = ParseGil(rawAmount);
        if (isPositive) _buffer += amount;
        else _buffer -= amount;
    }

    public static void CommitTrade(List<PlayerState> players)
    {
        if (string.IsNullOrEmpty(_currentPartner)) return;

        if (DropboxIntegration.IsPayoutTarget(_currentPartner))
        {
            var window = Plugin.Instance.GetMainWindow();
            window.AddDebugLog($"[TradeManager] Skipped commit for payout target: {_currentPartner}");
            DropboxIntegration.ClearDropboxPayoutTarget();
            _committed = true;
            Reset();
            return;
        }

        var p = players.FirstOrDefault(x => x.Name.Equals(_currentPartner, StringComparison.OrdinalIgnoreCase));
        if (p != null && p.IsActivePlayer)
        {
            p.Bank += _buffer;
            if (_buffer > 0) StatsManager.RecordIncome(_buffer);
            else if (_buffer < 0) StatsManager.RecordExpense(-_buffer);
            var window = Plugin.Instance.GetMainWindow();
            window.AddDebugLog($"[TradeManager] Committed: {_currentPartner} bank += {_buffer}");
        }
        _committed = true;
        Reset();
    }

    // --- Addon Lifecycle Callbacks (supplementary open/close detection) ---

    public static unsafe void OnTradeOpened(AddonEvent type, AddonArgs args)
    {
        // NOTE: Do NOT reset _currentPartner here.
        // The regex SetPartner fires BEFORE the Trade addon opens:
        //   Chat: "Du hast X einen Handel angeboten."  -> SetPartner("X")
        //   Addon: PostSetup                           -> OnTradeOpened
        _buffer = 0;
        _isTradeActive = true;
        _committed = false;
        _closedAtUtc = null;

        var window = Plugin.Instance.GetMainWindow();
        window.AddDebugLog($"[TradeManager] Trade opened (partner={_currentPartner ?? "?"})");
    }

    public static unsafe void OnTradeUpdated(AddonEvent type, AddonArgs args)
    {
        // Gil amounts come from regex, nothing to do here.
    }

    public static unsafe void OnTradeClosed(AddonEvent type, AddonArgs args)
    {
        if (!_isTradeActive) return;
        _isTradeActive = false;
        _closedAtUtc = DateTime.UtcNow;
        var window = Plugin.Instance.GetMainWindow();
        window.AddDebugLog($"[TradeManager] Trade closed, grace period started (partner={_currentPartner}, buffer={_buffer}, committed={_committed})");
    }

    public static void Tick()
    {
        if (_closedAtUtc == null) return;

        var elapsed = (DateTime.UtcNow - _closedAtUtc.Value).TotalSeconds;
        if (elapsed < 3.0) return;

        var window = Plugin.Instance.GetMainWindow();

        // Grace period expired — if regex CommitTrade didn't fire, apply fallback
        if (!_committed && !string.IsNullOrEmpty(_currentPartner) && _buffer != 0)
        {
            if (DropboxIntegration.IsPayoutTarget(_currentPartner))
            {
                window.AddDebugLog($"[TradeManager] Skipped fallback commit for payout target: {_currentPartner}");
                DropboxIntegration.ClearDropboxPayoutTarget();
            }
            else
            {
                var players = window.GetPlayers();
                var p = players.FirstOrDefault(x =>
                    x.Name.Equals(_currentPartner, StringComparison.OrdinalIgnoreCase));
                if (p != null && p.IsActivePlayer)
                {
                    p.Bank += _buffer;
                    if (_buffer > 0) StatsManager.RecordIncome(_buffer);
                    else if (_buffer < 0) StatsManager.RecordExpense(-_buffer);
                    window.AddDebugLog($"[TradeManager] Committed (fallback): {_currentPartner} bank += {_buffer}");
                }
            }
        }

        window.AddDebugLog($"[TradeManager] Grace period expired, resetting (partner={_currentPartner}, committed={_committed})");
        Reset();
    }

    public static void Reset()
    {
        _currentPartner = null;
        _buffer = 0;
        _isTradeActive = false;
        _committed = false;
        _closedAtUtc = null;
    }

    private static long ParseGil(string input)
    {
        string cleaned = RRX.Regex.Replace(input, @"[^\d]", "");
        return long.TryParse(cleaned, out var val) ? val : 0;
    }
}
