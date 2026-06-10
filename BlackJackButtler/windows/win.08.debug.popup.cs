using System;
using System.Text;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public class DebugLogWindow : Window
{
    private readonly BlackJackButtlerWindow _main;
    private int _lastVisibleEntryCount = -1;

    public DebugLogWindow(BlackJackButtlerWindow main) : base("BJB Chat Debug Popout")
    {
        _main = main;
        Size = new Vector2(500, 400);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        if (BJBGui.SmallButton("Clear Log"))
        {
            lock(_main.GetLogLock()) _main.GetDebugLog().Clear();
            _lastVisibleEntryCount = 0;
        }

        if (Plugin.IsDebugMode)
        {
            ImGui.SameLine();
            ImGui.Checkbox("Fast Tests", ref Plugin.IsSpeedMode);
            ImGui.SameLine();
            ImGui.Checkbox("Auto Players", ref Plugin.DebugAutoPlayers);
        }

        ImGui.SameLine();
        var verbose = _main.IsVerboseLogEnabled;
        if (ImGui.Checkbox("Verbose", ref verbose))
            _main.IsVerboseLogEnabled = verbose;

        ImGui.SameLine();
        var fullDebug = _main.IsFullDebugLogEnabled;
        if (!verbose) ImGui.BeginDisabled();
        if (ImGui.Checkbox("Full Debug", ref fullDebug))
            _main.IsFullDebugLogEnabled = fullDebug;
        if (!verbose) ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Log every command input and transformation step. Requires Verbose.");

        ImGui.SameLine();
        if (BJBGui.SmallButton("Copy All"))
        {
            CopyLogToClipboard();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Copy all currently visible entries in chronological order");
        }

        if (Plugin.IsDebugMode)
        {
            ImGui.SetNextItemWidth(-80);
            if (ImGui.InputText("##popout_debug_dice_sequence", ref Plugin.DebugDiceSequence, 1024))
                Plugin.ResetDebugDiceSequence();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Comma-separated debug dice sequence. '?' is random and loops with the sequence. '*' switches to random rolls after the preceding entries.");
            ImGui.SameLine();
            if (BJBGui.SmallButton("Reset##popout_debug_dice_sequence"))
                Plugin.ResetDebugDiceSequence();
        }

        ImGui.Separator();

        if (ImGui.BeginChild("popout_scroll_area", new Vector2(-1, -1), true))
        {
            List<BlackJackButtlerWindow.DebugEntry> logCopy;
            lock (_main.GetLogLock()) logCopy = _main.GetDebugLog().ToList();

            var visibleEntryCount = 0;
            for (int i = 0; i < logCopy.Count; i++)
            {
                var entry = logCopy[i];
                if (!_main.IsDebugEntryVisible(entry)) continue;
                visibleEntryCount++;

                var color = BlackJackButtlerWindow.GetDebugEntryColor(entry.Color)
                    ?? GetChannelColor(entry.Text);
                if (color.HasValue) ImGui.PushStyleColor(ImGuiCol.Text, color.Value);
                var displayText = BlackJackButtlerWindow.FormatDebugEntry(entry);
                if (ImGui.Selectable($"{displayText}##pop_{i}")) ImGui.SetClipboardText(displayText);
                if (color.HasValue) ImGui.PopStyleColor();
            }

            if (visibleEntryCount != _lastVisibleEntryCount)
            {
                ImGui.SetScrollHereY(1.0f);
                _lastVisibleEntryCount = visibleEntryCount;
            }
        }
        ImGui.EndChild();
    }

    private static Vector4? GetChannelColor(string text)
    {
        if (text.StartsWith("[Party]"))  return new Vector4(0.4f, 0.6f, 1.0f, 1.0f);
        if (text.StartsWith("[Yell]"))   return new Vector4(1.0f, 1.0f, 0.3f, 1.0f);
        if (text.StartsWith("[Shout]"))  return new Vector4(1.0f, 0.6f, 0.2f, 1.0f);
        if (text.StartsWith("[Tell]"))   return new Vector4(1.0f, 0.5f, 0.8f, 1.0f);
        if (text.StartsWith("[Say]"))    return new Vector4(0.95f, 0.95f, 0.95f, 1.0f);
        return null;
    }

    private void CopyLogToClipboard()
    {
        List<BlackJackButtlerWindow.DebugEntry> logCopy;
        lock (_main.GetLogLock()) logCopy = _main.GetDebugLog().ToList();

        var visibleEntries = logCopy
            .Where(_main.IsDebugEntryVisible)
            .ToList();

        if (visibleEntries.Count == 0)
        {
            ImGui.SetClipboardText("(No chat entries to copy)");
            return;
        }

        var sb = new StringBuilder(visibleEntries.Count * 100);
        sb.AppendLine($"=== BlackJack Buttler Debug Log ===");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Total Entries: {visibleEntries.Count}");
        sb.AppendLine($"Verbose Mode: {(_main.IsVerboseLogEnabled ? "ON" : "OFF")}");
        sb.AppendLine($"Full Debug Mode: {(_main.IsVerboseLogEnabled && _main.IsFullDebugLogEnabled ? "ON" : "OFF")}");
        sb.AppendLine($"=====================================");
        sb.AppendLine();

        foreach (var entry in visibleEntries)
        {
            sb.AppendLine(BlackJackButtlerWindow.FormatDebugEntry(entry));
        }

        ImGui.SetClipboardText(sb.ToString());
    }
}
