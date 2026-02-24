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
    private enum Page { Main, Regexes, Messages, Commands , OwnButtons , Settings , Vars , RoundLog , Debug , MacroImport , Thanks , Stats , Webhooks , Presets }
    private Page _page = Page.Main;

    private readonly Configuration _config;
    private Action _save;
    private bool _presetDirty = false;
    private readonly ChatLogBuffer _chatLog;
    private readonly Dalamud.Interface.ImGuiFileDialog.FileDialogManager _fileDialogManager = new();

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

    private bool _triggerVipConfirmPopup = false;
    private bool _isVipConfirmOpen = false;
    private PlayerState? _vipConfirmPlayer = null;
    private int _vipConfirmOldTier = 0;
    private int _vipConfirmNewTier = 0;

    private bool _triggerVenuePopup = false;
    private bool _isVenuePopupOpen = false;
    private string _venueNameBuffer = "";
    private VenueAddress? _pendingVenueAddress = null;
    private PlayerState? _pendingVipPlayer = null;
    private int _pendingVipTier = 0;

    private readonly Dictionary<string, long> _bankSnapshot = new();
    private readonly Dictionary<string, long> _betSnapshot = new();

    private Configuration? _tempImportConfig;
    private bool _showImportModal = false;
    private bool _openImportConfirmPopup = false;
    private bool _isSidebarVisible = true;
    private string? _pendingSettingsFocus;

    private bool _showRestoreSessionButton = false;
    private bool _showVarRefPanel = false;
    private int _panicConfirmStage = 0;
    private bool _highlightNewRound = false;
    private int _selectedWebhookIndex = -1;

    private bool _notepadLoaded = false;
    private readonly NotepadWindow _notepadWindow;

    private Vector2 _lastWindowPos;
    private Vector2 _lastWindowSize;

    public BlackJackButtlerWindow(Configuration config, Action save, ChatLogBuffer chatLog, NotepadWindow notepadWindow) : base($"BlackJack Buttler v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version} [Default]###BlackJackButtler")
    {
        _config = config;
        var origSave = save;
        _save = () => { _presetDirty = true; origSave(); };
        _chatLog = chatLog;
        _notepadWindow = notepadWindow;

        Size = new Vector2(1280, 580);
        SizeCondition = ImGuiCond.FirstUseEver;

        RespectCloseHotkey = false;

        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Comments,
            Priority = 90,
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

        _showRestoreSessionButton = SessionManager.HasSavedSession();
    }

    public void SetHighlightNewRound() => _highlightNewRound = true;
    public void Dispose() { }
    public void OpenMain() { _page = Page.Main; IsOpen = true; }
    public void OpenSettings() { _page = Page.Settings; IsOpen = true; }
    public List<PlayerState> GetPlayers() => _players;
    public PlayerState GetDealer() => _dealer;
    public (Vector2 Pos, Vector2 Size) GetWindowRect() => (_lastWindowPos, _lastWindowSize);

    public void OnUpdate()
    {
        if (IsRecognitionActive && (DateTime.Now - _lastSync).TotalMilliseconds > 1000)
        {
            _lastSync = DateTime.Now;
            SyncParty();
        }
    }

    public override void PreDraw()
    {
        var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        var label = _config.ActivePresetName ?? "Default";
        var dirty = _presetDirty ? "*" : "";
        WindowName = $"BlackJack Buttler v{ver} [{label}{dirty}]###BlackJackButtler";
    }

    public override void Draw()
    {
        _lastWindowPos = ImGui.GetWindowPos();
        _lastWindowSize = ImGui.GetWindowSize();

        if (_showRestoreSessionButton)
        {
            float availWidth = ImGui.GetContentRegionAvail().X;
            float xBtnWidth = 40f;
            float spacing = ImGui.GetStyle().ItemSpacing.X;

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.5f, 0.0f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1.0f, 0.6f, 0.1f, 1.0f));
            if (ImGui.Button("⚠ RESTORE PREVIOUS SESSION ⚠", new Vector2(availWidth - xBtnWidth - spacing, 40)))
                RestoreSessionFromFile();
            ImGui.PopStyleColor(2);

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
        var sidebarWidth = _isSidebarVisible ? 200f : 0f;

        var level = _config.CurrentLevel;

        if (_isSidebarVisible)
        {
            ImGui.BeginChild("bjb.sidebar", new Vector2(sidebarWidth, avail.Y), true);
            if (ImGui.SmallButton("<##hide_sidebar")) _isSidebarVisible = false;
            ImGui.SameLine();
            ImGui.TextUnformatted("BlackJack Buttler");

            ImGui.Separator();                  NavButton(Page.Main, "Main");
            ImGui.Separator();
            if(level >= UserLevel.Dev)          NavButton(Page.Regexes, "Regex");
            if(level >= UserLevel.Advanced)     NavButton(Page.Messages, "Messages");
            if(level >= UserLevel.Advanced)     NavButton(Page.Commands, "Commands");
            if(level >= UserLevel.Advanced)     NavButton(Page.OwnButtons, "Own Buttons");
            if(level >= UserLevel.Advanced)     NavButton(Page.Webhooks, "Webhooks");
            if(level >= UserLevel.Advanced)     NavButton(Page.Presets, "Presets");
            ImGui.Separator();                  NavButton(Page.Settings, "Settings");
                                                NavButton(Page.Stats, "Stats");
            ImGui.Separator();                  NavButton(Page.RoundLog, "Round History");
            if(level >= UserLevel.Dev)          NavButton(Page.Vars, "Variables");
            if(level >= UserLevel.Dev)          NavButton(Page.Debug, "DEBUG");
            if(level >= UserLevel.Dev)          NavButton(Page.MacroImport, "Macro Import");

            var remainingHeight = ImGui.GetContentRegionAvail().Y;
            if (remainingHeight > 50) ImGui.SetCursorPosY(ImGui.GetCursorPosY() + remainingHeight - 50);

                                                NavButton(Page.Thanks, "Thanks to");

            ImGui.EndChild();
            ImGui.SameLine();
        }

        BJBGui.ButtonTextColor = _config.ButtonTextColor;
        var btnHover  = new Vector4(Math.Min(_config.ButtonColor.X * 1.2f, 1f),
                                     Math.Min(_config.ButtonColor.Y * 1.2f, 1f),
                                     Math.Min(_config.ButtonColor.Z * 1.2f, 1f),
                                     _config.ButtonColor.W);
        var btnActive = new Vector4(_config.ButtonColor.X * 0.85f,
                                     _config.ButtonColor.Y * 0.85f,
                                     _config.ButtonColor.Z * 0.85f,
                                     _config.ButtonColor.W);
        ImGui.PushStyleColor(ImGuiCol.Button,        _config.ButtonColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, btnHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,  btnActive);
        ImGui.BeginChild("bjb.content", new Vector2(0, avail.Y), true);

        if (!_isSidebarVisible)
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

        switch (_page)
        {
            case Page.Main:         DrawMainPage(); break;
            case Page.Regexes:      DrawRegexPage(); break;
            case Page.Messages:     DrawMessagesPage(); break;
            case Page.Commands:     DrawCommandsPage(); break;
            case Page.OwnButtons:   DrawOwnButtonsPage(); break;
            case Page.Settings:     DrawSettingsPage(); break;
            case Page.Vars:         DrawVarsPage(); break;
            case Page.RoundLog:     DrawRoundLogPage(); break;
            case Page.Debug:        DrawDebugPage(); break;
            case Page.MacroImport:  DrawMacroImportPage(); break;
            case Page.Thanks:       DrawThanksPage(); break;
            case Page.Stats:        DrawStatsPage(); break;
            case Page.Webhooks:     DrawWebhooksPage(); break;
            case Page.Presets:      DrawPresetsPage(); break;
        }
        ImGui.EndChild();
        ImGui.PopStyleColor(3);

        _fileDialogManager.Draw();
        DropboxIntegration.DrawHelperWindow();
        DrawSplitMoneyPopup();
        DrawDDMoneyPopup();
        DrawVarRefPanel();
    }

    private void RestoreSessionFromFile()
    {
        if (SessionManager.RestoreSession(
            out var players,
            out var dealer,
            out var phase,
            out var history,
            out var historyIndex))
        {
            _players = players;
            _dealer = dealer;
            GameEngine.CurrentPhase = phase;

            GameLog.Clear();
            foreach (var (idx, snapshot) in history)
            {
                GameLog.RestoreSnapshot(idx, snapshot);
            }
            GameLog.SetIndex(historyIndex);

            IsRecognitionActive = true;

            _showRestoreSessionButton = false;
            _page = Page.Main;

            AddDebugLog("[SessionManager] Session restored successfully!", false);
            Plugin.Log.Information($"[SessionManager] Restored to Phase: {phase} with {players.Count} players");
        }
        else
        {
            _showRestoreSessionButton = false;
            AddDebugLog("[SessionManager] Failed to restore session!", false);
        }
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
