using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawPresetsPage()
    {
        ImGui.TextUnformatted("Presets");
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.3f, 1f), "[WIP] This section is under construction.");
    }
}
