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
    private static void TargetPayoutPlayer(PlayerState player)
    {
        GameEngine.TargetPlayer(player.Name);

        if (Plugin.IsDebugMode) return;

        Svc.Framework.RunOnTick(
            () =>
            {
                var obj = FindPlayerObject(player.Name, player.WorldId);
                if (obj == null) return;

                Plugin.TargetManager.Target = obj;
                Plugin.TargetManager.FocusTarget = obj;
            }
        );
    }

    private static bool TargetPayoutPlayerImmediate(PlayerState player)
    {
        GameEngine.TargetPlayer(player.Name);

        if (Plugin.IsDebugMode)
            return true;

        var obj = FindPlayerObject(player.Name, player.WorldId);
        if (obj == null)
            return false;

        Plugin.TargetManager.Target = obj;
        Plugin.TargetManager.FocusTarget = obj;
        return true;
    }

    private static bool IsPayoutTargetSelected()
    {
        if (Plugin.IsDebugMode) return true;
        var target = Plugin.TargetManager.Target;
        return target != null && target.Name.TextValue.Equals(_targetName, StringComparison.OrdinalIgnoreCase);
    }

    private static IGameObject? FindPlayerObject(string name, uint worldId)
    {
        return Plugin.ObjectTable.FirstOrDefault(
            o =>
                o is IPlayerCharacter pc
                && pc.IsTargetable
                && pc.Name.TextValue.Equals(name, StringComparison.OrdinalIgnoreCase)
                && (worldId == 0 || pc.HomeWorld.RowId == worldId)
        );
    }
}
