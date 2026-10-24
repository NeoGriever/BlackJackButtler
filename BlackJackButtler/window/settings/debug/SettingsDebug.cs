using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BlackJackButtler.Regex;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
#if DEBUG
    private void DrawSettingsDebugTab()
    {
        ImGui.TextUnformatted("Payout executor override");
        ImGui.SameLine(300f);
        var payoutExecutor = (int)_config.DebugPayoutExecutor;
        ImGui.SetNextItemWidth(260f);
        if (BJBGui.Combo("##debug_payout_executor", ref payoutExecutor, "Automatic\0Force integrated trade\0Force Dropbox\0"))
        {
            _config.DebugPayoutExecutor = (DebugPayoutExecutorVariant)payoutExecutor;
            _save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Automatic prefers Dropbox when it is installed and active. Otherwise BJB uses its integrated trade implementation.");

        var tradeLogging = _config.DebugTradeLogging;
        if (ImGui.Checkbox("Enable detailed payout/trade logging", ref tradeLogging))
        {
            _config.DebugTradeLogging = tradeLogging;
            _save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Disabled on every plugin load. Enables detailed PayoutManagement and ModularTrade entries in /xllog and the BJB debug log.");

        var dropbox = PayoutManagement.GetDropboxStatus();
        ImGui.TextDisabled($"Dropbox installed: {(dropbox.Installed ? "Yes" : "No")}");
        ImGui.TextDisabled($"Dropbox active: {(dropbox.Active ? "Yes" : "No")}");
    }
#endif
}
