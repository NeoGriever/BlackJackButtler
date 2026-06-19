using System;
using System.Linq;
using System.Text;
using Rx = System.Text.RegularExpressions.Regex;
using RxOpt = System.Text.RegularExpressions.RegexOptions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace BlackJackButtler.Chat;

public static class ChatMessageParser
{
  private static readonly System.Collections.Generic.Dictionary<char, int> GroupIconMap = new()
  {
      [''] = 1,
      [''] = 2,
      [''] = 3,
      [''] = 4,
      [''] = 5,
      [''] = 6,
      [''] = 7,
      [''] = 8,
      [''] = 9,
      [''] = 10,
      [''] = 11,
      [''] = 12,
      [''] = 13,
      [''] = 14,
      [''] = 15,
      [''] = 16,
      [''] = 17,
      [''] = 18,
      [''] = 19,
      [''] = 20,
  };

  private static readonly Rx DiceRangeText = new(
    @"^(?:Würfeln!|Random!)\s*\(\s*(\d+)\s*[-–—]\s*(\d+)\s*\)\s*(\d+)\s*[.!]?\s*$",
    RxOpt.Compiled | RxOpt.IgnoreCase
  );

  private static readonly Rx DiceRollText = new(
    @"^(?:Random!\s*)?(?:(.+?)\s+)?rolls?\s+(?:a\s+)?(\d+)\s*[.!]?\s*$",
    RxOpt.Compiled | RxOpt.IgnoreCase
  );

  public static ParsedChatMessage Parse(
    DateTime timestamp,
    SeString sender,
    SeString message,
    string localPlayerName,
    uint localWorldId,
    ulong localContentId,
    ulong sourceContentId,
    uint sourceWorldId,
    string canonicalSourceName,
    uint logNameType,
    int chatType)
  {
    var messageText = message.TextValue ?? string.Empty;

    var playerPayload = sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
    var displayedName = playerPayload?.PlayerName ?? ExtractNameFromTextPayloads(sender);
    var name = !string.IsNullOrWhiteSpace(canonicalSourceName) ? canonicalSourceName : displayedName;
    var worldId = sourceWorldId != 0
      ? unchecked((int)sourceWorldId)
      : playerPayload?.World.RowId is uint wid ? unchecked((int)wid) : -1;

    var tag = ExtractGroupTag(sender, displayedName);

    var isDice = TryParseDiceRoll(message, messageText, chatType, out var diceValue, out var diceSides);
    var isSelf = IsLocalPlayerMessage(
      displayedName,
      messageText,
      localPlayerName,
      localWorldId,
      localContentId,
      sourceContentId,
      sourceWorldId,
      logNameType,
      chatType,
      isDice,
      diceValue);
    var isEvent = isSelf && isDice;
    var color = ColorFromIdentity(name, worldId);
    var identitySource = sourceContentId != 0
      ? "ContentId"
      : playerPayload != null
        ? "PlayerPayload"
        : isSelf
          ? "ConfiguredDisplayName"
          : "DisplayText";

    return new ParsedChatMessage(
      timestamp,
      tag,
      name,
      worldId,
      sourceContentId,
      identitySource,
      messageText,
      isEvent,
      color,
      chatType,
      isDice,
      isDice ? diceValue : null,
      isDice ? diceSides : null
    );
  }


  private static string ExtractNameFromTextPayloads(SeString sender)
  {
    var candidates = sender.Payloads
    .OfType<TextPayload>()
    .Select(t => t.Text ?? string.Empty)
    .Where(t => t.Length >= 2)
    .Where(ContainsLetter)
    .ToList();

    return candidates.LastOrDefault() ?? string.Empty;
  }

  private static int ExtractGroupTag(SeString sender, string name)
  {
    foreach (var tp in sender.Payloads.OfType<TextPayload>())
    {
      var t = tp.Text ?? string.Empty;
      if (string.IsNullOrWhiteSpace(t))
      continue;

      if (!string.IsNullOrWhiteSpace(name) && string.Equals(t, name, StringComparison.Ordinal))
      continue;

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
      return GroupIconMap.TryGetValue(ch, out var n) ? n : 0;
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

  private static bool IsLocalPlayerMessage(
    string senderName,
    string messageText,
    string localPlayerName,
    uint localWorldId,
    ulong localContentId,
    ulong sourceContentId,
    uint sourceWorldId,
    uint logNameType,
    int chatType,
    bool isDice,
    int diceValue)
  {
    if (sourceContentId != 0 && localContentId != 0)
      return sourceContentId == localContentId;

    if (!string.IsNullOrWhiteSpace(localPlayerName)
        && string.Equals(senderName, localPlayerName, StringComparison.OrdinalIgnoreCase))
      return sourceWorldId == 0 || localWorldId == 0 || sourceWorldId == localWorldId;

    if (!isDice)
      return false;

    if (string.IsNullOrWhiteSpace(localPlayerName))
      return false;

    var expectedDisplayName = FormatLogDisplayName(localPlayerName, logNameType);
    if (!string.IsNullOrWhiteSpace(expectedDisplayName)
        && string.Equals(senderName.Trim(), expectedDisplayName, StringComparison.OrdinalIgnoreCase))
      return CommandExecutor.IsWaitingForDiceValue(diceValue);

    if (ChatLogBuffer.IsDiceChatType(chatType) && string.IsNullOrWhiteSpace(senderName))
      return CommandExecutor.IsWaitingForDiceValue(diceValue);

    var match = DiceRollText.Match(messageText);
    if (!match.Success)
      return false;

    var subject = match.Groups[1].Value.Trim();
    return subject.Equals("You", StringComparison.OrdinalIgnoreCase)
      || (subject.Equals(localPlayerName, StringComparison.OrdinalIgnoreCase)
          && CommandExecutor.IsWaitingForDiceValue(diceValue));
  }

  public static string FormatLogDisplayName(string fullName, uint logNameType)
  {
    var parts = fullName
      .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length < 2)
      return fullName.Trim();

    var forename = parts[0];
    var surname = string.Join(' ', parts.Skip(1));
    return logNameType switch
    {
      1 => $"{forename} {surname[0]}.",
      2 => $"{forename[0]}. {surname}",
      3 => $"{forename[0]}. {surname[0]}.",
      _ => fullName.Trim(),
    };
  }

  public static bool TryParseDiceRoll(
    SeString message,
    string messageText,
    int chatType,
    out int diceValue,
    out int? diceSides)
  {
    diceValue = 0;
    diceSides = null;
    var rangeMatch = DiceRangeText.Match(messageText);
    if (rangeMatch.Success)
    {
      if (!int.TryParse(rangeMatch.Groups[1].Value, out var minimum)
          || !int.TryParse(rangeMatch.Groups[2].Value, out var maximum)
          || !int.TryParse(rangeMatch.Groups[3].Value, out diceValue)
          || minimum <= 0
          || maximum < minimum
          || diceValue < minimum
          || diceValue > maximum)
        return false;
      if (minimum == 1)
        diceSides = maximum;
    }
    else
    {
      var rollMatch = DiceRollText.Match(messageText);
      if (!rollMatch.Success
          || !int.TryParse(rollMatch.Groups[2].Value, out diceValue)
          || diceValue <= 0)
        return false;
    }

    if (ChatLogBuffer.IsDiceChatType(chatType)
        || ChatLogBuffer.IsPartyChatType(chatType)
        || ChatLogBuffer.IsAllianceChatType(chatType))
      return true;

    var encoded = message.Encode();
    for (var i = 0; i < encoded.Length - 1; i++)
    {
      if (encoded[i] == 0x02 && encoded[i + 1] == 0x12)
        return true;
    }

    return false;
  }
}
