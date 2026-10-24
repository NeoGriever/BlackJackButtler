using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using BlackJackButtler.Regex;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void EnsureTimeZoneSelection()
    {
        if (_config.UtcOffsetMinutes == int.MinValue)
            _config.SetUtcBaseOffsetMinutes(_config.UtcOffsetHours * 60, null);

        if (!string.IsNullOrWhiteSpace(_config.UtcTimeZoneName)) return;
        var match = TimeZoneOptions.FirstOrDefault(option => option.OffsetMinutes == _config.UtcOffsetMinutes);
        if (match == null) return;

        _config.UtcTimeZoneName = match.Name;
        _save();
    }

    private bool IsCustomUtcSelection() => string.IsNullOrWhiteSpace(_config.UtcTimeZoneName)
        || TimeZoneOptions.All(
            option => !option.Name.Equals(_config.UtcTimeZoneName, StringComparison.OrdinalIgnoreCase)
                || option.OffsetMinutes != _config.UtcOffsetMinutes
        );

    private string GetTimeZoneLabel() => IsCustomUtcSelection() ? "Custom" : _config.UtcTimeZoneName;

    private void DrawCustomUtcEditor()
    {
        ImGui.SetNextItemWidth(120f);
        ImGui.InputText("UTC offset##v3_custom_utc_input", ref _customUtcInput, 16);
        ImGui.SameLine();
        if (BJBGui.Button("OK##v3_custom_utc_ok") && TryParseUtcOffset(_customUtcInput, out var offsetMinutes))
        {
            _config.SetUtcBaseOffsetMinutes(offsetMinutes, null);
            _editingCustomUtc = false;
            _save();
        }
        ImGui.SameLine();
        if (BJBGui.Button("Cancel##v3_custom_utc_cancel"))
            _editingCustomUtc = false;
        ImGui.SameLine();
        ImGui.TextDisabled("Examples: -3, +5:30, +5.75");
    }

    private static bool TryParseUtcOffset(string text, out int offsetMinutes)
    {
        text = text.Trim().Replace("UTC", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        var sign = 1;
        if (text.StartsWith('+')) text = text[1..];
        else if (text.StartsWith('-'))
        {
            sign = -1;
            text = text[1..];
        }

        var colonIndex = text.IndexOf(':');
        if (colonIndex > 0
            && text.IndexOf(':', colonIndex + 1) < 0
            && int.TryParse(text[..colonIndex], NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
            && int.TryParse(text[(colonIndex + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            && minutes >= 0 && minutes < 60)
        {
            offsetMinutes = Math.Clamp(sign * (hours * 60 + minutes), -12 * 60, 14 * 60);
            return true;
        }

        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var decimalHours))
        {
            offsetMinutes = Math.Clamp((int)MathF.Round(sign * decimalHours * 60f), -12 * 60, 14 * 60);
            return true;
        }

        offsetMinutes = 0;
        return false;
    }

    private static string FormatUtcOffset(int offsetMinutes)
    {
        var sign = offsetMinutes < 0 ? "-" : "+";
        var absolute = Math.Abs(offsetMinutes);
        return absolute % 60 == 0 ? $"UTC{sign}{absolute / 60}" : $"UTC{sign}{absolute / 60}:{absolute % 60:00}";
    }
}
