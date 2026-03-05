using System;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;

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
        const string Ruler = "===============================";

        long nowBank = StatsManager.GetNowBank();
        long startBank = StatsManager.StartBank;
        long diff = nowBank - startBank;
        long tips = StatsManager.Tips;
        long profit = diff - tips;
        float payoutPercent = _config.PayoutPercent;
        long payoutAmount = (long)(profit * (payoutPercent / 100.0));
        double clippedHours = StatsManager.GetClippedHours(_clipHoursMode);
        long gilPerHour = _config.GilPerHour;
        long hourlyDeduction = (long)(clippedHours * gilPerHour);
        long totalOutcome = payoutAmount - hourlyDeduction;

        ImGui.TextUnformatted("Calculation:");
        ImGui.TextUnformatted(Ruler);

        ImGui.TextUnformatted($"  Now:           {nowBank,18:N0}");
        ImGui.TextUnformatted($"  Before:      - {startBank,18:N0}");
        ImGui.TextUnformatted(Ruler);

        var diffColor = diff >= 0 ? new Vector4(0f, 1f, 0f, 1f) : new Vector4(1f, 0f, 0f, 1f);
        ImGui.TextColored(diffColor, $"  Diff:          {diff,18:N0}");
        ImGui.TextUnformatted($"  Tips:        - {tips,18:N0}");
        ImGui.TextUnformatted(Ruler);

        var profitColor = profit >= 0 ? new Vector4(0f, 1f, 0f, 1f) : new Vector4(1f, 0f, 0f, 1f);
        ImGui.TextColored(profitColor, $"  Profit:        {profit,18:N0}");
        ImGui.TextUnformatted($"  {payoutPercent:0}%:           {payoutAmount,18:N0}");

        string gilLabel = gilPerHour >= 1000000 ? $"{gilPerHour / 1000000.0:0.#}m"
                       : gilPerHour >= 1000   ? $"{gilPerHour / 1000}k"
                       : $"{gilPerHour:N0}";
        string hoursLabel = $"{(int)timePassed.TotalHours:D2}:{timePassed.Minutes:D2} x {gilLabel}";
        ImGui.TextUnformatted($"  {hoursLabel,-16} - {hourlyDeduction,18:N0}");
        ImGui.TextUnformatted(Ruler);

        var outcomeColor = totalOutcome >= 0 ? new Vector4(0f, 1f, 0f, 1f) : new Vector4(1f, 0f, 0f, 1f);
        ImGui.TextColored(outcomeColor, $"  Total outcome  {totalOutcome,18:N0}");

        ImGui.Spacing();

        if (BJBGui.Button("Copy"))
        {
            var sb = new StringBuilder();
            sb.AppendLine("Calculation:");
            sb.AppendLine(Ruler);
            sb.AppendLine($"  Now:           {nowBank,18:N0}");
            sb.AppendLine($"  Before:      - {startBank,18:N0}");
            sb.AppendLine(Ruler);
            sb.AppendLine($"  Diff:          {diff,18:N0}");
            sb.AppendLine($"  Tips:        - {tips,18:N0}");
            sb.AppendLine(Ruler);
            sb.AppendLine($"  Profit:        {profit,18:N0}");
            sb.AppendLine($"  {payoutPercent:0}%:           {payoutAmount,18:N0}");
            sb.AppendLine($"  {hoursLabel,-16} - {hourlyDeduction,18:N0}");
            sb.AppendLine(Ruler);
            sb.AppendLine($"  Total outcome  {totalOutcome,18:N0}");
            ImGui.SetClipboardText(sb.ToString());
        }
    }
}
