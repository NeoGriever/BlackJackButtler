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
    public static void Initialize()
    {
        if (_modularTradeInitialized)
            return;

        try
        {
            global::ModularTrade.TradeManager.Initialize(
                new ModularTrade.BjbDalamudTradeAdapter()
            );
            global::ModularTrade.TradeManager.TradeChanged += OnModularTradeChanged;
            _modularTradeInitialized = true;
            Trace("Initialized integrated ModularTrade executor.");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "[PayoutManagement] Failed to initialize integrated ModularTrade executor.");
        }
    }

    public static void Dispose()
    {
        if (!_modularTradeInitialized)
            return;

        global::ModularTrade.TradeManager.TradeChanged -= OnModularTradeChanged;
        if (global::ModularTrade.TradeManager.IsBusy)
            global::ModularTrade.TradeManager.Cancel();
        global::ModularTrade.TradeManager.Dispose();
        _modularTradeInitialized = false;
        Trace("Disposed integrated ModularTrade executor.");
    }
}
