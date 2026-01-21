using System;
using System.Linq;
using System.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace BlackJackButtler.Chat;

public static class ChatMessageParser
{
  public static ParsedChatMessage Parse(DateTime timestamp, SeString sender, SeString message)
  {
    // 1) Message: TextValue reicht fürs erste (Payloads später, wenn nötig)
    var messageText = message.TextValue ?? string.Empty;

    // 2) Name + WorldId: bevorzugt PlayerPayload
    var playerPayload = sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
    var name = playerPayload?.PlayerName ?? ExtractNameFromTextPayloads(sender);
    var worldId = playerPayload?.World.RowId is uint wid ? unchecked((int)wid) : -1;

    // 3) Tag ("", "", ...) aus TextPayloads extrahieren
    var tag = ExtractGroupTag(sender, name);

    // 4) Event-Erkennung:
    // Event=true, wenn kein sinnvoller Spielername gefunden wurde.
    // (Damit fallen System-/Statuszeilen sauber in Event=true.)
    var isEvent = string.IsNullOrWhiteSpace(name);

    return new ParsedChatMessage(
    timestamp,
    tag,
    name,
    worldId,
    messageText,
    isEvent
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

  private static string ExtractGroupTag(SeString sender, string name)
  {
    // Heuristik: Das Tag ist ein sehr kurzer TextPayload (meist 1 Zeichen),
    // der keine "normalen" Buchstaben/Ziffern enthält und nicht der Name ist.
    foreach (var tp in sender.Payloads.OfType<TextPayload>())
    {
      var t = tp.Text ?? string.Empty;
      if (string.IsNullOrWhiteSpace(t))
      continue;

      // Name überspringen
      if (!string.IsNullOrWhiteSpace(name) && string.Equals(t, name, StringComparison.Ordinal))
      continue;

      // typischer Tag: 1 Zeichen (private use / glyph) oder sehr kurz
      if (t.Length <= 2 && !ContainsLetterOrDigit(t))
      return t;

      // In manchen Fällen könnte das Tag als einzelnes Glyph mit letter=false durchgehen.
    }

    return string.Empty;
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
}
