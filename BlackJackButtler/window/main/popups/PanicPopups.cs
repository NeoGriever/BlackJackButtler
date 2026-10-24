using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    internal void TriggerPanicStage1() => _panicConfirmStage = 1;

    private void DrawPanicPopups()
    {
        if (_panicConfirmStage == 1)
            ImGui.OpenPopup("panic_confirm_1_v2");

        bool panicOpen1 = true;
        if (ImGui.BeginPopupModal("panic_confirm_1_v2", ref panicOpen1, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
        {
            ImGui.TextUnformatted("Are you sure?");
            ImGui.TextUnformatted("This will stop this round and jump back to the round start.");
            ImGui.Spacing();
            if (ImGui.Button("Yes##v2_panic1_yes"))
            {
                _panicConfirmStage = 2;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("No##v2_panic1_no"))
            {
                _panicConfirmStage = 0;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
        else if (_panicConfirmStage == 1)
            _panicConfirmStage = 0;

        if (_panicConfirmStage == 2)
            ImGui.OpenPopup("panic_confirm_2_v2");

        bool panicOpen2 = true;
        if (ImGui.BeginPopupModal("panic_confirm_2_v2", ref panicOpen2, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
        {
            ImGui.TextColored(new Vector4(1, 0.3f, 0.3f, 1), "Are you REALLY sure?");
            ImGui.TextUnformatted("This is only a rescue option if the round is stuck.");
            ImGui.Spacing();
            if (ImGui.Button("Yes, PANIC##v2_panic2_yes"))
            {
                ExecutePanic();
                _panicConfirmStage = 0;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("No##v2_panic2_no"))
            {
                _panicConfirmStage = 0;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
        else if (_panicConfirmStage == 2)
            _panicConfirmStage = 0;
    }
}
