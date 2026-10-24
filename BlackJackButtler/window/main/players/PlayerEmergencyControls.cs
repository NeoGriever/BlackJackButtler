using System;
using System.Numerics;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Game.ClientState.Objects.SubKinds;
using System.Threading.Tasks;
using BlackJackButtler.Chat;
using BlackJackButtler.Regex;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private static readonly Dictionary<string, string> _groupDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Initial", "Auto-deal" },
        { "Hit", "Auto-draw" },
        { "DD", "Auto-DD" },
        { "TD", "Auto-TD" },
        { "Split", "Auto-split" },
        { "SplitDraw", "Auto-split-draw" },
        { "Natural BlackJack Notify", "Nat.BJ" },
        { "Dirty BlackJack Notify", "Dirty BJ" },
        { "Charlie Notify", "Charlie" },
    };

    private void DrawEmergencyStopRow(PlayerState p)
    {
        string groupName = CommandExecutor.CurrentGroupName;
        string label = _groupDisplayNames.TryGetValue(groupName, out var display) ? display : $"Auto-{groupName}";

        ImGui.TextColored(new Vector4(1f, 0.5f, 0f, 1f), $"{label} ...");

        ImGui.SameLine(ImGui.GetColumnWidth() - 30f);

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.0f, 0.0f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.1f, 0.1f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.5f, 0.0f, 0.0f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));

        if (ImGui.Button($"X##estop_{p.UIID}", new Vector2(25, 0)))
        {
            var snapshotIndex = CommandExecutor.PreActionSnapshotIndex;

            CommandExecutor.CancelCurrentGroup();

            _config.AutoRun = false;
            _config.AutoInitialDeal = false;
            _save();

            if (snapshotIndex >= 0)
            {
                var phase = GameEngine.CurrentPhase;
                Chat.GameLog.ApplySnapshot(snapshotIndex, _players, ref _dealer, ref phase);
                GameEngine.CurrentPhase = phase;
                GameEngine.ClearForcedRecipient();
            }

            var restoredPlayer = _players.FirstOrDefault(
                pl =>
                    pl.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase)
            );
            restoredPlayer?.ResetHighlightsOnceConsistent();

            AddDebugLog($"[EmergencyStop] Aborted '{groupName}' for {p.DisplayName}, state restored, AutoRun disabled", false);
        }

        ImGui.PopStyleColor(4);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Emergency Stop: Abort action, restore state, disable Auto Run");
    }
}
