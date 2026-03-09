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

    private void DrawStatsPage()
    {
        ImGui.TextUnformatted("Stats");
        ImGui.Separator();

        if (_clipHoursMode < 0)
            _clipHoursMode = _config.ClipHoursMode;

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

        long gilPerHour = _config.GilPerHour;
        if (BJBGui.InputLong("Gil/Hour", ref gilPerHour, 50000, 100000))
        {
            _config.GilPerHour = gilPerHour;
            _save();
        }

        ImGui.Separator();

        var timePassed = StatsManager.GetTimePassed();
        ImGui.TextUnformatted($"Time passed:     {(int)timePassed.TotalHours:D2}:{timePassed.Minutes:D2}");

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

        ImGui.Spacing();

        if (StatsManager.StartTime != null)
        {
            DrawCalculation(timePassed);
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Round Log:");
        ImGui.Spacing();

        float logHeight = ImGui.GetContentRegionAvail().Y;
        if (logHeight < 60) logHeight = 60;
        if (ImGui.BeginChild("##round_log", new Vector2(0, logHeight), true))
        {
            for (int i = 0; i < StatsManager.RoundLog.Count; i++)
            {
                ImGui.TextWrapped($"#{i + 1}: {StatsManager.RoundLog[i]}");
            }
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
        long gilPerHour = _config.GilPerHour;
        long hourlyDeduction = (long)(clippedHours * gilPerHour);

        bool isLoss = profit < 0;
        long payoutAmount = isLoss ? 0 : (long)(profit * (payoutPercent / 100.0));
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

        int numAreaWidth = Math.Max(maxNumberWidth + 1, 13);
        int totalWidth = signCol + numAreaWidth;
        string ruler = new string('=', totalWidth);

        string Unsigned(string label, string number) =>
            label + number.PadLeft(totalWidth - label.Length);

        string Signed(string label, string number) =>
            label.PadRight(signCol - 1) + "-" + number.PadLeft(numAreaWidth);

        string gilLabel = gilPerHour >= 1000000 ? $"{gilPerHour / 1000000.0:0.#}m"
                        : gilPerHour >= 1000    ? $"{gilPerHour / 1000}k"
                        : gilPerHour.ToString("N0");
        string hoursLabel = $"  {(int)timePassed.TotalHours:D2}:{timePassed.Minutes:D2} x {gilLabel}";
        string payoutLabel = $"  {payoutPercent:0}%:";

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
            lines.Add((Unsigned(payoutLabel, tildes), null));
            lines.Add((Signed(hoursLabel, fHourly), null));
            lines.Add((ruler, null));
            lines.Add((Signed("  Total loss", fAbsTotal), totalOutcome >= 0 ? green : red));
        }
        else
        {
            lines.Add((Unsigned("  Profit:", fAbsProfit), green));
            lines.Add((Unsigned(payoutLabel, fPayout), null));
            lines.Add((Signed(hoursLabel, fHourly), null));
            lines.Add((ruler, null));
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
            foreach (var (text, _) in lines)
                sb.AppendLine(text);
            ImGui.SetClipboardText(sb.ToString());
        }
    }
}
