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
    private void DrawDealerRow()
    {
        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(0.05f, 0.05f, 0.05f, 1f)));

        ImGui.PushID("dealer_row");

        ImGui.TableNextColumn();
        var nameColor = new Vector4(1f, 1f, 0.2f, 1f);
        ImGui.TextColored(nameColor, _dealer.DisplayName);

        ImGui.TableNextColumn();
        DrawMultiHandCards(_dealer);

        ImGui.TableNextColumn();
        DrawMultiHandPoints(_dealer);

        ImGui.TableNextColumn();
        DrawDealerControls();

        ImGui.PopID();
    }
}
