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
    private void DrawUserStatsSessionPrompt()
    {
        if (_triggerUserStatsSessionPrompt)
        {
            _userStatsSessionPromptOpen = true;
            ImGui.OpenPopup("User Statistics##bjb.user_stats_session");
            _triggerUserStatsSessionPrompt = false;
        }

        var wasOpen = _userStatsSessionPromptOpen;
        if (ImGui.BeginPopupModal(
            "User Statistics##bjb.user_stats_session",
            ref _userStatsSessionPromptOpen,
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings
        ))
        {
            ImGui.TextUnformatted("Do you want to create a new user stats file?");
            ImGui.Spacing();
            if (BJBGui.Button("Yes", new Vector2(110f, 0f)))
            {
                UserStatisticsManager.StartSession();
                _selectedUserStatisticsPath = UserStatisticsManager.CurrentFilePath;
                SaveSessionFromUI();
                _userStatsSessionPromptOpen = false;
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();
            if (BJBGui.Button("No", new Vector2(110f, 0f)))
            {
                UserStatisticsManager.ContinueCurrentOrLatestSession();
                _selectedUserStatisticsPath = UserStatisticsManager.CurrentFilePath;
                SaveSessionFromUI();
                _userStatsSessionPromptOpen = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        if (wasOpen && !_userStatsSessionPromptOpen && !UserStatisticsManager.IsActive && IsRecognitionActive)
        {
            UserStatisticsManager.ContinueCurrentOrLatestSession();
            _selectedUserStatisticsPath = UserStatisticsManager.CurrentFilePath;
            SaveSessionFromUI();
        }
    }
}
