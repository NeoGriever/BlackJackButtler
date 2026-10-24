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
#if DEBUG
    private bool _debugVisualArtstyleMode;
    private readonly DebugVisualCanvasRenderer _debugVisualCanvas = new();

    private bool DrawDebugVisualArtstyleModeSwitch()
    {
        ImGui.TextUnformatted("Main view");
        ImGui.SameLine();
        var mode = _debugVisualArtstyleMode ? 1 : 0;
        DrawEnumButtons("debug_main_artstyle", ref mode, new[] { "Standard", "Visual" }, selected => _debugVisualArtstyleMode = selected == 1);
        ImGui.Separator();
        return _debugVisualArtstyleMode;
    }
#endif

#if DEBUG
    private void DrawDebugVisualCanvas()
    {
        var available = ImGui.GetContentRegionAvail();
        var canvasSize = new DebugVisualCanvasSize(Math.Max(available.X, 1f), Math.Max(available.Y, 1f));
        _debugVisualCanvas.Request(BuildDebugVisualPayload(canvasSize));
        _debugVisualCanvas.Draw(ImGui.GetCursorScreenPos(), new Vector2(canvasSize.Width, canvasSize.Height));
    }

    private DebugVisualPayload BuildDebugVisualPayload(DebugVisualCanvasSize canvas)
        => new(canvas, GameEngine.CurrentPhase.ToString(), IsRecognitionActive, BuildDebugVisualPlayer(_dealer), _players.Select(BuildDebugVisualPlayer).ToArray());

    private DebugVisualPlayer BuildDebugVisualPlayer(PlayerState player)
    {
        var hands = player.Hands.Select((hand, index) => new DebugVisualHand(index, hand.Bet, player.GetBestScore(index), hand.IsStand, hand.IsBust, hand.IsNaturalBlackJack, hand.IsCharlie, hand.IsDoubleDown, hand.IsTripleDown, hand.Cards.Select(card => new DebugVisualCard(card.Value, card.ValueLabel, card.Suit.ToString(), card.Symbol)).ToArray())).ToArray();
        var isVip = VipManager.GetPlayerTier(player.Name, VipManager.ResolveWorldName(player.WorldId)) > 0;
        return new DebugVisualPlayer(player.UIID, player.Name, player.Alias, player.DisplayName, player.WorldId, isVip, player.IsActivePlayer, player.IsOnHold, player.IsOnBench, player.IsCurrentTurn, player.IsImaginaryPlayer, player.Bank, player.CurrentBet, player.CurrentHandIndex, hands);
    }
#endif
}
