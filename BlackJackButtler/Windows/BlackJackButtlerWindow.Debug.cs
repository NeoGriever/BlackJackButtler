using System;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using BlackJackButtler.Chat;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawDebugPage()
    {
        ImGui.TextUnformatted("Chat Debug Logger");
        ImGui.SameLine();
        if (ImGui.SmallButton("Clear Log"))
            _debugLog.Clear();

        ImGui.SameLine();
        if (ImGui.SmallButton("Popout"))
            Plugin.Instance.OpenDebugPopout();

        ImGui.SameLine();
        if (ImGui.Checkbox("DEBUG MODE (Session Only)", ref Plugin.IsDebugMode))
        {
            if (Plugin.IsDebugMode)
                CreateTestData();
            else
                _players.RemoveAll(p => p.IsDebugPlayer);
        }

        ImGui.Separator();

        if (ImGui.BeginChild("debug_scroll_area", new Vector2(-1, -1), true))
        {
            for (int i = _debugLog.Count - 1; i >= 0; i--)
            {
                if (ImGui.Selectable($"{_debugLog[i]}##{i}"))
                {
                    ImGui.SetClipboardText(_debugLog[i]);
                }
            }
            ImGui.EndChild();
        }
    }

    public void SimulateChat(int type, string sender, string message)
    {
        if (!Plugin.IsDebugMode) return;

        Plugin.Framework.RunOnTick(() =>
        {
            Plugin.Instance.InjectChatMessage(type, 0, sender, sender, message);
        });
    }

    public void AddDebugLog(string line)
    {
        _debugLog.Add(line);
        while (_debugLog.Count > 200)
            _debugLog.RemoveAt(0);

        if (!Plugin.IsDebugMode) return;

        TrySimulateDiceCommand(line);
    }

    private void TrySimulateDiceCommand(string line)
    {
        if (!line.Contains("/dice", StringComparison.OrdinalIgnoreCase)) return;

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int max = 13;
        if (parts.Length >= 3 && int.TryParse(parts[2], out var customMax)) max = customMax;

        var rolled = Random.Shared.Next(1, max + 1);

        string diceResultMessage = $"Würfeln! (1-{max}) {rolled}";

        Plugin.Framework.RunOnTick(() =>
        {
            Plugin.Instance.InjectChatMessage(2105, 0, "SYSTEM", "SYSTEM", diceResultMessage);
        });
    }

    public List<string> GetDebugLog() => _debugLog;
}
