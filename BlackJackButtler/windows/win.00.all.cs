using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using BlackJackButtler.Chat;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow : Window, IDisposable
{
    private enum Page { Main, Regexes, Messages, Commands , Settings , Vars , RoundLog , Debug , Thanks }
    private Page _page = Page.Main;

    private readonly Configuration _config;
    private readonly Action _save;
    private readonly ChatLogBuffer _chatLog;

    public bool IsRecognitionActive = false;

    public void SyncPartyPublic() => SyncParty();

    private List<PlayerState> _players = new();

    private bool _showRegexWarningPopup;
    private bool _openRegexResetPopup = false;
    private bool _openForceDefaultsPopup = false;
    private DateTime _lastSync = DateTime.MinValue;

    private PlayerState _dealer = new() { Name = "Dealer", IsActivePlayer = true };
    private PlayerState? _editingAliasPlayer;
    private string _aliasInputBuffer = string.Empty;
    private bool _isAliasModalOpen = false;
    private bool _triggerAliasPopup = false;

    private Configuration? _tempImportConfig;
    private bool _showImportModal = false;
    private bool _isSidebarVisible = true;

    public BlackJackButtlerWindow(Configuration config, Action save, ChatLogBuffer chatLog) : base("BlackJack Buttler")
    {
        _config = config;
        _save = save;
        _chatLog = chatLog;

        Size = new Vector2(1150, 650);
        SizeCondition = ImGuiCond.FirstUseEver;

        RespectCloseHotkey = false;

        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Coffee,
            Priority = 100,
            Click = _ =>
            {
                Dalamud.Utility.Util.OpenLink("https://buymeacoffee.com/mindconstructor");
            },
            ShowTooltip = () =>
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                ImGui.SetTooltip("If you like the plugin,\nthink about to spend me something\nthrough buy me a coffee.\n\n<3 <3 <3");
            }
        });

        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Discord,
            Priority = 99,
            Click = _ =>
            {
                Dalamud.Utility.Util.OpenLink("https://discord.gg/CMCzEH4NZS");
            },
            ShowTooltip = () =>
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                ImGui.SetTooltip("Join my discord for\n- bug reports\n- ideas\n- faq\n- just talk\n\n(still working on it!)");
            }
        });
    }

    public void Dispose() { }
    public void OpenMain() { _page = Page.Main; IsOpen = true; }
    public void OpenSettings() { _page = Page.Settings; IsOpen = true; }
    public List<PlayerState> GetPlayers() => _players;
    public PlayerState GetDealer() => _dealer;

    public void OnUpdate()
    {
        if (IsRecognitionActive && (DateTime.Now - _lastSync).TotalMilliseconds > 1000)
        {
            _lastSync = DateTime.Now;
            SyncParty();
        }
    }

    public override void Draw()
    {
        var avail = ImGui.GetContentRegionAvail();
        var sidebarWidth = _isSidebarVisible ? 200f : 0f;

        var level = _config.CurrentLevel;

        if (_isSidebarVisible)
        {
            ImGui.BeginChild("bjb.sidebar", new Vector2(sidebarWidth, avail.Y), true);
            ImGui.TextUnformatted("BlackJack Buttler");
            ImGui.SameLine(ImGui.GetWindowWidth() - 30);
            if (ImGui.SmallButton("<##hide_sidebar")) _isSidebarVisible = false;

            ImGui.Separator();                  NavButton(Page.Main, "Main");
            ImGui.Separator();
            if(level >= UserLevel.Dev)          NavButton(Page.Regexes, "Regex");
            if(level >= UserLevel.Advanced)     NavButton(Page.Messages, "Messages");
            if(level >= UserLevel.Advanced)     NavButton(Page.Commands, "Commands");
            ImGui.Separator();                  NavButton(Page.Settings, "Settings");
            ImGui.Separator();                  NavButton(Page.RoundLog, "Round History");
            if(level >= UserLevel.Dev)          NavButton(Page.Vars, "Variables");
            if(level >= UserLevel.Dev)          NavButton(Page.Debug, "DEBUG");

            var remainingHeight = ImGui.GetContentRegionAvail().Y;
            if (remainingHeight > 50) ImGui.SetCursorPosY(ImGui.GetCursorPosY() + remainingHeight - 50);

                                                NavButton(Page.Thanks, "Thanks to");

            ImGui.EndChild();
            ImGui.SameLine();
        }

        ImGui.BeginChild("bjb.content", new Vector2(0, avail.Y), true);

        if (!_isSidebarVisible)
        {
            if (ImGui.SmallButton(">##show_sidebar")) _isSidebarVisible = true;
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
            if (ImGui.Button("I know, what i'm doing")) {
                _config.dismissDevWarning = true;
                _save();
            }
            ImGui.TextColored(new Vector4(0.9f, 0.6f, 0.0f, 1.0f), "");
            ImGui.Spacing();
        }

        switch (_page)
        {
            case Page.Main:         DrawMainPage(); break;
            case Page.Regexes:      DrawRegexPage(); break;
            case Page.Messages:     DrawMessagesPage(); break;
            case Page.Commands:     DrawCommandsPage(); break;
            case Page.Settings:     DrawSettingsPage(); break;
            case Page.Vars:         DrawVarsPage(); break;
            case Page.RoundLog:     DrawRoundLogPage(); break;
            case Page.Debug:        DrawDebugPage(); break;
            case Page.Thanks:       DrawThanksPage(); break;
        }
        ImGui.EndChild();
        DropboxIntegration.DrawHelperWindow();
    }

    private void NavButton(Page page, string label)
    {
        var selected = _page == page;

        if (selected)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.15f, 0.35f, 0.65f, 0.9f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.2f, 0.4f, 0.75f, 1f));
        }

        if (ImGui.Button(label, new Vector2(-1, 40))) _page = page;

        if (selected)
        {
            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar();
        }
    }

}
