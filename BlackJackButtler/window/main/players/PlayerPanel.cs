using System.Linq;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    internal void DrawPlayersPanel(string idSuffix = "")
    {
        _partyDissolved = _players.Count > 0 && !_players.Any(x => x.IsInParty);
        int columnCount = _config.PlayerRollingForThemselves ? 11 : 10;
        if (ImGui.BeginTable($"bjb_main_table_v2{idSuffix}", columnCount, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
        {
            SetupTableColumns();
            DrawPlayerTableHeaders();

            var playerSnapshot = _players.ToList();
            foreach (var player in playerSnapshot)
            {
                ImGui.TableNextRow();
                ApplyPlayerRowBackground(player);
                DrawPlayerRow(player, false);
            }
            ImGui.EndTable();
        }
    }
}
