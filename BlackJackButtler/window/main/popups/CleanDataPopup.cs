using System.Numerics;
using BlackJackButtler.Chat;
using BlackJackButtler.Regex;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawCleanDataPopup()
    {
        if (!ImGui.BeginPopupModal("bjb.clean_data.confirm.v2", ref _openCleanDataPopup, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        ImGui.TextColored(new Vector4(1f, 0.7f, 0.2f, 1f), "Clean residual session data?");
        ImGui.TextUnformatted("This will clear:");
        ImGui.BulletText("All recognized players (non-debug)");
        ImGui.BulletText("Dealer hand");
        ImGui.BulletText("DrawLogic debug hands and script cache");
        ImGui.Spacing();
        if (BJBGui.Button("Yes, clean##v2_clean_yes", new Vector2(160, 0)))
        {
            Chat.GameLog.PushSnapshot(_players, _dealer, GameEngine.CurrentPhase, "CleanData");
            RemovePlayersWithCompanionErase(p => !p.IsDebugPlayer);
            _dealer.Hands.Clear();
            DrawLogicDebugManager.Reset();
            Regex.RegexEngine.ClearNextRoundVotes();
            _save();
            _openCleanDataPopup = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (BJBGui.Button("Cancel##v2_clean_cancel", new Vector2(120, 0)))
        {
            _openCleanDataPopup = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }
}
