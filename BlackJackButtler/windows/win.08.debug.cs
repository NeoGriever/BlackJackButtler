using System;
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
            while (_debugLog.Count > 200)
                _debugLog.RemoveAt(0);
        }

        if (!Plugin.IsDebugMode) return;

        if (line.Contains("/dice") && (line.Contains("SYSTEM:") || line.Contains("[Router-Dispatch]")))
        {
            TrySimulateDiceCommand(line);
        }
    }

    private void DrawDebugPage()
    {
        if (ImGui.Checkbox("Enable Debug Mode", ref Plugin.IsDebugMode))
        {
            if (Plugin.IsDebugMode) CreateTestData();
            else
            {
                _players.Clear();
                _dealer = new PlayerState { Name = "Dealer", IsActivePlayer = true };
                GameEngine.CurrentPhase = GamePhase.Waiting;
                GameEngine.SetRuntimeContext(_players, _dealer);
                GameLog.Clear();
                IsRecognitionActive = false;
                _save();
            }
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("Popout Log")) Plugin.Instance.OpenDebugPopout();

        ImGui.SameLine();
        if (ImGui.SmallButton("Clear Log")) { lock(_logLock) _debugLog.Clear(); }

        ImGui.SameLine();
        if (ImGui.Button("Run /xllog")) Plugin.CommandManager.ProcessCommand("/xllog");

        ImGui.SameLine();
        ImGui.Checkbox("Verbose", ref _verboseMode);

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
            ImGui.EndChild();
        }
    }

    private void TrySimulateDiceCommand(string line)
    {
        if (!line.Contains("/dice")) return;

        int max = 13;
        var parts = line.Split(new[] { ' ', ':', ']', '/', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Equals("party", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length)
            {
                if (int.TryParse(parts[i + 1], out var val))
                {
                    max = val;
                    break;
                }
            }
        }

        var rolled = Random.Shared.Next(1, max + 1);
        string diceResultMessage = $"Würfeln! (1-{max}) {rolled}";

        Plugin.Framework.RunOnTick(() => {
            Plugin.Instance.InjectChatMessage(2105, 0, "SYSTEM", "SYSTEM", diceResultMessage);
        });
    }

    public List<DebugEntry> GetDebugLog() => _debugLog;
    public object GetLogLock() => _logLock;
}
