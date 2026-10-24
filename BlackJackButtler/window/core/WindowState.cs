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
    private enum Page { Main, Regexes, Messages, Commands , OwnButtons , Settings , Vars , RoundLog , Debug , Thanks , Stats , Presets , DrawLogic }
    private Page _page = Page.Main;

    private readonly Configuration _config;
    private Action _save;
    private bool _presetDirty = false;
    private bool _customEditMode = false;
    private readonly ChatLogBuffer _chatLog;
    private readonly Dalamud.Interface.ImGuiFileDialog.FileDialogManager _fileDialogManager = new();

    public bool IsRecognitionActive = false;

    public void SyncPartyPublic() => SyncParty();

    private List<PlayerState> _players = new();
    private readonly HashSet<string> _knownGroupMemberKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _newTradingPlayerKeys = new(StringComparer.OrdinalIgnoreCase);
    private bool _hasGroupMembershipBaseline;

    private bool _showRegexWarningPopup;
    private bool _openRegexResetPopup = false;
    private bool _openForceDefaultsPopup = false;
    private bool _openCmdForceDefaultsPopup = false;
    private bool _openCleanDataPopup = false;
    private string _startTimeInputBuffer = string.Empty;
    private string? _startTimeInputError;
    private PlayerState _dealer = new() { Name = "Dealer", IsActivePlayer = true, IsDealer = true };
    private PlayerState? _editingAliasPlayer;
    private const string AliasPopupTitle = "Set Player Alias##bjb_alias_popup";
    private string _aliasInputBuffer = string.Empty;
    private bool _isAliasModalOpen = false;
    private bool _triggerAliasPopup = false;

    private readonly Dictionary<string, long> _bankSnapshot = new();
    private readonly Dictionary<string, long> _betSnapshot = new();
    private readonly HashSet<string> _bankChanged = new();
    private readonly HashSet<string> _betChanged = new();
    private readonly Dictionary<string, (long amount, DateTime clickedAt)> _bankToTipUndo = new();

    private JObject? _tempImportJson;
    private bool _showImportModal = false;
    private bool _openImportConfirmPopup = false;

    private int _presetChangeCount = 0;
    private int? _presetImportTargetIndex;
    private bool _openPresetImportConfirm = false;
    private bool _showPresetImportModal = false;
    private bool _openHashedStatsConfirm = false;
    private bool _showHashedStatsModal = false;
    private string? _presetImportJson;
    private bool _isSidebarVisible = true;
    private Page _topTabsLastPage = Page.Main;
    private bool _topTabsForceReselect = false;
    private string? _pendingCommandsTab;
    private readonly HashSet<string> _pendingOpenCommandGroups = new(StringComparer.OrdinalIgnoreCase);
    private string? _pendingOwnButtonGroupName;

    private bool _showRestoreSessionButton = false;
    private bool _showVarRefPanel = false;
    private Page? _presetNavHoverPage;
    private int _panicConfirmStage = 0;
    private bool _highlightNewRound = false;
    private bool _partyDissolved = false;
    private bool _lastAllianceMode = false;
    private bool _panicInProgress = false;

    private bool _notepadLoaded = false;
    private readonly NotepadWindow _notepadWindow;
    private VariablesPopupWindow? _variablesPopupWindow;

    public void SetVariablesPopupWindow(VariablesPopupWindow w) => _variablesPopupWindow = w;
    public void ToggleVariablesPopup()
    {
        if (_variablesPopupWindow == null) return;
        _variablesPopupWindow.IsOpen = !_variablesPopupWindow.IsOpen;
    }

    private Vector2 _lastWindowPos;
    private Vector2 _lastWindowSize;
    private TablePopoutWindow? _tablePopoutWindow;
    private NearbyPopoutWindow? _nearbyPopoutWindow;

    public void SetPopoutWindows(TablePopoutWindow table, NearbyPopoutWindow nearby)
    {
        _tablePopoutWindow = table;
        _nearbyPopoutWindow = nearby;
    }
}
