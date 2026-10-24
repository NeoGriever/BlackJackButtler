using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;
using RRX = System.Text.RegularExpressions;
using System.Globalization;

namespace BlackJackButtler;

public static partial class CommandExecutor
{
    public static void NotifyDiceResult()
    {
        var window = Plugin.Instance.GetMainWindow();
        window.AddDebugLog("[Executor] NotifyDiceResult called - releasing wait");
        _wait = false;
        Volatile.Write(ref _expectedDiceSides, 0);
        ClearExpectedPlayerDice();
    }

    public static bool IsWaitingForDiceValue(int value)
    {
        var sides = Volatile.Read(ref _expectedDiceSides);
        return _wait && sides > 0 && value >= 1 && value <= sides;
    }

    public static bool TryAcceptPlayerDice(
        ParsedChatMessage message,
        List<PlayerState> players,
        PlayerState dealer
    )
    {
        if (!_wait || !_expectsPlayerDice || Volatile.Read(ref _expectedDiceSides) != 13
            || !message.DiceValue.HasValue)
            return false;

        if (message.DiceSides.HasValue && message.DiceSides.Value != 13)
            return false;
        if (!message.DiceSides.HasValue && !ChatLogBuffer.IsDiceChatType(message.ChatType))
            return false;
        if (message.DiceValue.Value is < 1 or > 13)
            return false;

        var expected = players.FirstOrDefault(
            p =>
                p.Name.Equals(_expectedDicePlayerName, StringComparison.OrdinalIgnoreCase)
                && (_expectedDicePlayerWorldId == 0 || p.WorldId == _expectedDicePlayerWorldId)
        );
        if (expected == null || !SenderMatchesExpectedPlayer(message, expected))
        {
            Plugin.Instance.GetMainWindow().AddDebugLog(
                $"[PlayerDice] Ignored roll from '{message.Name}': waiting for '{_expectedDicePlayerName}'"
            );
            return false;
        }

        var card = GameEngine.MapDice13ToCardValue(message.DiceValue.Value);
        Plugin.Instance.GetMainWindow().AddDebugLog(
            $"[PlayerDice] Accepted {message.DiceValue.Value}/13 from {expected.DisplayName} as card {card}"
        );
        DiceResultHandler.HandleDiceResult(card, Plugin.Instance.Configuration, players, dealer);
        return true;
    }

    private static bool SenderMatchesExpectedPlayer(ParsedChatMessage message, PlayerState expected)
    {
        if (message.WorldId > 0 && expected.WorldId != 0 && (uint)message.WorldId != expected.WorldId)
            return false;

        var sender = message.Name.Trim();
        if (sender.Equals(expected.Name, StringComparison.OrdinalIgnoreCase)
            || sender.Equals(expected.ResolvedName, StringComparison.OrdinalIgnoreCase))
            return true;

        var parts = expected.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            return false;
        var surname = string.Join(' ', parts.Skip(1));
        return sender.Equals($"{parts[0]} {surname[0]}.", StringComparison.OrdinalIgnoreCase)
            || sender.Equals($"{parts[0][0]}. {surname}", StringComparison.OrdinalIgnoreCase)
            || sender.Equals($"{parts[0][0]}. {surname[0]}.", StringComparison.OrdinalIgnoreCase);
    }

    private static void ExpectPlayerDice(PlayerState player)
    {
        var physicalPlayer = PlayerIdentityManager.GetReferencedPlayer(
            Plugin.Instance.GetMainWindow().GetPlayers(),
            player
        ) ?? player;
        _expectedDicePlayerName = physicalPlayer.Name;
        _expectedDicePlayerWorldId = physicalPlayer.WorldId;
        _expectsPlayerDice = true;
    }

    private static void ClearExpectedPlayerDice()
    {
        _expectsPlayerDice = false;
        _expectedDicePlayerName = string.Empty;
        _expectedDicePlayerWorldId = 0;
    }

    public static void SetPreActionSnapshot(int snapshotIndex)
    {
        _preActionSnapshotIndex = snapshotIndex;
    }

    public static void CancelCurrentGroup()
    {
        var window = Plugin.Instance.GetMainWindow();
        window.AddDebugLog("[Executor] CancelCurrentGroup called - setting cancel flag");
        _cancel = true;
        _wait = false;
        Volatile.Write(ref _expectedDiceSides, 0);
        ClearExpectedPlayerDice();
        _delayCts?.Cancel();
    }
}
