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

    private static readonly string[] AllWorldNames = {
        "Cerberus", "Louisoix", "Moogle", "Omega", "Phantom", "Ragnarok", "Sagittarius", "Spriggan",
        "Alpha", "Lich", "Odin", "Phoenix", "Raiden", "Shiva", "Twintania", "Zodiark",
        "Bismarck", "Ravana", "Sephirot", "Sophia", "Zurvan",
        "Adamantoise", "Cactuar", "Faerie", "Gilgamesh", "Jenova", "Midgardsormr", "Sargatanas", "Siren",
        "Balmung", "Brynhildr", "Coeurl", "Diabolos", "Goblin", "Malboro", "Mateus", "Zalera",
        "Cuchulainn", "Golem", "Halicarnassus", "Kraken", "Maduin", "Marilith", "Rafflesia", "Seraph",
        "Behemoth", "Excalibur", "Exodus", "Famfrit", "Hyperion", "Lamia", "Leviathan", "Ultros",
        "Aegis", "Atomos", "Carbuncle", "Garuda", "Gungnir", "Kujata", "Tonberry", "Typhon",
        "Alexander", "Bahamut", "Durandal", "Fenrir", "Ifrit", "Ridill", "Tiamat", "Ultima",
        "Anima", "Asura", "Chocobo", "Hades", "Ixion", "Masamune", "Pandaemonium", "Titan",
        "Belias", "Mandragora", "Ramuh", "Shinryu", "Unicorn", "Valefor", "Yojimbo", "Zeromus",
    };

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

        var p = ResolvePlayer(_currentPartner, players);
        if (p != null && p.IsActivePlayer)
        {
            p.Bank += _buffer;
            var window = Plugin.Instance.GetMainWindow();
            window.AddDebugLog($"[TradeManager] Committed: {_currentPartner} → {p.Name} bank += {_buffer}");
        }
        else
        {
            var window = Plugin.Instance.GetMainWindow();
            window.AddDebugLog($"[TradeManager] CommitTrade: no matching player found for '{_currentPartner}'");
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
            var players = window.GetPlayers();
            var p = ResolvePlayer(_currentPartner, players);
            if (p != null && p.IsActivePlayer)
            {
                p.Bank += _buffer;
                window.AddDebugLog($"[TradeManager] Committed (fallback): {_currentPartner} bank += {_buffer}");
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

    private static PlayerState? ResolvePlayer(string partnerName, List<PlayerState> players)
    {
        // Stage 1: Exact match (case-insensitive)
        var exact = players.FirstOrDefault(x =>
            x.Name.Equals(partnerName, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        // Stage 2: Strip world name suffix (case-sensitive)
        //   "Elitross SioutJenova" → strip "Jenova" → "Elitross Siout"
        //   Only strip when there is NO space before the world name
        foreach (var world in AllWorldNames)
        {
            if (partnerName.EndsWith(world, StringComparison.Ordinal) &&
                partnerName.Length > world.Length)
            {
                char charBefore = partnerName[partnerName.Length - world.Length - 1];
                if (charBefore != ' ')
                {
                    string stripped = partnerName.Substring(0, partnerName.Length - world.Length);
                    var match = players.FirstOrDefault(x =>
                        x.Name.Equals(stripped, StringComparison.Ordinal));
                    if (match != null) return match;
                }
            }
        }

        // Stage 3: StartsWith fallback with longest match
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
