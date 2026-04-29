using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Game.Chat;
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
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace BlackJackButtler;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    internal static Plugin Instance { get; private set; } = null!;

    internal static Action<string>? DebugCommandSink { get; private set; }

    private const string CommandName = "/bjb";

    public Configuration Configuration { get; }

    public static bool IsDebugMode = false;
    public static bool IsSpeedMode = false;

    private readonly WindowSystem windowSystem = new("BlackJackButtler");
    private readonly BlackJackButtlerWindow mainWindow;
    private readonly ChatLogBuffer chatLog = new(20);
    private readonly DebugLogWindow debugLogWindow;
    private readonly DrawLogicDebugWindow drawLogicDebugWindow;
    private readonly NotepadWindow notepadWindow;
    private readonly CustomButtonBarWindow buttonBarWindow;
    private readonly UpdatePopupWindow updatePopupWindow;
    private readonly ImportantNoticeWindow importantNoticeWindow;
    private readonly BlacklistBannerWindow blacklistBannerWindow;
    private DateTime _lastSync = DateTime.MinValue;
    private DateTime _lastIdleTick = DateTime.MinValue;
    private volatile bool _autoActionInFlight = false;
    private bool _frameworkHooked = false;
    private bool _chatHooked = false;
    private DateTime _lastAutoLog = DateTime.MinValue;
    private GamePhase _lastPhase = GamePhase.Waiting;
    private DateTime _lastChatActivity = DateTime.MinValue;
    private bool _autoContinueWaiting = false;

    public static bool AutoContinueActive => Instance?._autoContinueWaiting ?? false;
    public static double AutoContinueElapsedSeconds => Instance != null && Instance._autoContinueWaiting
        ? (DateTime.Now - Instance._lastChatActivity).TotalSeconds : 0.0;

    public void OpenDebugPopout() => debugLogWindow.IsOpen = true;
    public void OpenDrawLogicDebug() => drawLogicDebugWindow.IsOpen = true;
    public void OpenButtonBar() { buttonBarWindow.RequestRepositioning(); buttonBarWindow.IsOpen = true; }
    public void CloseButtonBar() => buttonBarWindow.IsOpen = false;
    public BlackJackButtlerWindow GetMainWindow() => mainWindow;
    private string _cachedLocalName = string.Empty;

    public Plugin()
    {
        Instance = this;

        ECommonsMain.Init(PluginInterface, this);

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        if (DefaultsMigration.RunMigration(Configuration))
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

        StatsManager.Init(Configuration);
        RoundLogManager.Init(PluginInterface.GetPluginConfigDirectory());
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

        debugLogWindow = new DebugLogWindow(mainWindow);
        windowSystem.AddWindow(debugLogWindow);

        drawLogicDebugWindow = new DrawLogicDebugWindow();
        windowSystem.AddWindow(drawLogicDebugWindow);

        updatePopupWindow = new UpdatePopupWindow(Configuration, () => Configuration.Save());
        windowSystem.AddWindow(updatePopupWindow);

        importantNoticeWindow = new ImportantNoticeWindow(Configuration, () => Configuration.Save());
        windowSystem.AddWindow(importantNoticeWindow);

        blacklistBannerWindow = new BlacklistBannerWindow();
        windowSystem.AddWindow(blacklistBannerWindow);
        blacklistBannerWindow.IsOpen = true;

        DebugCommandSink = mainWindow.AddDebugLog;
        windowSystem.AddWindow(mainWindow);

        var currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
        if (Configuration.LastSeenVersion != currentVersion && !Configuration.DisableUpdatePopup)
            updatePopupWindow.IsOpen = true;
        Configuration.LastSeenVersion = currentVersion;

        if (!Configuration.ImportantNoticeAcknowledged)
            importantNoticeWindow.IsOpen = true;

        Configuration.Save();

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand) {HelpMessage = "Open BlackJack Buttler."});

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += mainWindow.OpenMain;
        PluginInterface.UiBuilder.OpenConfigUi += mainWindow.OpenSettings;

        AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "Trade", TradeManager.OnTradeOpened);
        AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "Trade", TradeManager.OnTradeUpdated);
        AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "Trade", TradeManager.OnTradeClosed);

        UpdateEventHooks();

        Log.Information("BlackJack Buttler loaded.");
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (mainWindow == null) return;

        if (!mainWindow.IsRecognitionActive)
        {
            if (!mainWindow.IsOpen) return;
            if ((DateTime.Now - _lastIdleTick).TotalSeconds < 2) return;
            _lastIdleTick = DateTime.Now;

            _cachedLocalName = ObjectTable.LocalPlayer?.Name.TextValue ?? string.Empty;
            GameEngine.SetRuntimeContext(mainWindow.GetPlayers(), mainWindow.GetDealer());
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
        if (Configuration.AutoInitialDeal && GameEngine.CurrentPhase == GamePhase.InitialDeal)
        {
            if (!CommandExecutor.IsRunning && !CommandExecutor.IsFollowUpPending && !_autoActionInFlight)
            {
                var players = mainWindow.GetPlayers();
                var currentPlayer = players.FirstOrDefault(p => p.IsCurrentTurn);
                if (currentPlayer != null && currentPlayer.IsActivePlayer && !currentPlayer.IsOnHold && !currentPlayer.HasInitialHandDealt)
                {
                    mainWindow.AddDebugLog($"[AutoDeal] Starting deal for {currentPlayer.DisplayName}");
                    _autoActionInFlight = true;
                    Task.Run(async () => {
                        try
                        {
                            await GameEngine.ActionDealHand(currentPlayer, Configuration, players);
                            mainWindow.AddDebugLog($"[AutoDeal] Deal completed for {currentPlayer.DisplayName}");
                        }
                        finally { _autoActionInFlight = false; }
                    });
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
                    mainWindow.AddDebugLog($"[AutoDeal] Blocked: IsRunning={CommandExecutor.IsRunning}, FollowUp={CommandExecutor.IsFollowUpPending}, InFlight={_autoActionInFlight}");
                    _lastAutoLog = DateTime.Now;
                }
            }
        }

        // Auto Dealer Draw
        if (Configuration.AutoDealerDraw && GameEngine.CurrentPhase == GamePhase.DealerTurn)
        {
            if (!CommandExecutor.IsRunning && !CommandExecutor.IsFollowUpPending && !_autoActionInFlight)
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
                            _autoActionInFlight = true;
                            var players = mainWindow.GetPlayers();
                            Task.Run(async () => {
                                try { await GameEngine.DealerHit(Configuration, players); }
                                finally { _autoActionInFlight = false; }
                            });
                        }
                        else
                        {
                            mainWindow.AddDebugLog($"[AutoDealer] Stand: score={score} (soft={isSoft}) vs {(Configuration.DealerSoftRule ? "soft" : "hard")} {Configuration.DealerDrawsUntil}");
                            _autoActionInFlight = true;
                            var players = mainWindow.GetPlayers();
                            Task.Run(async () => {
                                try {
                                    await GameEngine.DealerStand(Configuration, players);
                                    await GameEngine.EvaluateFinalResults(players, dealer, Configuration);
                                    mainWindow.AddDebugLog("[AutoDealer] DealerStand + EvaluateFinalResults completed");
                                }
                                finally { _autoActionInFlight = false; }
                            });
                        }
                    }
                }
            }
            else
            {
                if ((DateTime.Now - _lastAutoLog).TotalSeconds >= 2)
                {
                    mainWindow.AddDebugLog($"[AutoDealer] Blocked: IsRunning={CommandExecutor.IsRunning}, FollowUp={CommandExecutor.IsFollowUpPending}, InFlight={_autoActionInFlight}");
                    _lastAutoLog = DateTime.Now;
                }
            }
        }

        // Dropbox + Trade + JoinQueue + NearbyAlert
        DropboxIntegration.Update();
        TradeManager.Tick();
        JoinQueueManager.Tick(Configuration);

        if (Configuration.NearbyAlertEnabled && Configuration.ShowNearbyPlayers)
        {
            var nearby = NearbyPlayersManager.GetNearbyPlayers(Configuration);
            NearbyAlertManager.Update(nearby, Configuration);
        }

        if (Configuration.AutoContinue && mainWindow.IsRecognitionActive)
        {
            var phase = GameEngine.CurrentPhase;
            if ((phase == GamePhase.Waiting || phase == GamePhase.Payout)
                && !CommandExecutor.IsRunning && !CommandExecutor.IsFollowUpPending && !_autoActionInFlight)
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
                    if (activePlayers.Count >= 2 || !Configuration.AutostartRoundOnlyOnMultiplePlayers)
                    {
                        _autoActionInFlight = true;
                        Task.Run(async () => {
                            try { await Task.Run(() => GameEngine.StartInitialDeal(players, Configuration)); }
                            finally { _autoActionInFlight = false; }
                        });
                    }
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
        bool needFramework = mainWindow.IsOpen || mainWindow.IsRecognitionActive;
        bool needChat = mainWindow.IsRecognitionActive;

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
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= mainWindow.OpenMain;
        PluginInterface.UiBuilder.OpenConfigUi -= mainWindow.OpenSettings;

        AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "Trade", TradeManager.OnTradeOpened);
        AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "Trade", TradeManager.OnTradeUpdated);
        AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, "Trade", TradeManager.OnTradeClosed);

        if (_chatHooked) ChatGui.ChatMessage -= OnChatMessage;
        if (_frameworkHooked) Framework.Update -= OnFrameworkUpdate;

        NearbyAlertManager.Dispose();
        DrawLogicScriptManager.Dispose();

        CommandManager.RemoveHandler(CommandName);

        windowSystem.RemoveAllWindows();
        mainWindow.Dispose();
        DebugCommandSink = null;
    }

    private void OnCommand(string command, string args)
    {
        mainWindow.OpenMain();
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        var senderText = message.Sender.TextValue ?? string.Empty;
        var messageText = message.Message.TextValue ?? string.Empty;
        var pp = message.Sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
        var playerName = pp?.PlayerName ?? string.Empty;
        var worldId = pp?.World.RowId ?? 0u;

        InjectChatMessage((int)message.LogKind, worldId, playerName, senderText, messageText, message.Sender, message.Message);
    }

    public void InjectChatMessage(int type, uint worldId, string playerName, string senderText, string messageText, SeString? rawSender = null, SeString? rawMessage = null)
    {
        _lastChatActivity = DateTime.Now;

        string logName = !string.IsNullOrEmpty(playerName) ? playerName : senderText;
        string logLine = string.IsNullOrEmpty(logName) ? messageText : $"{logName}: {messageText}";

        mainWindow.AddDebugLog($"[{DateTime.Now:T}] {logLine}", true);

        var localName = _cachedLocalName;
        var s = rawSender ?? new SeString(new TextPayload(senderText));
        var m = rawMessage ?? new SeString(new TextPayload(messageText));

        var parsed = ChatMessageParser.Parse(DateTime.Now, s, m, localName);

        chatLog.Add(parsed);
        RegexEngine.ProcessIncoming(parsed, Configuration, mainWindow.GetPlayers(), mainWindow.GetDealer());
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
