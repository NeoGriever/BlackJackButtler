using System.Numerics;
using System.Collections.Generic;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public class DebugLogWindow : Window
{
    private readonly List<string> _logRef;

    public DebugLogWindow(List<string> logReference) : base("BJB Chat Debug Popout")
    {
        _logRef = logReference;
        Size = new Vector2(500, 400);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        if (ImGui.SmallButton("Clear Log")) _logRef.Clear();
        ImGui.Separator();

        if (ImGui.BeginChild("popout_scroll_area", new Vector2(-1, -1), true))
        {
            for (int i = 0; i < _logRef.Count; i++)
            {
                if (ImGui.Selectable($"{_logRef[i]}##pop_{i}"))
                {
                    ImGui.SetClipboardText(_logRef[i]);
                }
            }
            ImGui.EndChild();
        }
    }
}
