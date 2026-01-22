using System;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using BlackJackButtler.Chat;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow : Window, IDisposable
{
    private enum Page { Main, Regexes, Messages, Vars, Settings }
    private Page _page = Page.Main;

    private readonly Configuration _config;
    private readonly Action _save;
    private readonly ChatLogBuffer _chatLog;

    // Status für Main
    private bool _isRecognitionActive = false;
    private List<PlayerState> _players = new();

    // Status für Regex
    private bool _showRegexWarningPopup;

    // Status für Messages
    private bool _openForceDefaultsPopup = false;

    public BlackJackButtlerWindow(Configuration config, Action save, ChatLogBuffer chatLog) : base("BlackJack Buttler")
    {
        _config = config;
        _save = save;
        _chatLog = chatLog;

        Size = new Vector2(1150, 650);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }
    public void OpenMain() { _page = Page.Main; IsOpen = true; }
    public void OpenSettings() { _page = Page.Settings; IsOpen = true; }
    public List<PlayerState> GetPlayers() => _players;

    public override void Draw()
    {
        var avail = ImGui.GetContentRegionAvail();
        var sidebarWidth = 200f;

        ImGui.BeginChild("bjb.sidebar", new Vector2(sidebarWidth, avail.Y), true);
        ImGui.TextUnformatted("BlackJack Buttler");
        ImGui.Separator();

        NavButton(Page.Main, "Main");
        NavButton(Page.Regexes, "Regex");
        NavButton(Page.Messages, "Messages");
        NavButton(Page.Vars, "Variables");
        NavButton(Page.Settings, "Settings");

        ImGui.EndChild();
        ImGui.SameLine();

        ImGui.BeginChild("bjb.content", new Vector2(0, avail.Y), true);
        switch (_page)
        {
            case Page.Main:         DrawMainPage(); break;
            case Page.Regexes:      DrawRegexPage(); break;
            case Page.Messages:     DrawMessagesPage(); break;
            case Page.Vars:         DrawVarsPage(); break;
            case Page.Settings:     DrawSettingsPage(); break;
        }
        ImGui.EndChild();
    }

    private void NavButton(Page page, string label)
    {
        var selected = _page == page;
        if (selected) ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 2f);
        if (ImGui.Button(label, new Vector2(-1, 40))) _page = page;
        if (selected) ImGui.PopStyleVar();
    }

    private void DrawSettingsPage()
    {
        ImGui.TextUnformatted("Settings");
        ImGui.Separator();
        ImGui.TextDisabled("Work in Progress...");
    }
}
