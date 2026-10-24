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
    private void DrawBurgerMenuPopup(UserLevel level)
    {
        if (!ImGui.BeginPopup("bjb_burger_menu", ImGuiWindowFlags.AlwaysAutoResize)) return;

        NavButtonBurger(Page.Main, "Main");
        ImGui.Separator();
        if (ShouldShowPage(Page.Regexes, level))     NavButtonBurger(Page.Regexes, "Regex");
        if (ShouldShowPage(Page.Messages, level))    NavButtonBurger(Page.Messages, "Messages");
        if (ShouldShowPage(Page.Commands, level))    NavButtonBurger(Page.Commands, "Commands");
        if (ShouldShowPage(Page.Presets, level))     NavButtonBurger(Page.Presets, "Presets");
        ImGui.Separator();
        if (ShouldShowPage(Page.Settings, level))    NavButtonBurger(Page.Settings, "Settings");
        if (ShouldShowPage(Page.Stats, level))       NavButtonBurger(Page.Stats, "Stats");
        ImGui.Separator();
        if (ShouldShowPage(Page.Debug, level))       NavButtonBurger(Page.Debug, "DEBUG");
        if (ShouldShowPage(Page.DrawLogic, level))   NavButtonBurger(Page.DrawLogic, "Draw Logic");
        ImGui.Separator();
        if (ShouldShowPage(Page.Thanks, level))      NavButtonBurger(Page.Thanks, "Thanks to");

        ImGui.EndPopup();
    }

    private void NavButtonBurger(Page page, string label)
    {
        var selected = _page == page;
        if (selected) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.15f, 0.35f, 0.65f, 0.9f));
        bool highlightSource = _presetNavHoverPage == page;
        if (highlightSource)
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.62f, 0.2f, 1f));

        if (_customEditMode && _config.CurrentLevel == UserLevel.Custom && page != Page.Main)
        {
            var pageName = page.ToString();
            bool visible = _config.CustomVisiblePages.Contains(pageName);
            if (ImGui.Checkbox($"##bcv_{pageName}", ref visible))
            {
                if (visible) { if (!_config.CustomVisiblePages.Contains(pageName)) _config.CustomVisiblePages.Add(pageName); }
                else _config.CustomVisiblePages.Remove(pageName);
                _save();
            }
            ImGui.SameLine();
        }

        if (ImGui.Button(label, new Vector2(220, 28)) && CanNavigateFromCurrentPage(page)) { _page = page; ImGui.CloseCurrentPopup(); }

        if (highlightSource) ImGui.PopStyleColor();
        if (selected) ImGui.PopStyleColor();
    }
}
