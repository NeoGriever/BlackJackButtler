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
    private bool _triggerMigratePopup = false;

    private static readonly string CurrentChangelog =
        "v1.8.4.1 — Changes since v1.8.4.0\n" +
        "\n" +
        "Main View:\n" +
        "- Feature: Added Version 2 Compact density mode with reduced spacing, smaller text, hidden alias column, and simplified Bank/Bet inputs\n" +
        "- Feature: Version 2 Compact opens alias editing by double-clicking the player name\n" +
        "- Feature: Bank input unlock now lives directly in the player table Bank header\n" +
        "- Change: Version 2 Compact only shows Bank-to-Tip and Max Bet actions while Shift is held\n" +
        "- Fix: Classic player table now uses the correct column count, preventing shifted row contents\n" +
        "\n" +
        "Settings:\n" +
        "- Change: Main View selection is now consistently labeled Classic / Version 2\n" +
        "- Feature: Added Normal / Compact density switch below the Version 2 main view setting\n" +
        "- Fix: Natural and Dirty Blackjack settings no longer duplicate the Dirty Blackjack label\n" +
        "\n" +
        "Presets:\n" +
        "- Feature: Preset assignment category checkboxes can now be shown or hidden above the list\n" +
        "- Feature: Hovering preset assignment checkboxes subtly highlights the related navigation source\n" +
        "- Feature: Preset preview now lives in a right-side column and previews the current active configuration by default\n" +
        "- Feature: Multiple presets can be added to a numbered temporary preview stack and applied in that order\n" +
        "- Feature: Preset preview now includes simulated Debug dice rolls\n" +
        "- Feature: Preview stack uses compact >/< controls next to Use, plus Back to current and Use as shown actions\n" +
        "- Change: Preview output now shows the logical party-chat lines without section headers\n" +
        "\n" +
        "Regex & Commands:\n" +
        "- Feature: Custom regex entries now render in a separate list below standard entries and can be manually reordered\n" +
        "- Feature: Own Buttons are now grouped into the Commands page as a second tab\n" +
        "- Feature: Commands now has a Variables tab with Popout and new <dealerHand> / ${dealerHand} support\n" +
        "\n" +
        "Stats & Debug:\n" +
        "- Feature: Added 1k, 5k, and Custom tip buttons to Stats\n" +
        "- Change: Generate new Players now fills debug players up to 7 instead of replacing the full debug group\n" +
        "- Fix: Generate new Players is disabled once the debug group is full\n" +
        "\n" +
        "Draw Logic:\n" +
        "- Feature: Updated the default visualizer script with camera-facing rounded cards and dynamic line thickness\n";

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

        if (_triggerMigratePopup)
        {
            ImGui.OpenPopup("bjb.migrate.confirm");
            _triggerMigratePopup = false;
        }

        if (ImGui.BeginChild("##changelog_content", new Vector2(-1, -150), false))
        {
            ImGui.TextWrapped(CurrentChangelog);
        }
        ImGui.EndChild();

        ImGui.Separator();
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.5f, 0.8f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.6f, 0.9f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.15f, 0.4f, 0.7f, 1f));

        if (ImGui.Button("Migrate Configurations", new Vector2(-1, 0)))
        {
            _triggerMigratePopup = true;
        }

        ImGui.PopStyleColor(3);

        ImGui.TextWrapped("Recommended: Updates Charlie, Natural BlackJack and Dirty BlackJack commands and messages.");

        ImGui.Spacing();

        var open = true;
        if (ImGui.BeginPopupModal("bjb.migrate.confirm", ref open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextWrapped(
                "This will update/replace/create the specific commands " +
                "and messages for Charlie, Natural BlackJack and " +
                "Dirty BlackJack. It will NOT replace modified messages.");
            ImGui.Spacing();

            if (BJBGui.Button("Yes"))
            {
                if (DefaultsMigration.MigrateNotifyGroups(_config))
                    _save();
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (BJBGui.Button("Cancel"))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

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
