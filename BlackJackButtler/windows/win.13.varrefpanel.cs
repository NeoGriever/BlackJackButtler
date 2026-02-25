using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawVarRefPanel()
    {
        if (!_showVarRefPanel) return;

        ImGui.SetNextWindowPos(new Vector2(_lastWindowPos.X + _lastWindowSize.X + 10, _lastWindowPos.Y), ImGuiCond.Appearing);
        ImGui.SetNextWindowSize(new Vector2(420, 480), ImGuiCond.FirstUseEver);

        if (!ImGui.Begin("Variable Reference###bjb_varref", ref _showVarRefPanel, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings))
        {
            ImGui.End();
            return;
        }

        var culture = CultureInfo.GetCultureInfo("en-US");

        ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), "Context Tokens");
        ImGui.Separator();
        CopyRow("<t>", "Target player name (alias-aware)");
        CopyRow("<points>", "Current hand points (e.g. 15/25)");
        CopyRow("<cards>", "Current hand cards");
        CopyRow("<minbet>", $"Min bet ({_config.MinBet.ToString("N0", culture)} Gil)");
        CopyRow("<maxbet>", $"Max bet ({_config.MaxBet.ToString("N0", culture)} Gil)");
        CopyRow("<betrange>", "Bet range with VIP tiers");
        CopyRow("<winners>", "Winner names (after payout)");
        CopyRow("<pushed>", "Push names (after payout)");
        CopyRow("<loosers>", "Loser names (after payout)");
        CopyRow("<busted>", "Busted names (after payout)");
        CopyRow("<results>", "Full result summary (after payout)");

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), "Score Token");
        ImGui.Separator();
        CopyRow("+{PlayerScore}", "Dice roll as card value (1-11)");

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), "Message Batch Reference");
        ImGui.Separator();
        CopyRow("#{BatchName}", "Insert random/next message from batch");

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), "Auto Session Variables");
        ImGui.Separator();
        CopyRow("${bankamount}", "Player bank amount");
        CopyRow("${betamount}", "Player current bet");
        CopyRow("${lastwin}", "Player last win amount");
        CopyRow("${dealerpoints}", "Dealer best score");
        CopyRow("${HandIndex}", "Hand label for splits (e.g. [Hand 1])");
        CopyRow("${playerCards}", "Same as <cards>");

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), "Custom Variables");
        ImGui.Separator();
        ImGui.TextDisabled("${name}  = persistent (keeps value)");
        ImGui.TextDisabled("$${name} = consumed (cleared after use)");
        ImGui.TextDisabled("Set via regex action or Variables page.");

        ImGui.End();
    }

    private static void CopyRow(string token, string description)
    {
        if (BJBGui.SmallButton($"Copy##{token}"))
            ImGui.SetClipboardText(token);
        ImGui.SameLine();
        ImGui.TextUnformatted(token);
        ImGui.SameLine();
        ImGui.TextDisabled($"- {description}");
    }
}
