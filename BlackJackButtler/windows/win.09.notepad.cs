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

    public NotepadWindow(Configuration config, Action save) : base("BJB Notepad")
    {
        _config = config;
        _save = save;
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
