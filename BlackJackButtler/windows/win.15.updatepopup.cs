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
        "v1.8.4.4\n" +
        "\n" +
        "NEW — Short-Result Message Configuration:\n" +
        "- Build the ${results} message from a top-to-bottom list of rules\n" +
        "- Each rule picks a data source (Winners, Pushed, Lost, Busted, or none) and its own template with a <data> placeholder\n" +
        "- Conditional visibility: show a rule only if content before or after is empty, or only when its own data is empty\n" +
        "- Compress repeated names per rule and wrap everything with an optional outer result template\n" +
        "- Live example output, per-step Undo, and JSON import/export for the whole rule list\n" +
        "- Added a Player rolling for themselves setting (players roll their required cards with /dice 13, /dice alliance 13, or /random)\n" +
        "- Added a Gil visual setting (General) with three display styles for all Gil input fields: plain digits, grouped, and fixed-width grouped (default)\n" +
        "- Added a Top Tabs menu style (General) that lays the pages out as tabs across the top, alongside Sidebar and Burger Menu\n" +
        "\n" +
        "CHANGED:\n" +
        "- Gil input fields (including Stats: tips, wage, Gil/hour, house bank, start bank) share the configurable Gil visual layout\n" +
        "\n" +
        "FIXED:\n" +
        "- Fixed player alias names not being displayed/matched correctly\n" +
        "- Fixed the Short-Result rule editor collapsing every time a rule's data source or template was edited\n";

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
