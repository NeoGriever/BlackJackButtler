using System;
using System.Collections.Generic;

namespace BlackJackButtler.Chat;

internal sealed class BoundedLogBuffer<T>
{
    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly Queue<T> _entries;
    private IReadOnlyList<T>? _snapshot;

    public BoundedLogBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        _entries = new Queue<T>(capacity);
    }

    public void Add(T entry)
    {
        lock (_gate)
        {
            if (_entries.Count == _capacity)
                _entries.Dequeue();
            _entries.Enqueue(entry);
            _snapshot = null;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
            _snapshot = Array.Empty<T>();
        }
    }

    public IReadOnlyList<T> Snapshot()
    {
        lock (_gate)
            return _snapshot ??= Array.AsReadOnly(_entries.ToArray());
    }
}
