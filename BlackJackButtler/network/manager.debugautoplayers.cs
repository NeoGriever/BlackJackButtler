using System;
using System.Linq;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static class DebugAutoPlayerManager
{
    private static DateTime _seenStateAt = DateTime.MinValue;
    private static DateTime _dueAt = DateTime.MinValue;
    private static string _targetName = string.Empty;
    private static bool _inFlight;

    public static void Reset()
    {
        _seenStateAt = DateTime.MinValue;
        _dueAt = DateTime.MinValue;
        _targetName = string.Empty;
        _inFlight = false;
    }

    public static void Tick(Configuration cfg)
    {
        if (!Plugin.IsDebugMode || !Plugin.DebugAutoPlayers)
        {
            Reset();
            return;
        }

        if (CommandExecutor.LastStateFiredAt > _seenStateAt)
        {
            _seenStateAt = CommandExecutor.LastStateFiredAt;
            _targetName = CommandExecutor.LastStateTargetName;
            _dueAt = DateTime.Now.AddSeconds(0.3);
        }

        if (_inFlight || _dueAt == DateTime.MinValue || DateTime.Now < _dueAt)
            return;

        if (CommandExecutor.IsRunning || CommandExecutor.IsFollowUpPending)
            return;

        if (GameEngine.CurrentPhase != GamePhase.PlayersTurn)
            return;

        var window = Plugin.Instance.GetMainWindow();
        var players = window.GetPlayers();
        var player = players.FirstOrDefault(p =>
            p.IsCurrentTurn
            && p.IsActivePlayer
            && !p.IsOnHold
            && (string.IsNullOrWhiteSpace(_targetName)
                || p.DisplayName.Equals(_targetName, StringComparison.OrdinalIgnoreCase)
                || p.Name.Equals(_targetName, StringComparison.OrdinalIgnoreCase)));

        player ??= players.FirstOrDefault(p =>
            p.IsCurrentTurn && p.IsActivePlayer && !p.IsOnHold);

        if (player == null || player.Hands.Count == 0)
            return;

        _dueAt = DateTime.MinValue;
        _inFlight = true;
        Task.Run(async () =>
        {
            try
            {
                await ExecuteDecision(player, cfg, players);
            }
            finally
            {
                _inFlight = false;
            }
        });
    }

    private static async Task ExecuteDecision(PlayerState player, Configuration cfg, System.Collections.Generic.List<PlayerState> players)
    {
        var window = Plugin.Instance.GetMainWindow();
        if (player.CurrentHandIndex < 0 || player.CurrentHandIndex >= player.Hands.Count)
            player.CurrentHandIndex = 0;

        var hand = player.Hands[player.CurrentHandIndex];
        if (hand.IsStand || hand.IsBust)
            return;

        if (CanSplit(player, hand, cfg))
        {
            EnsureDebugBank(player, player.CurrentBet, "Split");
            window.AddDebugLog($"[DebugAutoPlayers] {player.DisplayName}: Split");
            await GameEngine.ActionSplit(player, cfg, players);
            return;
        }

        var (hard, _) = player.CalculatePoints(player.CurrentHandIndex);
        if (CanDoubleDown(player, hand, cfg) && (hard == 10 || hard == 11))
        {
            EnsureDebugBank(player, player.CurrentBet, "DD");
            window.AddDebugLog($"[DebugAutoPlayers] {player.DisplayName}: DD");
            await GameEngine.ActionDD(player, cfg, players);
            return;
        }

        if (hard < 17)
        {
            window.AddDebugLog($"[DebugAutoPlayers] {player.DisplayName}: Hit");
            await GameEngine.ActionHit(player, cfg, players);
            return;
        }

        window.AddDebugLog($"[DebugAutoPlayers] {player.DisplayName}: Stand");
        await GameEngine.ActionStand(player, cfg, players);
    }

    private static void EnsureDebugBank(PlayerState player, long required, string reason)
    {
        if (player.Bank >= required)
            return;

        var before = player.Bank;
        player.Bank = required;
        Plugin.Instance.GetMainWindow().AddDebugLog(
            $"[DebugAutoPlayers] {player.DisplayName}: bank topped up for {reason} ({before:N0} -> {player.Bank:N0})");
    }

    private static bool CanDoubleDown(PlayerState player, HandState hand, Configuration cfg)
    {
        if (!cfg.EnableDoubleDown || hand.Cards.Count != 2)
            return false;

        if (player.Hands.Count > 1 && !cfg.AllowDoubleDownAfterSplit)
            return false;

        return true;
    }

    private static bool CanSplit(PlayerState player, HandState hand, Configuration cfg)
    {
        if (!cfg.EnableSplit || hand.Cards.Count != 2 || player.Hands.Count >= cfg.MaxHandsPerPlayer)
            return false;

        return cfg.IdenticalSplitOnly
            ? hand.Cards[0].Value == hand.Cards[1].Value
            : PlayerState.GetCardScoreValue(hand.Cards[0].Value) == PlayerState.GetCardScoreValue(hand.Cards[1].Value);
    }
}
