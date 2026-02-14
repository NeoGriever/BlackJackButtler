using System;

namespace BlackJackButtler;

public static class StatsManager
{
    public static long SessionIncome;
    public static long SessionExpense;
    public static int SessionRounds;

    private static Configuration? _config;
    private static Action? _save;

    public static void Init(Configuration config, Action save)
    {
        _config = config;
        _save = save;
    }

    public static void RecordIncome(long amount)
    {
        SessionIncome += amount;
        if (_config != null)
        {
            _config.OverallIncome += amount;
            _save?.Invoke();
        }
    }

    public static void RecordExpense(long amount)
    {
        SessionExpense += amount;
        if (_config != null)
        {
            _config.OverallExpense += amount;
            _save?.Invoke();
        }
    }

    public static void RecordRound()
    {
        SessionRounds++;
        if (_config != null)
        {
            _config.OverallRounds++;
            _save?.Invoke();
        }
    }

    public static void ResetOverall()
    {
        SessionIncome = 0;
        SessionExpense = 0;
        SessionRounds = 0;

        if (_config != null)
        {
            _config.OverallIncome = 0;
            _config.OverallExpense = 0;
            _config.OverallRounds = 0;
            _save?.Invoke();
        }
    }
}
