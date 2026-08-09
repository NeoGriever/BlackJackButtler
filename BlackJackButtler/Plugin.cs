using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Game.Chat;
using Dalamud.Game.Config;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using BlackJackButtler.Chat;
using BlackJackButtler.Windows;
using BlackJackButtler.Regex;
using System.Runtime.InteropServices;
using ECommons;

using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace BlackJackButtler;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    internal static Plugin Instance { get; private set; } = null!;

    internal static Action<string>? DebugCommandSink { get; private set; }

    private const string CommandName = "/bjb";

    public Configuration Configuration { get; }

    public static bool IsDebugMode = false;
    public static bool IsSpeedMode = false;
    public static bool DebugAutoPlayers = false;
    public static string DebugDiceSequence = "7,10,9,4,4,8,3,3,10,6,5,4,3,2";
    private static int _debugDiceSequenceIndex = 0;

    private readonly WindowSystem windowSystem = new("BlackJackButtler");
    private readonly BlackJackButtlerWindow mainWindow;
    private readonly ChatLogBuffer chatLog = new(500);
    private readonly DebugLogWindow debugLogWindow;
    private ChatBoxWindow chatBoxWindow = null!;
    private readonly DrawLogicDebugWindow drawLogicDebugWindow;
    private readonly NotepadWindow notepadWindow;
    private readonly CustomButtonBarWindow buttonBarWindow;
    private readonly TablePopoutWindow tablePopoutWindow;
    private readonly NearbyPopoutWindow nearbyPopoutWindow;
    private readonly UpdatePopupWindow updatePopupWindow;
    private readonly ImportantNoticeWindow importantNoticeWindow;
    private readonly BlacklistBannerWindow blacklistBannerWindow;
    private VariablesPopupWindow variablesPopupWindow = null!;
    private DateTime _lastSync = DateTime.MinValue;
    private DateTime _lastIdleTick = DateTime.MinValue;
    private bool _frameworkHooked = false;
    private bool _chatHooked = false;
    private readonly object _pendingChatGate = new();
    private readonly Queue<PendingChatMessage> _pendingChatMessages = new();
    private Hook<RaptureLogModule.Delegates.AddMsgSourceEntry>? _chatSourceHook;
    private int _autoActionGeneration = 0;
    private const double ImGuiTextRecoveryWindowSeconds = 5.0;
    private const int ImGuiTextRecoveryStableFrameTarget = 12;
    private const double ImGuiTextMonitorIntervalSeconds = 10.0;
    private DateTime _imguiTextRecoveryStarted = DateTime.MinValue;
    private DateTime _lastImGuiTextMonitorCheck = DateTime.MinValue;
    private int _imguiTextRecoveryStableFrames = 0;
    private bool _imguiTextRecoveryLogged = false;
    private DateTime _lastAutoLog = DateTime.MinValue;
    private GamePhase _lastPhase = GamePhase.Waiting;
    private DateTime _lastChatActivity = DateTime.MinValue;
    private bool _autoContinueWaiting = false;

    public static bool AutoContinueActive => Instance?._autoContinueWaiting ?? false;
    public static double AutoContinueElapsedSeconds => Instance != null && Instance._autoContinueWaiting
        ? (DateTime.Now - Instance._lastChatActivity).TotalSeconds : 0.0;
    public static int DebugDiceSequenceIndex => Volatile.Read(ref _debugDiceSequenceIndex);

    public static void ResetDebugDiceSequence()
    {
        Volatile.Write(ref _debugDiceSequenceIndex, 0);
    }

    public static bool TryGetNextDebugDiceRoll(int sides, out int rolled)
    {
        rolled = 0;
        if (sides <= 0 || string.IsNullOrWhiteSpace(DebugDiceSequence))
            return false;

        var tokens = DebugDiceSequence
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (tokens.Count == 0)
            return false;

        var starIndex = tokens.FindIndex(x => x == "*");
        var prefixTokens = starIndex >= 0 ? tokens.Take(starIndex).ToList() : tokens;
        var index = Interlocked.Increment(ref _debugDiceSequenceIndex) - 1;

        if (starIndex >= 0 && index >= prefixTokens.Count)
        {
            rolled = Random.Shared.Next(1, sides + 1);
            return true;
        }

        var sequenceTokens = starIndex >= 0 ? prefixTokens : tokens;
        if (sequenceTokens.Count == 0)
        {
            rolled = Random.Shared.Next(1, sides + 1);
            return true;
        }

        var token = sequenceTokens[index % sequenceTokens.Count];
        if (token == "?")
        {
            rolled = Random.Shared.Next(1, sides + 1);
            return true;
        }

        if (!int.TryParse(token, out var value) || value < 1 || value > sides)
        {
            rolled = Random.Shared.Next(1, sides + 1);
            return true;
        }

        rolled = value;
        return true;
    }

    public void OpenDebugPopout() => debugLogWindow.IsOpen = true;
    public void OpenDrawLogicDebug() => drawLogicDebugWindow.IsOpen = true;
    public void OpenButtonBar() { buttonBarWindow.RequestRepositioning(); buttonBarWindow.IsOpen = true; }
    public void CloseButtonBar() => buttonBarWindow.IsOpen = false;
    public void OpenChatBox() { chatBoxWindow.IsOpen = true; UpdateEventHooks(); }
    public void OpenChangelog() => updatePopupWindow.IsOpen = true;
    public BlackJackButtlerWindow GetMainWindow() => mainWindow;
    public int CurrentAutoActionGeneration => Volatile.Read(ref _autoActionGeneration);
    public bool IsAutoActionGenerationCurrent(int generation) => generation == CurrentAutoActionGeneration;
    public void RunAutoAction(string context, Func<Task> action, Func<bool>? isStillEnabled = null, string? queueKey = null)
    {
        var generation = CurrentAutoActionGeneration;
        GameActionQueueManager.Enqueue(
            context,
            action,
            queueKey ?? context,
            () => IsAutoActionGenerationCurrent(generation)
                && (isStillEnabled == null || isStillEnabled()));
    }

    public void ResetAutoActionState(bool cancelCurrentGroup)
    {
        Interlocked.Increment(ref _autoActionGeneration);
        _autoContinueWaiting = false;
        GameActionQueueManager.CancelAll(cancelCurrentGroup);
    }
    private string _cachedLocalName = string.Empty;

    public Plugin()
    {
        Instance = this;

        ECommonsMain.Init(PluginInterface, this);

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        if (Configuration.EnsureShortResultRules())
            Configuration.Save();

        if (Configuration.EnsureLayout3Migrations())
            Configuration.Save();

        if (!Configuration.EnableAllianceSupport)
        {
            Configuration.EnableAllianceSupport = true;
            Configuration.Save();
        }

        if (DefaultsMigration.RunMigration(Configuration))
            Configuration.Save();

        if (DefaultsMigration.EnsureGameplayRegexPatterns(Configuration))
            Configuration.Save();

        if (DefaultsMigration.MigrateTellDotToken(Configuration))
            Configuration.Save();

        if (Configuration.EnsurePresetMigrations())
            Configuration.Save();

        if (!Configuration.NotifyGroupsMigrated)
        {
            Configuration.NotifyGroupsMigrated = true;
            DefaultsMigration.MigrateNotifyGroups(Configuration);
            Configuration.Save();
        }

        if (!Configuration.MenuStyleMigrated)
        {
            Configuration.MenuStyleMigrated = true;
            if (Configuration.UseBurgerMenu)
                Configuration.MenuStyle = MenuStyleMode.BurgerMenu;
            Configuration.Save();
        }

        StatsManager.Init(Configuration);
        UserStatisticsManager.Init(PluginInterface.GetPluginConfigDirectory());
        RoundLogManager.Init(PluginInterface.GetPluginConfigDirectory());
        StatsLogManager.Init(PluginInterface.GetPluginConfigDirectory());
        ActivityLogManager.Init(PluginInterface.GetPluginConfigDirectory());
        VipManager.Init(Path.GetDirectoryName(PluginInterface.GetPluginConfigDirectory())!);
        DrawLogicScriptManager.Init(PluginInterface.GetPluginConfigDirectory(), Configuration);
        BlacklistManager.Init(Configuration);

        notepadWindow = new NotepadWindow(Configuration, () => Configuration.Save(), () => mainWindow?.GetWindowRect() ?? (Vector2.Zero, Vector2.Zero));
        windowSystem.AddWindow(notepadWindow);

        mainWindow = new BlackJackButtlerWindow(Configuration, () => Configuration.Save(), chatLog, notepadWindow);

        buttonBarWindow = new CustomButtonBarWindow(Configuration, () => Configuration.Save(),
            () => mainWindow?.GetWindowRect() ?? (Vector2.Zero, Vector2.Zero), mainWindow);
        windowSystem.AddWindow(buttonBarWindow);
        if (Configuration.ButtonBarPopout) buttonBarWindow.IsOpen = true;

        PresetStorage.Initialize(PluginInterface.GetPluginConfigDirectory());
        if (Configuration.PresetsMigrated && PresetStorage.PresetsFileExists())
        {
            var loaded = PresetStorage.Load();
            Configuration.Presets.Clear();
            Configuration.Presets.AddRange(loaded);
        }

        // Popout-Zustand ist nur session-stabil — beim Plugin-Start immer geschlossen
        Configuration.TablePopout  = false;
        Configuration.NearbyPopout = false;

        tablePopoutWindow = new TablePopoutWindow(Configuration, () => Configuration.Save(), mainWindow);
        windowSystem.AddWindow(tablePopoutWindow);

        nearbyPopoutWindow = new NearbyPopoutWindow(Configuration, () => Configuration.Save(), mainWindow);
        windowSystem.AddWindow(nearbyPopoutWindow);

        mainWindow.SetPopoutWindows(tablePopoutWindow, nearbyPopoutWindow);

        debugLogWindow = new DebugLogWindow(mainWindow);
        windowSystem.AddWindow(debugLogWindow);

        chatBoxWindow = new ChatBoxWindow(chatLog, Configuration);
        windowSystem.AddWindow(chatBoxWindow);

        drawLogicDebugWindow = new DrawLogicDebugWindow();
        windowSystem.AddWindow(drawLogicDebugWindow);

        updatePopupWindow = new UpdatePopupWindow(Configuration, () => Configuration.Save());
        windowSystem.AddWindow(updatePopupWindow);

        importantNoticeWindow = new ImportantNoticeWindow(Configuration, () => Configuration.Save());
        windowSystem.AddWindow(importantNoticeWindow);

        blacklistBannerWindow = new BlacklistBannerWindow();
        windowSystem.AddWindow(blacklistBannerWindow);
        blacklistBannerWindow.IsOpen = true;

        variablesPopupWindow = new VariablesPopupWindow(mainWindow);
        windowSystem.AddWindow(variablesPopupWindow);
        mainWindow.SetVariablesPopupWindow(variablesPopupWindow);

        DebugCommandSink = mainWindow.AddDebugLog;
        windowSystem.AddWindow(mainWindow);

        var currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
        if (Configuration.LastSeenVersion != currentVersion && !Configuration.DisableUpdatePopup)
            updatePopupWindow.IsOpen = true;
        Configuration.LastSeenVersion = currentVersion;

        if (!Configuration.ImportantNoticeAcknowledged)
            importantNoticeWindow.IsOpen = true;

        Configuration.Save();

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand) {HelpMessage = "Open BlackJack Buttler. Use '/bjb chat' to open the BJB Messenger."});

        PluginInterface.UiBuilder.Draw += EnsureImGuiTextVisibleOnLoad;
        PluginInterface.UiBuilder.Draw += MonitorImGuiTextVisibility;
        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += mainWindow.OpenMain;
        PluginInterface.UiBuilder.OpenConfigUi += mainWindow.OpenSettings;

        AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "Trade", TradeManager.OnTradeOpened);
        AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "Trade", TradeManager.OnTradeUpdated);
        AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "Trade", TradeManager.OnTradeClosed);

        InitializeChatSourceHook();
        UpdateEventHooks();

        Log.Information("BlackJack Buttler loaded.");
    }

    private void EnsureImGuiTextVisibleOnLoad()
    {
        if (_imguiTextRecoveryStarted == DateTime.MinValue)
            _imguiTextRecoveryStarted = DateTime.Now;

        try
        {
            var elapsed = (DateTime.Now - _imguiTextRecoveryStarted).TotalSeconds;
            var inStartupRaceWindow = elapsed <= 1.0;
            var textIsTransparent = IsImGuiTextTransparent();

            if (inStartupRaceWindow || textIsTransparent)
            {
                ApplyImGuiTextVisibilityFix();
                _imguiTextRecoveryStableFrames = 0;

                if (textIsTransparent && !_imguiTextRecoveryLogged)
                {
                    _imguiTextRecoveryLogged = true;
                    Log.Warning("Recovered transparent ImGui text style during plugin load.");
                }

                return;
            }

            _imguiTextRecoveryStableFrames++;
            if (_imguiTextRecoveryStableFrames >= ImGuiTextRecoveryStableFrameTarget || elapsed >= ImGuiTextRecoveryWindowSeconds)
                PluginInterface.UiBuilder.Draw -= EnsureImGuiTextVisibleOnLoad;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ImGui text recovery during plugin load failed.");
            PluginInterface.UiBuilder.Draw -= EnsureImGuiTextVisibleOnLoad;
        }
    }

    private void MonitorImGuiTextVisibility()
    {
        var now = DateTime.Now;
        if (_lastImGuiTextMonitorCheck != DateTime.MinValue
            && (now - _lastImGuiTextMonitorCheck).TotalSeconds < ImGuiTextMonitorIntervalSeconds)
            return;

        _lastImGuiTextMonitorCheck = now;

        try
        {
            if (!IsImGuiTextTransparent())
                return;

            ApplyImGuiTextVisibilityFix();
            Log.Warning("Recovered transparent ImGui text style during periodic monitor check.");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Periodic ImGui text visibility monitor failed.");
        }
    }

    private static bool IsImGuiTextTransparent()
    {
        var style = ImGui.GetStyle();
        var text = style.Colors[(int)ImGuiCol.Text];
        var disabled = style.Colors[(int)ImGuiCol.TextDisabled];
        return style.Alpha < 0.95f || text.W < 0.95f || disabled.W < 0.35f;
    }

    private static void ApplyImGuiTextVisibilityFix()
    {
        var style = ImGui.GetStyle();
        var selectedBg = style.Colors[(int)ImGuiCol.TextSelectedBg];

        style.Alpha = 1f;
        style.Colors[(int)ImGuiCol.Text] = new Vector4(1f, 1f, 1f, 1f);
        style.Colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.55f, 0.55f, 0.55f, 1f);

        if (selectedBg.W <= 0.01f)
            style.Colors[(int)ImGuiCol.TextSelectedBg] = new Vector4(0.26f, 0.59f, 0.98f, 0.35f);
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (mainWindow == null) return;

        DrainPendingChatMessages();

        if (!mainWindow.IsRecognitionActive)
        {
            if (!mainWindow.IsOpen) return;
            if ((DateTime.Now - _lastIdleTick).TotalSeconds < 2) return;
            _lastIdleTick = DateTime.Now;

            _cachedLocalName = ObjectTable.LocalPlayer?.Name.TextValue ?? string.Empty;
            GameEngine.SetRuntimeContext(mainWindow.GetPlayers(), mainWindow.GetDealer());
            GroupContextManager.Refresh(Configuration);
            return;
        }

        _cachedLocalName = ObjectTable.LocalPlayer?.Name.TextValue ?? string.Empty;
        GameEngine.SetRuntimeContext(mainWindow.GetPlayers(), mainWindow.GetDealer());

        var currentPhase = GameEngine.CurrentPhase;
        if (currentPhase != _lastPhase)
        {
            _lastPhase = currentPhase;
            if (Configuration.LookEveryTime &&
                currentPhase is GamePhase.Waiting or GamePhase.InitialDeal
                             or GamePhase.DealerTurn or GamePhase.Payout)
            {
                ViewDirectionManager.ApplyViewDirectionImmediate(Configuration);
            }
        }

        // Party-Sync (throttled auf 1x/Sekunde)
        if ((DateTime.Now - _lastSync).TotalMilliseconds > 1000)
        {
            mainWindow.SyncPartyPublic();
            _lastSync = DateTime.Now;
        }

        // Auto Initial Deal
        if (Configuration.EnableAutomation && Configuration.AutoInitialDeal
            && GameEngine.CurrentPhase == GamePhase.InitialDeal)
        {
            if (!CommandExecutor.IsRunning && !CommandExecutor.IsFollowUpPending)
            {
                var players = mainWindow.GetPlayers();
                var currentPlayer = players.FirstOrDefault(p => p.IsCurrentTurn);
                if (currentPlayer != null && currentPlayer.IsActivePlayer && !currentPlayer.IsOnHold && !currentPlayer.HasInitialHandDealt)
                {
                    mainWindow.AddDebugLog($"[AutoDeal] Starting deal for {currentPlayer.DisplayName}");
                    RunAutoAction(
                        $"AutoDeal:{currentPlayer.Name}",
                        async () =>
                        {
                            await GameEngine.ActionDealHand(currentPlayer, Configuration, players);
                            mainWindow.AddDebugLog($"[AutoDeal] Deal completed for {currentPlayer.DisplayName}");
                        },
                        () => Configuration.AutoInitialDeal
                            && GameEngine.CurrentPhase == GamePhase.InitialDeal
                            && currentPlayer.IsCurrentTurn
                            && !currentPlayer.HasInitialHandDealt,
                        $"AutoDeal:{currentPlayer.Name}");
                }
                else
                {
                    if ((DateTime.Now - _lastAutoLog).TotalSeconds >= 2)
                    {
                        mainWindow.AddDebugLog($"[AutoDeal] No eligible player found (current={currentPlayer?.DisplayName ?? "null"}, isActive={currentPlayer?.IsActivePlayer}, isOnHold={currentPlayer?.IsOnHold}, hasDealt={currentPlayer?.HasInitialHandDealt})");
                        _lastAutoLog = DateTime.Now;
                    }
                }
            }
            else
            {
                if ((DateTime.Now - _lastAutoLog).TotalSeconds >= 2)
                {
                    mainWindow.AddDebugLog($"[AutoDeal] Blocked: IsRunning={CommandExecutor.IsRunning}, FollowUp={CommandExecutor.IsFollowUpPending}");
                    _lastAutoLog = DateTime.Now;
                }
            }
        }

        // Auto Dealer Draw
        if (Configuration.EnableAutomation && Configuration.AutoDealerDraw
            && GameEngine.CurrentPhase == GamePhase.DealerTurn)
        {
            if (!CommandExecutor.IsRunning && !CommandExecutor.IsFollowUpPending)
            {
                var dealer = mainWindow.GetDealer();
                if (dealer != null && dealer.Hands.Count > 0 && dealer.Hands[0].Cards.Count > 0)
                {
                    var hand = dealer.Hands[0];
                    if (!hand.IsBust && !hand.IsStand)
                    {
                        var (min, max) = dealer.CalculatePoints(0);
                        int score = (max.HasValue && max.Value <= 21) ? max.Value : min;
                        bool isSoft = max.HasValue && max.Value <= 21 && max.Value != min;
                        bool shouldHit = score < Configuration.DealerDrawsUntil
                            || (Configuration.DealerSoftRule && isSoft && score == Configuration.DealerDrawsUntil);
                        if (shouldHit)
                        {
                            mainWindow.AddDebugLog($"[AutoDealer] Hit: score={score} (soft={isSoft}) vs {(Configuration.DealerSoftRule ? "soft" : "hard")} {Configuration.DealerDrawsUntil}");
                            var players = mainWindow.GetPlayers();
                            RunAutoAction(
                                "AutoDealerHit",
                                () => GameEngine.DealerHit(Configuration, players),
                                () => Configuration.AutoDealerDraw && GameEngine.CurrentPhase == GamePhase.DealerTurn,
                                "AutoDealerAction");
                        }
                        else
                        {
                            mainWindow.AddDebugLog($"[AutoDealer] Stand: score={score} (soft={isSoft}) vs {(Configuration.DealerSoftRule ? "soft" : "hard")} {Configuration.DealerDrawsUntil}");
                            var players = mainWindow.GetPlayers();
                            RunAutoAction(
                                "AutoDealerStand",
                                async () =>
                                {
                                    await GameEngine.DealerStand(Configuration, players);
                                    await GameEngine.EvaluateFinalResults(players, dealer, Configuration);
                                    mainWindow.AddDebugLog("[AutoDealer] DealerStand + EvaluateFinalResults completed");
                                },
                                () => Configuration.AutoDealerDraw && GameEngine.CurrentPhase == GamePhase.DealerTurn,
                                "AutoDealerAction");
                        }
                    }
                }
            }
            else
            {
                if ((DateTime.Now - _lastAutoLog).TotalSeconds >= 2)
                {
                    mainWindow.AddDebugLog($"[AutoDealer] Blocked: IsRunning={CommandExecutor.IsRunning}, FollowUp={CommandExecutor.IsFollowUpPending}");
                    _lastAutoLog = DateTime.Now;
                }
            }
        }

        // Payout + Trade + JoinQueue + NearbyAlert
        DebugAutoPlayerManager.Tick(Configuration);
        PayoutManagement.Tick();
        TradeManager.Tick();
        JoinQueueManager.Tick(Configuration);

        if (Configuration.NearbyAlertEnabled && Configuration.ShowNearbyPlayers)
        {
            var nearby = NearbyPlayersManager.GetNearbyPlayers(Configuration);
            NearbyAlertManager.Update(nearby, Configuration);
        }

        if (Configuration.ShowNearbyPlayers)
        {
            var nearby = NearbyPlayersManager.GetNearbyPlayers(Configuration);
            NearbyAutoActManager.Update(nearby, Configuration);
        }

        if (Configuration.EnableAutomation && Configuration.ShowAutoContinueButton
            && Configuration.AutoContinue && mainWindow.IsRecognitionActive)
        {
            if (GameEngine.CanAcceptInterRoundDetectors())
            {
                if (!_autoContinueWaiting)
                {
                    _lastChatActivity = DateTime.Now;
                    _autoContinueWaiting = true;
                }
                else if ((DateTime.Now - _lastChatActivity).TotalSeconds >= Configuration.AutoContinueDelay)
                {
                    _autoContinueWaiting = false;
                    var players = mainWindow.GetPlayers();
                    var activePlayers = players.Where(p => p.IsActivePlayer && !p.IsOnHold).ToList();
                    if (activePlayers.Count == 0)
                        return;

                    var underfunded = activePlayers.Where(GameEngine.IsPlayerUnableToCoverBet).ToList();
                    if (underfunded.Count > 0)
                    {
                        mainWindow.SetHighlightNewRound();
                        InsufficientBetQueueManager.EnqueueMany(underfunded, Configuration, "AutoContinue");
                        mainWindow.AddDebugLog("[AutoContinue] Auto-start blocked: at least one active player cannot cover their bet.");
                        return;
                    }

                    RunAutoAction(
                        "AutoContinue",
                        () => GameEngine.StartInitialDeal(players, Configuration),
                        () => Configuration.AutoContinue && GameEngine.CanAcceptInterRoundDetectors(),
                        "RoundStart");
                }
            }
            else
            {
                _autoContinueWaiting = false;
            }
        }
        else
        {
            _autoContinueWaiting = false;
        }
    }

    public void UpdateEventHooks()
    {
        bool needChat = mainWindow.IsRecognitionActive || (chatBoxWindow != null && chatBoxWindow.IsOpen);
        bool needFramework = mainWindow.IsOpen || mainWindow.IsRecognitionActive || needChat;

        if (needFramework && !_frameworkHooked)
        {
            Framework.Update += OnFrameworkUpdate;
            _frameworkHooked = true;
        }
        else if (!needFramework && _frameworkHooked)
        {
            Framework.Update -= OnFrameworkUpdate;
            _frameworkHooked = false;
        }

        if (needChat && !_chatHooked)
        {
            ChatGui.ChatMessage += OnChatMessage;
            _chatHooked = true;
        }
        else if (!needChat && _chatHooked)
        {
            ChatGui.ChatMessage -= OnChatMessage;
            _chatHooked = false;
        }
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= EnsureImGuiTextVisibleOnLoad;
        PluginInterface.UiBuilder.Draw -= MonitorImGuiTextVisibility;
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= mainWindow.OpenMain;
        PluginInterface.UiBuilder.OpenConfigUi -= mainWindow.OpenSettings;

        AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "Trade", TradeManager.OnTradeOpened);
        AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "Trade", TradeManager.OnTradeUpdated);
        AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, "Trade", TradeManager.OnTradeClosed);

        if (_chatHooked) ChatGui.ChatMessage -= OnChatMessage;
        if (_frameworkHooked) Framework.Update -= OnFrameworkUpdate;
        _chatSourceHook?.Dispose();

        NearbyAlertManager.Dispose();
        DrawLogicScriptManager.Dispose();

        CommandManager.RemoveHandler(CommandName);

        windowSystem.RemoveAllWindows();
        mainWindow.Dispose();
        DebugCommandSink = null;
    }

    private void OnCommand(string command, string args)
    {
        var trimmed = (args ?? string.Empty).Trim();
        if (string.Equals(trimmed, "chat", StringComparison.OrdinalIgnoreCase))
        {
            OpenChatBox();
            return;
        }
        mainWindow.OpenMain();
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        var pending = new PendingChatMessage(
            (int)message.LogKind,
            message.Sender,
            message.Message);

        lock (_pendingChatGate)
            _pendingChatMessages.Enqueue(pending);
    }

    private unsafe void InitializeChatSourceHook()
    {
        try
        {
            _chatSourceHook = GameInteropProvider.HookFromAddress<RaptureLogModule.Delegates.AddMsgSourceEntry>(
                RaptureLogModule.MemberFunctionPointers.AddMsgSourceEntry,
                OnAddChatSourceEntry);
            _chatSourceHook.Enable();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ChatIdentity] Failed to initialize native chat source hook; using name fallbacks.");
        }
    }

    private unsafe void OnAddChatSourceEntry(
        RaptureLogModule* module,
        ulong contentId,
        ulong accountId,
        int messageIndex,
        ushort worldId,
        ushort chatType)
    {
        _chatSourceHook!.Original(module, contentId, accountId, messageIndex, worldId, chatType);

        lock (_pendingChatGate)
        {
            if (_pendingChatMessages.Count == 0)
                return;

            var pending = _pendingChatMessages.Last();
            pending.SourceContentId = contentId;
            pending.SourceWorldId = worldId;
            pending.NativeChatType = chatType;
        }
    }

    private void DrainPendingChatMessages()
    {
        List<PendingChatMessage> pending;
        lock (_pendingChatGate)
        {
            if (_pendingChatMessages.Count == 0)
                return;

            pending = _pendingChatMessages.ToList();
            _pendingChatMessages.Clear();
        }

        foreach (var message in pending)
            ProcessChatMessage(message);
    }

    private void ProcessChatMessage(PendingChatMessage pending)
    {
        var pp = pending.Sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
        var playerName = pp?.PlayerName ?? string.Empty;
        var worldId = pending.SourceWorldId != 0 ? pending.SourceWorldId : pp?.World.RowId ?? 0u;

        if (pending.SourceContentId != 0)
        {
            var member = GroupContextManager.GetCurrentMembers(Configuration)
                .FirstOrDefault(x => x.ContentId == pending.SourceContentId);
            if (member != null)
            {
                playerName = member.Name;
                worldId = member.WorldId;
            }
            else if (pending.SourceContentId == PlayerState.ContentId)
            {
                playerName = PlayerState.CharacterName
                    ?? ObjectTable.LocalPlayer?.Name.TextValue
                    ?? playerName;
                worldId = PlayerState.HomeWorld.RowId;
            }
        }

        InjectChatMessage(
            pending.Type,
            worldId,
            playerName,
            pending.Sender.TextValue ?? string.Empty,
            pending.Message.TextValue ?? string.Empty,
            pending.Sender,
            pending.Message,
            pending.SourceContentId);
    }

    public uint GetLogNameType()
        => GameConfig.TryGet(UiConfigOption.LogNameType, out uint value) ? value : 0u;

    public void InjectChatMessage(
        int type,
        uint worldId,
        string playerName,
        string senderText,
        string messageText,
        SeString? rawSender = null,
        SeString? rawMessage = null,
        ulong sourceContentId = 0)
    {
        string logName = !string.IsNullOrEmpty(playerName) ? playerName : senderText;
        string logLine = string.IsNullOrEmpty(logName) ? messageText : $"{logName}: {messageText}";

        mainWindow.AddDebugLog($"[{DateTime.Now:T}] {logLine}", true);

        var localName = !string.IsNullOrWhiteSpace(_cachedLocalName)
            ? _cachedLocalName
            : PlayerState.CharacterName
                ?? ObjectTable.LocalPlayer?.Name.TextValue
                ?? string.Empty;
        var s = rawSender ?? new SeString(new TextPayload(senderText));
        var m = rawMessage ?? new SeString(new TextPayload(messageText));

        var parsed = ChatMessageParser.Parse(
            DateTime.Now,
            s,
            m,
            localName,
            PlayerState.HomeWorld.RowId,
            PlayerState.ContentId,
            sourceContentId,
            worldId,
            playerName,
            GetLogNameType(),
            type);

        if (ChatLogBuffer.IsSystemChatType(type))
            GroupContextManager.ObserveSystemMessage(parsed.Message);

        if (ChatLogBuffer.IsSupportedGroupChatType(type, Configuration) || parsed.IsDice)
            _lastChatActivity = DateTime.Now;

        if (parsed.IsDice)
        {
            mainWindow.AddDebugLog(
                $"[DiceDetection] Type={type}, Sender='{parsed.Name}', " +
                $"CID={parsed.SourceContentId}, Identity={parsed.IdentitySource}, " +
                $"OwnRoll={parsed.Event}, Value={parsed.DiceValue?.ToString() ?? "(none)"}");
            StatsLogManager.AppendDiceResult(parsed.Message);
        }

        chatLog.Add(parsed);
        RegexEngine.ProcessIncoming(parsed, Configuration, mainWindow.GetPlayers(), mainWindow.GetDealer());
    }

    private sealed class PendingChatMessage
    {
        public PendingChatMessage(int type, SeString sender, SeString message)
        {
            Type = type;
            Sender = sender;
            Message = message;
        }

        public int Type { get; }
        public SeString Sender { get; }
        public SeString Message { get; }
        public ulong SourceContentId { get; set; }
        public uint SourceWorldId { get; set; }
        public ushort NativeChatType { get; set; }
    }

    private static string DumpPayloads(SeString s)
    {
        var sb = new StringBuilder(1024);

        for (var i = 0; i < s.Payloads.Count; i++)
        {
            var p = s.Payloads[i];
            sb.Append(i).Append(": ").Append(p.GetType().Name);

            if (p is TextPayload tp)
                sb.Append(" -> \"").Append(tp.Text).Append('"');
            else if (p is PlayerPayload pp)
                sb.Append($" -> PlayerName=\"{pp.PlayerName}\", WorldId={pp.World}");
            else
                sb.Append(" -> ").Append(p.ToString());

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string ToHex(byte[] bytes)
    {
        if (bytes.Length == 0)
        return string.Empty;

        var sb = new StringBuilder(bytes.Length * 3);
        for (var i = 0; i < bytes.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(bytes[i].ToString("X2"));
        }
        return sb.ToString();
    }

}
