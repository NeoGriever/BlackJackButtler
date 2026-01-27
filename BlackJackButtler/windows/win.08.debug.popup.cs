using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public class DebugLogWindow : Window
{
    private readonly BlackJackButtlerWindow _main;

    public DebugLogWindow(BlackJackButtlerWindow main) : base("BJB Chat Debug Popout")
    {
        _main = main;
        Size = new Vector2(500, 400);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        if (ImGui.SmallButton("Clear Log"))
        {
            lock(_main.GetLogLock()) _main.GetDebugLog().Clear();
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("Copy All"))
        {
            CopyLogToClipboard();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Copy all chat entries to clipboard in chronological order");
        }

        ImGui.Separator();

        if (ImGui.BeginChild("popout_scroll_area", new Vector2(-1, -1), true))
        {
            List<BlackJackButtlerWindow.DebugEntry> logCopy;
            lock (_main.GetLogLock()) logCopy = _main.GetDebugLog().ToList();

            for (int i = logCopy.Count - 1; i >= 0; i--)
            {
                var entry = logCopy[i];
                if (!entry.IsChat) continue;
                if (ImGui.Selectable($"{entry.Text}##pop_{i}")) ImGui.SetClipboardText(entry.Text);
            }
            ImGui.EndChild();
        }
    }

    private void CopyLogToClipboard()
    {
        List<BlackJackButtlerWindow.DebugEntry> logCopy;
        lock (_main.GetLogLock()) logCopy = _main.GetDebugLog().ToList();

        // Only chat entries in popout
        var chatEntries = logCopy.Where(e => e.IsChat).ToList();

        if (chatEntries.Count == 0)
        {
            ImGui.SetClipboardText("(No chat entries to copy)");
            return;
        }

        // Build string in chronological order (oldest first)
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
