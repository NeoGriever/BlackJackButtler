using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BlackJackButtler;

public static class BlacklistManager
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private const string BlacklistUrl =
        "https://raw.githubusercontent.com/NeoGriever/BlackJackButtler/main/blacklist.json";

    private static volatile bool _isBlacklisted;
    private static volatile bool _isBlocked;
    private static volatile bool _checkComplete;
    private static volatile string _bannerMessage = "";

    public static bool IsBlacklisted => _isBlacklisted;
    public static bool IsBlocked => _isBlocked;
    public static bool CheckComplete => _checkComplete;
    public static string BannerMessage => _bannerMessage;

    public static void Init(Configuration config)
    {
        EvaluateStoredState(config);
        Task.Run(() => FetchAndCheck(config));
    }

    private static void EvaluateStoredState(Configuration config)
    {
        if (string.IsNullOrEmpty(config.BlacklistDetectedAt))
            return;

        _isBlacklisted = true;

        if (!DateTime.TryParse(config.BlacklistDetectedAt, null,
                DateTimeStyles.RoundtripKind, out var detectedAt))
            return;

        var elapsed = DateTime.UtcNow - detectedAt;
        if (elapsed.TotalDays >= 3)
        {
            _isBlocked = true;
            config.BlacklistActive = true;
            config.Save();
        }
        else
        {
            int daysLeft = Math.Max(1, (int)Math.Ceiling(3 - elapsed.TotalDays));
            _bannerMessage = daysLeft == 1
                ? "Access revoked. You have 1 day left. If you want to talk about the reason, join the discord."
                : $"Access revoked. You have {daysLeft} days left. If you want to talk about the reason, join the discord.";
        }
    }

    private static async Task FetchAndCheck(Configuration config)
    {
        try
        {
            var response = await _http.GetAsync(BlacklistUrl);
            if (!response.IsSuccessStatusCode)
            {
                _checkComplete = true;
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var entries = JsonConvert.DeserializeObject<List<BlacklistEntry>>(json);
            if (entries == null)
            {
                _checkComplete = true;
                return;
            }

            string playerName = "";
            string homeWorld = "";
            for (int i = 0; i < 30; i++)
            {
                var local = Plugin.ObjectTable?.LocalPlayer;
                if (local != null)
                {
                    playerName = local.Name.TextValue;
                    homeWorld = local.HomeWorld.Value.Name.ToString();
                    break;
                }
                await Task.Delay(1000);
            }

            if (string.IsNullOrEmpty(playerName))
            {
                _checkComplete = true;
                return;
            }

            bool found = false;
            foreach (var entry in entries)
            {
                if (string.Equals(entry.Name, playerName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(entry.World, homeWorld, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }

            if (found && string.IsNullOrEmpty(config.BlacklistDetectedAt))
            {
                config.BlacklistDetectedAt = DateTime.UtcNow.ToString("o");
                config.Save();
                _isBlacklisted = true;
                _bannerMessage = "Access revoked. You have 3 days left. If you want to talk about the reason, join the discord.";
            }
            else if (found)
            {
                _isBlacklisted = true;
                EvaluateStoredState(config);
            }
            else if (!found && !string.IsNullOrEmpty(config.BlacklistDetectedAt))
            {
                config.BlacklistDetectedAt = "";
                config.BlacklistActive = false;
                config.Save();
                _isBlacklisted = false;
                _isBlocked = false;
                _bannerMessage = "";
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[BlacklistManager] Check failed: {ex.Message}");
        }
        finally
        {
            _checkComplete = true;
        }
    }
}

public sealed class BlacklistEntry
{
    public string Name { get; set; } = "";
    public string World { get; set; } = "";
}
