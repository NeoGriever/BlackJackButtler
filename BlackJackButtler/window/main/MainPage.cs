using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawMainPage()
    {
#if DEBUG
        if (DrawDebugVisualArtstyleModeSwitch())
        {
            DrawDebugVisualCanvas();
            return;
        }
#endif

        DrawMainLayout();
    }

    private void DrawMainLayout()
    {
        DrawMainHeader();
        ImGui.Spacing();
        DrawMainCustomButtonBar();
        DrawAutoContinueControls();

        if (!_config.TablePopout)
        {
            ImGui.Spacing();
            DrawDealerPanel();
            ImGui.Spacing();
            DrawPlayersPanel();
        }

        if (!_config.NearbyPopout)
            DrawNearbyPlayersSection();

        DrawMainSharedPopups();

    }
}
