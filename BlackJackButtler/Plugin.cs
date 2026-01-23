using System;
using System.Linq;
using System.Text;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using BlackJackButtler.Chat;
using BlackJackButtler.Windows;
using BlackJackButtler.Regex;

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
    internal static Plugin Instance { get; private set; } = null!;

    internal static Action<string>? DebugCommandSink { get; private set; }

    private const string CommandName = "/bjb";

    public Configuration Configuration { get; }

    public static bool IsDebugMode = false;

    private readonly WindowSystem windowSystem = new("BlackJackButtler");
    private readonly BlackJackButtlerWindow mainWindow;
    private readonly ChatLogBuffer chatLog = new(20);
    private readonly DebugLogWindow debugLogWindow;

    public void OpenDebugPopout() => debugLogWindow.IsOpen = true;
    public BlackJackButtlerWindow GetMainWindow() => mainWindow;

    public Plugin()
    {
        Instance = this;
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        var changed = false;
        Configuration.EnsureDefaultBatchesOnce();
        changed |= Configuration.EnsureDefaultBatchesOnce();
        if (changed)
        Configuration.Save();

        mainWindow = new BlackJackButtlerWindow(Configuration, () => Configuration.Save(), chatLog);

        debugLogWindow = new DebugLogWindow(mainWindow.GetDebugLog());
        windowSystem.AddWindow(debugLogWindow);

        DebugCommandSink = mainWindow.AddDebugLog;
        windowSystem.AddWindow(mainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open BlackJack Buttler."
            });

            PluginInterface.UiBuilder.Draw += windowSystem.Draw;
            PluginInterface.UiBuilder.OpenMainUi += mainWindow.OpenMain;
            PluginInterface.UiBuilder.OpenConfigUi += mainWindow.OpenSettings;

            ChatGui.ChatMessage += OnChatMessage;

            Log.Information("BlackJack Buttler loaded.");
        }

        public void Dispose()
        {
            PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
            PluginInterface.UiBuilder.OpenMainUi -= mainWindow.OpenMain;
            PluginInterface.UiBuilder.OpenConfigUi -= mainWindow.OpenSettings;

            ChatGui.ChatMessage -= OnChatMessage;

            CommandManager.RemoveHandler(CommandName);

            windowSystem.RemoveAllWindows();
            mainWindow.Dispose();
            DebugCommandSink = null;
        }

        private void OnCommand(
        string command,
        string args
        )
        {
            mainWindow.OpenMain();
        }

        private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            var senderText = sender.TextValue ?? string.Empty;
            var messageText = message.TextValue ?? string.Empty;
            var pp = sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
            var playerName = pp?.PlayerName ?? string.Empty;
            var worldId = pp?.World.RowId ?? 0u;

            InjectChatMessage((int)type, worldId, playerName, senderText, messageText, sender, message);
        }

        public void InjectChatMessage(int type, uint worldId, string playerName, string senderText, string messageText, SeString? rawSender = null, SeString? rawMessage = null)
        {
            mainWindow.AddDebugLog($"[{DateTime.Now:T}] [{type}] [{senderText}]: {messageText}");

            if (type != (int)XivChatType.Party && type != (int)XivChatType.SystemMessage && type != 569 && type != 2105 && type != 4153 && type != 8249)
                return;

            var localName = ObjectTable.LocalPlayer?.Name.TextValue ?? string.Empty;

            var s = rawSender ?? new SeString(new TextPayload(senderText));
            var m = rawMessage ?? new SeString(new TextPayload(messageText));

            var parsed = ChatMessageParser.Parse(DateTime.Now, s, m, localName);

            var finalParsed = parsed;
            if (IsDebugMode && messageText.Contains("Würfeln!"))
            {
                finalParsed = new ParsedChatMessage(
                    parsed.Timestamp,
                    parsed.GroupIndexNumber,
                    parsed.Name,
                    parsed.WorldId,
                    parsed.Message,
                    true,
                    parsed.ColorU32
                );
            }

            chatLog.Add(finalParsed);
            RegexEngine.ProcessIncoming(finalParsed, Configuration, mainWindow.GetPlayers(), mainWindow.GetDealer());
        }

        private static string DumpPayloads(
        SeString s
        )
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

        private static string ToHex(
        byte[] bytes
        )
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
