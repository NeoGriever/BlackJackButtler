using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace BlackJackButtler.Windows;

public class NotepadWindow : Window
{
    private string _text = "";
    private bool _loaded = false;
    private readonly Configuration _config;
    private readonly Action _save;
    private readonly Func<(Vector2, Vector2)>? _getMainWindowRect;
    private bool _needsPositioning = true;

    public NotepadWindow(Configuration config, Action save, Func<(Vector2, Vector2)>? getMainWindowRect = null) : base("BJB Notepad")
    {
        _config = config;
        _save = save;
        _getMainWindowRect = getMainWindowRect;
        Size = new Vector2(200, 200);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void LoadContent()
    {
        if (_loaded) return;
        _text = _config.NotepadText;
        _loaded = true;
    }

    public override void PreDraw()
    {
        if (_needsPositioning && _getMainWindowRect != null)
        {
            var (pos, size) = _getMainWindowRect();
            if (size.X > 0)
            {
                Position = new Vector2(pos.X + size.X + 10, pos.Y);
                PositionCondition = ImGuiCond.Appearing;
                _needsPositioning = false;
            }
        }
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar();
    }

    public override void Draw()
    {
        ImGui.PushFont(UiBuilder.MonoFont);
        if (ImGui.InputTextMultiline("##notepad", ref _text, 65536, new Vector2(-1, -1)))
        {
            _config.NotepadText = _text;
            _save();
        }
        ImGui.PopFont();
    }
}
