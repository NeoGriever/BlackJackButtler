using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private int _clipHoursMode = -1;
    private long _editStartBankValue;

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
        }
        ImGui.EndDisabled();

        ImGui.SameLine();

        ImGui.BeginDisabled(!StatsManager.IsRunning);
        if (BJBGui.Button("Stop Bank"))
        {
            StatsManager.StopSession();
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
                BJBGui.InputLong("##edit_start_bank_input", ref _editStartBankValue, 1000, 10000);
                if (BJBGui.SmallButton("OK##edit_start_bank_ok") || ImGui.IsKeyPressed(ImGuiKey.Enter))
                {
                    StatsManager.StartBank = _editStartBankValue;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }

        ImGui.Separator();

        ImGui.TextUnformatted($"Tips:            {StatsManager.Tips:N0}");

        var io = ImGui.GetIO();
        bool holdingShift = io.KeyShift;

        if (BJBGui.SmallButton("50k"))
            StatsManager.AddTip(holdingShift ? -50000 : 50000);
        ImGui.SameLine();
        if (BJBGui.SmallButton("100k"))
            StatsManager.AddTip(holdingShift ? -100000 : 100000);
        ImGui.SameLine();
        if (BJBGui.SmallButton("500k"))
            StatsManager.AddTip(holdingShift ? -500000 : 500000);

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

        bool useFixed = _config.UseFixedWage;
        if (ImGui.Checkbox("Fixed Wage", ref useFixed))
        {
            _config.UseFixedWage = useFixed;
            _save();
        }

        if (_config.UseFixedWage)
        {
            long fixedWage = _config.FixedWage;
            if (BJBGui.InputLong("Fixed Wage##input", ref fixedWage, 50000, 100000))
            {
                _config.FixedWage = fixedWage;
                _save();
            }
        }
        else
        {
            long gilPerHour = _config.GilPerHour;
            if (BJBGui.InputLong("Gil/Hour", ref gilPerHour, 50000, 100000))
            {
                _config.GilPerHour = gilPerHour;
                _save();
            }
        }

        ImGui.Separator();

        var timePassed = StatsManager.GetTimePassed();
        ImGui.TextUnformatted($"Time passed:     {(int)timePassed.TotalHours:D2}:{timePassed.Minutes:D2}");

        if (!_config.UseFixedWage)
        {
            ImGui.TextUnformatted("Clip hours:");
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

        if (StatsManager.StartTime != null)
        {
            DrawCalculation(timePassed);
        }
    }

    private void DrawRoundLogTab()
    {
        ImGui.Spacing();

        var io = ImGui.GetIO();
        ImGui.BeginDisabled(!io.KeyCtrl);
        if (BJBGui.SmallButton("Clear##roundlog_clear"))
        {
            RoundLogManager.ClearLog();
        }
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Hold CTRL to clear");

        ImGui.Spacing();

        float logHeight = ImGui.GetContentRegionAvail().Y;
        if (logHeight < 60) logHeight = 60;
        if (ImGui.BeginChild("##persistent_round_log", new Vector2(0, logHeight), true))
        {
            ImGui.PushFont(UiBuilder.MonoFont);
            var log = RoundLogManager.GetLog();
            string separator = new string('=', 50);

            for (int i = log.Count - 1; i >= 0; i--)
            {
                var entry = log[i];
                ImGui.TextUnformatted(separator);
                ImGui.TextUnformatted(entry.Timestamp);
                foreach (var line in entry.Lines)
                    ImGui.TextUnformatted(line);
            }

            if (log.Count > 0)
                ImGui.TextUnformatted(separator);

            ImGui.PopFont();
        }
        ImGui.EndChild();
    }

    private void DrawCalculation(TimeSpan timePassed)
    {
        const int signCol = 16;

        long nowBank = StatsManager.GetNowBank();
        long startBank = StatsManager.StartBank;
        long diff = nowBank - startBank;
        long tips = StatsManager.Tips;
        long profit = diff - tips;
        float payoutPercent = _config.PayoutPercent;
        double clippedHours = StatsManager.GetClippedHours(_clipHoursMode);

        long hourlyDeduction;
        if (_config.UseFixedWage)
            hourlyDeduction = _config.FixedWage;
        else
            hourlyDeduction = (long)Math.Round(clippedHours * _config.GilPerHour);

        bool isLoss = profit < 0;
        long payoutAmount = isLoss ? 0 : (long)Math.Round(profit * (payoutPercent / 100.0));
        long totalOutcome = isLoss ? profit - hourlyDeduction : payoutAmount - hourlyDeduction;

        string fNow = nowBank.ToString("N0");
        string fStart = startBank.ToString("N0");
        string fDiff = diff.ToString("N0");
        string fAbsDiff = Math.Abs(diff).ToString("N0");
        string fTips = tips.ToString("N0");
        string fAbsProfit = Math.Abs(profit).ToString("N0");
        string fPayout = payoutAmount.ToString("N0");
        string fHourly = hourlyDeduction.ToString("N0");
        string fAbsTotal = Math.Abs(totalOutcome).ToString("N0");

        int maxNumberWidth = new[] { fNow, fStart, fAbsDiff, fTips, fAbsProfit, fHourly, fAbsTotal }
            .Concat(isLoss ? Array.Empty<string>() : new[] { fPayout })
            .Max(s => s.Length);

        int numAreaWidth = Math.Max(maxNumberWidth + 1, 15);
        int totalWidth = signCol + numAreaWidth;
        string ruler = new string('=', totalWidth + 2);

        string Unsigned(string label, string number) =>
            label + number.PadLeft(totalWidth - label.Length);

        string Signed(string label, string number) =>
            label.PadRight(signCol - 1) + "-" + number.PadLeft(numAreaWidth);

        var green = new Vector4(0f, 1f, 0f, 1f);
        var red = new Vector4(1f, 0f, 0f, 1f);

        var lines = new List<(string text, Vector4? color)>
        {
            ("Calculation:", null),
            (ruler, null),
            (Unsigned("  Now:", fNow), null),
            (Signed("  Before:", fStart), null),
            (ruler, null),
            (Unsigned("  Diff:", fDiff), diff >= 0 ? green : red),
            (Signed("  Tips:", fTips), null),
            (ruler, null),
        };

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
            string hoursLabel = $"  {(int)timePassed.TotalHours:D2}:{timePassed.Minutes:D2} x {gilLabel}";
            lines.Add((Signed(hoursLabel, fHourly), null));
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
}
