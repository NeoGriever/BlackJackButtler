using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public class NearbyPopoutWindow : Window
{
    private readonly Configuration _config;
    private readonly Action _save;
    private readonly BlackJackButtlerWindow _mainWindow;

    public NearbyPopoutWindow(Configuration config, Action save, BlackJackButtlerWindow mainWindow)
        : base("BJB Nearby Players##bjb_nearby_popout",
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse)
    {
        _config = config;
        _save = save;
        _mainWindow = mainWindow;
        SizeCondition = ImGuiCond.FirstUseEver;
        Size = new Vector2(340, 400);
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(200, 120),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public override void OnClose()
    {
        _config.NearbyPopout = false;
        _save();
    }

    public override void Draw()
    {
        if (ImGui.IsWindowAppearing()) return;
        _mainWindow.DrawNearbyPlayersSection(true);
    }
}
