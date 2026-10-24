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
    private void NavButton(Page page, string label)
    {
        var selected = _page == page;

        if (selected)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
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
            if (ImGui.Checkbox($"##cv_{pageName}", ref visible))
            {
                if (visible) { if (!_config.CustomVisiblePages.Contains(pageName)) _config.CustomVisiblePages.Add(pageName); }
                else _config.CustomVisiblePages.Remove(pageName);
                _save();
            }
            ImGui.SameLine();
            if (ImGui.Button(label, new Vector2(-1, 40)) && CanNavigateFromCurrentPage(page)) _page = page;
        }
        else
        {
            if (ImGui.Button(label, new Vector2(-1, 40)) && CanNavigateFromCurrentPage(page)) _page = page;
        }

        if (highlightSource)
            ImGui.PopStyleColor();

        if (selected)
        {
            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar();
        }
    }

    private bool ShouldShowPage(Page page, UserLevel level)
    {
        if (level == UserLevel.Custom)
            return _customEditMode || _config.CustomVisiblePages.Contains(page.ToString());

        return page switch
        {
            Page.Regexes or Page.Vars or Page.Debug or Page.DrawLogic => level >= UserLevel.Dev,
            Page.Messages or Page.Commands or Page.Presets => level >= UserLevel.Advanced,
            Page.OwnButtons => false,
            _ => true,
        };
    }

    private void EnsureCustomVisiblePages()
    {
        if (_config.CustomVisiblePages.Count > 0) return;
        foreach (Page p in Enum.GetValues(typeof(Page)))
            if (p != Page.Main) _config.CustomVisiblePages.Add(p.ToString());
        _save();
    }

    private bool CanNavigateFromCurrentPage(Page targetPage)
    {
        return true;
    }
}
