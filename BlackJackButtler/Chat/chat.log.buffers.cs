using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text;

namespace BlackJackButtler.Chat;

public sealed class ChatLogBuffer
{
  private readonly object _gate = new();
  private readonly int _capacity;
  private readonly Queue<ParsedChatMessage> _items;

  public ChatLogBuffer(int capacity = 20)
  {
    _capacity = Math.Max(1, capacity);
    _items = new Queue<ParsedChatMessage>(_capacity);
  }

  public void Add(ParsedChatMessage entry)
  {
    lock (_gate)
    {
      while (_items.Count >= _capacity)
      _items.Dequeue();

      _items.Enqueue(entry);
    }
  }

  public void Clear()
  {
    lock (_gate)
    _items.Clear();
  }

  public IReadOnlyList<ParsedChatMessage> Snapshot()
  {
    lock (_gate)
    return _items.ToList();
  }

  public static bool IsPartyChatType(int t)
  {
    return t == (int)XivChatType.Party
        || t == (int)XivChatType.CrossParty
        || t == 64;
  }

  public static bool IsTellChatType(int t)
  {
    return t == (int)XivChatType.TellIncoming
        || t == (int)XivChatType.TellOutgoing;
  }

  public static bool IsSayChatType(int t)
  {
    return t == (int)XivChatType.Say;
  }

  public static bool IsSystemChatType(int t)
  {
    return t == (int)XivChatType.SystemMessage
        || t == (int)XivChatType.SystemError
        || t == (int)XivChatType.Notice
        || t == (int)XivChatType.GatheringSystemMessage;
  }
}

public sealed record ParsedChatMessage(
  DateTime Timestamp,
  int GroupIndexNumber,
  string Name,
  int WorldId,
  string Message,
  bool Event,
  uint ColorU32,
  int ChatType,
  bool IsDice
);
