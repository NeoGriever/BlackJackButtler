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
    public enum DebugLogColor
    {
        Default,
        CommandInput,
        CommandOutput,
    }

    public enum DebugLogDetail
    {
        Normal,
        Verbose,
        FullDebug,
    }

    public record DebugEntry(
        DateTime Timestamp,
        string Text,
        bool IsChat,
        DebugLogColor Color,
        DebugLogDetail Detail);
    private readonly List<DebugEntry> _debugLog = new();
    private readonly object _logLock = new();
    private bool _verboseMode = true;
    private bool _fullDebugMode;
    private int _lastDebugVisibleEntryCount = -1;

    public void AddDebugLog(string line) => AddDebugLog(line, false);

    public void AddDebugLog(string line, bool isChat) => AddDebugLog(line, isChat, DebugLogColor.Default);

    public void AddDebugLog(string line, bool isChat, DebugLogColor color)
        => AddDebugLog(line, isChat, color, isChat ? DebugLogDetail.Normal : DebugLogDetail.Verbose);

    public void AddFullDebugLog(string line, DebugLogColor color = DebugLogColor.Default)
    {
        if (!_verboseMode || !_fullDebugMode)
            return;

        AddDebugLog(line, false, color, DebugLogDetail.FullDebug);
    }

    private void AddDebugLog(string line, bool isChat, DebugLogColor color, DebugLogDetail detail)
    {
        lock (_logLock)
        {
            _debugLog.Add(new DebugEntry(DateTime.Now, line, isChat, color, detail));
            while (_debugLog.Count > 15000)
                _debugLog.RemoveAt(0);
        }

        if (!Plugin.IsDebugMode) return;
    }

    private void DrawDebugPage()
    {
        if (!ImGui.BeginTabBar("##debug_tabs"))
            return;

        if (ImGui.BeginTabItem("Log"))
        {
            DrawDebugLogTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Variables"))
        {
            if (ImGui.BeginTabBar("##debug_variable_tabs"))
            {
                if (ImGui.BeginTabItem("Session Variables"))
                {
                    DrawVarsPage();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Reference"))
                {
                    DrawVariableReferenceTab();
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private void DrawDebugLogTab()
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
        if (BJBGui.SmallButton("Clear Log"))
        {
            lock (_logLock) _debugLog.Clear();
            _lastDebugVisibleEntryCount = 0;
        }

        ImGui.SameLine();
        if (BJBGui.Button("Run /xllog")) Plugin.CommandManager.ProcessCommand("/xllog");

        ImGui.SameLine();
        ImGui.Checkbox("Verbose", ref _verboseMode);

        ImGui.SameLine();
        if (!_verboseMode) ImGui.BeginDisabled();
        ImGui.Checkbox("Full Debug", ref _fullDebugMode);
        if (!_verboseMode) ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Log every command input and transformation step. Requires Verbose.");

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

            var visibleEntryCount = 0;
            for (int i = 0; i < logCopy.Count; i++)
            {
                var entry = logCopy[i];
                if (!IsDebugEntryVisible(entry)) continue;
                visibleEntryCount++;
                var color = GetDebugEntryColor(entry.Color);
                if (color.HasValue) ImGui.PushStyleColor(ImGuiCol.Text, color.Value);
                var displayText = FormatDebugEntry(entry);
                if (ImGui.Selectable($"{displayText}##{i}")) ImGui.SetClipboardText(displayText);
                if (color.HasValue) ImGui.PopStyleColor();
            }

            if (visibleEntryCount != _lastDebugVisibleEntryCount)
            {
                ImGui.SetScrollHereY(1.0f);
                _lastDebugVisibleEntryCount = visibleEntryCount;
            }
        }
        ImGui.EndChild();
    }

    internal static Vector4? GetDebugEntryColor(DebugLogColor color)
    {
        return color switch
        {
            DebugLogColor.CommandInput => new Vector4(0.45f, 0.8f, 1f, 1f),
            DebugLogColor.CommandOutput => new Vector4(1f, 0.85f, 0.2f, 1f),
            _ => null,
        };
    }

    public List<DebugEntry> GetDebugLog() => _debugLog;
    public object GetLogLock() => _logLock;
    public bool IsVerboseLogEnabled
    {
        get => _verboseMode;
        set => _verboseMode = value;
    }
    public bool IsFullDebugLogEnabled
    {
        get => _fullDebugMode;
        set => _fullDebugMode = value;
    }

    public bool IsDebugEntryVisible(DebugEntry entry)
        => entry.IsChat
            || (_verboseMode
                && (entry.Detail != DebugLogDetail.FullDebug || _fullDebugMode));

    public static string FormatDebugEntry(DebugEntry entry)
        => $"[{entry.Timestamp:HH:mm:ss.fff}] {entry.Text}";

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

        var filteredLog = logCopy.Where(IsDebugEntryVisible).ToList();

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
        sb.AppendLine($"Full Debug Mode: {(_verboseMode && _fullDebugMode ? "ON" : "OFF")}");
        sb.AppendLine($"=====================================");
        sb.AppendLine();

        foreach (var entry in filteredLog)
        {
            sb.AppendLine(FormatDebugEntry(entry));
        }

        ImGui.SetClipboardText(sb.ToString());

        AddDebugLog($"[DEBUG] Copied {filteredLog.Count} log entries to clipboard");
    }
}
