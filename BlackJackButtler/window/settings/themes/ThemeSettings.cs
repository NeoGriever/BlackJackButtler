using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BlackJackButtler.Themes;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private const float ThemeMoveWidth = 26f;
    private string _newThemeName = "MyTheme";
    private string? _themeSettingsError;
    private bool _themeManualMode;
    private readonly Dictionary<string, string> _themeJsonDrafts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _themeRenameDrafts = new(StringComparer.OrdinalIgnoreCase);
    private string _themeDeleteName = "";
    private bool _themeDeletePending;

    private void EnsureThemeLayers()
    {
        _config.ThemeLayers ??= new();
        var changed = false;
        if (!_config.ThemeAreasMigrated)
        {
            foreach (var layer in _config.ThemeLayers)
            {
                var old = (int)layer.Areas;
                var migrated = ThemeArea.None;
                if ((old & 1) != 0) migrated |= ThemeArea.Main;
                if ((old & 2) != 0) migrated |= ThemeArea.Settings;
                if ((old & 4) != 0) migrated |= ThemeArea.Regex | ThemeArea.Messages | ThemeArea.Commands | ThemeArea.Presets | ThemeArea.Stats | ThemeArea.DrawLogic;
                layer.Areas = migrated;
            }
            _config.ThemeAreasMigrated = true;
            changed = true;
        }
        if (_config.SelectedTheme != "Base")
        {
            if (_themes.Names.Contains(_config.SelectedTheme)
                && _config.ThemeLayers.All(layer => !layer.Name.Equals(_config.SelectedTheme, StringComparison.OrdinalIgnoreCase)))
                _config.ThemeLayers.Insert(0, new ThemeLayer { Name = _config.SelectedTheme });
            _config.SelectedTheme = "Base";
            changed = true;
        }
        foreach (var name in _themes.Names.Where(name => name != "Base"))
        {
            if (_config.ThemeLayers.Any(layer => layer.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;
            _config.ThemeLayers.Add(new ThemeLayer { Name = name, Areas = ThemeArea.None });
            changed = true;
        }
        if (changed) _config.Save();
    }

    private void DrawThemeSettings()
    {
        ImGui.SetNextItemWidth(220f);
        ImGui.InputText("##new_theme_name", ref _newThemeName, 64);
        ImGui.SameLine();
        if (BJBGui.Button("Create from Base")) RunThemeAction(() => AddTheme(_themes.CreateCopy(_newThemeName)));
        ImGui.SameLine();
        if (BJBGui.Button("Import clipboard")) RunThemeAction(() => AddTheme(_themes.Import(ImGui.GetClipboardText(), "Imported Theme")));
        ImGui.SameLine();
        if (BJBGui.Button("Import file")) OpenThemeImportDialog();
        ImGui.SameLine();
        if (BJBGui.Button("Reload files")) RunThemeAction(() => { _themes.Reload(); EnsureThemeLayers(); });

        if (!string.IsNullOrEmpty(_themes.Error)) ImGui.TextColored(new Vector4(1f, 0.4f, 0.3f, 1f), _themes.Error);
        if (!string.IsNullOrEmpty(_themeSettingsError)) ImGui.TextColored(new Vector4(1f, 0.4f, 0.3f, 1f), _themeSettingsError);

        for (var index = 0; index < _config.ThemeLayers.Count; index++)
        {
            var layer = _config.ThemeLayers[index];
            var moved = false;
            ImGui.PushID($"theme_layer_{layer.Name}");
            if (index > 0 && BJBGui.Button("↑##theme_up", new Vector2(ThemeMoveWidth, ImGui.GetFrameHeight())))
            {
                (_config.ThemeLayers[index - 1], _config.ThemeLayers[index]) = (_config.ThemeLayers[index], _config.ThemeLayers[index - 1]);
                _save();
                moved = true;
            }
            else if (index == 0) ImGui.Dummy(new Vector2(ThemeMoveWidth, ImGui.GetFrameHeight()));
            ImGui.SameLine();
            if (index < _config.ThemeLayers.Count - 1 && BJBGui.Button("↓##theme_down", new Vector2(ThemeMoveWidth, ImGui.GetFrameHeight())))
            {
                (_config.ThemeLayers[index + 1], _config.ThemeLayers[index]) = (_config.ThemeLayers[index], _config.ThemeLayers[index + 1]);
                _save();
                moved = true;
            }
            else if (index == _config.ThemeLayers.Count - 1) ImGui.Dummy(new Vector2(ThemeMoveWidth, ImGui.GetFrameHeight()));
            if (moved)
            {
                ImGui.PopID();
                break;
            }
            ImGui.SameLine();
            DrawThemeEntry(layer.Name, layer, index + 1);
            ImGui.PopID();
        }
        ImGui.Dummy(new Vector2(ThemeMoveWidth, ImGui.GetFrameHeight()));
        ImGui.SameLine();
        ImGui.Dummy(new Vector2(ThemeMoveWidth, ImGui.GetFrameHeight()));
        ImGui.SameLine();
        DrawThemeEntry("Base", null, _config.ThemeLayers.Count + 1);
        if (_themeDeletePending)
        {
            ImGui.OpenPopup("Delete theme");
            _themeDeletePending = false;
        }
        DrawThemeDeletePopup();
    }

    private void DrawThemeEntry(string name, ThemeLayer? layer, int priority)
    {
        var theme = _themes.Get(name);
        var colored = ThemeDefinition.TryColor(theme.HeaderColor, out var headerColor);
        if (colored)
        {
            ImGui.PushStyleColor(ImGuiCol.Header, headerColor);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, Vector4.Min(headerColor * 1.15f, Vector4.One));
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, headerColor * 0.85f);
        }
        var areaText = layer == null ? "Locked · base" : !layer.Enabled ? "Disabled" : layer.Areas == ThemeArea.None ? "No pages" : layer.Areas.ToString();
        var open = ImGui.CollapsingHeader($"{priority:00}  {name}  [{areaText}]###theme_header_{name}");
        if (colored) ImGui.PopStyleColor(3);
        if (!open) return;
        var indent = ThemeMoveWidth * 2f + ImGui.GetStyle().ItemSpacing.X * 2f + 16f;
        ImGui.Indent(indent);
        if (layer == null)
        {
            if (BJBGui.Button("Duplicate Base")) RunThemeAction(() => AddTheme(_themes.Import(_themes.Export("Base"), "Base Copy")));
            ImGui.BeginDisabled();
            DrawThemeUiEditor(name, theme, true);
            ImGui.EndDisabled();
        }
        else
        {
            DrawThemeEntryControls(layer);
            ImGui.Separator();
            if (ImGui.RadioButton("UI editor", !_themeManualMode)) _themeManualMode = false;
            ImGui.SameLine();
            if (ImGui.RadioButton("JSON editor", _themeManualMode))
            {
                _themeManualMode = true;
                _themeJsonDrafts.Remove(name);
            }
            if (_themeManualMode) DrawThemeJsonEditor(name);
            else DrawThemeUiEditor(name, theme);
        }
        ImGui.Unindent(indent);
    }

    private void DrawThemeEntryControls(ThemeLayer layer)
    {
        var enabled = layer.Enabled;
        if (ImGui.Checkbox("##theme_enabled", ref enabled))
        {
            layer.Enabled = enabled;
            _save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Enabled");
        ImGui.SameLine(0f, 24f);
        DrawThemeAreaToggle(layer, ThemeArea.Main, "Main");
        ImGui.SameLine();
        DrawThemeAreaToggle(layer, ThemeArea.Regex, "Regex");
        ImGui.SameLine();
        DrawThemeAreaToggle(layer, ThemeArea.Messages, "Messages");
        ImGui.SameLine();
        DrawThemeAreaToggle(layer, ThemeArea.Commands, "Commands");
        ImGui.SameLine();
        DrawThemeAreaToggle(layer, ThemeArea.Presets, "Preset");
        ImGui.SameLine();
        DrawThemeAreaToggle(layer, ThemeArea.Settings, "Settings");
        ImGui.SameLine();
        DrawThemeAreaToggle(layer, ThemeArea.Stats, "Stats");
        ImGui.SameLine();
        DrawThemeAreaToggle(layer, ThemeArea.DrawLogic, "Draw Logic");
        if (!_themeRenameDrafts.TryGetValue(layer.Name, out var renameDraft)) renameDraft = layer.Name;
        ImGui.SetNextItemWidth(220f);
        ImGui.InputText("Name", ref renameDraft, 64);
        _themeRenameDrafts[layer.Name] = renameDraft;
        ImGui.SameLine();
        if (BJBGui.Button("Rename")) RunThemeAction(() =>
        {
            var oldName = layer.Name;
            _themes.Rename(oldName, renameDraft);
            layer.Name = renameDraft;
            _themeRenameDrafts.Remove(oldName);
            _themeJsonDrafts.Remove(oldName);
            _save();
        });
        ImGui.SameLine();
        if (BJBGui.Button("Duplicate")) RunThemeAction(() => AddTheme(_themes.Import(_themes.Export(layer.Name), layer.Name + " Copy")));
        ImGui.SameLine();
        if (BJBGui.Button("Delete"))
        {
            _themeDeleteName = layer.Name;
            _themeDeletePending = true;
        }
        if (BJBGui.Button("Export clipboard")) ImGui.SetClipboardText(_themes.Export(layer.Name));
        ImGui.SameLine();
        if (BJBGui.Button("Export file")) OpenThemeExportDialog(layer.Name);
    }

    private void DrawThemeAreaToggle(ThemeLayer layer, ThemeArea area, string label)
    {
        var enabled = (layer.Areas & area) != 0;
        if (ImGui.Checkbox("##theme_area_" + area, ref enabled))
        {
            layer.Areas = enabled ? layer.Areas | area : layer.Areas & ~area;
            _save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(label);
    }

    private void AddTheme(string name)
    {
        _config.ThemeLayers.Insert(0, new ThemeLayer { Name = name });
        _save();
    }

    private void RunThemeAction(Action action)
    {
        try { action(); _themeSettingsError = null; }
        catch (Exception ex) { _themeSettingsError = ex.Message; }
    }

    private void DrawThemeDeletePopup()
    {
        if (!ImGui.BeginPopupModal("Delete theme", ImGuiWindowFlags.AlwaysAutoResize)) return;
        ImGui.TextUnformatted(_themeDeleteName);
        if (BJBGui.Button("Delete"))
        {
            RunThemeAction(() =>
            {
                _themes.Delete(_themeDeleteName);
                _config.ThemeLayers.RemoveAll(layer => layer.Name == _themeDeleteName);
                _save();
            });
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (BJBGui.Button("Cancel")) ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }
}
