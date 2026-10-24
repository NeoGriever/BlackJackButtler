using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using BlackJackButtler.Chat;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.ChatMethods;
using ECommons.DalamudServices;
using ECommons.Reflection;
using FFXIVClientStructs.FFXIV.Component.GUI;
using static ECommons.GenericHelpers;

namespace BlackJackButtler;

public static partial class PayoutManagement
{
    public static void DrawHelperWindow()
    {
        if (!IsActive) return;

        var mainWindow = Plugin.Instance.GetMainWindow();
        var rect = mainWindow.GetWindowRect();
        if (rect.Size.X > 0)
            ImGui.SetNextWindowPos(new Vector2(rect.Pos.X + rect.Size.X + 10, rect.Pos.Y), ImGuiCond.Appearing);

        var open = true;
        ImGui.SetNextWindowSize(new Vector2(300, 220), ImGuiCond.FirstUseEver);
        if (ImGui.Begin($"Payout Management: {_targetName}###bjb_payout_management", ref open, ImGuiWindowFlags.NoCollapse))
        {
            var player = GetCurrentPlayer();
            var currentBank = player?.Bank ?? _lastKnownBank;

            ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), $"Remaining payout: {_remainingAmount:N0} Gil");
            ImGui.TextDisabled($"Player bank: {currentBank:N0} Gil");
            ImGui.TextUnformatted($"Current chunk: {_currentChunk:N0} Gil");
            ImGui.TextDisabled($"State: {_state}");
            ImGui.Spacing();

            if (_startAmount > 0)
            {
                var progress = 1.0f - ((float)Math.Max(0, _remainingAmount) / _startAmount);
                ImGui.ProgressBar(Math.Clamp(progress, 0f, 1f), new Vector2(-1, 0), $"{(int)(progress * 100)}%");
            }

            if (ImGui.Button("Cancel Payout", new Vector2(-1, 0)))
                Cancel();

            ImGui.End();
        }

        if (!open)
            Cancel();
    }
}
