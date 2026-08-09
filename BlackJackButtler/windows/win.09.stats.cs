using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.IO;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private int _clipHoursMode = -1;
    private long _editStartBankValue;
    private bool _confirmEraseNormalStatsLog;
    private long _customTipValue = 100000;
    private string? _selectedUserStatisticsPath;
    private bool _editingTips;
    private bool _focusTipsInput;
    private string _tipsInput = string.Empty;

    private void DrawStatsPage()
    {
        if (_clipHoursMode < 0)
            _clipHoursMode = _config.ClipHoursMode;

        if (ImGui.BeginTabBar("##stats_tabs"))
        {
            if (ImGui.BeginTabItem("Stats"))
            {
                DrawStatsTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Round Log"))
            {
                DrawRoundLogTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Round History"))
            {
                DrawRoundLogPage();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("User Statistics"))
            {
                DrawUserStatisticsTab();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void DrawStatsTab()
    {
        ImGui.Spacing();

        ImGui.BeginDisabled(StatsManager.IsRunning);
        if (BJBGui.Button("Start Bank"))
        {
            StatsManager.StartSession();
            SaveSessionFromUI();
        }
        ImGui.EndDisabled();

        ImGui.SameLine();

        ImGui.BeginDisabled(!StatsManager.IsRunning);
        if (BJBGui.Button("End Bank"))
        {
            StatsManager.StopSession();
            SaveSessionFromUI();
        }
        ImGui.EndDisabled();

        if (StatsManager.StartTime != null)
        {
            ImGui.TextUnformatted($"Start Bank:      {StatsManager.StartBank:N0}");
            ImGui.SameLine();
            ImGui.PushFont(UiBuilder.IconFont);
            if (BJBGui.SmallButton(FontAwesomeIcon.PencilAlt.ToIconString() + "##edit_start_bank"))
            {
                _editStartBankValue = StatsManager.StartBank;
                ImGui.OpenPopup("edit_start_bank_popup");
            }
            ImGui.PopFont();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Edit Start Bank");

            if (ImGui.BeginPopup("edit_start_bank_popup"))
            {
                ImGui.TextUnformatted("Start Bank:");
                ImGui.SetNextItemWidth(200f);
                BJBGui.InputLongFormatted("##edit_start_bank_input", ref _editStartBankValue);
                if (BJBGui.SmallButton("OK##edit_start_bank_ok") || ImGui.IsKeyPressed(ImGuiKey.Enter))
                {
                    StatsManager.StartBank = _editStartBankValue;
                    SaveSessionFromUI();
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }

            ImGui.TextUnformatted($"Start Time:      {StatsManager.StartTime.Value.ToString("M/d/yyyy h:mmtt", CultureInfo.InvariantCulture).ToLower()}");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(180f);
            ImGui.InputText("##start_time_input", ref _startTimeInputBuffer, 32);
            ImGui.SameLine();
            if (BJBGui.SmallButton("Set##start_time_set"))
            {
                if (DateTime.TryParseExact(
                        _startTimeInputBuffer.Trim(),
                        new[] { "M/d/yyyy h:mmtt", "M/d/yyyy hh:mmtt", "M/d/yyyy h:mm tt", "M/d/yyyy hh:mm tt" },
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeLocal,
                        out var parsed))
                {
                    StatsManager.StartTime = DateTime.SpecifyKind(parsed, DateTimeKind.Local);
                    _startTimeInputError = null;
                    _startTimeInputBuffer = "";
                    SaveSessionFromUI();
                }
                else
                {
                    _startTimeInputError = "Invalid format. Expected: M/D/YYYY HH:MMam|pm";
                }
            }
            if (!string.IsNullOrEmpty(_startTimeInputError))
                ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), _startTimeInputError);
        }

        ImGui.Separator();

        ImGui.TextUnformatted("Tips:");
        ImGui.SameLine();
        if (_editingTips)
        {
            if (_focusTipsInput)
            {
                ImGui.SetKeyboardFocusHere();
                _focusTipsInput = false;
            }
            ImGui.SetNextItemWidth(180f);
            var submitted = ImGui.InputText("##tips_direct_input", ref _tipsInput, 32,
                ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.EnterReturnsTrue);
            ImGui.SameLine();
            if (submitted || BJBGui.SmallButton("OK##tips_direct_ok"))
            {
                var numeric = _tipsInput.Replace(",", string.Empty, StringComparison.Ordinal).Trim();
                if (long.TryParse(numeric, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
                {
                    StatsManager.Tips = Math.Max(0, value);
                    SaveSessionFromUI();
                    _editingTips = false;
                }
            }
        }
        else
        {
            ImGui.TextUnformatted(StatsManager.Tips.ToString("N0", CultureInfo.InvariantCulture));
            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                _tipsInput = StatsManager.Tips.ToString(CultureInfo.InvariantCulture);
                _editingTips = true;
                _focusTipsInput = true;
            }
        }

        var io = ImGui.GetIO();
        bool holdingShift = io.KeyShift;

        var tipButtons = new (string Label, long Amount)[]
        {
            ("1k", 1_000), ("5k", 5_000), ("10k", 10_000), ("50k", 50_000),
            ("100k", 100_000), ("250k", 250_000), ("500k", 500_000), ("1m", 1_000_000),
        };
        for (var i = 0; i < tipButtons.Length; i++)
        {
            if (i > 0) ImGui.SameLine();
            if (!BJBGui.SmallButton(tipButtons[i].Label)) continue;
            StatsManager.AddTip(holdingShift ? -tipButtons[i].Amount : tipButtons[i].Amount);
            SaveSessionFromUI();
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton("Custom"))
            ImGui.OpenPopup("custom_tip_popup");

        if (ImGui.BeginPopup("custom_tip_popup"))
        {
            ImGui.TextUnformatted("Tip amount:");
            ImGui.SetNextItemWidth(180f);
            BJBGui.InputLongFormatted("##custom_tip_value", ref _customTipValue);
            if (_customTipValue < 0) _customTipValue = 0;
            if (BJBGui.SmallButton("Add##custom_tip_add") || ImGui.IsKeyPressed(ImGuiKey.Enter))
            {
                StatsManager.AddTip(holdingShift ? -_customTipValue : _customTipValue);
                SaveSessionFromUI();
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        if (holdingShift)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1f, 1f, 0f, 1f), "(Shift = subtract)");
        }

        ImGui.Separator();

        float payout = _config.PayoutPercent;
        if (BJBGui.SliderFloat("Payout %", ref payout, 0f, 100f, "%.0f%%"))
        {
            _config.PayoutPercent = payout;
            _save();
        }

        ImGui.TextUnformatted("Fixed Wage");
        ImGui.SameLine(170f);
        if (BJBOnOffSwitch.Draw("stats_fixed_wage", ref _config.UseFixedWage)) _save();
        ImGui.SameLine();
        ImGui.SetNextItemWidth(170f);
        if (_config.UseFixedWage)
        {
            var fixedWage = _config.FixedWage;
            if (BJBGui.InputLongFormatted("##fixed_wage", ref fixedWage))
            {
                _config.FixedWage = Math.Max(0, fixedWage);
                _save();
            }
        }
        else
        {
            var gilPerInterval = _config.GilPerHour;
            if (BJBGui.InputLongFormatted("##wage_rate", ref gilPerInterval))
            {
                _config.GilPerHour = Math.Max(0, gilPerInterval);
                _save();
            }
            ImGui.SameLine();
            var interval = (int)_config.WageIntervalMode;
            ImGui.SetNextItemWidth(130f);
            if (BJBGui.Combo("##wage_interval", ref interval,
                    "Gil/Minute\0Gil/15 Min\0Gil/30 Min\0Gil/Hour\0Gil/2 Hours\0"))
            {
                _config.WageIntervalMode = (WageInterval)Math.Clamp(interval, 0, 4);
                _save();
            }
        }

        ImGui.Separator();

        var timePassed = StatsManager.GetTimePassed();
        ImGui.TextUnformatted($"Time passed:     {(int)timePassed.TotalHours:D2}:{timePassed.Minutes:D2}");

        if (!_config.UseFixedWage)
        {
            ImGui.TextUnformatted("Clip intervals:");
            ImGui.SameLine();

            string[] modes = { "Up", "Down", "Even" };
            for (int i = 0; i < 3; i++)
            {
                if (i > 0) ImGui.SameLine();
                bool selected = _clipHoursMode == i;
                if (selected)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, _config.HighlightColor);
                    ImGui.PushStyleColor(ImGuiCol.Text, _config.HighlightTextColor);
                }
                if (ImGui.SmallButton(modes[i]))
                {
                    _clipHoursMode = i;
                    _config.ClipHoursMode = i;
                    _save();
                }
                if (selected) ImGui.PopStyleColor(2);
            }
        }

        ImGui.Spacing();

        if (ImGui.CollapsingHeader("Stats Settings##stats_settings"))
        {
            if (ImGui.Checkbox("Subtract player banks from profit", ref _config.StatsSubtractPlayerBanks)) _save();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("When enabled, the sum of all player banks is subtracted from\nthe manager's on-hand difference before profit is calculated.");

            ImGui.TextUnformatted("House Bank (gil)");
            ImGui.SameLine(200f);
            ImGui.SetNextItemWidth(200f);
            long houseVal = _config.StatsHouseBank;
            if (BJBGui.InputLongFormatted("##house_bank", ref houseVal))
            {
                _config.StatsHouseBank = Math.Max(0, houseVal);
                _save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Amount kept aside as house float.\nSubtracted from diff, then added back after payout/wage\n(\"Return to mngr\" shows the final amount after refund).");
        }

        ImGui.Spacing();

        if (StatsManager.StartTime != null)
        {
            DrawCalculation(timePassed);
        }
    }

    private void DrawUserStatisticsTab()
    {
        var sessions = UserStatisticsManager.GetSessions();
        if (sessions.Count == 0)
        {
            ImGui.TextDisabled("No user statistics sessions have been recorded yet.");
            ImGui.TextWrapped("A new text file is created under userstats when Group Detector starts.");
            return;
        }

        var currentPath = UserStatisticsManager.CurrentFilePath;
        if (string.IsNullOrWhiteSpace(_selectedUserStatisticsPath)
            || sessions.All(session => !session.FilePath.Equals(_selectedUserStatisticsPath, StringComparison.OrdinalIgnoreCase)))
        {
            _selectedUserStatisticsPath = UserStatisticsManager.IsActive
                                          && !string.IsNullOrWhiteSpace(currentPath)
                                          && sessions.Any(session => session.FilePath.Equals(currentPath, StringComparison.OrdinalIgnoreCase))
                ? currentPath
                : sessions[0].FilePath;
        }

        var selected = sessions.FirstOrDefault(session =>
                           session.FilePath.Equals(_selectedUserStatisticsPath, StringComparison.OrdinalIgnoreCase))
                       ?? sessions[0];

        ImGui.TextUnformatted("Session");
        ImGui.SameLine(100f);
        ImGui.SetNextItemWidth(360f);
        var preview = FormatUserStatisticsSessionLabel(selected);
        if (ImGui.BeginCombo("##user_statistics_session", preview))
        {
            foreach (var session in sessions)
            {
                var isSelected = session.FilePath.Equals(selected.FilePath, StringComparison.OrdinalIgnoreCase);
                if (ImGui.Selectable(FormatUserStatisticsSessionLabel(session), isSelected))
                    _selectedUserStatisticsPath = session.FilePath;
                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        ImGui.SameLine();
        if (UserStatisticsManager.IsActive) ImGui.BeginDisabled();
        if (BJBGui.Button("Save As..."))
        {
            var sourcePath = selected.FilePath;
            var defaultName = Path.GetFileNameWithoutExtension(sourcePath);
            _fileDialogManager.SaveFileDialog(
                "Export User Statistics",
                "Text Files{.txt}",
                defaultName,
                ".txt",
                (ok, path) =>
                {
                    if (ok && !string.IsNullOrWhiteSpace(path))
                        File.Copy(sourcePath, path, true);
                });
        }
        if (UserStatisticsManager.IsActive) ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(UserStatisticsManager.IsActive
                ? "Deactivate Group Detector before exporting user statistics."
                : "Export the selected user statistics text file.");

        ImGui.Spacing();
        ImGui.TextDisabled(selected.IsActive
            ? $"Live session started {selected.StartedAt:yyyy-MM-dd HH:mm:ss}"
            : $"Session {selected.StartedAt:yyyy-MM-dd HH:mm:ss} - {selected.EndedAt:yyyy-MM-dd HH:mm:ss}");
        ImGui.TextDisabled(selected.FilePath);
        ImGui.Separator();

        if (selected.Players.Count == 0)
        {
            ImGui.TextDisabled("No completed player trades have been recorded in this session.");
            return;
        }

        var height = Math.Max(100f, ImGui.GetContentRegionAvail().Y);
        if (ImGui.BeginChild("##user_statistics_content", new Vector2(0, height), true))
        {
            ImGui.PushFont(UiBuilder.MonoFont);
            foreach (var player in selected.Players.OrderBy(player => player.Identity, StringComparer.OrdinalIgnoreCase))
            {
                var result = player.Result;
                ImGui.TextUnformatted(player.Identity);
                ImGui.TextUnformatted($"  Traded in: {player.TradedIn,18:N0}");
                ImGui.TextUnformatted($"  Paid out:  {player.PaidOut,18:N0}");
                ImGui.TextColored(
                    result >= 0
                        ? new Vector4(0.25f, 1f, 0.35f, 1f)
                        : new Vector4(1f, 0.3f, 0.3f, 1f),
                    $"  Result:    {(result >= 0 ? "+" : "-")} {Math.Abs(result),16:N0}");
                ImGui.Spacing();
            }
            ImGui.PopFont();
        }
        ImGui.EndChild();
    }

    private static string FormatUserStatisticsSessionLabel(UserStatisticsSession session)
    {
        var status = session.IsActive ? "LIVE" : "Completed";
        return $"{session.StartedAt:yyyy-MM-dd HH:mm:ss} ({status})";
    }

    private void DrawRoundLogTab()
    {
        ImGui.Spacing();

        if (ImGui.BeginTabBar("##stats_log_tabs"))
        {
            if (ImGui.BeginTabItem("Normal Log"))
            {
                DrawStatsLogViewer(debugLog: false);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Debug Log"))
            {
                DrawStatsLogViewer(debugLog: true);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawStatsLogViewer(bool debugLog)
    {
        var path = debugLog ? StatsLogManager.DebugLogPath : StatsLogManager.NormalLogPath;
        ImGui.TextUnformatted(string.IsNullOrWhiteSpace(path) ? "No statistics log active." : path);

        if (BJBGui.SmallButton($"Copy All##statslog_copy_{debugLog}"))
        {
            var sb = new StringBuilder();
            sb.AppendLine("```");
            foreach (var line in StatsLogManager.ReadCurrentLines(debugLog))
                sb.AppendLine(line);
            sb.AppendLine("```");
            ImGui.SetClipboardText(sb.ToString());
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton($"Export HTML##statslog_html_{debugLog}"))
            StatsLogManager.ExportCurrentHtml(debugLog);

        if (!debugLog)
        {
            ImGui.SameLine();
            if (BJBGui.SmallButton("Erase Log##statslog_erase"))
            {
                _confirmEraseNormalStatsLog = true;
                ImGui.OpenPopup("Erase current normal statistics log?");
            }
        }

        if (ImGui.BeginPopupModal("Erase current normal statistics log?", ref _confirmEraseNormalStatsLog, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextUnformatted("Delete the current normal statistics log file?");
            ImGui.TextDisabled("This does not reset numbering and does not affect the debug log.");
            ImGui.Spacing();

            if (BJBGui.Button("Delete", new Vector2(120, 0)))
            {
                StatsLogManager.EraseNormalLog();
                _confirmEraseNormalStatsLog = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (BJBGui.Button("Cancel", new Vector2(120, 0)))
            {
                _confirmEraseNormalStatsLog = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        ImGui.Spacing();

        float logHeight = ImGui.GetContentRegionAvail().Y;
        if (logHeight < 60) logHeight = 60;
        if (ImGui.BeginChild($"##persistent_stats_log_{debugLog}", new Vector2(0, logHeight), true))
        {
            ImGui.PushFont(UiBuilder.MonoFont);
            foreach (var line in StatsLogManager.ReadCurrentLines(debugLog))
                ImGui.TextUnformatted(line);
            ImGui.PopFont();
        }
        ImGui.EndChild();
    }

    private void DrawCalculation(TimeSpan timePassed)
    {
        const int signCol = 16;

        long nowBank = StatsManager.GetNowBank();
        long startBank = StatsManager.StartBank;
        long houseBank = _config.StatsHouseBank;
        long bankSum = _config.StatsSubtractPlayerBanks ? _players.Sum(p => p.Bank) : 0;
        long diff = nowBank - startBank - bankSum - houseBank;
        long tips = StatsManager.Tips;
        long profit = diff - tips;
        float payoutPercent = _config.PayoutPercent;
        double clippedWageUnits = StatsManager.GetClippedWageUnits(_clipHoursMode, _config.WageIntervalMode);

        long hourlyDeduction;
        if (_config.UseFixedWage)
            hourlyDeduction = _config.FixedWage;
        else
            hourlyDeduction = (long)Math.Round(clippedWageUnits * _config.GilPerHour);

        bool isLoss = profit < 0;
        long payoutAmount = isLoss ? 0 : (long)Math.Round(profit * (payoutPercent / 100.0));
        long totalOutcome = isLoss ? profit - hourlyDeduction : payoutAmount - hourlyDeduction;
        long finalOutcome = totalOutcome + houseBank;

        string fNow = nowBank.ToString("N0");
        string fStart = startBank.ToString("N0");
        string fBankSum = bankSum.ToString("N0");
        string fHouse = houseBank.ToString("N0");
        string fDiff = diff.ToString("N0");
        string fAbsDiff = Math.Abs(diff).ToString("N0");
        string fTips = tips.ToString("N0");
        string fAbsProfit = Math.Abs(profit).ToString("N0");
        string fPayout = payoutAmount.ToString("N0");
        string fHourly = hourlyDeduction.ToString("N0");
        string fAbsTotal = Math.Abs(totalOutcome).ToString("N0");
        string fAbsFinal = Math.Abs(finalOutcome).ToString("N0");

        int maxNumberWidth = new[] { fNow, fStart, fAbsDiff, fTips, fAbsProfit, fHourly, fAbsTotal, fAbsFinal }
            .Concat(isLoss ? Array.Empty<string>() : new[] { fPayout })
            .Concat(bankSum > 0 ? new[] { fBankSum } : Array.Empty<string>())
            .Concat(houseBank > 0 ? new[] { fHouse } : Array.Empty<string>())
            .Max(s => s.Length);

        int numAreaWidth = Math.Max(maxNumberWidth + 1, 15);
        int totalWidth = signCol + numAreaWidth;
        string ruler = new string('=', totalWidth + 2);

        string Unsigned(string label, string number) =>
            label + number.PadLeft(totalWidth - label.Length);

        string Signed(string label, string number) =>
            label.PadRight(signCol - 1) + "-" + number.PadLeft(numAreaWidth);

        string SignedPlus(string label, string number) =>
            label.PadRight(signCol - 1) + "+" + number.PadLeft(numAreaWidth);

        var green = new Vector4(0f, 1f, 0f, 1f);
        var red = new Vector4(1f, 0f, 0f, 1f);

        var lines = new List<(string text, Vector4? color)>
        {
            ("Calculation:", null),
            (ruler, null),
            (Unsigned("  Now:", fNow), null),
            (Signed("  Before:", fStart), null),
        };

        if (_config.StatsSubtractPlayerBanks && bankSum > 0)
            lines.Add((Signed("  Player banks:", fBankSum), null));

        if (houseBank > 0)
            lines.Add((Signed("  House bank:", fHouse), null));

        lines.Add((ruler, null));
        lines.Add((Unsigned("  Diff:", fDiff), diff >= 0 ? green : red));
        lines.Add((Signed("  Tips:", fTips), null));
        lines.Add((ruler, null));

        if (isLoss)
        {
            lines.Add((Signed("  Loss:", fAbsProfit), red));
            string tildes = new string('~', numAreaWidth + 1);
            string payoutLabel = $"  {payoutPercent:0}%:";
            lines.Add((Unsigned(payoutLabel, tildes), null));
        }
        else
        {
            lines.Add((Unsigned("  Profit:", fAbsProfit), green));
            string payoutLabel = $"  {payoutPercent:0}%:";
            lines.Add((Unsigned(payoutLabel, fPayout), null));
        }

        if (_config.UseFixedWage)
        {
            string wageLabel = hourlyDeduction >= 1000000 ? $"{hourlyDeduction / 1000000.0:0.#}m"
                             : hourlyDeduction >= 1000    ? $"{hourlyDeduction / 1000}k"
                             : hourlyDeduction.ToString("N0");
            lines.Add((Signed($"  Fixed {wageLabel}:", fHourly), null));
        }
        else
        {
            long gilPerHour = _config.GilPerHour;
            string gilLabel = gilPerHour >= 1000000 ? $"{gilPerHour / 1000000.0:0.#}m"
                            : gilPerHour >= 1000    ? $"{gilPerHour / 1000}k"
                            : gilPerHour.ToString("N0");
            string intervalLabel = GetWageIntervalLabel(_config.WageIntervalMode);
            string wageLabel = $"  {(int)timePassed.TotalHours:D2}:{timePassed.Minutes:D2} x {gilLabel}/{intervalLabel}";
            lines.Add((Signed(wageLabel, fHourly), null));
        }

        lines.Add((ruler, null));

        if (isLoss)
        {
            lines.Add((Signed("  Total loss", fAbsTotal), totalOutcome >= 0 ? green : red));
        }
        else
        {
            string totalStr = totalOutcome >= 0 ? fAbsTotal : $"-{fAbsTotal}";
            lines.Add((Unsigned("  Total outcome", totalStr), totalOutcome >= 0 ? green : red));
        }

        if (houseBank > 0)
        {
            lines.Add((SignedPlus("  House bank:", fHouse), null));
            lines.Add((ruler, null));
            string finalStr = finalOutcome >= 0 ? fAbsFinal : $"-{fAbsFinal}";
            lines.Add((Unsigned("  Return to mngr", finalStr), finalOutcome >= 0 ? green : red));
        }

        if (_config.HashedStats)
        {
            string hash = StatsHashManager.C(lines.Select(l => l.text));
            lines.Add(("", null));
            lines.Add(("", null));
            lines.Add((Unsigned("  Integrity:", hash), null));
        }

        ImGui.PushFont(UiBuilder.MonoFont);
        foreach (var (text, color) in lines)
        {
            if (color.HasValue)
                ImGui.TextColored(color.Value, text);
            else
                ImGui.TextUnformatted(text);
        }
        ImGui.PopFont();

        ImGui.Spacing();

        if (BJBGui.Button("Copy"))
        {
            var sb = new StringBuilder();
            sb.AppendLine("```");
            foreach (var (text, _) in lines)
                sb.AppendLine(text);
            sb.AppendLine("```");
            ImGui.SetClipboardText(sb.ToString());
        }
    }

    private static string GetWageIntervalLabel(WageInterval interval) => interval switch
    {
        WageInterval.Minute => "min",
        WageInterval.FifteenMinutes => "15m",
        WageInterval.ThirtyMinutes => "30m",
        WageInterval.TwoHours => "2h",
        _ => "h",
    };
}
