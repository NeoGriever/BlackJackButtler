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
    private const string DefaultScriptContent = """
// --- 1. GLOBAL SETTINGS & CAMERA MATH ---
// Get distance to camera for dynamic line thickness
SetVar("dx", Minus(<cameraPosition>.x, <pos>.x))
SetVar("dy", Minus(<cameraPosition>.y, <pos>.y))
SetVar("dz", Minus(<cameraPosition>.z, <pos>.z))
SetVar("dist", Sqrt(Plus(Plus(Mul(GetVar("dx"), GetVar("dx")), Mul(GetVar("dy"), GetVar("dy"))), Mul(GetVar("dz"), GetVar("dz")))))

// Dynamic Line Thickness: Thinner far away, thicker close (0.4 to 2.0 range)
SetLineThickness(Clamp(Div(8.0, Max(1, GetVar("dist"))), 0.4, 2.0))

IterateHand {
    IterateCard {
        // --- 2. DYNAMIC RADIUS & FAN MATH ---
        // Base radius is 0.5. If more than 6 cards, increase radius by 0.05 per card.
        SetVar("extraCards", Max(0, Minus(<CardsTotal>, 4)))
        SetVar("radius", Plus(0.4, Mul(GetVar("extraCards"), 0.023)))

        // Fan layout: 140 degrees total (70 left, 70 right)
        // Convert to radians: 70 deg = ~1.2217 rad
        SetVar("halfCone", 0.65)
        SetVar("divisor", Max(Minus(<CardsTotal>, 1), 1))

        // Calculate the angle 'a' within the 140-degree cone
        // If only 1 card, it stays at the center (0 offset from camera direction)
        SetVar("interpolation", Minus(Div(<CardIndex>, GetVar("divisor")), 0.5))
        SetVar("a", Mul(GetVar("interpolation"), Mul(GetVar("halfCone"), 2)))

        // Local coordinates relative to camera direction
        // Sin(a) is horizontal offset, -Cos(a) pulls it towards the camera
        SetVar("lx", Mul(Sin(GetVar("a")), GetVar("radius")))
        SetVar("lz", Mul(Cos(GetVar("a")), Mul(GetVar("radius"), -1)))

        // World-rotation matrix based on camera direction
        SetVar("wx", Plus(Mul(GetVar("lx"), Cos(<cameraDirection>)), Mul(GetVar("lz"), Sin(<cameraDirection>))))
        SetVar("wz", Minus(Mul(GetVar("lz"), Cos(<cameraDirection>)), Mul(GetVar("lx"), Sin(<cameraDirection>))))

        // --- 3. DRAWING THE CARD WITH ROUNDED CORNERS ---
        SetDrawColor(0, 0, 0, 1) // Border Black
        SetFillColor(1, 1, 1, 0.85) // Card Face White

        SetVar("targetY", Plus(Plus(<pos>.y, 1.3), Mul(<HandIndex>, 0.3)))
        BeginShape(Plus(<pos>.x, GetVar("wx")), GetVar("targetY"), Plus(<pos>.z, GetVar("wz")))

        // Card Dimensions: Width 0.09, Height 0.16 (Half: 0.045 / 0.08)
        // Corner Radius: 0.015
        SetVar("r", 0.015)
        SetVar("hw", 0.045)
        SetVar("hh", 0.08)
        SetVar("innerW", Minus(GetVar("hw"), GetVar("r")))
        SetVar("innerH", Minus(GetVar("hh"), GetVar("r")))

        BeginPath()
        // Start Top-Right Corner Arc (5 Segments)
        MoveTo(GetVar("innerW"), GetVar("hh"), 0)
        LineTo(Plus(GetVar("innerW"), Mul(GetVar("r"), 0.38)), Plus(GetVar("innerH"), Mul(GetVar("r"), 0.92)), 0)
        LineTo(Plus(GetVar("innerW"), Mul(GetVar("r"), 0.70)), Plus(GetVar("innerH"), Mul(GetVar("r"), 0.70)), 0)
        LineTo(Plus(GetVar("innerW"), Mul(GetVar("r"), 0.92)), Plus(GetVar("innerH"), Mul(GetVar("r"), 0.38)), 0)
        LineTo(GetVar("hw"), GetVar("innerH"), 0)

        // Bottom-Right Corner Arc
        LineTo(GetVar("hw"), Mul(GetVar("innerH"), -1), 0)
        LineTo(Plus(GetVar("innerW"), Mul(GetVar("r"), 0.92)), Minus(Mul(GetVar("innerH"), -1), Mul(GetVar("r"), 0.38)), 0)
        LineTo(Plus(GetVar("innerW"), Mul(GetVar("r"), 0.70)), Minus(Mul(GetVar("innerH"), -1), Mul(GetVar("r"), 0.70)), 0)
        LineTo(Plus(GetVar("innerW"), Mul(GetVar("r"), 0.38)), Minus(Mul(GetVar("innerH"), -1), Mul(GetVar("r"), 0.92)), 0)
        LineTo(GetVar("innerW"), Mul(GetVar("hh"), -1), 0)

        // Bottom-Left Corner Arc
        LineTo(Mul(GetVar("innerW"), -1), Mul(GetVar("hh"), -1), 0)
        LineTo(Minus(Mul(GetVar("innerW"), -1), Mul(GetVar("r"), 0.38)), Minus(Mul(GetVar("innerH"), -1), Mul(GetVar("r"), 0.92)), 0)
        LineTo(Minus(Mul(GetVar("innerW"), -1), Mul(GetVar("r"), 0.70)), Minus(Mul(GetVar("innerH"), -1), Mul(GetVar("r"), 0.70)), 0)
        LineTo(Minus(Mul(GetVar("innerW"), -1), Mul(GetVar("r"), 0.92)), Minus(Mul(GetVar("innerH"), -1), Mul(GetVar("r"), 0.38)), 0)
        LineTo(Mul(GetVar("hw"), -1), Mul(GetVar("innerH"), -1), 0)

        // Top-Left Corner Arc
        LineTo(Mul(GetVar("hw"), -1), GetVar("innerH"), 0)
        LineTo(Minus(Mul(GetVar("innerW"), -1), Mul(GetVar("r"), 0.92)), Plus(GetVar("innerH"), Mul(GetVar("r"), 0.38)), 0)
        LineTo(Minus(Mul(GetVar("innerW"), -1), Mul(GetVar("r"), 0.70)), Plus(GetVar("innerH"), Mul(GetVar("r"), 0.70)), 0)
        LineTo(Minus(Mul(GetVar("innerW"), -1), Mul(GetVar("r"), 0.38)), Plus(GetVar("innerH"), Mul(GetVar("r"), 0.92)), 0)
        LineTo(Mul(GetVar("innerW"), -1), GetVar("hh"), 0)
        ClosePath()

        // Content
        SetDrawColor(<CardColorR>, <CardColorG>, <CardColorB>, 1)
        SetFillColor(<CardColorR>, <CardColorG>, <CardColorB>, 0)
        IterateLetter {
            DrawChar(<letter>, Mul(Minus(<letterIndex>, Mul(Minus(<letterTotal>, 1), 0.5)), -0.035), 0, 0.035)
        }

        FinishShape()

        // --- 4. ORIENTATION ---
        // Rotate the shape to face the camera (0 flip to see the front)
        Rotate(AlterRot(<cameraDirection>, 0))

        Draw()
    }
}

""";

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
