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
    private static PlayerState? GetCurrentPlayer()
    {
        return GetPayoutSource(Plugin.Instance.GetMainWindow().GetPlayers());
    }

    private static void StartNextQueuedPayout()
    {
        var players = Plugin.Instance.GetMainWindow().GetPlayers();
        while (QueuedPayouts.TryDequeue(out var request))
        {
            var player = players.FirstOrDefault(
                candidate =>
                    candidate.Name.Equals(request.PlayerName, StringComparison.OrdinalIgnoreCase)
                    && (request.WorldId == 0 || candidate.WorldId == request.WorldId)
            );
            if (player == null || player.Bank <= 0)
            {
                DebugLog(
                    $"[PayoutManagement] Skipped queued payout for {request.PlayerName}: player missing or bank empty"
                );
                continue;
            }

            var amount = Math.Min(request.Amount, player.Bank);
            if (TryStartPayout(player, amount) == PayoutStartResult.Started)
                return;

            DebugLog(
                $"[PayoutManagement] Skipped queued payout for {player.DisplayName}: could not start payout"
            );
        }
    }
}
