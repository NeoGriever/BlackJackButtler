using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using BlackJackButtler.Chat;

namespace BlackJackButtler.Windows;

public class TablePopoutWindow : Window
{
    private readonly Configuration _config;
    private readonly Action _save;
    private readonly BlackJackButtlerWindow _mainWindow;

    public TablePopoutWindow(Configuration config, Action save, BlackJackButtlerWindow mainWindow)
        : base("BJB Table##bjb_table_popout",
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse)
    {
        _config = config;
        _save = save;
        _mainWindow = mainWindow;
        SizeCondition = ImGuiCond.FirstUseEver;
        Size = new Vector2(700, 400);
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public override void OnClose()
    {
        _config.TablePopout = false;
        _save();
    }

    public override void Draw()
    {
        // IsWindowAppearing() = erster Frame nach dem Öffnen: DC.ChildWindows noch nicht initialisiert,
        // BeginTable würde einen Null-Pointer in die Sort-Liste schreiben → Crash in EndFrame.
        if (ImGui.IsWindowAppearing()) return;

        DrawTablePopoutControls();
        ImGui.Separator();
        _mainWindow.DrawDealerPanelV2("_po");
        ImGui.Spacing();
        _mainWindow.DrawPlayersPanelV2("_po");
        // DrawMainSharedPopupsV2 wird NICHT aufgerufen — Popups gehören zum Hauptfenster-Kontext
    }

    private void DrawTablePopoutControls()
    {
        float btnW = 80f;

        // STOP — permanent sichtbar, nur klickbar wenn Command läuft
        if (!CommandExecutor.IsRunning) ImGui.BeginDisabled();
        ImGui.PushStyleColor(ImGuiCol.Button, CommandExecutor.IsRunning
            ? new Vector4(0.7f, 0.0f, 0.0f, 1.0f)
            : new Vector4(0.3f, 0.3f, 0.3f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.1f, 0.1f, 1.0f));
        if (ImGui.Button("STOP##tbl_stop", new Vector2(btnW, 0)))
            CommandExecutor.CancelCurrentGroup();
        ImGui.PopStyleColor(2);
        if (!CommandExecutor.IsRunning) ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(CommandExecutor.IsRunning ? "Stop currently running commands" : "No command group is running.");

        // PANIC — permanent sichtbar, nur klickbar bei Ctrl+Shift
        ImGui.SameLine();
        var io = ImGui.GetIO();
        bool canPanic = io.KeyCtrl && io.KeyShift;
        if (!canPanic) ImGui.BeginDisabled();
        ImGui.PushStyleColor(ImGuiCol.Button, canPanic
            ? new Vector4(0.5f, 0.0f, 0.0f, 1.0f)
            : new Vector4(0.3f, 0.3f, 0.3f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.65f, 0.0f, 0.0f, 1.0f));
        if (ImGui.Button("PANIC##tbl_panic", new Vector2(btnW, 0)))
            _mainWindow.TriggerPanicStage1();
        ImGui.PopStyleColor(2);
        if (!canPanic) ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Hold Ctrl+Shift to enable PANIC");
    }
}
