using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using BlackJackButtler.Windows;

namespace BlackJackButtler;

public sealed class Plugin : IDalamudPlugin
{
  [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
  [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
  [PluginService] internal static IPluginLog Log { get; private set; } = null!;

  private const string CommandName = "/bjb";

  public Configuration Configuration { get; }

  private readonly WindowSystem windowSystem = new("BlackJackButtler");
  private readonly BlackJackButtlerWindow mainWindow;

  public Plugin()
  {
    Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

    mainWindow = new BlackJackButtlerWindow(Configuration, () => Configuration.Save());
    windowSystem.AddWindow(mainWindow);

    CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
    {
      HelpMessage = "Open BlackJack Buttler."
      });

      PluginInterface.UiBuilder.Draw += windowSystem.Draw;

      // Optional: let the default Dalamud "open plugin UI" open our main window
      PluginInterface.UiBuilder.OpenMainUi += mainWindow.OpenMain;
      PluginInterface.UiBuilder.OpenConfigUi += mainWindow.OpenSettings;

      Log.Information("BlackJack Buttler loaded.");
    }

    public void Dispose()
    {
      PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
      PluginInterface.UiBuilder.OpenMainUi -= mainWindow.OpenMain;
      PluginInterface.UiBuilder.OpenConfigUi -= mainWindow.OpenSettings;

      CommandManager.RemoveHandler(CommandName);

      windowSystem.RemoveAllWindows();
      mainWindow.Dispose();
    }

    private void OnCommand(string command, string args) => mainWindow.OpenMain();

    public void ToggleMainUi() => mainWindow.Toggle();
  }
