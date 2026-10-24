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
    private void HighlightActionButton(PlayerState p, string label, ref bool highlightField, bool enabled, Action onClick)
    {
        if (!enabled) ImGui.BeginDisabled();
        bool shouldHighlight = highlightField && enabled;

        bool clicked = shouldHighlight ? BJBGui.ButtonHighlighted($"{label}##btn_{label}_{p.UIID}", _config.HighlightColor, _config.HighlightTextColor) : BJBGui.Button($"{label}##btn_{label}_{p.UIID}");

        if (clicked)
        {
            highlightField = false;
            onClick?.Invoke();
        }

        if (!enabled) ImGui.EndDisabled();
    }
}
