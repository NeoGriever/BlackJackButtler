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

public partial class BlackJackButtlerWindow : Window, IDisposable
{
    public BlackJackButtlerWindow(Configuration config, Action save, ChatLogBuffer chatLog, NotepadWindow notepadWindow) : base($"BlackJack Buttler v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version} [Default]###BlackJackButtler")
    {
        _config = config;
        _themes = new Themes.ThemeManager(Plugin.PluginInterface.GetPluginConfigDirectory());
        EnsureThemeLayers();
        var origSave = save;
        _save = () => { _presetDirty = true; origSave(); };
        _chatLog = chatLog;
        _notepadWindow = notepadWindow;

        Size = new Vector2(1280, 580);
        SizeCondition = ImGuiCond.FirstUseEver;

        RespectCloseHotkey = false;

        TitleBarButtons.Add(
            new TitleBarButton
            {
                Icon = FontAwesomeIcon.StickyNote,
                Priority = 70,
                Click = _ =>
                {
                    if (_notepadWindow.IsOpen)
                    {
                        _notepadWindow.IsOpen = false;
                    }
                    else
                    {
                        if (!_notepadLoaded) { _notepadLoaded = true; _notepadWindow.LoadContent(); }
                        _notepadWindow.IsOpen = true;
                    }
                },
                ShowTooltip = () =>
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    ImGui.SetTooltip(_notepadWindow.IsOpen ? "Close Notepad" : "Open Notepad");
                }
            }
        );

        TitleBarButtons.Add(
            new TitleBarButton
            {
                Icon = FontAwesomeIcon.CommentDots,
                Priority = 80,
                Click = _ =>
                {
                    Plugin.Instance.OpenChatBox();
                },
                ShowTooltip = () =>
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    ImGui.SetTooltip("Open BJB Messenger\n(party chat + dice rolls)");
                }
            }
        );

        TitleBarButtons.Add(
            new TitleBarButton
            {
                Icon = FontAwesomeIcon.Comments,
                Priority = 90,
                Click = _ =>
                {
                    Dalamud.Utility.Util.OpenLink("https://discord.gg/HBh4nSbuJp");
                },
                ShowTooltip = () =>
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    ImGui.SetTooltip("Join my discord for\n- bug reports\n- ideas\n- faq\n- just talk\n\n(still working on it!)");
                }
            }
        );

        TitleBarButtons.Add(
            new TitleBarButton
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
            }
        );

        _showRestoreSessionButton = SessionManager.HasRestorableGameSession();
    }
}
