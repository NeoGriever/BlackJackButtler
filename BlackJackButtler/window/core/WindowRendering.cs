using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using BlackJackButtler.Chat;
using Newtonsoft.Json.Linq;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    public override void Draw()
    {
        if (BlacklistManager.IsBlocked)
        {
            ImGui.TextColored(new Vector4(0.6f, 0.0f, 0.0f, 1f), "This plugin has been disabled.");
            return;
        }

        _lastWindowPos = ImGui.GetWindowPos();
        _lastWindowSize = ImGui.GetWindowSize();
        DrawThemeBackground();
        ApplyWindowFontScale();


        if (_showRestoreSessionButton)
        {
            float availWidth = ImGui.GetContentRegionAvail().X;
            float xBtnWidth = 40f;
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            float restoreBtnWidth = Math.Max(availWidth - xBtnWidth - spacing, 1f);

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.5f, 0.0f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1.0f, 0.6f, 0.1f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.Text, BJBGui.OrangeHighlightTextColor);
            if (ImGui.Button("⚠ RESTORE PREVIOUS SESSION ⚠", new Vector2(restoreBtnWidth, 40)))
                RestoreSessionFromFile();
            ImGui.PopStyleColor(3);

            ImGui.SameLine();

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.4f, 0.4f, 0.4f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.5f, 0.5f, 0.5f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.3f, 0.3f, 0.3f, 1.0f));
            if (ImGui.Button("X##dismiss_restore", new Vector2(xBtnWidth, 40)))
                ImGui.OpenPopup("dismiss_session_popup");
            ImGui.PopStyleColor(3);

            if (ImGui.BeginPopup("dismiss_session_popup"))
            {
                ImGui.TextUnformatted("Dismiss session backup? (irreversible)");
                if (ImGui.Button("Yes##dismiss_yes"))
                {
                    _showRestoreSessionButton = false;
                    SessionManager.ClearSession();
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("No##dismiss_no"))
                    ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }

            ImGui.Separator();
        }

        var avail = ImGui.GetContentRegionAvail();
        var childHeight = Math.Max(avail.Y, 1f);
        var burgerMode = _config.MenuStyle == MenuStyleMode.BurgerMenu;
        var topTabsMode = _config.MenuStyle == MenuStyleMode.TopTabs;
        var sidebarMode = _config.MenuStyle == MenuStyleMode.Sidebar;
        var sidebarWidth = (_isSidebarVisible && sidebarMode) ? 200f : 0f;

        var level = _config.CurrentLevel;
        if (level == UserLevel.Custom) EnsureCustomVisiblePages();

        if (_isSidebarVisible && sidebarMode)
        {
            ImGui.BeginChild("bjb.sidebar", new Vector2(sidebarWidth, childHeight), true);
            if (ImGui.SmallButton("<##hide_sidebar")) _isSidebarVisible = false;
            ImGui.SameLine();
            ImGui.TextUnformatted("BlackJack Buttler");
            if (level == UserLevel.Custom)
            {
                ImGui.SameLine();
                var wasCustomEditMode = _customEditMode;
                if (wasCustomEditMode) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.2f, 0.9f));
                if (ImGui.SmallButton("\u270F##custom_edit")) _customEditMode = !_customEditMode;
                if (wasCustomEditMode) ImGui.PopStyleColor();
            }
            else
            {
                _customEditMode = false;
            }

            ImGui.Separator();                                          NavButton(Page.Main, "Main");
            ImGui.Separator();
            if(ShouldShowPage(Page.Regexes, level))                     NavButton(Page.Regexes, "Regex");
            if(ShouldShowPage(Page.Messages, level))                    NavButton(Page.Messages, "Messages");
            if(ShouldShowPage(Page.Commands, level))                    NavButton(Page.Commands, "Commands");
            if(ShouldShowPage(Page.Presets, level))                     NavButton(Page.Presets, "Presets");
            ImGui.Separator();
            if(ShouldShowPage(Page.Settings, level))                    NavButton(Page.Settings, "Settings");
            if(ShouldShowPage(Page.Stats, level))                       NavButton(Page.Stats, "Stats");
            ImGui.Separator();
            if(ShouldShowPage(Page.Debug, level))                       NavButton(Page.Debug, "DEBUG");
            if(ShouldShowPage(Page.DrawLogic, level))                   NavButton(Page.DrawLogic, "Draw Logic");

            var remainingHeight = ImGui.GetContentRegionAvail().Y;
            if (remainingHeight > 50) ImGui.SetCursorPosY(ImGui.GetCursorPosY() + remainingHeight - 50);

            if (!_config.HideThanksPage && ShouldShowPage(Page.Thanks, level))
                NavButton(Page.Thanks, "Thanks to");

            ImGui.EndChild();
            ImGui.SameLine();
        }

        BJBGui.ButtonTextColor = CurrentTheme.Fonts.TryGetValue("button", out var themeButtonFont) && Themes.ThemeDefinition.TryColor(themeButtonFont.Color, out var themeButtonText)
            ? themeButtonText : _config.ButtonTextColor;
        BJBGui.GilVisual = _config.GilVisual;
        var btnHover  = new Vector4(Math.Min(_config.ButtonColor.X * 1.2f, 1f), Math.Min(_config.ButtonColor.Y * 1.2f, 1f), Math.Min(_config.ButtonColor.Z * 1.2f, 1f), _config.ButtonColor.W);
        var btnActive = new Vector4(_config.ButtonColor.X * 0.85f, _config.ButtonColor.Y * 0.85f, _config.ButtonColor.Z * 0.85f, _config.ButtonColor.W);
        ImGui.PushStyleColor(ImGuiCol.Button,        ThemeButtonColor("Button", _config.ButtonColor));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ThemeButtonColor("ButtonHovered", btnHover));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,  ThemeButtonColor("ButtonActive", btnActive));
        BeginThemedContentChild(childHeight);

        if (burgerMode)
        {
            ImGui.PushFont(UiBuilder.IconFont);
            if (BJBGui.SmallButton(FontAwesomeIcon.Bars.ToIconString() + "##bjb_burger"))
                ImGui.OpenPopup("bjb_burger_menu");
            ImGui.PopFont();
            ImGui.SameLine();
            ImGui.TextDisabled($"Page: {_page}");
            if (level == UserLevel.Custom)
            {
                ImGui.SameLine();
                var wasCustomEditMode = _customEditMode;
                if (wasCustomEditMode) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.2f, 0.9f));
                if (ImGui.SmallButton("✏##custom_edit_burger")) _customEditMode = !_customEditMode;
                if (wasCustomEditMode) ImGui.PopStyleColor();
            }
            else
            {
                _customEditMode = false;
            }
            ImGui.Separator();
            DrawBurgerMenuPopup(level);
        }
        else if (topTabsMode)
        {
            DrawTopTabs(level);
        }
        else if (!_isSidebarVisible)
        {
            if (BJBGui.SmallButton(">##show_sidebar")) _isSidebarVisible = true;
            ImGui.SameLine();
            ImGui.TextDisabled($"Page: {_page}");
            ImGui.Separator();
        }

        if(level >= UserLevel.Dev && !_config.dismissDevWarning) {
            ImGui.TextColored(new Vector4(1.0f, 0.0f, 0.0f, 1.0f), "!!! Warning !!!");
            ImGui.TextColored(new Vector4(1.0f, 0.4f, 0.0f, 1.0f), "You are in dev mode!");
            ImGui.TextColored(new Vector4(0.9f, 0.6f, 0.0f, 1.0f), "");
            ImGui.TextColored(new Vector4(0.9f, 0.6f, 0.0f, 1.0f), "Make sure you know what you're doing. Dev mode lets you change everything.");
            ImGui.TextColored(new Vector4(0.9f, 0.6f, 0.0f, 1.0f), "And it's easy to change the wrong thing.");
            ImGui.TextColored(new Vector4(0.9f, 0.6f, 0.0f, 1.0f), "");
            if (BJBGui.Button("I know, what i'm doing")) {
                _config.dismissDevWarning = true;
                _save();
            }
            ImGui.TextColored(new Vector4(0.9f, 0.6f, 0.0f, 1.0f), "");
            ImGui.Spacing();
        }

        if (topTabsMode)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(3f, 3f));
            ImGui.BeginChild("bjb.page", new Vector2(0f, 0f), false);
        }

        switch (_page)
        {
            case Page.Main:         DrawMainPage(); break;
            case Page.Regexes:      DrawRegexPage(); break;
            case Page.Messages:     DrawMessagesPage(); break;
            case Page.Commands:     DrawCommandsPage(); break;
            case Page.OwnButtons:   _page = Page.Commands; DrawCommandsPage(); break;
            case Page.Settings:     DrawThemedSettingsPage(); break;
            case Page.Vars:         DrawVarsPage(); break;
            case Page.RoundLog:     DrawRoundLogPage(); break;
            case Page.Debug:        DrawDebugPage(); break;
            case Page.Thanks:
                if (_config.HideThanksPage) { _page = Page.Main; DrawMainPage(); }
                else DrawThanksPage();
                break;
            case Page.Stats:        DrawStatsPage(); break;
            case Page.Presets:      DrawPresetsPage(); break;
            case Page.DrawLogic:    DrawDrawLogicPage(); break;
        }

        if (topTabsMode)
        {
            ImGui.EndChild();
            ImGui.PopStyleVar();
        }
        ImGui.EndChild();
        ImGui.PopStyleColor(3);

        DrawUserStatsSessionPrompt();
        _fileDialogManager.Draw();
        PayoutManagement.DrawHelperWindow();
        DrawSplitMoneyPopup();
        DrawDDMoneyPopup();
        DrawHandEditPopup();
        DrawVarRefPanel();
        DrawDrawLogicDocPanel();
        ExecuteDrawLogic();
    }
}
