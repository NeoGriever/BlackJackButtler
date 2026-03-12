using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BlackJackButtler;

public static class StatsManager
{
    public static long StartBank;
    public static long? EndBank;
    public static DateTime? StartTime;
    public static DateTime? EndTime;
    public static long Tips;
    public static List<string> RoundLog = new();
    public static bool IsRunning;

    private static Configuration? _config;

    public static void Init(Configuration config)
    {
        _config = config;
    }

    public static void StartSession()
    {
        StartBank = GetCurrentGil();
        EndBank = null;
        StartTime = DateTime.Now;
        EndTime = null;
        Tips = 0;
        RoundLog.Clear();
        IsRunning = true;
    }

    public static void StopSession()
    {
        EndBank = GetCurrentGil();
        EndTime = DateTime.Now;
        IsRunning = false;
    }

    public static unsafe long GetCurrentGil()
    {
        var inv = FFXIVClientStructs.FFXIV.Client.Game.InventoryManager.Instance();
        if (inv == null) return 0;
        return inv->GetGil();
    }

    public static long GetNowBank()
    {
        return EndBank ?? GetCurrentGil();
    }

    public static void AddTip(long amount)
    {
        Tips += amount;
        if (Tips < 0) Tips = 0;
    }

    public static void RecordRound(PlayerState dealer, List<PlayerState> players, Configuration cfg)
    {
        var parts = new List<string>();

        foreach (var p in players.Where(x => x.IsActivePlayer && !x.IsOnHold))
        {
            string worldName = VipManager.ResolveWorldName(p.WorldId);
            string nameWorld = string.IsNullOrEmpty(worldName) ? p.Name : $"{p.Name}@{worldName}";

            for (int h = 0; h < p.Hands.Count; h++)
            {
                string cards = p.GetCardsString(h);
                int score = p.GetBestScore(h);
                long result = p.LastRoundResult;
                string sign = result >= 0 ? "+" : "";
                parts.Add($"[{nameWorld}: {cards}, {score}, {sign}{result:N0}]");
            }
        }

        string dealerCards = dealer.GetCardsString(0);
        int dealerScore = dealer.GetBestScore(0);
        bool dealerBust = dealer.Hands.Count > 0 && dealer.Hands[0].IsBust;
        string dealerPart = dealerBust
            ? $"[Dealer: {dealerCards}, BUST]"
            : $"[Dealer: {dealerCards}, {dealerScore}]";
        parts.Add(dealerPart);

        long sum = players.Where(x => x.IsActivePlayer && !x.IsOnHold).Sum(p => p.LastRoundResult);
        string sumSign = sum >= 0 ? "+" : "";
        parts.Add($"Sum: {sumSign}{sum:N0}");

        RoundLog.Add(string.Join(", ", parts));
    }

    public static TimeSpan GetTimePassed()
    {
        if (StartTime == null) return TimeSpan.Zero;
        var end = EndTime ?? DateTime.Now;
        return end - StartTime.Value;
    }

    public static double GetClippedHours(int mode)
    {
        var span = GetTimePassed();
        double minutes = span.TotalMinutes;

        return mode switch
        {
            0 => Math.Ceiling(minutes / 60.0),
            1 => Math.Floor(minutes / 60.0),
            2 => Math.Round(minutes / 60.0, MidpointRounding.AwayFromZero),
            _ => Math.Ceiling(minutes / 60.0),
        };
    }
}
