using System;
using System.Text;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using BlackJackButtler.Chat;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    public record DebugEntry(string Text, bool IsChat);
    private readonly List<DebugEntry> _debugLog = new();
    private readonly object _logLock = new();
    private bool _verboseMode = true;

    public void AddDebugLog(string line) => AddDebugLog(line, false);

    public void AddDebugLog(string line, bool isChat)
    {
        lock (_logLock)
        {
            _debugLog.Add(new DebugEntry(line, isChat));
            while (_debugLog.Count > 15000)
            _debugLog.RemoveAt(0);
        }

        if (!Plugin.IsDebugMode) return;
    }

    private void DrawDebugPage()
    {
        if (ImGui.Checkbox("Enable Debug Mode", ref Plugin.IsDebugMode))
        {
            if (Plugin.IsDebugMode) EnableDebugMode();
            else DisableDebugMode();
        }

        if (Plugin.IsDebugMode)
        {
            ImGui.SameLine();
            ImGui.Checkbox("Fast Tests", ref Plugin.IsSpeedMode);
            ImGui.SameLine();
            ImGui.Checkbox("Auto Players", ref Plugin.DebugAutoPlayers);
        }

        ImGui.SameLine();
        if (BJBGui.SmallButton("Popout Log")) Plugin.Instance.OpenDebugPopout();

        ImGui.SameLine();
        if (BJBGui.SmallButton("Clear Log")) { lock(_logLock) _debugLog.Clear(); }

        ImGui.SameLine();
        if (BJBGui.Button("Run /xllog")) Plugin.CommandManager.ProcessCommand("/xllog");

        ImGui.SameLine();
        ImGui.Checkbox("Verbose", ref _verboseMode);

        ImGui.SameLine();
        if (BJBGui.Button("Copy All"))
        {
            CopyDebugLogToClipboard();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Copy all visible log entries to clipboard in chronological order");
        }

        if (Plugin.IsDebugMode)
        {
            ImGui.SameLine();
            int debugPlayerCount = _players.Count(p => p.IsDebugPlayer);
            bool debugPartyFull = debugPlayerCount >= 7;
            if (debugPartyFull) ImGui.BeginDisabled();
            if (BJBGui.Button("Generate new Players"))
                GenerateRandomDebugPlayers();
            if (debugPartyFull) ImGui.EndDisabled();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(debugPartyFull
                    ? "Debug group is already full"
                    : "Add random debug players until the group has 7 players");

            ImGui.SetNextItemWidth(-80);
            if (ImGui.InputText("##debug_dice_sequence", ref Plugin.DebugDiceSequence, 1024))
                Plugin.ResetDebugDiceSequence();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Comma-separated debug dice sequence. '?' is random and loops with the sequence. '*' switches to random rolls after the preceding entries.");
            ImGui.SameLine();
            if (BJBGui.SmallButton("Reset##debug_dice_sequence"))
                Plugin.ResetDebugDiceSequence();
        }

        ImGui.Separator();

        if (ImGui.BeginChild("debug_scroll_area", new Vector2(-1, -1), true))
        {
            List<DebugEntry> logCopy;
            lock (_logLock) logCopy = _debugLog.ToList();

            for (int i = logCopy.Count - 1; i >= 0; i--)
            {
                var entry = logCopy[i];
                if (!_verboseMode && !entry.IsChat) continue;
                if (ImGui.Selectable($"{entry.Text}##{i}")) ImGui.SetClipboardText(entry.Text);
            }
        }
        ImGui.EndChild();
    }

    public List<DebugEntry> GetDebugLog() => _debugLog;
    public object GetLogLock() => _logLock;

    private void EnableDebugMode()
    {
        Plugin.IsDebugMode = true;
        Plugin.ResetDebugDiceSequence();
        GameEngine.SetDebugMode(true);
        CreateTestData();
        IsRecognitionActive = true;
        Plugin.Instance.UpdateEventHooks();
    }

    private void DisableDebugMode()
    {
        ClearPlayersWithCompanionErase();
        _dealer = new PlayerState { Name = "Dealer", IsActivePlayer = true, IsDealer = true };
        GameEngine.CurrentPhase = GamePhase.Waiting;
        GameEngine.SetDebugMode(false);
        GameEngine.SetRuntimeContext(_players, _dealer);
        GameLog.Clear();
        IsRecognitionActive = false;
        Plugin.IsDebugMode = false;
        Plugin.Instance.UpdateEventHooks();
        _save();
    }

    private void CopyDebugLogToClipboard()
    {
        List<DebugEntry> logCopy;
        lock (_logLock) logCopy = _debugLog.ToList();

        var filteredLog = logCopy.Where(entry => _verboseMode || entry.IsChat).ToList();

        if (filteredLog.Count == 0)
        {
            ImGui.SetClipboardText("(No log entries to copy)");
            return;
        }

        var sb = new StringBuilder(filteredLog.Count * 100);
        sb.AppendLine($"=== BlackJack Buttler Debug Log ===");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Total Entries: {filteredLog.Count}");
        sb.AppendLine($"Verbose Mode: {(_verboseMode ? "ON" : "OFF")}");
        sb.AppendLine($"=====================================");
        sb.AppendLine();

        foreach (var entry in filteredLog)
        {
            sb.AppendLine(entry.Text);
        }

        ImGui.SetClipboardText(sb.ToString());

        AddDebugLog($"[DEBUG] Copied {filteredLog.Count} log entries to clipboard");
    }
}
