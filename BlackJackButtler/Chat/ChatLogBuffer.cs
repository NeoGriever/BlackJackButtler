using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text;

namespace BlackJackButtler.Chat;

public sealed class ChatLogBuffer
{
    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly Queue<ChatLogEntry> _items;

    public ChatLogBuffer(int capacity = 20)
    {
        _capacity = Math.Max(1, capacity);
        _items = new Queue<ChatLogEntry>(_capacity);
    }

    public void Add(ChatLogEntry entry)
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

    public IReadOnlyList<ChatLogEntry> Snapshot()
    {
        lock (_gate)
            return _items.ToList();
    }
}

public sealed record ChatLogEntry(
    DateTime Timestamp,
    XivChatType ChatType,
    int ChatTypeRaw,
    string SenderText,
    string MessageText,
    string SenderHex,
    string MessageHex
);
