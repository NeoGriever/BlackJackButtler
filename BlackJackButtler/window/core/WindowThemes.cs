using System;
using System.Numerics;
using BlackJackButtler.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private readonly ThemeManager _themes;
    private int _themeColorCount;
    private int _themeVarCount;
    private bool _themeFontPushed;
    private ThemeCorners? _themeWindowCorners;
    private Vector4 _themeWindowBackgroundColor;
    private ThemeDefinition? _currentTheme;

    private ThemeDefinition CurrentTheme => _currentTheme ?? _themes.Get("Base");

    private void PushTheme()
    {
        var area = _page switch
        {
            Page.Main => ThemeArea.Main,
            Page.Regexes => ThemeArea.Regex,
            Page.Messages => ThemeArea.Messages,
            Page.Commands or Page.OwnButtons => ThemeArea.Commands,
            Page.Presets => ThemeArea.Presets,
            Page.Settings => ThemeArea.Settings,
            Page.Stats => ThemeArea.Stats,
            Page.DrawLogic => ThemeArea.DrawLogic,
            _ => ThemeArea.None
        };
        _currentTheme = _themes.Resolve(_config.ThemeLayers, area);
        var theme = CurrentTheme;
        _themeColorCount = 0;
        _themeVarCount = 0;
        foreach (var pair in theme.Colors)
        {
            if (!Enum.TryParse<ImGuiCol>(pair.Key, true, out var slot) || !ThemeDefinition.TryColor(pair.Value, out var color)) continue;
            ImGui.PushStyleColor(slot, color);
            _themeColorCount++;
        }
        PushRounding(theme, "window", ImGuiStyleVar.WindowRounding);
        PushRounding(theme, "child", ImGuiStyleVar.ChildRounding);
        PushRounding(theme, "frame", ImGuiStyleVar.FrameRounding);
        PushRounding(theme, "button", ImGuiStyleVar.FrameRounding);
        _themeWindowCorners = theme.Corners.TryGetValue("window", out var windowCorners)
            && windowCorners.Rounding is > 0f
            && !(windowCorners.TopLeft && windowCorners.TopRight && windowCorners.BottomLeft && windowCorners.BottomRight)
            ? windowCorners : null;
        if (_themeWindowCorners != null)
        {
            _themeWindowBackgroundColor = ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg];
            ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.TitleBg, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, Vector4.Zero);
            _themeColorCount += 4;
        }
        BJBGui.ThemeButtonCorners = theme.Corners.TryGetValue("button", out var buttonCorners) ? buttonCorners : null;
        BJBGui.ThemeButtonFont = theme.Fonts.TryGetValue("button", out var buttonFont) ? buttonFont : null;
        if (_themes.GetImage(theme) != null)
        {
            ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
            _themeColorCount += 2;
        }
        if (theme.Fonts.TryGetValue("window", out var font) && font.Family.Equals("Mono", StringComparison.OrdinalIgnoreCase))
        {
            ImGui.PushFont(UiBuilder.MonoFont);
            _themeFontPushed = true;
        }
        if (font != null && ThemeDefinition.TryColor(font.Color, out var fontColor))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, fontColor);
            _themeColorCount++;
        }
    }

    private void PushRounding(ThemeDefinition theme, string area, ImGuiStyleVar style)
    {
        if (!theme.Corners.TryGetValue(area, out var corners) || corners.Rounding is not float rounding) return;
        ImGui.PushStyleVar(style, Math.Clamp(rounding, 0f, 32f));
        _themeVarCount++;
    }

    private void PopTheme()
    {
        _currentTheme = null;
        _themeWindowCorners = null;
        BJBGui.ThemeButtonCorners = null;
        BJBGui.ThemeButtonFont = null;
        if (_themeFontPushed) ImGui.PopFont();
        _themeFontPushed = false;
        ImGui.PopStyleVar(_themeVarCount);
        ImGui.PopStyleColor(_themeColorCount);
    }

    private void ApplyWindowFontScale()
    {
        if (CurrentTheme.Fonts.TryGetValue("window", out var font))
        {
            BJBGui.ThemeWindowFontScale = Math.Clamp(font.Size, 0.5f, 3f);
            ImGui.SetWindowFontScale(BJBGui.ThemeWindowFontScale);
        }
        else BJBGui.ThemeWindowFontScale = 1f;
    }

    private void DrawThemedSettingsPage()
    {
        var windowFont = CurrentTheme.Fonts.TryGetValue("window", out var baseFont) ? baseFont : null;
        if (!CurrentTheme.Fonts.TryGetValue("settings", out var font))
        {
            DrawSettingsPage();
            return;
        }
        var pushedFont = font.Family.Equals("Mono", StringComparison.OrdinalIgnoreCase) && (windowFont == null || !windowFont.Family.Equals("Mono", StringComparison.OrdinalIgnoreCase));
        if (pushedFont) ImGui.PushFont(UiBuilder.MonoFont);
        var pushedColor = ThemeDefinition.TryColor(font.Color, out var color);
        if (pushedColor) ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.SetWindowFontScale(Math.Clamp(font.Size, 0.5f, 3f));
        BJBGui.ThemeWindowFontScale = Math.Clamp(font.Size, 0.5f, 3f);
        DrawSettingsPage();
        ImGui.SetWindowFontScale(windowFont == null ? 1f : Math.Clamp(windowFont.Size, 0.5f, 3f));
        BJBGui.ThemeWindowFontScale = windowFont == null ? 1f : Math.Clamp(windowFont.Size, 0.5f, 3f);
        if (pushedColor) ImGui.PopStyleColor();
        if (pushedFont) ImGui.PopFont();
    }

    private void DrawThemeBackground()
    {
        if (_themeWindowCorners != null)
        {
            var start = ImGui.GetWindowPos();
            var windowEnd = start + ImGui.GetWindowSize();
            ImGui.GetBackgroundDrawList().AddRectFilled(start, windowEnd, ImGui.ColorConvertFloat4ToU32(_themeWindowBackgroundColor), _themeWindowCorners.Rounding!.Value, _themeWindowCorners.Flags);
        }
        var image = _themes.GetImage(CurrentTheme);
        if (image == null) return;
        var pos = ImGui.GetCursorScreenPos();
        var end = ImGui.GetWindowPos() + ImGui.GetWindowSize();
        ImGui.GetWindowDrawList().AddImage(image.Handle, pos, end);
    }

    private void BeginThemedContentChild(float height)
    {
        var custom = CurrentTheme.Corners.TryGetValue("child", out var corners)
            && corners.Rounding is > 0f
            && !(corners.TopLeft && corners.TopRight && corners.BottomLeft && corners.BottomRight)
            && string.IsNullOrWhiteSpace(CurrentTheme.BackgroundImage);
        var color = ImGui.GetStyle().Colors[(int)ImGuiCol.ChildBg];
        if (custom) ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        ImGui.BeginChild("bjb.content", new Vector2(0, height), true);
        if (custom)
        {
            ImGui.PopStyleColor();
            var start = ImGui.GetWindowPos();
            ImGui.GetWindowDrawList().AddRectFilled(start, start + ImGui.GetWindowSize(), ImGui.ColorConvertFloat4ToU32(color), corners!.Rounding!.Value, corners.Flags);
        }
    }

    private Vector4 ThemeButtonColor(string key, Vector4 fallback)
    {
        return CurrentTheme.Colors.TryGetValue(key, out var value) && ThemeDefinition.TryColor(value, out var color) ? color : fallback;
    }
}
