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

    private static string? _autoReloadPath;
    private static DateTime _lastAutoReloadCheck;

    public const string DefaultScriptFileName = "visualize cards.txt";
    private const string DefaultScriptContent =
        "SetLineThickness(1.5)\n"
      + "\n"
      + "IterateHand {\n"
      + "    IterateCard {\n"
      + "        SetVar(\"brad\",  Minus(90, Mul(<CardsTotal>, 7))\n"
      + "        SetVar(\"erad\",  Plus(90, Mul(<CardsTotal>, 7))\n"
      + "\n"
      + "        SetVar(\"bRad\", Mul(GetVar(\"brad\"), Div(3.14159, 180)))\n"
      + "        SetVar(\"eRad\", Mul(GetVar(\"erad\"), Div(3.14159, 180)))\n"
      + "        SetVar(\"range\", Minus(GetVar(\"eRad\"), GetVar(\"bRad\")))\n"
      + "        SetVar(\"a\", Plus(GetVar(\"bRad\"), Mul(GetVar(\"range\"), Div(<CardIndex>, Max(Minus(<CardsTotal>, 1), 1)))))\n"
      + "        SetVar(\"lx\", Mul(Cos(GetVar(\"a\")), -0.35))\n"
      + "        SetVar(\"lz\", Mul(Sin(GetVar(\"a\")), 0.35))\n"
      + "\n"
      + "        SetVar(\"wx\", Plus(Mul(GetVar(\"lx\"), Cos(<dealerDirection>)), Mul(GetVar(\"lz\"), Sin(<dealerDirection>))))\n"
      + "        SetVar(\"wz\", Minus(Mul(GetVar(\"lz\"), Cos(<dealerDirection>)), Mul(GetVar(\"lx\"), Sin(<dealerDirection>))))\n"
      + "\n"
      + "        SetDrawColor(0, 0, 0, 1)\n"
      + "        SetFillColor(1, 1, 1, 1)\n"
      + "\n"
      + "        BeginShape(Plus(<pos>.x, GetVar(\"wx\")), Plus(Plus(<pos>.y, 1.3), Mul(<HandIndex>, 0.3)), Plus(<pos>.z, GetVar(\"wz\")))\n"
      + "        BeginPath()\n"
      + "        MoveTo(-0.045, -0.08, 0)\n"
      + "        LineTo(0.045, -0.08, 0)\n"
      + "        LineTo(0.045, 0.08, 0)\n"
      + "        LineTo(-0.045, 0.08, 0)\n"
      + "        ClosePath()\n"
      + "\n"
      + "        SetDrawColor(<CardColorR>, <CardColorG>, <CardColorB>, 1)\n"
      + "        SetFillColor(0, 0, 0, 0)\n"
      + "\n"
      + "        IterateLetter {\n"
      + "            DrawChar(<letter>, Mul(Minus(<letterIndex>, Mul(Minus(<letterTotal>, 1), 0.5)), -0.035), 0, 0.05)\n"
      + "        }\n"
      + "\n"
      + "        FinishShape()\n"
      + "        Rotate(AlterRot(<dealerDirection>, 180))\n"
      + "        Draw()\n"
      + "    }\n"
      + "}\n";

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
        _autoReloadPath = relativePath;
        _lastAutoReloadCheck = DateTime.MinValue;
    }

    public static void ClearAutoReload()
    {
        _autoReloadPath = null;
    }

    public static bool CheckAndApplyFileChange(DrawLogicEntry entry)
    {
        if (_autoReloadPath != entry.ScriptPath) return false;

        var now = DateTime.UtcNow;
        if ((now - _lastAutoReloadCheck).TotalSeconds < 1.0) return false;
        _lastAutoReloadCheck = now;

        var content = ReloadScript(entry.ScriptPath);
        if (content == entry.Script) return false;

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

    public static string CreateDefaultScript()
    {
        EnsureBaseDir();
        WriteScript(DefaultScriptFileName, DefaultScriptContent);
        return DefaultScriptFileName;
    }

    public static string GetBaseDirDisplay() => _baseDir;

    public static string GetBaseDir() => _baseDir;
}
