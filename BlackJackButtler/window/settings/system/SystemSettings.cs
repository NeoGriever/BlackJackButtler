using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using BlackJackButtler.Chat;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawSystemSettings(int level)
    {
        DrawMoneyRingSettings();
        DrawSettingsToggle("Disable Update Popup", "system_disable_update_popup", ref _config.DisableUpdatePopup);
        if (BJBGui.Button("Open Changelog"))
            Plugin.Instance.OpenChangelog();
        DrawSettingsToggle("Hide Thanks page", "system_hide_thanks", ref _config.HideThanksPage);
        var allowZeroBet = GameEngine.AllowZeroBetForSession;
        ImGui.TextUnformatted("Allow 0 bet");
        ImGui.SameLine(260f);
        if (BJBOnOffSwitch.Draw("system_allow_zero_bet", ref allowZeroBet))
            GameEngine.AllowZeroBetForSession = allowZeroBet;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Session-only setting. Resets to disabled when the plugin reloads.");

        ImGui.Spacing();
        DrawSettingsToggle("Wait Range Expanded", "system_wait_range_expanded", ref _config.UnlockWaitTimer);

        ImGui.TextUnformatted("Hashed Stats");
        ImGui.SameLine(260f);
        var hashedStats = _config.HashedStats;
        if (BJBOnOffSwitch.Draw("system_hashed_stats", ref hashedStats))
        {
            if (!hashedStats)
                _openHashedStatsConfirm = true;
            else
            {
                _config.HashedStats = true;
                _save();
            }
        }

        ImGui.Spacing();
        if (BJBGui.Button("Export Config File as JSON")) ExportConfig();
        ImGui.SameLine();
        if (BJBGui.Button("Import Config File as JSON")) ImportConfig();
    }
}
