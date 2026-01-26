using System;
using System.Collections.Generic;
using System.Linq;

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
}

public sealed record ParsedChatMessage(
  DateTime Timestamp,
  int GroupIndexNumber,
  string Name,
  int WorldId,
  string Message,
  bool Event,
  uint ColorU32
);
