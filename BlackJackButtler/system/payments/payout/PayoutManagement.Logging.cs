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
    private static void Trace(string message)
    {
#if DEBUG
        if (Plugin.Instance.Configuration.DebugTradeLogging)
            Plugin.Log.Debug($"[PayoutManagement] {message}");
#endif
    }

    private static void DebugLog(string message)
    {
#if DEBUG
        if (Plugin.Instance.Configuration.DebugTradeLogging)
            Plugin.Instance.GetMainWindow().AddDebugLog(message);
#endif
    }
}
