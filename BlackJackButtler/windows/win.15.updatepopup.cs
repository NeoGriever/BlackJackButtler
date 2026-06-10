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
        "v1.8.4.2 — Changes since v1.8.4.0\n" +
        "\n" +
        "NEW — Alliance & Large Table Support:\n" +
        "- Full alliance support is now always active: one dealer can manage up to 23 players\n" +
        "- Party and alliance members are detected and synchronized automatically\n" +
        "- Party chat commands and group /dice commands automatically use alliance chat while an alliance is active\n" +
        "- Group mode is detected once before each command chain, keeping large-table command execution responsive\n" +
        "- Added an Alliance settings tab for configuring the Nearby J action\n" +
        "- Added Create Alliance Invite Button, which creates and opens a reusable Alliance Invite custom command\n" +
        "\n" +
        "NEW:\n" +
        "- Added Version 2 Compact view for denser player and dealer controls\n" +
        "- Added /skip to omit command rows when a resolved variable such as <winners> is empty\n" +
        "- Added Payout to command references, Own Button actions, regex actions, and reaction command selections\n" +
        "- Added a session-only Allow 0 bet setting; it resets when the plugin reloads\n" +
        "- Added Standard Message and Fast Message preset creation buttons\n" +
        "- Added stacked preset previews with ordered temporary application and simulated Debug dice rolls\n" +
        "- Added <dealerHand> and ${dealerHand} command variables\n" +
        "- Added 1k, 5k, and Custom tip buttons to Stats\n" +
        "- Added live User Statistics with per-player traded-in, paid-out, and profit/loss totals\n" +
        "- Group Detector startup can create a new User Statistics file or continue the current one without duplicate players\n" +
        "- User Statistics sessions are stored as timestamped text files and can be reviewed or exported after Group Detector stops\n" +
        "- Added an Open Changelog button to System settings\n" +
        "- Added Full Debug command tracing and complete Verbose logs with millisecond timestamps\n" +
        "\n" +
        "CHANGED:\n" +
        "- Player names now always show the full character name; double-click a name to edit its alias\n" +
        "- Aliases are displayed in yellow and regular names in light blue\n" +
        "- Removed the Alias column and the Bank/Bet +/- controls\n" +
        "- Bank and Bet fields now use thousands separators; invalid bets are shown in orange\n" +
        "- Bank input locking is now controlled from the Bank table header\n" +
        "- Version 2 header controls are grouped more clearly and include CFG and STI Nearby controls\n" +
        "- Own Buttons now live in the Commands page\n" +
        "- Variables now lives under DEBUG, while Round History now lives under Stats\n" +
        "- Custom regex entries are separated from standard entries and can be reordered\n" +
        "- Preset previews now simulate a complete round: dealer draw, player draw, state, stand, rule-based dealer turns, and result\n" +
        "- Debug logs are chronological, with new entries displayed at the bottom\n" +
        "- Main View selection is now consistently labeled Classic / Version 2\n" +
        "\n" +
        "FIXED:\n" +
        "- Fixed stale party/alliance members remaining after players leave or a group is dissolved\n" +
        "- Group Detector activation and deactivation now strictly validates every listed player against the current group\n" +
        "- Fixed rounds and queued actions failing because game operations ran outside the main thread\n" +
        "- Fixed Bank Tell execution after Bet Change and other queued actions\n" +
        "- Fixed round starts with active zero-bet players unless Allow 0 bet is enabled\n" +
        "- Fixed empty result variables producing unwanted chat rows when used with /skip\n" +
        "- Fixed party/alliance /dice routing in command chains and referenced commands\n" +
        "- Fixed shifted Classic table columns and player header alignment\n" +
        "- Fixed Natural and Dirty Blackjack setting labels\n" +
        "- Fixed Debug player generation replacing existing players or exceeding the group limit\n" +
        "- Expanded diagnostics for blocked, rejected, stale, skipped, and failed queued actions\n";

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
