using System;
using System.IO;

namespace BlackJackButtler;

internal sealed class FileReadCache<T> where T : class
{
    private readonly object _gate = new();
    private string? _path;
    private FileStamp _stamp;
    private T? _value;

    private readonly record struct FileStamp(long Length, DateTime LastWriteUtc, DateTime CreatedUtc)
    {
        public static FileStamp? Read(string path)
        {
            var info = new FileInfo(path);
            return info.Exists ? new FileStamp(info.Length, info.LastWriteTimeUtc, info.CreationTimeUtc) : null;
        }
    }

    public T? Read(string path, Func<string, T?> readFile)
    {
        lock (_gate)
        {
            var before = FileStamp.Read(path);
            if (before.HasValue && _value != null && _path == path && _stamp == before.Value)
                return _value;

            _value = null;
            _path = null;
            if (!before.HasValue)
                return null;

            var value = readFile(path);
            if (value != null && FileStamp.Read(path) == before)
            {
                _path = path;
                _stamp = before.Value;
                _value = value;
            }
            return value;
        }
    }

    public void Invalidate()
    {
        lock (_gate)
        {
            _path = null;
            _value = null;
        }
    }
}
