using System;
using System.Numerics;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Game.ClientState.Objects.SubKinds;
using System.Threading.Tasks;
using BlackJackButtler.Chat;
using BlackJackButtler.Regex;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private bool IsLocalPlayerPartyLeader()
    {
        return true;
    }

    private string GetPartyLeaderName()
    {
        return Plugin.PlayerState.CharacterName
            ?? Plugin.ObjectTable.LocalPlayer?.Name.TextValue
            ?? string.Empty;
    }
}
