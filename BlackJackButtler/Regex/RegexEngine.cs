using System;
using RRX = System.Text.RegularExpressions;
using BlackJackButtler.Chat;

namespace BlackJackButtler.Regex;

public static class RegexEngine
{
  // Test-State: hier nur als Beispiel; später kommt ein richtiger State-Store rein
  public static int? LastDetectedCardValue { get; private set; }

  public static void ProcessIncoming(ParsedChatMessage msg, Configuration cfg)
  {
    // 1) Standard-Algorithmus: nur für Event (eigener Dice-Roll)
    if (msg.Event)
    {
      var card = TryParseBlackjackCardFromDice(msg.Message);
      if (card.HasValue)
      LastDetectedCardValue = card.Value;
    }

    // 2) User Regexes (noch ohne Actions, erstmal nur “matchbar”)
    foreach (var entry in cfg.UserRegexes)
    {
      if (!entry.Enabled) continue;
      if (string.IsNullOrWhiteSpace(entry.Pattern)) continue;

      var options = entry.CaseSensitive ? RRX.RegexOptions.Compiled : (RRX.RegexOptions.Compiled | RRX.RegexOptions.IgnoreCase);

      RRX.Regex rx;
      try { rx = new RRX.Regex(entry.Pattern, options); }
      catch { continue; } // ungültige Regex ignorieren (UI validieren wir später)

      var m = rx.Match(msg.Message);
      if (!m.Success) continue;

      // In diesem Schritt: noch keine Action – später:
      // - SetVariable: cfg/state["name"] = ...
      // - Reaction: Timer/SendChat
    }
  }

  // “Würfelzahl-Erkennung” aus Event-Message:
  // Wir ziehen die letzte Zahl aus dem Text und mappen:
  // 1->11, 2-9->selbst, 10-13->10
  private static int? TryParseBlackjackCardFromDice(string text)
  {
    if (string.IsNullOrWhiteSpace(text))
    return null;

    var m = RRX.Regex.Match(text, @"(\d+)\s*$");
    if (!m.Success) return null;

    if (!int.TryParse(m.Groups[1].Value, out var rolled))
    return null;

    return rolled switch
    {
      1 => 11,
      >= 2 and <= 9 => rolled,
      >= 10 and <= 13 => 10,
      _ => (int?)null
    };
  }
}
