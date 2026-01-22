using System;
using System.Numerics;
using System.Linq;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawDebugPage()
    {
        ImGui.TextUnformatted("Chat Debug Logger");
        ImGui.SameLine();
        if (ImGui.SmallButton("Clear Log")) _debugLog.Clear();

        ImGui.SameLine();
        if (ImGui.SmallButton("Copy All to Clipboard"))
        {
            var fullLog = string.Join("\n\n", _debugLog);
            ImGui.SetClipboardText(fullLog);
        }

        ImGui.Separator();

        if (ImGui.BeginChild("debug_scroll_area", new Vector2(-1, -1), true))
        {
            for (int i = 0; i < _debugLog.Count; i++)
            {
                var line = _debugLog[i];
                if (ImGui.Selectable($"{line}##debug_{i}"))
                {
                    ImGui.SetClipboardText(line);
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Click to copy this line");
                }
            }
            ImGui.EndChild();
        }
    }
}
