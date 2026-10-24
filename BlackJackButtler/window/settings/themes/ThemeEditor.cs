using System;
using System.Numerics;
using BlackJackButtler.Themes;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawThemeUiEditor(string name, ThemeDefinition theme, bool readOnly = false)
    {
        DrawThemeColorGroup(name, theme, "List header", readOnly, "HeaderColor");
        DrawThemeFontGroup(name, theme, "Window", "window", readOnly);
        DrawThemeFontGroup(name, theme, "Settings", "settings", readOnly);
        DrawThemeFontGroup(name, theme, "Buttons", "button", readOnly);
        DrawThemeColorGroup(name, theme, "General colors", readOnly, "WindowBg", "ChildBg", "PopupBg", "Text", "TextDisabled", "Border");
        DrawThemeColorGroup(name, theme, "Buttons", readOnly, "Button", "ButtonHovered", "ButtonActive");
        DrawThemeColorGroup(name, theme, "Fields", readOnly, "FrameBg", "FrameBgHovered", "FrameBgActive", "CheckMark", "SliderGrab", "SliderGrabActive");
        DrawThemeColorGroup(name, theme, "Tabs and lists", readOnly, "Header", "HeaderHovered", "HeaderActive", "Tab", "TabHovered", "TabActive");
        DrawThemeCornersGroup(name, theme, "Window", "window", true, readOnly);
        DrawThemeCornersGroup(name, theme, "Content", "child", true, readOnly);
        DrawThemeCornersGroup(name, theme, "Fields", "frame", false, readOnly);
        DrawThemeCornersGroup(name, theme, "Buttons", "button", true, readOnly);
        Header("Background image");
        ImGui.Indent(16f);
        if (BeginThemeFields("image"))
        {
            ThemeFieldRow("Path or URL");
            var source = theme.BackgroundImage;
            if (readOnly && string.IsNullOrEmpty(source)) ImGui.TextDisabled("None");
            else
            {
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("##image_source", ref source, 2048))
                {
                    theme.BackgroundImage = source;
                    SaveThemeUi(name, theme);
                }
            }
            ImGui.EndTable();
        }
        ImGui.Unindent(16f);
    }

    private void DrawThemeFontGroup(string name, ThemeDefinition theme, string title, string key, bool readOnly)
    {
        Header(title + " font");
        ImGui.Indent(16f);
        if (!theme.Fonts.TryGetValue(key, out var font)) theme.Fonts[key] = font = new ThemeFont();
        if (BeginThemeFields("font_" + key))
        {
            ThemeFieldRow("Family");
            var family = font.Family == "Mono" ? 1 : 0;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.Combo("##family", ref family, "Default\0Mono\0"))
            {
                font.Family = family == 1 ? "Mono" : "Default";
                SaveThemeUi(name, theme);
            }
            ThemeFieldRow("Size");
            var size = font.Size;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.DragFloat("##size", ref size, 0.01f, 0.5f, 3f, "%.2fx"))
            {
                font.Size = Math.Clamp(size, 0.5f, 3f);
                SaveThemeUi(name, theme);
            }
            ThemeFieldRow("Color");
            if (readOnly && string.IsNullOrEmpty(font.Color))
            {
                ImGui.TextDisabled("Inherited");
                ImGui.EndTable();
                ImGui.Unindent(16f);
                return;
            }
            var color = ThemeDefinition.TryColor(font.Color, out var parsed) ? parsed : ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
            ImGui.SetNextItemWidth(-1);
            if (ImGui.ColorEdit4("##font_color", ref color))
            {
                font.Color = ToThemeColor(color);
                SaveThemeUi(name, theme);
            }
            ImGui.EndTable();
        }
        ImGui.Unindent(16f);
    }

    private void DrawThemeColorGroup(string name, ThemeDefinition theme, string title, bool readOnly, params string[] keys)
    {
        Header(title);
        ImGui.Indent(16f);
        if (BeginThemeFields("colors_" + title))
        {
            foreach (var key in keys)
            {
                ThemeFieldRow(key == "HeaderColor" ? "Collapsed bar" : key);
                var text = key == "HeaderColor" ? theme.HeaderColor : theme.Colors.TryGetValue(key, out var stored) ? stored : null;
                if (readOnly && string.IsNullOrEmpty(text))
                {
                    ImGui.TextDisabled("Inherited");
                    continue;
                }
                var fallback = key == "HeaderColor" ? ImGui.GetStyle().Colors[(int)ImGuiCol.Header] : Enum.TryParse<ImGuiCol>(key, out var slot) ? ImGui.GetStyle().Colors[(int)slot] : Vector4.One;
                var color = ThemeDefinition.TryColor(text, out var parsed) ? parsed : fallback;
                ImGui.SetNextItemWidth(-1);
                if (!ImGui.ColorEdit4("##" + key, ref color)) continue;
                if (key == "HeaderColor") theme.HeaderColor = ToThemeColor(color);
                else theme.Colors[key] = ToThemeColor(color);
                SaveThemeUi(name, theme);
            }
            ImGui.EndTable();
        }
        ImGui.Unindent(16f);
    }

    private void DrawThemeCornersGroup(string name, ThemeDefinition theme, string title, string key, bool individual, bool readOnly)
    {
        Header(title + " corners");
        ImGui.Indent(16f);
        if (!theme.Corners.TryGetValue(key, out var corners)) theme.Corners[key] = corners = new ThemeCorners();
        if (BeginThemeFields("corners_" + key))
        {
            ThemeFieldRow("Rounding");
            if (readOnly && corners.Rounding == null) ImGui.TextDisabled("Inherited");
            else
            {
                var rounding = corners.Rounding ?? 0f;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.DragFloat("##rounding", ref rounding, 0.25f, 0f, 32f, "%.1f px"))
                {
                    corners.Rounding = Math.Clamp(rounding, 0f, 32f);
                    SaveThemeUi(name, theme);
                }
            }
            if (individual)
            {
                DrawCornerToggle(name, theme, "Top left", corners.TopLeft, value => corners.TopLeft = value);
                DrawCornerToggle(name, theme, "Top right", corners.TopRight, value => corners.TopRight = value);
                DrawCornerToggle(name, theme, "Bottom left", corners.BottomLeft, value => corners.BottomLeft = value);
                DrawCornerToggle(name, theme, "Bottom right", corners.BottomRight, value => corners.BottomRight = value);
            }
            ImGui.EndTable();
        }
        ImGui.Unindent(16f);
    }

    private void DrawCornerToggle(string name, ThemeDefinition theme, string label, bool value, Action<bool> apply)
    {
        ThemeFieldRow(label);
        if (!ImGui.Checkbox("##" + label.Replace(' ', '_'), ref value)) return;
        apply(value);
        SaveThemeUi(name, theme);
    }

    private static bool BeginThemeFields(string id)
    {
        if (!ImGui.BeginTable("theme_fields_" + id, 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp)) return false;
        ImGui.TableSetupColumn("Field", ImGuiTableColumnFlags.WidthFixed, 170f);
        ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
        return true;
    }

    private static void ThemeFieldRow(string label)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(label);
        ImGui.TableSetColumnIndex(1);
    }

    private static string ToThemeColor(Vector4 color) => $"#{(byte)(Math.Clamp(color.X, 0f, 1f) * 255f):X2}{(byte)(Math.Clamp(color.Y, 0f, 1f) * 255f):X2}{(byte)(Math.Clamp(color.Z, 0f, 1f) * 255f):X2}{(byte)(Math.Clamp(color.W, 0f, 1f) * 255f):X2}";

    private void SaveThemeUi(string name, ThemeDefinition theme) => RunThemeAction(() => _themes.Save(name, theme));

    private void DrawThemeJsonEditor(string name)
    {
        if (!_themeJsonDrafts.TryGetValue(name, out var draft)) draft = _themes.Export(name);
        ImGui.InputTextMultiline("##theme_json", ref draft, 131072, new Vector2(-1f, 330f));
        _themeJsonDrafts[name] = draft;
        if (BJBGui.Button("Apply JSON")) RunThemeAction(() =>
        {
            _themes.SaveJson(name, draft);
            _themeJsonDrafts[name] = _themes.Export(name);
        });
        ImGui.SameLine();
        if (BJBGui.Button("Reset JSON")) _themeJsonDrafts[name] = _themes.Export(name);
    }
}
