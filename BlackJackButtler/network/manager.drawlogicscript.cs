using System;
using System.Collections.Generic;
using System.IO;

namespace BlackJackButtler;

public static class DrawLogicScriptManager
{
    private static string _baseDir = string.Empty;
    private static string _configDir = string.Empty;
    private static Configuration? _config;
    private static readonly Dictionary<string, string> _scriptCache = new();

    private static FileSystemWatcher? _watcher;
    private static string? _watchedPath;
    private static volatile bool _fileChanged;

    public static void Init(string configDir, Configuration config)
    {
        _configDir = configDir;
        _config = config;
        _baseDir = GetBaseDir(config);
        EnsureBaseDir();
        MigrateInlineScripts(config.DrawLogicEntries);
    }

    public static void Dispose()
    {
        ClearAutoReload();
        _scriptCache.Clear();
    }

    private static string GetBaseDir(Configuration config)
    {
        if (!string.IsNullOrEmpty(config.DrawLogicScriptDir))
            return config.DrawLogicScriptDir;
        return Path.Combine(_configDir, "drawlogic");
    }

    private static void EnsureBaseDir()
    {
        if (!Directory.Exists(_baseDir))
            Directory.CreateDirectory(_baseDir);
    }

    public static string ReadScript(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return "";
        if (_scriptCache.TryGetValue(relativePath, out var cached)) return cached;

        var fullPath = Path.Combine(_baseDir, relativePath);
        if (!File.Exists(fullPath)) return "";

        try
        {
            var content = File.ReadAllText(fullPath);
            _scriptCache[relativePath] = content;
            return content;
        }
        catch { return ""; }
    }

    public static void WriteScript(string relativePath, string content)
    {
        if (string.IsNullOrEmpty(relativePath)) return;
        EnsureBaseDir();

        var fullPath = Path.Combine(_baseDir, relativePath);
        try
        {
            File.WriteAllText(fullPath, content);
            _scriptCache[relativePath] = content;
        }
        catch { }
    }

    public static string ReloadScript(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return "";
        _scriptCache.Remove(relativePath);

        var fullPath = Path.Combine(_baseDir, relativePath);
        if (!File.Exists(fullPath)) return "";

        try
        {
            var content = File.ReadAllText(fullPath);
            _scriptCache[relativePath] = content;
            return content;
        }
        catch { return ""; }
    }

    public static string CreateNewFile(int index)
    {
        EnsureBaseDir();
        var fileName = $"{index}.txt";
        var fullPath = Path.Combine(_baseDir, fileName);

        while (File.Exists(fullPath))
        {
            index++;
            fileName = $"{index}.txt";
            fullPath = Path.Combine(_baseDir, fileName);
        }

        File.WriteAllText(fullPath, "");
        return fileName;
    }

    public static void SoftDeleteFile(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return;
        var fullPath = Path.Combine(_baseDir, relativePath);
        if (!File.Exists(fullPath)) return;

        try
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(relativePath);
            var now = DateTime.Now;
            var newName = $"{nameWithoutExt}.del.{now:yyyy-MM-dd}.{now:HH-mm-ss}.txt";
            var newPath = Path.Combine(_baseDir, newName);
            File.Move(fullPath, newPath);
            _scriptCache.Remove(relativePath);
        }
        catch { }
    }

    public static void SetAutoReload(string relativePath)
    {
        ClearAutoReload();
        if (string.IsNullOrEmpty(relativePath)) return;

        var fullPath = Path.Combine(_baseDir, relativePath);
        if (!File.Exists(fullPath)) return;

        _watchedPath = relativePath;
        _fileChanged = false;

        try
        {
            _watcher = new FileSystemWatcher(Path.GetDirectoryName(fullPath)!, Path.GetFileName(fullPath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += (_, _) => _fileChanged = true;
        }
        catch
        {
            _watcher = null;
            _watchedPath = null;
        }
    }

    public static void ClearAutoReload()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
        _watchedPath = null;
        _fileChanged = false;
    }

    public static bool CheckAndApplyFileChange(DrawLogicEntry entry)
    {
        if (!_fileChanged) return false;
        if (_watchedPath != entry.ScriptPath) return false;

        _fileChanged = false;
        var content = ReloadScript(entry.ScriptPath);
        entry.Script = content;
        return true;
    }

    public static void MigrateInlineScripts(List<DrawLogicEntry> entries)
    {
        bool changed = false;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (!string.IsNullOrEmpty(entry.Script) && string.IsNullOrEmpty(entry.ScriptPath))
            {
                var path = CreateNewFile(i);
                WriteScript(path, entry.Script);
                entry.ScriptPath = path;
                entry.Script = "";
                changed = true;
            }
        }
        if (changed)
            _config?.Save();
    }

    public static string GetBaseDirDisplay() => _baseDir;
}
