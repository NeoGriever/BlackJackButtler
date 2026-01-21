using System;
using System.Linq;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using BlackJackButtler.Chat;
using BlackJackButtler.Windows;

namespace BlackJackButtler;

public sealed class Plugin : IDalamudPlugin
{
  [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
  [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
  [PluginService] internal static IPluginLog Log { get; private set; } = null!;
  [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

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
      // Nur Party-Chat loggen (Debug)
      if (type != XivChatType.Party)
      return;

      // Textwerte (wie bisher)
      var senderText = sender.TextValue ?? string.Empty;
      var messageText = message.TextValue ?? string.Empty;

      // Rohbytes (Payload-encoded)
      var senderBytes = sender.Encode();
      var messageBytes = message.Encode();

      chatLog.Add(new ChatLogEntry(
      DateTime.Now,
      type,
      (int)type,
      senderText,
      messageText,
      ToHex(senderBytes),
      ToHex(messageBytes)
      ));
    }

    private static string ToHex(byte[] bytes)
    {
      if (bytes.Length == 0)
      return string.Empty;

      // "00 02 41 ..." Format
      var sb = new StringBuilder(bytes.Length * 3);
      for (var i = 0; i < bytes.Length; i++)
      {
        if (i > 0) sb.Append(' ');
        sb.Append(bytes[i].ToString("X2"));
      }
      return sb.ToString();
    }



  }
