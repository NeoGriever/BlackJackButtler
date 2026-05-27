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
    private int _lastChatEntryCount = -1;

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
            _lastChatEntryCount = 0;
        }

        if (Plugin.IsDebugMode)
        {
            ImGui.SameLine();
            ImGui.Checkbox("Fast Tests", ref Plugin.IsSpeedMode);
            ImGui.SameLine();
            ImGui.Checkbox("Auto Players", ref Plugin.DebugAutoPlayers);
        }

        ImGui.SameLine();
        if (BJBGui.SmallButton("Copy All"))
        {
            CopyLogToClipboard();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Copy all chat entries to clipboard in chronological order");
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

            var chatEntryCount = 0;
            for (int i = 0; i < logCopy.Count; i++)
            {
                var entry = logCopy[i];
                if (!entry.IsChat) continue;
                chatEntryCount++;

                var color = GetChannelColor(entry.Text);
                if (color.HasValue) ImGui.PushStyleColor(ImGuiCol.Text, color.Value);
                if (ImGui.Selectable($"{entry.Text}##pop_{i}")) ImGui.SetClipboardText(entry.Text);
                if (color.HasValue) ImGui.PopStyleColor();
            }

            if (chatEntryCount != _lastChatEntryCount)
            {
                ImGui.SetScrollHereY(1.0f);
                _lastChatEntryCount = chatEntryCount;
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

        var chatEntries = logCopy.Where(e => e.IsChat).ToList();

        if (chatEntries.Count == 0)
        {
            ImGui.SetClipboardText("(No chat entries to copy)");
            return;
        }

        var sb = new StringBuilder(chatEntries.Count * 100);
        sb.AppendLine($"=== BlackJack Buttler Chat Log ===");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Total Entries: {chatEntries.Count}");
        sb.AppendLine($"===================================");
        sb.AppendLine();

        foreach (var entry in chatEntries)
        {
            sb.AppendLine(entry.Text);
        }

        ImGui.SetClipboardText(sb.ToString());
    }
}
