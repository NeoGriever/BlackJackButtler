using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace BlackJackButtler.Windows;

public class VariablesPopupWindow : Window
{
    private readonly BlackJackButtlerWindow _mainWindow;

    public VariablesPopupWindow(BlackJackButtlerWindow mainWindow)
        : base("BJB Session Variables###bjb_vars_popup",
               ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings)
    {
        _mainWindow = mainWindow;
        Size = new Vector2(560, 420);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        _mainWindow.DrawVarsPage();
    }
}
