using System;
using System.Numerics;
using BlackJackButtler.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

namespace BlackJackButtler.Windows;

public abstract class ThemedWindow : Window
{
    private int colorCount;
    private int varCount;
    private bool fontPushed;
    private ThemeDefinition? activeTheme;
    protected virtual ThemeArea ThemeScope => ThemeArea.None;

    protected ThemedWindow(string name, ImGuiWindowFlags flags = ImGuiWindowFlags.None) : base(name, flags) { }

    public override void PreDraw()
    {
        var manager = ThemeManager.Active;
        if (manager == null) return;
        var theme = manager.Resolve(Plugin.Instance.Configuration.ThemeLayers, ThemeScope);
        activeTheme = theme;
        BJBGui.ThemeWindowFontScale = 1f;
        foreach (var pair in theme.Colors)
        {
            if (!Enum.TryParse<ImGuiCol>(pair.Key, true, out var slot) || !ThemeDefinition.TryColor(pair.Value, out var color)) continue;
            ImGui.PushStyleColor(slot, color);
            colorCount++;
        }
        if (manager.GetImage(theme) != null)
        {
            ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
            colorCount += 2;
        }
        PushRounding(theme, "window", ImGuiStyleVar.WindowRounding);
        PushRounding(theme, "child", ImGuiStyleVar.ChildRounding);
        PushRounding(theme, "frame", ImGuiStyleVar.FrameRounding);
        PushRounding(theme, "button", ImGuiStyleVar.FrameRounding);
        BJBGui.ThemeButtonCorners = theme.Corners.TryGetValue("button", out var corners) ? corners : null;
        BJBGui.ThemeButtonFont = theme.Fonts.TryGetValue("button", out var buttonFont) ? buttonFont : null;
        BJBGui.ButtonTextColor = buttonFont != null && ThemeDefinition.TryColor(buttonFont.Color, out var buttonColor) ? buttonColor : Plugin.Instance.Configuration.ButtonTextColor;
        if (theme.Fonts.TryGetValue("window", out var font) && font.Family == "Mono")
        {
            ImGui.PushFont(UiBuilder.MonoFont);
            fontPushed = true;
        }
        if (font != null && ThemeDefinition.TryColor(font.Color, out var fontColor))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, fontColor);
            colorCount++;
        }
    }

    private void PushRounding(ThemeDefinition theme, string key, ImGuiStyleVar slot)
    {
        if (!theme.Corners.TryGetValue(key, out var corners) || corners.Rounding is not float rounding) return;
        ImGui.PushStyleVar(slot, Math.Clamp(rounding, 0f, 32f));
        varCount++;
    }

    public override void PostDraw()
    {
        activeTheme = null;
        if (fontPushed) ImGui.PopFont();
        fontPushed = false;
        ImGui.PopStyleVar(varCount);
        ImGui.PopStyleColor(colorCount);
        varCount = 0;
        colorCount = 0;
        BJBGui.ThemeButtonCorners = null;
        BJBGui.ThemeButtonFont = null;
        BJBGui.ThemeWindowFontScale = 1f;
    }

    protected void BeginThemeDraw()
    {
        if (activeTheme == null) return;
        if (activeTheme.Fonts.TryGetValue("window", out var font))
        {
            BJBGui.ThemeWindowFontScale = Math.Clamp(font.Size, 0.5f, 3f);
            ImGui.SetWindowFontScale(BJBGui.ThemeWindowFontScale);
        }
        var image = ThemeManager.Active?.GetImage(activeTheme);
        if (image == null) return;
        var start = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddImage(image.Handle, start, ImGui.GetWindowPos() + ImGui.GetWindowSize());
    }
}
