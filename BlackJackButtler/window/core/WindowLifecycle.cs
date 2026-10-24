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
    public void SetHighlightNewRound() => _highlightNewRound = true;
    public void Dispose()
    {
        _themes.Dispose();
#if DEBUG
        _debugVisualCanvas.Dispose();
#endif
    }
    public void OpenMain() { _page = Page.Main; IsOpen = true; Plugin.Instance.UpdateEventHooks(); }
    public void OpenSettings() { _page = Page.Settings; IsOpen = true; Plugin.Instance.UpdateEventHooks(); }

    internal void SaveSessionFromUI()
    {
        SessionManager.SaveSession(_players, _dealer, GameEngine.CurrentPhase, IsRecognitionActive);
    }

    public override void OnClose()
    {
        Plugin.Instance.UpdateEventHooks();
    }
    public List<PlayerState> GetPlayers() => _players;
    public PlayerState GetDealer() => _dealer;
    public (Vector2 Pos, Vector2 Size) GetWindowRect() => (_lastWindowPos, _lastWindowSize);

    public override void PreDraw()
    {
        PushTheme();
        DeckCard.ShowSuits = !_config.HideCardSuits;
        if (_presetDirty)
        {
            RecomputePresetChangeCount();
            _presetDirty = false;
        }
        var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        var label = _config.ActivePresetName ?? "Default";
        var dirty = _presetChangeCount > 0 ? "*" : "";
        WindowName = $"BlackJack Buttler v{ver} [{label}{dirty}]###BlackJackButtler";
    }

    public override void PostDraw() => PopTheme();
}
