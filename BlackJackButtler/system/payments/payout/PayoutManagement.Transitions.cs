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
    private static double StateElapsed => (DateTime.UtcNow - _stateEnteredAt).TotalSeconds;

    private static void SetState(PayoutState state, string log)
    {
        _state = state;
        _stateEnteredAt = DateTime.UtcNow;
        DebugLog($"[PayoutManagement] {state}: {log}");
        Trace($"State={state}, executor={_executor}, target={_targetName}@{_targetWorldId}, chunk={_currentChunk}, remaining={_remainingAmount}: {log}");
    }
}
