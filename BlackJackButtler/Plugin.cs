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

  private const string CommandName = "/bjb";

  public Configuration Configuration { get; }

  private readonly WindowSystem windowSystem = new("BlackJackButtler");
  private readonly BlackJackButtlerWindow mainWindow;
  private readonly ChatLogBuffer chatLog = new(20);

  public Plugin()
  {
    Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
    Configuration.EnsureDefaults();
    Configuration.Save();

    mainWindow = new BlackJackButtlerWindow(Configuration, () => Configuration.Save(), chatLog);
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
    }

    private void OnCommand(string command, string args) => mainWindow.OpenMain();

    private void OnChatMessage(
      XivChatType type,
      int timestamp,
      ref SeString sender,
      ref SeString message,
      ref bool isHandled
    )
    {
      if (type != XivChatType.Party)
      return;

      var senderText = sender.TextValue ?? string.Empty;
      var messageText = message.TextValue ?? string.Empty;

      var senderBytes = sender.Encode();
      var messageBytes = message.Encode();

      // PlayerPayload aus Sender extrahieren (Name/WorldId)
      var pp = sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
      var playerName = pp?.PlayerName ?? string.Empty;
      var worldId = pp?.World.RowId ?? 0u;

      // Payloads als Debug-Text auflisten
      var senderPayloadDump = DumpPayloads(sender);

      if (type != XivChatType.Party)
        return;

      var localName = ObjectTable.LocalPlayer?.Name.TextValue ?? string.Empty;
      var parsed = ChatMessageParser.Parse(DateTime.Now, sender, message, localName);
      chatLog.Add(parsed);
      RegexEngine.ProcessIncoming(parsed, Configuration);
    }

    private static string DumpPayloads(SeString s)
    {
      var sb = new StringBuilder(1024);

      for (var i = 0; i < s.Payloads.Count; i++)
      {
        var p = s.Payloads[i];
        sb.Append(i).Append(": ").Append(p.GetType().Name);

        // Ein paar hilfreiche Spezialfälle:
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
