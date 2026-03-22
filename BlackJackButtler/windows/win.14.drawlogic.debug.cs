using System.Numerics;
using System.Text;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public class DrawLogicDebugWindow : Window
{
    public DrawLogicDebugWindow() : base("DrawLogic Debug###bjb_dl_debug")
    {
        Size = new Vector2(600, 500);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        if (BJBGui.SmallButton("Capture"))
            DrawLogicInterpreter.TriggerDebugCapture();
        ImGui.SameLine();
        if (BJBGui.SmallButton("Clear"))
            DrawLogicInterpreter.ClearDebugLog();
        ImGui.SameLine();
        if (BJBGui.SmallButton("Copy"))
        {
            var sb = new StringBuilder();
            foreach (var line in DrawLogicInterpreter.DebugLog)
                sb.AppendLine(line);
            ImGui.SetClipboardText(sb.ToString());
        }

        ImGui.Separator();

        var log = DrawLogicInterpreter.DebugLog;
        if (log.Count == 0)
        {
            ImGui.TextDisabled("No capture yet. Edit a script or press Capture.");
            return;
        }

        ImGui.PushFont(Dalamud.Interface.UiBuilder.MonoFont);
        if (ImGui.BeginChild("dl_debug_scroll", new Vector2(-1, -1), true))
        {
            foreach (var line in log)
            {
                if (line.StartsWith("\n---"))
                {
                    ImGui.Spacing();
                    ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), line.TrimStart('\n'));
                }
                else if (line.Contains("→"))
                    ImGui.TextColored(new Vector4(0.5f, 0.9f, 0.5f, 1f), line);
                else
                    ImGui.TextUnformatted(line);
            }
            ImGui.EndChild();
        }
        ImGui.PopFont();
    }
}
