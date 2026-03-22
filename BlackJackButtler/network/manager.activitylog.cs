using System;
using System.IO;

namespace BlackJackButtler;

public static class ActivityLogManager
{
    private static string _logDir = string.Empty;

    public static void Init(string pluginConfigDir)
    {
        _logDir = Path.Combine(pluginConfigDir, "logs");
        if (!Directory.Exists(_logDir))
            Directory.CreateDirectory(_logDir);
    }

    private static string GetLogFilePath()
        => Path.Combine(_logDir, $"bjb_log_{DateTime.Now:yyyy-MM-dd}.txt");

    private static void Append(string line)
    {
        try { File.AppendAllText(GetLogFilePath(), $"[{DateTime.Now:HH:mm:ss}] {line}\n"); }
        catch { /* silent */ }
    }

    public static void LogPlayerJoin(string name)
        => Append($"JOIN: {name}");

    public static void LogPlayerLeave(string name)
        => Append($"LEAVE: {name}");

    public static void LogBetSet(string name, long bet)
        => Append($"BET: {name} → {bet:N0}");

    public static void LogBankChange(string name, long oldBank, long newBank)
        => Append($"BANK_EDIT: {name} {oldBank:N0} → {newBank:N0} (Δ {newBank - oldBank:+#,0;-#,0;0})");

    public static void LogRoundEnd(PlayerState dealer, System.Collections.Generic.List<PlayerState> players)
    {
        if (dealer.Hands.Count > 0)
        {
            int dScore = dealer.GetBestScore(0);
            string dCards = dealer.GetCardsString(0);
            Append($"DEALER: {dCards} ({dScore})");
        }

        foreach (var p in players)
        {
            if (!p.IsActivePlayer || p.IsOnHold) continue;
            for (int i = 0; i < p.Hands.Count; i++)
            {
                int score = p.GetBestScore(i);
                string cards = p.GetCardsString(i);
                string bj = p.Hands[i].IsNaturalBlackJack ? " BJ" : (p.Hands[i].IsCharlie ? " Charlie" : (score == 21 ? " 21" : ""));
                Append($"PLAYER: {p.DisplayName} Hand{(p.Hands.Count > 1 ? $"#{i + 1}" : "")}: {cards} ({score}{bj}) | Bank: {p.Bank:N0}");
            }
        }
    }
}
