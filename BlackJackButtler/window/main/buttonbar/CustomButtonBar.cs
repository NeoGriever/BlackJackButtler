using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawMainCustomButtonBar()
    {
        if (_config.CustomButtonEntries.Count == 0 && _config.CustomCommandGroups.Count == 0) return;

        ImGui.TextDisabled("Custom Actions");
        ImGui.SameLine();
        DrawCustomButtonBar();
    }
}
