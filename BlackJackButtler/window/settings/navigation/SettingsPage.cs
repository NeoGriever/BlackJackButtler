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
    private void DrawSettingsPage()
    {
        if (_openImportConfirmPopup) {
            _showImportModal = true;
            ImGui.OpenPopup("import_confirm_popup");
            _openImportConfirmPopup = false;
        }

        if (_openHashedStatsConfirm) {
            _showHashedStatsModal = true;
            ImGui.OpenPopup("hashedstats_confirm_popup");
            _openHashedStatsConfirm = false;
        }

        int level = (int)_config.CurrentLevel;

        DrawSettingsTabs(level);

        if (ImGui.BeginPopupModal("import_confirm_popup", ref _showImportModal, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("How do you want to import?");
            if (BJBGui.Button("Full Replace (Wipe current)")) {
                DoFullReplace();
                _showImportModal = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (BJBGui.Button("Merge (Keep custom items)")) {
                DoMerge();
                _showImportModal = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        if (ImGui.BeginPopupModal("hashedstats_confirm_popup", ref _showHashedStatsModal, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextColored(new Vector4(1, 0.6f, 0, 1), "WARNING");
            ImGui.Spacing();
            ImGui.TextWrapped("Disabling stats integrity hashing may cause credibility issues with venue operators and managers.");
            ImGui.Spacing();
            ImGui.TextWrapped("Please confirm that disabling this feature has been explicitly discussed with the relevant venue operator or manager.");
            ImGui.Spacing();
            ImGui.Spacing();
            if (BJBGui.Button("Confirm Disable##hashedstats"))
            {
                _config.HashedStats = false;
                _save();
                _showHashedStatsModal = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (BJBGui.Button("Cancel##hashedstats"))
            {
                _showHashedStatsModal = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }
}
