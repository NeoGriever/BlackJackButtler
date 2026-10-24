BLACKJACK BUTTLER
SOURCE FILE LOCATION REFERENCE

All paths are relative to the repository root.
Directory names identify responsibility; listed file names identify implementation.

FEATURE LOOKUP

  Plugin entry point
    BlackJackButtler/Plugin.cs

  Configuration and defaults
    BlackJackButtler/config.cs
    BlackJackButtler/system/configuration
    BlackJackButtler/system/configuration/defaults

  Command chains and tokens
    BlackJackButtler/system/commands
    BlackJackButtler/classes/commands

  Card deck and game state
    BlackJackButtler/classes/game
    BlackJackButtler/system/game

  Turn order and dealing
    BlackJackButtler/system/game/engine/GameEngine.TurnFlow.cs
    BlackJackButtler/system/game/engine/GameEngine.HandDealing.cs
    BlackJackButtler/system/game/engine/GameEngine.InitialDeal.cs

  Split and double/triple down
    BlackJackButtler/system/game/engine/GameEngine.Split.cs
    BlackJackButtler/system/game/engine/GameEngine.DoubleDown.cs
    BlackJackButtler/system/game/engine/GameEngine.TripleDown.cs

  Dealer actions and settlement
    BlackJackButtler/system/game/engine/GameEngine.DealerActions.cs
    BlackJackButtler/system/game/engine/GameEngine.FinalResults.cs
    BlackJackButtler/system/game/engine/GameEngine.PayoutRules.cs

  Banking and payout execution
    BlackJackButtler/system/banking
    BlackJackButtler/system/payments/payout

  Trade adapter and state machine
    BlackJackButtler/ModularTrade/BjbDalamudTradeAdapter.cs
    ModularTrade

  Simulated players and dice
    BlackJackButtler/system/debug/manager.debugautoplayers.cs
    BlackJackButtler/system/commands/command.executor.dicehandler.cs

  Action queue
    BlackJackButtler/system/queues/manager.gameactionqueue.cs

  Party and group detection
    BlackJackButtler/system/party
    BlackJackButtler/window/main/groups

  Chat, variables and regex
    BlackJackButtler/system/chat
    BlackJackButtler/system/variables
    BlackJackButtler/system/regex

  Nearby player automation
    BlackJackButtler/system/nearby
    BlackJackButtler/window/main/nearby
    BlackJackButtler/window/settings/nearby

  Sound effects
    BlackJackButtler/system/audio
    BlackJackButtler/window/settings/sounds

  Drawing scripts and overlays
    BlackJackButtler/system/drawlogic
    BlackJackButtler/window/pages/drawlogic

  Round logs and statistics
    BlackJackButtler/system/rounds
    BlackJackButtler/system/statistics
    BlackJackButtler/window/pages/statistics

  Session backup and recovery
    BlackJackButtler/system/session
    BlackJackButtler/window/core/SessionRestore.cs

  Main window and navigation
    BlackJackButtler/window/core
    BlackJackButtler/window/main
    BlackJackButtler/window/navigation

  Settings
    BlackJackButtler/window/settings

  Themes
    BlackJackButtler/classes/themes
    BlackJackButtler/system/themes
    BlackJackButtler/window/settings/themes
    BlackJackButtler/themes/base.json

  Dialog windows
    BlackJackButtler/window/main/popups
    BlackJackButtler/window/popouts

  Debug visual canvas
    BlackJackButtler/window/rendering/DebugVisualCanvas.cs
    BlackJackButtler/visual-canvas.js
    BlackJackButtler/artstyle-layout.json

  Blacklist
    BlackJackButtler/system/security/manager.blacklist.cs
    blacklist.json

  Plugin distribution, stable and testing
    pluginmaster.json
    plugins/BlackJackButtler/latest.zip
    plugins/BlackJackButtler/testing/latest.zip
    plugins/BlackJackButtler/icon.png

PROJECT FILES

  .editorconfig
  .gitignore
  .github/workflows/pr-build.yml
  BlackJackButtler.sln
  NuGet.config
  README.md
  LICENSE
  changelog.md
  baseconfig.json
  blacklist.json
  pluginmaster.json
  DrawCodeDocs.de.md
  DrawCodeDocs.en.md
  ModularTrade/README.md
  filestruct.md
  BlackJackButtler/BlackJackButtler.csproj
  BlackJackButtler/BlackJackButtler.json
  ModularTrade/ModularTrade.csproj

SOURCE DIRECTORY INDEX

  BlackJackButtler/classes/commands/
    Command definitions and custom button models
    command.models.cs

  BlackJackButtler/classes/configuration/
    Default configuration snapshots
    DefaultsSnapshot.cs

  BlackJackButtler/classes/game/
    Cards, hands, players and game phases
    game.enums.cs
    states.hand.cs
    states.player.cs

  BlackJackButtler/classes/regex/
    Regex action and rule models
    regex.models.cs

  BlackJackButtler/ModularTrade/
    Dalamud implementation of the trade adapter
    BjbDalamudTradeAdapter.cs

  BlackJackButtler/system/audio/
    Per-player sound preferences and effects
    player.sound.effects.cs

  BlackJackButtler/system/banking/
    Imaginary-player bank transfers
    manager.imaginaryplayerbank.cs

  BlackJackButtler/system/cache/
    File-backed caching
    cache.file.cs

  BlackJackButtler/system/chat/formatting/
    Short result formatting
    short.result.formatter.cs

  BlackJackButtler/system/chat/logging/
    Bounded chat logs and buffers
    bounded.log.buffer.cs
    chat.log.buffers.cs

  BlackJackButtler/system/chat/parsing/
    Incoming chat parsing
    chat.parser.cs

  BlackJackButtler/system/chat/routing/
    Outgoing chat command routing
    chat.command.router.cs

  BlackJackButtler/system/commands/
    Command execution, dice handling and token resolution
    CommandExecutor.CommandParsing.cs
    CommandExecutor.Dice.cs
    CommandExecutor.ExecuteGroup.cs
    CommandExecutor.ExecuteInternalGroup.cs
    CommandExecutor.State.cs
    CommandExecutor.Tokens.cs
    command.executor.dicehandler.cs

  BlackJackButtler/system/configuration/defaults/
    Default configuration migration and seeding
    DefaultsMigration.CommandTokens.cs
    DefaultsMigration.Merge.cs
    DefaultsMigration.Models.cs
    DefaultsMigration.NotifyGroups.cs
    DefaultsMigration.Regex.cs
    DefaultsMigration.Run.cs
    DefaultsMigration.Seeding.cs
    DefaultsMigration.Snapshots.cs

  BlackJackButtler/system/configuration/
    Default configuration and preset management
    manager.defaults.cs
    manager.presets.cs

  BlackJackButtler/system/debug/
    Simulated players and test automation
    manager.debugautoplayers.cs

  BlackJackButtler/system/drawlogic/
    Drawing scripts, interpretation and world drawing
    DrawLogicInterpreter.cs
    WorldDrawing.cs
    manager.drawlogicdebug.cs
    manager.drawlogicscript.cs

  BlackJackButtler/system/game/engine/
    Game rules, phases, turns, dealing and payouts
    GameEngine.ActionRules.cs
    GameEngine.BasicActions.cs
    GameEngine.Bench.cs
    GameEngine.BetValidation.cs
    GameEngine.CardRouting.cs
    GameEngine.DealEligibility.cs
    GameEngine.DealerActions.cs
    GameEngine.DoubleDown.cs
    GameEngine.FinalResults.cs
    GameEngine.HandDealing.cs
    GameEngine.InitialDeal.cs
    GameEngine.PayoutRules.cs
    GameEngine.PostCardEvents.cs
    GameEngine.SessionSync.cs
    GameEngine.Split.cs
    GameEngine.State.cs
    GameEngine.Targeting.cs
    GameEngine.TripleDown.cs
    GameEngine.TurnFlow.cs

  BlackJackButtler/system/game/
    Deck operations and game logging
    game.log.cs
    manager.deck.cs

  BlackJackButtler/system/logging/
    Activity logs
    manager.activitylog.cs

  BlackJackButtler/system/nearby/
    Nearby players, alerts and automatic actions
    manager.nearby.cs
    manager.nearbyalert.cs
    manager.nearbyautoact.cs
    manager.nearbynumbers.cs
    manager.viewdirection.cs

  BlackJackButtler/system/party/
    Party and alliance detection and companion synchronization
    manager.companionsync.cs
    manager.groupcontext.cs

  BlackJackButtler/system/payments/payout/
    Payment routing and payout state machine
    PayoutManagement.AmountParsing.cs
    PayoutManagement.Cancellation.cs
    PayoutManagement.Dropbox.cs
    PayoutManagement.Lifecycle.cs
    PayoutManagement.Logging.cs
    PayoutManagement.ModularTrade.cs
    PayoutManagement.Players.cs
    PayoutManagement.Queue.cs
    PayoutManagement.Requests.cs
    PayoutManagement.State.cs
    PayoutManagement.StateMachine.cs
    PayoutManagement.Targeting.cs
    PayoutManagement.Transitions.cs

  BlackJackButtler/system/players/
    Player identity and dice preferences
    manager.playeridentity.cs
    manager.playerrollpreferences.cs

  BlackJackButtler/system/queues/
    Action, join, bet and bank queues
    manager.banktellqueue.cs
    manager.gameactionqueue.cs
    manager.insufficientbetqueue.cs
    manager.joinqueue.cs
    manager.setbetqueue.cs

  BlackJackButtler/system/regex/
    Regex execution and automation
    regex.engine.cs

  BlackJackButtler/system/rounds/
    Round history and rollback
    manager.roundlog.cs
    manager.roundrollback.cs

  BlackJackButtler/system/security/
    Blacklist checks
    manager.blacklist.cs

  BlackJackButtler/system/session/
    Session persistence
    manager.backup.cs

  BlackJackButtler/system/statistics/
    Statistics and history reporting
    manager.stats.cs
    manager.statshash.cs
    manager.statslog.cs
    manager.userstats.cs

  BlackJackButtler/system/trading/
    Trade detection and coordination
    manager.trade.cs

  BlackJackButtler/system/variables/
    Runtime variable resolution
    manager.vars.cs

  BlackJackButtler/system/venue/
    Venue and VIP management
    manager.vip.cs

  BlackJackButtler/system/world/
    World name lookup
    manager.worldnames.cs

  BlackJackButtler/window/components/
    Reusable UI controls and widgets
    BJBGui.cs
    BJBOnOffSwitch.cs
    BJBStepInput.cs
    PieAmountMenu.cs

  BlackJackButtler/window/core/
    Main window state, lifecycle and rendering
    BlackJackButtlerWindow.cs
    SessionRestore.cs
    WindowLifecycle.cs
    WindowRendering.cs
    WindowState.cs

  BlackJackButtler/window/main/
    Main page and shared main-page state
    AutoContinueBar.cs
    MainPage.cs
    MainState.cs

  BlackJackButtler/window/main/betting/
    Bet ring UI
    MoneyRing.cs

  BlackJackButtler/window/main/buttonbar/
    Custom button bar and editor
    CustomButtonBar.cs
    CustomButtonBarWindow.cs
    CustomButtons.cs

  BlackJackButtler/window/main/dealer/
    Dealer area and controls
    DealerControls.cs
    DealerPanel.cs
    DealerRow.cs

  BlackJackButtler/window/main/debug/
    Debug player controls and visual state
    DebugPlayers.cs
    DebugVisual.cs

  BlackJackButtler/window/main/groups/
    Group detection and synchronization views
    GroupLeader.cs
    GroupStatsPrompt.cs
    GroupSync.cs
    GroupValidation.cs

  BlackJackButtler/window/main/header/
    Header and toolbar controls
    Header.cs
    HeaderControls.cs

  BlackJackButtler/window/main/nearby/
    Nearby player views and overlay
    NearbyCommands.cs
    NearbyIgnoreList.cs
    NearbyPlayers.cs
    NearbyPopoutWindow.cs
    NearbyRangeOverlay.cs
    NearbySettings.cs

  BlackJackButtler/window/main/panic/
    Emergency actions
    PanicExecutor.cs

  BlackJackButtler/window/main/players/
    Player rows, actions and hands
    PaymentReminder.cs
    PlayerActionButton.cs
    PlayerControls.cs
    PlayerEmergencyControls.cs
    PlayerHands.cs
    PlayerLifecycle.cs
    PlayerPanel.cs
    PlayerRenderHelpers.cs
    PlayerRow.cs
    PlayerTableLayout.cs

  BlackJackButtler/window/main/popouts/
    Main-page table popout
    TablePopoutWindow.cs

  BlackJackButtler/window/main/popups/
    Main-page dialogs
    AliasPopup.cs
    CleanDataPopup.cs
    DoubleDownMoneyPopup.cs
    HandEditPopup.cs
    PanicPopups.cs
    SharedPopups.cs
    SplitMoneyPopup.cs

  BlackJackButtler/window/navigation/
    Sidebar, burger and tab navigation
    BurgerNavigation.cs
    SidebarNavigation.cs
    TopTabsNavigation.cs

  BlackJackButtler/window/pages/commands/
    Command configuration page
    CommandsPage.cs

  BlackJackButtler/window/pages/custombuttons/
    Custom button configuration page
    CustomButtonsPage.cs

  BlackJackButtler/window/pages/debug/
    Debug page and log window
    DebugLogWindow.cs
    DebugPage.cs

  BlackJackButtler/window/pages/drawlogic/
    Drawing scripts and documentation views
    DrawLogicDebugWindow.cs
    DrawLogicDocumentation.cs
    DrawLogicPage.cs

  BlackJackButtler/window/pages/log/
    Round history page
    RoundLogPage.cs

  BlackJackButtler/window/pages/messages/
    Message configuration page
    MessagesPage.cs

  BlackJackButtler/window/pages/presets/
    Preset management page
    PresetsPage.cs

  BlackJackButtler/window/pages/regex/
    Regex configuration page
    RegexPage.cs

  BlackJackButtler/window/pages/statistics/
    Statistics page
    StatisticsPage.cs

  BlackJackButtler/window/pages/thanks/
    Credits page
    ThanksPage.cs

  BlackJackButtler/window/pages/variables/
    Variables and reference views
    VariablesPage.cs
    VariablesPopupWindow.cs

  BlackJackButtler/window/popouts/
    Independent windows and helpers
    BlacklistBannerWindow.cs
    ChatBoxWindow.cs
    ImportantNoticeWindow.cs
    NotepadWindow.cs
    PayoutManagement.HelperWindow.cs
    UpdatePopupWindow.cs
    VariableReferencePanel.cs

  BlackJackButtler/window/rendering/
    Debug visual canvas renderer
    DebugVisualCanvas.cs

  BlackJackButtler/window/settings/automation/
    Automation settings
    AutomationSettings.cs

  BlackJackButtler/window/settings/betting/
    Betting and bet-ring settings
    BetDraft.cs
    BettingSettings.cs
    MoneyRingSettings.cs

  BlackJackButtler/window/settings/controls/
    Common settings controls and layout
    CommandSelector.cs
    LayoutFields.cs
    SettingsInputs.cs
    SettingsLayout.cs

  BlackJackButtler/window/settings/debug/
    Debug settings
    SettingsDebug.cs

  BlackJackButtler/window/settings/import/
    Configuration import and file dialogs
    ConfigFileDialogs.cs
    ConfigImport.cs

  BlackJackButtler/window/settings/navigation/
    Settings navigation and page
    SettingsPage.cs
    SettingsTabs.cs

  BlackJackButtler/window/settings/nearby/
    Nearby alerts and sounds settings
    NearbySettings.cs
    NearbySounds.cs

  BlackJackButtler/window/settings/presets/
    Default presets
    DefaultPresets.cs

  BlackJackButtler/window/settings/rules/
    Gameplay and result rules
    GameRulesSettings.cs
    ShortResultRulesEditor.cs
    TripleDownStateSupport.cs

  BlackJackButtler/window/settings/sounds/
    Player sound settings
    PlayerSoundSettings.cs

  BlackJackButtler/window/settings/state/
    Settings editor state
    SettingsEditorState.cs
    SettingsViewStateAndTimeZoneData.cs

  BlackJackButtler/window/settings/system/
    System settings
    SystemSettings.cs

  BlackJackButtler/window/settings/time/
    Time and delay settings
    TimeDelaySettings.cs
    TimeZoneEditor.cs

  BlackJackButtler/window/settings/visual/
    Visual settings
    VisualSettings.cs

  ModularTrade/
    Independent Gil transfer library and adapter contract
    ITradeGameAdapter.cs
    TradeEngine.cs
    TradeEventType.cs
    TradeManager.cs
    TradeOptions.cs
    TradePlanner.cs
    TradeSession.cs
    TradeSnapshot.cs

SUPPORTING ASSETS

  BlackJackButtler/images/
    icon.png

  BlackJackButtler/presets/
    fast-message-preset.json

  BlackJackButtler/tools/
    stats_validator.html

  uigraphic/
    background map border 288p px.gif
    background map border 288p px.png
    background map border 720p.png
    background map border HD.png
    cardframe_large.png
    gameboard border.png

  plugins/BlackJackButtler/
    icon.png
    latest.zip

  plugins/BlackJackButtler/testing/
    latest.zip

DEVELOPMENT ASSETS

  BlackJackButtler/artstyle-layout.json
  BlackJackButtler/visual-canvas.js
  BlackJackButtler/packages.lock.json

END OF REFERENCE
