using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public class CustomButtonBarWindow : Window
{
    private readonly Configuration _config;
    private readonly Action _save;
    private readonly Func<(Vector2, Vector2)>? _getMainWindowRect;
    private readonly BlackJackButtlerWindow _mainWindow;
    private bool _needsPositioning = true;

    public CustomButtonBarWindow(Configuration config, Action save,
        Func<(Vector2, Vector2)>? getMainWindowRect, BlackJackButtlerWindow mainWindow)
        : base("##bjb_buttonbar", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize)
    {
        _config = config;
        _save = save;
        _getMainWindowRect = getMainWindowRect;
        _mainWindow = mainWindow;
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void RequestRepositioning() => _needsPositioning = true;

    public override void PreDraw()
    {
        if (_needsPositioning && _getMainWindowRect != null)
        {
            var (pos, size) = _getMainWindowRect();
            if (size.X > 0)
            {
                Position = new Vector2(pos.X, pos.Y - 50);
                PositionCondition = ImGuiCond.Appearing;
                _needsPositioning = false;
            }
        }

        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar
                  | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize;
        if (_config.ButtonBarNoBackground) flags |= ImGuiWindowFlags.NoBackground;
        if (_config.ButtonBarLocked) flags |= ImGuiWindowFlags.NoMove;
        Flags = flags;
    }

    public override void Draw()
    {
        _mainWindow.RenderCustomButtons("bar", _config.ButtonBarLayout == ButtonBarLayout.Vertical);

        if (_config.ButtonBarLayout == ButtonBarLayout.Horizontal)
            ImGui.SameLine();

        if (ImGui.SmallButton("\u2716##bar_close"))
        {
            _config.ButtonBarPopout = false;
            IsOpen = false;
            _save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Close popout");

        if (!_config.ButtonBarLocked)
        {
            if (_config.ButtonBarLayout == ButtonBarLayout.Horizontal)
                ImGui.SameLine();
            ImGui.TextDisabled("\u2261");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Drag window to move");
        }
    }
}
