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
    private List<(Page page, string label)> BuildTopTabPages(UserLevel level)
    {
        var pages = new List<(Page page, string label)> { (Page.Main, "Main") };
        if (ShouldShowPage(Page.Regexes, level))   pages.Add((Page.Regexes, "Regex"));
        if (ShouldShowPage(Page.Messages, level))  pages.Add((Page.Messages, "Messages"));
        if (ShouldShowPage(Page.Commands, level))  pages.Add((Page.Commands, "Commands"));
        if (ShouldShowPage(Page.Presets, level))   pages.Add((Page.Presets, "Presets"));
        if (ShouldShowPage(Page.Settings, level))  pages.Add((Page.Settings, "Settings"));
        if (ShouldShowPage(Page.Stats, level))     pages.Add((Page.Stats, "Stats"));
        if (ShouldShowPage(Page.Debug, level))     pages.Add((Page.Debug, "DEBUG"));
        if (ShouldShowPage(Page.DrawLogic, level)) pages.Add((Page.DrawLogic, "Draw Logic"));
        if (!_config.HideThanksPage && ShouldShowPage(Page.Thanks, level)) pages.Add((Page.Thanks, "Thanks to"));
        return pages;
    }

    private void DrawTopTabs(UserLevel level)
    {
        if (level == UserLevel.Custom)
        {
            var wasCustomEditMode = _customEditMode;
            if (wasCustomEditMode) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.2f, 0.9f));
            if (ImGui.SmallButton("✏##custom_edit_tabs")) _customEditMode = !_customEditMode;
            if (wasCustomEditMode) ImGui.PopStyleColor();
        }
        else
        {
            _customEditMode = false;
        }

        var pages = BuildTopTabPages(level);

        if (level == UserLevel.Custom && _customEditMode)
        {
            DrawTopTabEditRow(pages);
            return;
        }

        var forceSelect = _page != _topTabsLastPage || _topTabsForceReselect;
        _topTabsForceReselect = false;

        const float tabFontScale = 1.15f;
        var enlargedPad = ImGui.GetStyle().FramePadding + new Vector2(8f, 4f);
        ImGui.SetWindowFontScale(tabFontScale);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, enlargedPad);

        var tabSpacing = ImGui.GetStyle().ItemInnerSpacing.X;
        var tabsWidth = 0f;
        foreach (var (_, label) in pages)
            tabsWidth += ImGui.CalcTextSize(label).X + enlargedPad.X * 2f + tabSpacing + 4f;
        var avail = ImGui.GetContentRegionAvail().X;
        if (tabsWidth < avail)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (avail - tabsWidth));

        if (ImGui.BeginTabBar("bjb.toptabs", ImGuiTabBarFlags.FittingPolicyScroll))
        {
            foreach (var (page, label) in pages)
            {
                var flags = forceSelect && page == _page ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
                bool highlightSource = _presetNavHoverPage == page;
                if (highlightSource) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.62f, 0.2f, 1f));
                var open = ImGui.BeginTabItem(label, flags);
                if (highlightSource) ImGui.PopStyleColor();
                if (open)
                {
                    if (page != _page)
                    {
                        if (CanNavigateFromCurrentPage(page)) _page = page;
                        else _topTabsForceReselect = true;
                    }
                    ImGui.EndTabItem();
                }
            }
            ImGui.EndTabBar();
        }

        ImGui.PopStyleVar();
        ImGui.SetWindowFontScale(1f);

        _topTabsLastPage = _page;
    }

    private void DrawTopTabEditRow(List<(Page page, string label)> pages)
    {
        ImGui.SameLine();
        var style = ImGui.GetStyle();
        var rightEdge = ImGui.GetWindowContentRegionMax().X;
        for (var i = 0; i < pages.Count; i++)
        {
            if (i > 0)
            {
                var predicted = ImGui.GetItemRectMax().X + style.ItemSpacing.X
                              + ImGui.CalcTextSize(pages[i].label).X + style.FramePadding.X * 2f;
                if (predicted < rightEdge) ImGui.SameLine();
            }
            NavButtonTab(pages[i].page, pages[i].label);
        }
        ImGui.Separator();
    }

    private void NavButtonTab(Page page, string label)
    {
        var selected = _page == page;
        if (selected)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.15f, 0.35f, 0.65f, 0.9f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.2f, 0.4f, 0.75f, 1f));
        }
        bool highlightSource = _presetNavHoverPage == page;
        if (highlightSource)
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.62f, 0.2f, 1f));

        if (_customEditMode && _config.CurrentLevel == UserLevel.Custom && page != Page.Main)
        {
            var pageName = page.ToString();
            bool visible = _config.CustomVisiblePages.Contains(pageName);
            if (ImGui.Checkbox($"##tcv_{pageName}", ref visible))
            {
                if (visible) { if (!_config.CustomVisiblePages.Contains(pageName)) _config.CustomVisiblePages.Add(pageName); }
                else _config.CustomVisiblePages.Remove(pageName);
                _save();
            }
            ImGui.SameLine();
        }

        if (ImGui.Button(label) && CanNavigateFromCurrentPage(page)) _page = page;

        if (highlightSource) ImGui.PopStyleColor();
        if (selected) ImGui.PopStyleColor(2);
    }
}
