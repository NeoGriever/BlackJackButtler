using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace BlackJackButtler.Chat;

public static class ChatMessageParser
{

  private static readonly Regex DiceTextDe = new(
    @"^Würfeln!\s*\(\d+\s*-\s*\d+\)\s*\d+\s*$",
    RegexOptions.Compiled
  );

  private static readonly Regex DiceTextEn = new(
    @"\brolls?\s+a\s+\d+\b",
    RegexOptions.Compiled | RegexOptions.IgnoreCase
  );

  public static ParsedChatMessage Parse(DateTime timestamp, SeString sender, SeString message, string localPlayerName)
  {
    var messageText = message.TextValue ?? string.Empty;

    var playerPayload = sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
    var name = playerPayload?.PlayerName ?? ExtractNameFromTextPayloads(sender);
    var worldId = playerPayload?.World.RowId is uint wid ? unchecked((int)wid) : -1;

    var tag = ExtractGroupTag(sender, name);

    // Nur eigener Character darf Event triggern
    var isSelf = !string.IsNullOrWhiteSpace(localPlayerName)
    && string.Equals(name, localPlayerName, StringComparison.Ordinal);

    // Event = ausschließlich eigener Würfelwurf
    var isEvent = isSelf && IsDiceRoll(message, messageText);
    var color = ColorFromIdentity(name, worldId);

    return new ParsedChatMessage(
      timestamp,
      tag,
      name,
      worldId,
      messageText,
      isEvent,
      color
    );

  }


  private static string ExtractNameFromTextPayloads(SeString sender)
  {
    // Heuristik: Der Name ist typischerweise der längste "normale" TextPayload,
    // der Buchstaben enthält. (Bei dir: "Valenth Siveria")
    var candidates = sender.Payloads
    .OfType<TextPayload>()
    .Select(t => t.Text ?? string.Empty)
    .Where(t => t.Length >= 2)
    .Where(ContainsLetter)
    .ToList();

    // In deinen Dumps ist der Name oft der letzte passende TextPayload.
    return candidates.LastOrDefault() ?? string.Empty;
  }

  private static int ExtractGroupTag(SeString sender, string name)
  {
    // Wir suchen nach einem einzelnen Glyph-TextPayload, das nicht der Name ist.
    foreach (var tp in sender.Payloads.OfType<TextPayload>())
    {
      var t = tp.Text ?? string.Empty;
      if (string.IsNullOrWhiteSpace(t))
      continue;

      if (!string.IsNullOrWhiteSpace(name) && string.Equals(t, name, StringComparison.Ordinal))
      continue;

      // Kandidat: sehr kurz und keine normalen Zeichen
      if (t.Length <= 2 && !ContainsLetterOrDigit(t))
      {
        var ch = t[0];
        var mapped = MapGroupIconToNumber(ch);
        if (mapped != 0)
        return mapped;
      }
    }

    return 0;
  }

  private static int MapGroupIconToNumber(char ch)
  {
    const int start = '\uE08F';
    const int end   = '\uE0A2';

    var code = (int)ch;
    if (code < start || code > end)
    return 0;

    return (code - start) + 1;
  }

  private static bool ContainsLetter(string s)
  {
    foreach (var ch in s)
    if (char.IsLetter(ch))
    return true;
    return false;
  }

  private static bool ContainsLetterOrDigit(string s)
  {
    foreach (var ch in s)
    if (char.IsLetterOrDigit(ch))
    return true;
    return false;
  }

  private static uint ColorFromIdentity(string name, int worldId)
  {
    var key = $"{name}|{worldId}";
    var hash = Fnv1a32(key);

    var r = (byte)(hash & 0xFF);
    var g = (byte)((hash >> 8) & 0xFF);
    var b = (byte)((hash >> 16) & 0xFF);

    const float brighten = 0.55f;
    r = (byte)(r + (255 - r) * brighten);
    g = (byte)(g + (255 - g) * brighten);
    b = (byte)(b + (255 - b) * brighten);

    return PackColorU32(r, g, b, 255);
  }

  private static uint Fnv1a32(string s)
  {
    unchecked
    {
      const uint offset = 2166136261;
      const uint prime = 16777619;

      uint hash = offset;
      foreach (var ch in s)
      {
        hash ^= ch;
        hash *= prime;
      }
      return hash;
    }
  }

  private static uint PackColorU32(byte r, byte g, byte b, byte a)
  {
    return (uint)(a << 24 | b << 16 | g << 8 | r);
  }

  private static bool IsDiceRoll(SeString message, string messageText)
  {
    var textLooksLikeDice = DiceTextDe.IsMatch(messageText) || DiceTextEn.IsMatch(messageText);
    if (!textLooksLikeDice)
    return false;

    var enc = message.Encode();
    var markerCount = 0;

    for (var i = 0; i < enc.Length - 1; i++)
    {
      if (enc[i] == 0x02 && enc[i + 1] == 0x12)
      markerCount++;
    }

    return markerCount >= 2;
  }
}
