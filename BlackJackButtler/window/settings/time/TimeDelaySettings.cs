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
    private void DrawTimeDelaySettings()
    {
        EnsureTimeZoneSelection();

        ImGui.TextUnformatted("Time Zone");
        ImGui.SameLine();
        ImGui.TextDisabled($"Current: {GetTimeZoneLabel()} ({FormatUtcOffset(_config.GetUtcOffsetMinutes())})");

        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##v3_timezone_filter", "Filter", ref _timeZoneFilter, 100);
        ImGui.Spacing();

        var filter = _timeZoneFilter.Trim();
        if (ImGui.BeginChild("##v3_timezone_list", new Vector2(0, 270f), true, ImGuiWindowFlags.HorizontalScrollbar))
        {
            var lastOffset = int.MinValue;
            foreach (var option in TimeZoneOptions.Where(
                option => string.IsNullOrWhiteSpace(filter)
                             || option.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                             || FormatUtcOffset(option.OffsetMinutes).Contains(filter, StringComparison.OrdinalIgnoreCase)
            ))
            {
                if (lastOffset != option.OffsetMinutes)
                {
                    if (lastOffset != int.MinValue)
                    {
                        ImGui.NewLine();
                        ImGui.Spacing();
                    }
                    ImGui.TextDisabled(FormatUtcOffset(option.OffsetMinutes));
                    ImGui.SameLine();
                    lastOffset = option.OffsetMinutes;
                }

                var selected = _config.UtcTimeZoneName.Equals(option.Name, StringComparison.OrdinalIgnoreCase)
                    && _config.UtcOffsetMinutes == option.OffsetMinutes;
                if (selected)
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.26f, 0.46f, 0.72f, 1f));

                if (BJBGui.Button($"{option.Name}##v3_tz_{option.Name}"))
                {
                    _config.SetUtcBaseOffsetMinutes(option.OffsetMinutes, option.Name);
                    _editingCustomUtc = false;
                    _save();
                }

                if (selected) ImGui.PopStyleColor();
                ImGui.SameLine();
            }
        }
        ImGui.EndChild();

        ImGui.Spacing();
        ImGui.TextUnformatted("Summer/Winter Time");
        ImGui.SameLine(260f);
        if (BJBOnOffSwitch.Draw("v3_summer_time", ref _config.UtcSummerTime)) _save();
        ImGui.SameLine();
        ImGui.TextDisabled("On adds +1 hour to the selected base offset.");

        if (IsCustomUtcSelection())
        {
            ImGui.Spacing();
            if (BJBGui.Button($"Custom {FormatUtcOffset(_config.UtcOffsetMinutes)}##v3_custom_utc"))
            {
                _editingCustomUtc = true;
                _customUtcInput = FormatUtcOffset(_config.UtcOffsetMinutes).Replace("UTC", string.Empty);
            }

            if (_editingCustomUtc)
                DrawCustomUtcEditor();
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Delay");
        ImGui.SameLine(260f);
        ImGui.TextDisabled("Second snapping is always enabled.");
    }
}
