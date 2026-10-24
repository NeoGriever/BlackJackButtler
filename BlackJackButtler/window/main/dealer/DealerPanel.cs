using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    internal void DrawDealerPanel(string idSuffix = "")
    {
        if (ImGui.BeginTable($"bjb_dealer_table_v2{idSuffix}", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 170);
            ImGui.TableSetupColumn("Cards", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            ImGui.TableSetupColumn("Points", ImGuiTableColumnFlags.WidthFixed, 65);
            ImGui.TableSetupColumn("Dealer Controls", ImGuiTableColumnFlags.WidthFixed, 240);
            ImGui.TableHeadersRow();
            ImGui.TableNextRow();
            DrawDealerRow();
            ImGui.EndTable();
        }
    }
}
