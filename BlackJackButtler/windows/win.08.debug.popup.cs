using System.Numerics;
using System.Collections.Generic;
using System.Linq;
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
        if (ImGui.SmallButton("Clear Log")) { lock(_main.GetLogLock()) _main.GetDebugLog().Clear(); }
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
}
