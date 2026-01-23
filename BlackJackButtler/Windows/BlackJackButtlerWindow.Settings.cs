using System;
using System.Linq;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawSettingsPage()
    {
        ImGui.TextUnformatted("Gameplay Settings");
        ImGui.Separator();

        if (ImGui.Checkbox("First Deal, then Play", ref _config.FirstDealThenPlay))
        {
            _save();
        }

        if (_config.FirstDealThenPlay)
        {
            ImGui.TextDisabled("Mode: All players receive 2 cards first, then the play round startsPass.");
        }
        else
        {
            ImGui.TextDisabled("Mode: Each player receives cards and plays their turn immediately before moving to next.");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled("General Settings WIP...");
    }
}
