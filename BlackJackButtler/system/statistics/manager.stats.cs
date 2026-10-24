using System;

namespace BlackJackButtler;

public static class StatsManager
{
    public static long StartBank;
    public static long? EndBank;
    public static DateTime? StartTime;
    public static DateTime? EndTime;
    public static long Tips;
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

    public static double GetClippedWageUnits(int mode, WageInterval interval)
    {
        var intervalMinutes = interval switch
        {
            WageInterval.Minute => 1d,
            WageInterval.FifteenMinutes => 15d,
            WageInterval.ThirtyMinutes => 30d,
            WageInterval.TwoHours => 120d,
            _ => 60d,
        };
        var units = GetTimePassed().TotalMinutes / intervalMinutes;
        return mode switch
        {
            0 => Math.Ceiling(units),
            1 => Math.Floor(units),
            2 => Math.Round(units, MidpointRounding.AwayFromZero),
            _ => Math.Ceiling(units),
        };
    }
}
