using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public class BlacklistBannerWindow : Window
{
    public BlacklistBannerWindow()
        : base("##bjb_blacklist_banner",
               ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
               ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse |
               ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings)
    {
        RespectCloseHotkey = false;
    }

    public override bool DrawConditions()
    {
        return BlacklistManager.IsBlacklisted && !BlacklistManager.IsBlocked
               && !string.IsNullOrEmpty(BlacklistManager.BannerMessage);
    }

    public override void PreDraw()
    {
        var viewport = ImGui.GetMainViewport();
        var viewportPos = viewport.WorkPos;
        var viewportSize = viewport.Size;

        ImGui.SetNextWindowPos(viewportPos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(viewportSize.X, 40), ImGuiCond.Always);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.4f, 0.0f, 0.0f, 0.95f));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor();
    }

    public override void OnClose()
    {
        if (BlacklistManager.IsBlacklisted && !BlacklistManager.IsBlocked)
            IsOpen = true;
    }

    public override void Draw()
    {
        var message = BlacklistManager.BannerMessage;
        var textSize = ImGui.CalcTextSize(message);
        var windowSize = ImGui.GetWindowSize();

        ImGui.SetCursorPos(new Vector2(
            (windowSize.X - textSize.X) / 2,
            (windowSize.Y - textSize.Y) / 2));
        ImGui.TextColored(new Vector4(1f, 1f, 1f, 1f), message);
    }
}
