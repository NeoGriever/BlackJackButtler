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
    private void SetupTableColumns()
    {
        ImGui.TableSetupColumn("V", ImGuiTableColumnFlags.WidthFixed, 25);
        ImGui.TableSetupColumn("J", ImGuiTableColumnFlags.WidthFixed, 25);
        ImGui.TableSetupColumn("R", ImGuiTableColumnFlags.WidthFixed, 25);
        ImGui.TableSetupColumn("P", ImGuiTableColumnFlags.WidthFixed, 25);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 170);
        ImGui.TableSetupColumn("Bank", ImGuiTableColumnFlags.WidthStretch, 1.0f);
        ImGui.TableSetupColumn("Bet", ImGuiTableColumnFlags.WidthStretch, 1.0f);
        ImGui.TableSetupColumn("Cards", ImGuiTableColumnFlags.WidthFixed, 150);
        ImGui.TableSetupColumn("Points", ImGuiTableColumnFlags.WidthFixed, 80);
        if (_config.PlayerRollingForThemselves)
            ImGui.TableSetupColumn("S", ImGuiTableColumnFlags.WidthFixed, 25);
        ImGui.TableSetupColumn("Controls", ImGuiTableColumnFlags.WidthFixed, 120);
    }

    private void DrawPlayerTableHeaders()
    {
        static void CenteredHeader(string text)
        {
            float width = ImGui.GetContentRegionAvail().X;
            float textWidth = ImGui.CalcTextSize(text).X;
            float offset = MathF.Max(0f, (width - textWidth) * 0.5f);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2f);
            ImGui.TextUnformatted(text);
        }

        static void HeaderText(string text)
        {
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2f);
            ImGui.TextUnformatted(text);
        }

        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
        int columnCount = ImGui.TableGetColumnCount();
        for (int column = 0; column < columnCount; column++)
        {
            ImGui.TableSetColumnIndex(column);
            string? name = ImGui.TableGetColumnName(column);
            if (name is "V" or "A" or "J" or "R" or "P" or "S")
            {
                CenteredHeader(name);
            }
            else if (name == "Bank")
            {
                if (ImGui.Checkbox("##bank_header_unlock", ref _config.EnableBankInput)) _save();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Enable Bank input");
                ImGui.SameLine(0f, 4f);
                HeaderText("Bank");
            }
            else
            {
                HeaderText(name ?? string.Empty);
            }
        }
    }
}
