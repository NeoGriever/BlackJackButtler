using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private List<string> _thanksToNamesTesting = new() {"Sleip", "Xia Dove", "Hadesyra Ravenshadow", "Le'grasstoucher", "Amystra Pinkypie"};

    private List<string> _thanksToNamesSupport = new() {};

    private void DrawThanksPage()
    {
        var backgroundColor = new Vector4(0.05f, 0.05f, 0.15f, 0.95f);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, backgroundColor);
        var availRegion = ImGui.GetContentRegionAvail();
        ImGui.BeginChild("##thanks_bg", availRegion, false);

        var titleColor = new Vector4(1.0f, 0.84f, 0.0f, 1.0f);
        ImGui.PushFont(ImGui.GetFont());
        ImGui.SetWindowFontScale(1.5f);

        var title = "Big thanks to my testers";
        var titleSize = ImGui.CalcTextSize(title);
        var windowWidth = ImGui.GetWindowWidth();
        ImGui.SetCursorPosX((windowWidth - titleSize.X) / 2);

        ImGui.TextColored(titleColor, title);
        ImGui.SetWindowFontScale(1.0f);
        ImGui.PopFont();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Spacing();

        List<string> notifyLines = new()
        {
            "Big thanks goes to this people,",
            "who helped me testing the tool and fix some critical bugs"
        };

        foreach (var line in notifyLines)
        {
            var entrySize = ImGui.CalcTextSize(line);
            ImGui.SetCursorPosX((windowWidth - entrySize.X) / 2);
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1.0f), line);
        }

        ImGui.Spacing();

        var nameColor = new Vector4(1.0f, 0.7f, 0.7f, 1.0f);
        var glowColor = new Vector4(0.8f, 0.0f, 0.0f, 0.3f);

        foreach (var name in _thanksToNamesTesting)
        {
            var nameSize = ImGui.CalcTextSize(name);
            var posX = (windowWidth - nameSize.X) / 2;

            var drawList = ImGui.GetWindowDrawList();
            var cursorPos = ImGui.GetCursorScreenPos();
            var textPos = new Vector2(posX + cursorPos.X, cursorPos.Y);

            for (int offsetX = -2; offsetX <= 2; offsetX++)
            {
                for (int offsetY = -2; offsetY <= 2; offsetY++)
                {
                    if (offsetX == 0 && offsetY == 0) continue;
                    var glowPos = new Vector2(textPos.X + offsetX, textPos.Y + offsetY);
                    drawList.AddText(glowPos, ImGui.ColorConvertFloat4ToU32(glowColor), name);
                }
            }

            ImGui.SetCursorPosX(posX);
            ImGui.TextColored(nameColor, name);

            ImGui.Spacing();
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.SetWindowFontScale(1.5f);

        ImGui.PushFont(ImGui.GetFont());
        ImGui.SetWindowFontScale(1.5f);

        var title2 = "Big thanks to my supporters";
        var titleSize2 = ImGui.CalcTextSize(title2);
        ImGui.SetCursorPosX((windowWidth - titleSize2.X) / 2);

        ImGui.TextColored(titleColor, title2);
        ImGui.SetWindowFontScale(1.0f);
        ImGui.PopFont();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Spacing();

        List<string> notifyLines2 = new()
        {
            "Big thanks goes to this people,",
            "who support me through buymeacoffee.com/mindconstructor"
        };

        foreach (var line2 in notifyLines2)
        {
            var entrySize2 = ImGui.CalcTextSize(line2);
            ImGui.SetCursorPosX((windowWidth - entrySize2.X) / 2);
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1.0f), line2);
        }

        ImGui.Spacing();

        var nameColor2 = new Vector4(0.3f, 0.8f, 0.7f, 1.0f);
        var glowColor2 = new Vector4(0.2f, 0.9f, 0.4f, 0.3f);

        foreach (var name2 in _thanksToNamesSupport)
        {
            var nameSize2 = ImGui.CalcTextSize(name2);
            var posX2 = (windowWidth - nameSize2.X) / 2;

            var drawList2 = ImGui.GetWindowDrawList();
            var cursorPos2 = ImGui.GetCursorScreenPos();
            var textPos2 = new Vector2(posX2 + cursorPos2.X - 8, cursorPos2.Y);

            for (int offsetX2 = -2; offsetX2 <= 2; offsetX2++)
            {
                for (int offsetY2 = -2; offsetY2 <= 2; offsetY2++)
                {
                    if (offsetX2 == 0 && offsetY2 == 0) continue;
                    var glowPos2 = new Vector2(textPos2.X + offsetX2, textPos2.Y + offsetY2);
                    drawList2.AddText(glowPos2, ImGui.ColorConvertFloat4ToU32(glowColor2), name2);
                }
            }

            ImGui.SetCursorPosX(posX2);
            ImGui.TextColored(nameColor2, name2);

            ImGui.Spacing();
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }
}
