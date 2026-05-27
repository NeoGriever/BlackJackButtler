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
        "v1.8.4.0 — Changes since v1.8.2.1\n" +
        "\n" +
        "ImGui & Plugin Stability:\n" +
        "- Fix: Transparent text recovery now always active (was debug-only before)\n" +
        "- Fix: Startup race-window (1 s) ensures text style is applied on plugin load\n" +
        "- Fix: Periodic monitor (10 s) detects and restores transparent ImGui text at runtime\n" +
        "- Fix: German dice roll regex (escape was double-encoded, breaking DE client detection)\n" +
        "- Fix: ImGui style stack corruption when toggling custom edit mode\n" +
        "- Fix: Main window layout clamps child/restore-button sizes to valid values\n" +
        "- Fix: Child windows close correctly when ImGui skips drawing their contents\n" +
        "- Fix: MP3 sound files play correctly via dedicated Mp3FileReader\n" +
        "\n" +
        "Main Tab — V2 Layout:\n" +
        "- Feature: Compact single-line header row replaces multi-line session/automation/utility blocks\n" +
        "- Feature: Table Popout — dealer & player table as separate floating window\n" +
        "- Feature: Nearby Players Popout — nearby list as separate floating window\n" +
        "- Feature: [Tbl] / [Nby] toggle buttons in V2 header\n" +
        "- Fix: Table popout CLR crash — ImGui popup context isolation\n" +
        "- Fix: Popout windows are session-only (always closed on plugin load)\n" +
        "\n" +
        "Player Table:\n" +
        "- Feature: Auto-Ready (R) column added to player table\n" +
        "- Feature: Player controls row shifts down for multi-hand sessions (Hand 2+)\n" +
        "- Feature: Player join/leave buttons use highlighted variants when appropriate\n" +
        "\n" +
        "Regex & Chat Detection:\n" +
        "- Feature: Per-entry chat source selection: Party, Tell, Say, System (checkbox row in editor)\n" +
        "- Feature: New regex actions: SetBet, InviteNearby\n" +
        "- Change: New entries default to Party source only\n" +
        "- Fix: Normal Say messages no longer reach party-only regex entries\n" +
        "- Fix: Trade-action entries always match System messages regardless of source setting\n" +
        "\n" +
        "Payout & Trades:\n" +
        "- Feature: New full-state-machine PayoutManagement replaces DropboxIntegration\n" +
        "- Feature: Dropbox is always preferred; local trade-clone used as fallback only\n" +
        "- Feature: Auto-confirm payout trades option (Settings → Gameplay)\n" +
        "- Fix: Dropbox detected without cache and opened when needed\n" +
        "- Fix: Payout stops correctly when receiver cancels or closes trade without payment\n" +
        "\n" +
        "Statistics Logs (new):\n" +
        "- Feature: Numbered statistics log files (00000001.log …) in statistics_logs/ directory\n" +
        "- Feature: Generated /p and /party messages auto-written with timestamps\n" +
        "- Feature: Dice results and trade events included in log\n" +
        "- Feature: Group Detector reactivation within 5 hours reuses the same log file\n" +
        "- Feature: One-click HTML export overwrites the matching .htm file\n" +
        "- Feature: Debug log resets at each round start; visible in dedicated Debug Log tab\n" +
        "- Feature: Stats page → Statistics Logs tab with Normal/Debug viewer, Copy All, HTML, Erase\n" +
        "\n" +
        "Debug Mode:\n" +
        "- Feature: Auto Players — basic-strategy AI plays debug players automatically\n" +
        "  (Hits <17, stands ≥17, doubles down on hard 10/11, splits when allowed)\n" +
        "- Feature: Configurable debug dice sequence (comma-separated values, ? = random, * = random from here on)\n" +
        "- Feature: Sequence resets on new round start or via Reset button\n" +
        "- Fix: Debug dice now fires DiceResultHandler (full card-application pipeline)\n" +
        "- Fix: Debug dice no longer sends a command to FFXIV chat\n" +
        "\n" +
        "Preset System:\n" +
        "- Feature: Presets migrated to separate presets.json file (automatic)\n" +
        "- Feature: 15 granular apply-category checkboxes per preset\n" +
        "- Feature: Created/Updated timestamps per preset entry\n" +
        "- Feature: Sort order management via up/down arrows\n" +
        "- Feature: Collapsible entries with compact color-coded category indicators\n" +
        "- Feature: \"Use\" / \"Upd\" / \"Dup\" buttons with Yes/No confirmation\n" +
        "- Feature: Auto-derived title color from active categories; custom color override\n" +
        "- Feature: Command preview (Dealer Draw / Player Draw / Hit simulation)\n" +
        "- Fix: Preset migration no longer causes presets to disappear\n" +
        "- Fix: Preset update correctly respects enabled category checkboxes\n" +
        "\n" +
        "Card-Companion App (new):\n" +
        "- Feature: Companion Sync — mirrors active player card state to an external server\n" +
        "- Feature: Configurable server address and timeout (Settings → System)\n" +
        "\n" +
        "Game Engine & Dealing:\n" +
        "- Feature: SplitDraw fires a follow-up state prompt group after the draw completes\n" +
        "- Fix: Dealer IsCurrentTurn set at deal start and cleared after payout\n" +
        "- Fix: CompanionSync state pushed at each turn transition\n" +
        "- Fix: All-busted path goes through BeginPayoutOutput (consistent payout flow)\n" +
        "\n" +
        "Command Executor:\n" +
        "- Fix: Command list snapshot-copied before execution (prevents mutation mid-run)\n" +
        "- Fix: State group name/target recorded after group completes, not before\n" +
        "- Fix: <betrange> token checks BetLimitEntries before legacy VipBetTiers\n" +
        "\n" +
        "Auto-Start & Betting:\n" +
        "- Feature: Auto-start blocked when any active player cannot cover their current bet\n" +
        "- Feature: InsufficientBetQueueManager with configurable command (Settings → Gameplay)\n" +
        "- Feature: AutoBet post-command selector (Settings → Gameplay)\n" +
        "\n" +
        "Nearby Players:\n" +
        "- Feature: Fixed-center capture for nearby area display\n" +
        "- Feature: Foot numbers toggle\n" +
        "- Feature: NearbyAutoAct — fires a command when new players enter the nearby radius\n" +
        "\n" +
        "Settings & Build:\n" +
        "- Feature: Hide Thanks page option (System tab)\n" +
        "- Added base NAudio package; EnableWindowsTargeting = true in csproj\n" +
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
