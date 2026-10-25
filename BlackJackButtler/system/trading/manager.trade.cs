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

    private static readonly string[] AllWorldNames = WorldNameManager.AllWorldNames;

    public static bool IsTradeActive => _isTradeActive;
    public static string? CurrentPartner => _currentPartner;

    public static void SetPartner(string name)
    {
        _currentPartner = name.Trim();
        _buffer = 0;
        _closedAtUtc = null;
        Plugin.Instance.GetMainWindow().TryAutoActivateTradingPlayer(_currentPartner);
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

        var p = PayoutManagement.ResolveActivePayoutLedgerPlayer(_currentPartner, players)
            ?? ResolvePlayer(_currentPartner, players);
        if (p != null && p.IsActivePlayer)
        {
            long before = p.Bank;
            p.Bank += _buffer;
            RecordTradeLine(p, before, p.Bank);
            CompanionSyncManager.SendPlayerBankBetUpdate(Plugin.Instance.Configuration, p);
            var window = Plugin.Instance.GetMainWindow();
            window.AddDebugLog($"[TradeManager] Committed: {_currentPartner} → {p.Name} bank += {_buffer}");
            Plugin.Log.Information($"[TradeLedger] TradeCommit: partner={_currentPartner}, player={p.DisplayName}, before={before:N0}, delta={_buffer:N0}, after={p.Bank:N0}");
        }
        else
        {
            var window = Plugin.Instance.GetMainWindow();
            window.AddDebugLog($"[TradeManager] CommitTrade: no matching player found for '{_currentPartner}'");
            Plugin.Log.Warning($"[TradeLedger] TradeCommit unmatched partner={_currentPartner}, delta={_buffer:N0}");
        }
        _committed = true;
        Reset();
    }

    public static unsafe void OnTradeOpened(AddonEvent type, AddonArgs args)
    {
        _buffer = 0;
        _isTradeActive = true;
        _committed = false;
        _closedAtUtc = null;

        var window = Plugin.Instance.GetMainWindow();
        window.AddDebugLog($"[TradeManager] Trade opened (partner={_currentPartner ?? "?"})");
    }

    public static unsafe void OnTradeUpdated(AddonEvent type, AddonArgs args)
    {
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

        if (!_committed && !string.IsNullOrEmpty(_currentPartner) && _buffer != 0)
        {
            var players = window.GetPlayers();
            var p = PayoutManagement.ResolveActivePayoutLedgerPlayer(_currentPartner, players)
                ?? ResolvePlayer(_currentPartner, players);
            if (p != null && p.IsActivePlayer)
            {
                long before = p.Bank;
                p.Bank += _buffer;
                RecordTradeLine(p, before, p.Bank);
                CompanionSyncManager.SendPlayerBankBetUpdate(Plugin.Instance.Configuration, p);
                window.AddDebugLog($"[TradeManager] Committed (fallback): {_currentPartner} bank += {_buffer}");
                Plugin.Log.Information($"[TradeLedger] TradeCommit fallback: partner={_currentPartner}, player={p.DisplayName}, before={before:N0}, delta={_buffer:N0}, after={p.Bank:N0}");
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

    private static void RecordTradeLine(PlayerState p, long before, long after)
    {
        long delta = after - before;
        if (delta == 0) return;
        string line = delta > 0 ? RoundLogManager.BuildTradeLineOutbound(p.DisplayName, before, after) : RoundLogManager.BuildTradeLineInbound(p.DisplayName, before, after);
        RoundLogManager.AddTradeLine(p.DisplayName, line);
        StatsLogManager.AppendTrade(p, delta);
        UserStatisticsManager.RecordTrade(p, delta);
    }

    public static string StripWorldSuffix(string name)
    {
        foreach (var world in AllWorldNames)
        {
            if (name.EndsWith(world, StringComparison.Ordinal) &&
                name.Length > world.Length)
            {
                char charBefore = name[name.Length - world.Length - 1];
                if (charBefore != ' ')
                    return name.Substring(0, name.Length - world.Length);
            }
        }
        return name;
    }

    internal static PlayerState? ResolvePlayer(string partnerName, List<PlayerState> players)
    {
        var exact = players.FirstOrDefault(
            x =>
                x.Name.Equals(partnerName, StringComparison.OrdinalIgnoreCase)
        );
        if (exact != null) return exact;

        foreach (var world in AllWorldNames)
        {
            if (partnerName.EndsWith(world, StringComparison.Ordinal) &&
                partnerName.Length > world.Length)
            {
                char charBefore = partnerName[partnerName.Length - world.Length - 1];
                if (charBefore != ' ')
                {
                    string stripped = partnerName.Substring(0, partnerName.Length - world.Length);
                    var match = players.FirstOrDefault(
                        x =>
                            x.Name.Equals(stripped, StringComparison.Ordinal)
                    );
                    if (match != null) return match;
                }
            }
        }

        PlayerState? best = null;
        int bestLen = 0;
        foreach (var p in players)
        {
            if (p.Name.Length > bestLen &&
                partnerName.StartsWith(p.Name, StringComparison.OrdinalIgnoreCase) &&
                partnerName.Length > p.Name.Length)
            {
                best = p;
                bestLen = p.Name.Length;
            }
        }
        return best;
    }

    private static long ParseGil(string input)
    {
        string cleaned = RRX.Regex.Replace(input, @"[^\d]", "");
        return long.TryParse(cleaned, out var val) ? val : 0;
    }
}
