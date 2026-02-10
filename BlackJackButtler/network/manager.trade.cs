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

    public static bool IsTradeActive => _isTradeActive;
    public static string? CurrentPartner => _currentPartner;

    // --- Regex-driven methods (called from RegexEngine) ---

    public static void SetPartner(string name)
    {
        _currentPartner = name.Trim();
        _buffer = 0;
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
        var window = Plugin.Instance.GetMainWindow();

        // Fallback: if regex CommitTrade didn't fire, apply what we have
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

        window.AddDebugLog($"[TradeManager] Trade closed (partner={_currentPartner}, buffer={_buffer}, committed={_committed})");
        Reset();
    }

    public static void Reset()
    {
        _currentPartner = null;
        _buffer = 0;
        _isTradeActive = false;
        _committed = false;
    }

    private static long ParseGil(string input)
    {
        string cleaned = RRX.Regex.Replace(input, @"[^\d]", "");
        return long.TryParse(cleaned, out var val) ? val : 0;
    }
}
