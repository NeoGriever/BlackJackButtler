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
        "v1.8.3.1 — Changes since v1.8.2.1\n" +
        "\n" +
        "Fixes carried over from v1.8.2.2:\n" +
        "- Fix: ImGui style stack corruption when toggling custom edit mode\n" +
        "- Fix: Main window layout clamps child/restore-button sizes to valid values\n" +
        "- Fix: Child windows now close correctly when ImGui skips drawing their contents\n" +
        "- Fix: MP3 sound files now play correctly via dedicated Mp3FileReader\n" +
        "\n" +
        "Main Tab UI:\n" +
        "- Feature: Compact single-line header row (V2 layout)\n" +
        "- Feature: Table Popout — dealer & player table as separate floating window\n" +
        "- Feature: Nearby Players Popout — nearby list as separate floating window\n" +
        "- Feature: [Tbl] / [Nby] toggle buttons in Main tab header\n" +
        "- Fix: Table popout CLR crash — ImGui popup context isolation\n" +
        "- Fix: Popout windows no longer auto-open on plugin load (session-only)\n" +
        "\n" +
        "Preset System:\n" +
        "- Feature: Preset migration to a separate presets.json file\n" +
        "- Feature: Granular apply-categories (15 checkboxes per preset)\n" +
        "- Feature: Created / Updated timestamps per preset\n" +
        "- Feature: Sort order management via up/down arrows\n" +
        "- Feature: Collapsible entries with compact color-coded checkboxes\n" +
        "- Feature: \"Use\" button in header row with Yes/No confirmation\n" +
        "- Feature: \"Upd\" button with Yes/No confirmation and 2s safety delay\n" +
        "- Feature: \"Dup\" (Duplicate) button with Yes/No confirmation\n" +
        "- Feature: Command preview (Dealer Draw / Player Draw / Player Hit simulation)\n" +
        "- Feature: Auto-derived title color from active category combination\n" +
        "- Feature: Custom title color override with color picker and reset\n" +
        "- Fix: Preset migration no longer causes presets to disappear\n" +
        "- Fix: Preset update popup window context mismatch resolved\n" +
        "- Fix: Preset update now correctly respects enabled category checkboxes\n" +
        "- Fix: Preset sort order normalization on load\n" +
        "\n" +
        "Player Table:\n" +
        "- Feature: Player controls row shifts down for multi-hand sessions (Hand 2+)\n" +
        "- Feature: Auto-Ready (R) column added to player table\n" +
        "\n" +
        "────────────────────────────────────────────────\n" +
        "Searching for active testers for beta-versions before release.\n" +
        "Join the Discord server for more informations or questions about this.\n";

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
