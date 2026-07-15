using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public class UpdatePopupWindow : Window
{
    private readonly Configuration _config;
    private readonly Action _save;

    private static readonly string CurrentChangelog =
        "v1.8.4.9\n" +
        "\n" +
        "COMMANDS:\n" +
        "- Anti-Double is now available only for Party and Alliance command lines. Static texts and matching message-block references can enable AD across all normalized matches.\n" +
        "- AD comparisons trim the final text and ignore <se.*> tags. Every generated Party/Alliance line refreshes the history, while only AD lines compare against its latest entry.\n" +
        "- A match skips both the message and its associated delay and is reported explicitly in the Verbose debug log. Enabling an AD command line switches on Avoid double Messages as a one-shot action.\n" +
        "- Preset chat previews now simulate the same AD history and comparison rules.\n" +
        "\n" +
        "First of all, Latency: I did not copy anything of SBJ, because i don't have SBJ. I have no access to any code or any file from SBJ. Just because i have the not finished idea of a companion tool does not mean, that this idea is stolen. Straigt blocking me after you blaming me for maked up things is just stupid. You're of course removed from the thanks page after all this bullshit you talking about me, where i have more than enough evidence, was a logical reaction. YOU are just a bad person and i don't have any regrets to kick you out of the plugin at all.\n";

    public UpdatePopupWindow(Configuration config, Action save)
        : base("The BlackJack Buttler has learned something new###BJBUpdatePopup",
               ImGuiWindowFlags.NoCollapse)
    {
        _config = config;
        _save = save;
        Size = new Vector2(900, 760);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void PreDraw()
    {
        var viewport = ImGui.GetMainViewport();
        var center = viewport.GetCenter();
        ImGui.SetNextWindowPos(new Vector2(center.X - 450, center.Y - 380), ImGuiCond.FirstUseEver);
    }

    public override void Draw()
    {
        ImGui.PushFont(UiBuilder.MonoFont);

        if (ImGui.BeginChild("##changelog_content", new Vector2(-1, -150), false))
        {
            ImGui.TextWrapped(CurrentChangelog);
        }
        ImGui.EndChild();

        ImGui.Separator();
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.85f, 0.35f, 0.1f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.95f, 0.45f, 0.15f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.75f, 0.25f, 0.05f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.95f, 0.6f, 1f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.5f);
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.15f, 0.1f, 0.05f, 1f));

        var coffeeSize = ImGui.CalcTextSize("Buy me a coffee");
        if (ImGui.Button("Buy me a coffee", new Vector2(coffeeSize.X + 20, 0)))
        {
            Dalamud.Utility.Util.OpenLink("https://buymeacoffee.com/mindconstructor");
        }

        ImGui.PopStyleColor(5);
        ImGui.PopStyleVar();

        ImGui.Spacing();

        if (BJBGui.Button("Dismiss"))
        {
            IsOpen = false;
        }
        ImGui.SameLine();
        if (BJBGui.Button("Don't show it again"))
        {
            _config.DisableUpdatePopup = true;
            _save();
            IsOpen = false;
        }

        ImGui.PopFont();
    }
}
