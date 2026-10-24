using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using BlackJackButtler.Regex;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawSettingsTabs(int level)
    {
        if (!ImGui.BeginTabBar("##settings_v3_tabs"))
            return;

        DrawSettingsTab(0, "General", () => DrawGeneralSettings(level));
        DrawSettingsTab(1, "Automation", DrawAutomationSettings);
        DrawSettingsTab(2, "Rules", DrawRulesSettings);
        DrawSettingsTab(3, "Betting", DrawBettingSettings);
        DrawSettingsTab(4, "Time & Delay", DrawTimeDelaySettings);
        DrawSettingsTab(5, "Nearby Players", DrawNearbySettingsContent);
        DrawSettingsTab(6, "Visual", DrawVisualSettings);
        DrawSettingsTab(7, "Themes", DrawThemeSettings);
        DrawSettingsTab(8, "System", () => DrawSystemSettings(level));
#if DEBUG
        DrawSettingsTab(9, "DEBUG", DrawSettingsDebugTab);
#endif

        ImGui.EndTabBar();
    }

    private void DrawSettingsTab(int index, string label, Action draw)
    {
        if (!ImGui.BeginTabItem(label))
            return;

        _settingsTabIndex = index;
        ImGui.Spacing();
        draw();
        ImGui.EndTabItem();
    }

    private void DrawGeneralSettings(int level)
    {
        ImGui.TextUnformatted("User Level");
        DrawEnumButtons(
            "user_level_v3",
            ref level,
            new[] { "Beginner", "Advanced", "Profi", "Custom" },
            idx =>
            {
                _config.CurrentLevel = (UserLevel)idx;
                _save();
            },
            separateBeforeIndex: 3
        );

        ImGui.Spacing();
        ImGui.TextUnformatted("Menu Style");
        var menuStyle = (int)_config.MenuStyle;
        DrawEnumButtons(
            "menu_style_v3",
            ref menuStyle,
            new[] { "Side", "Burger", "Tabs" },
            idx =>
            {
                _config.MenuStyle = (MenuStyleMode)idx;
                _save();
            }
        );

        ImGui.Spacing();
        ImGui.TextUnformatted("Gil Display");
        var gilVisual = (int)_config.GilVisual;
        ImGui.PushFont(UiBuilder.MonoFont);
        DrawEnumButtons(
            "gil_display_v3",
            ref gilVisual,
            new[] { "12345678", "12,345,678", ", 12,345,678" },
            idx =>
            {
                _config.GilVisual = (GilVisualMode)idx;
                _save();
            },
            joined: false
        );
        ImGui.PopFont();
    }
}
