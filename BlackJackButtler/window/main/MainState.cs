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
    private readonly PieAmountMenu _moneyRing = new();

    private DateTime? _groupDetectorActivatedAt;
    private bool _triggerUserStatsSessionPrompt;
    private bool _userStatsSessionPromptOpen;
    private float _rotationEditDegrees;
}
