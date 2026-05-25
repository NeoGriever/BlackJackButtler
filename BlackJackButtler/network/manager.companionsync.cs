using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BlackJackButtler;

public static class CompanionSyncManager
{
    private const int HeaderSize = 9;
    private const int HandSize = 9;
    private const int MaxCardsPerHand = 11;
    private const int DealerHeaderFlag = 0x08;
    private static readonly HttpClient Client = new();
    private static readonly SemaphoreSlim SendGate = new(1, 1);
    private sealed record CompanionBatchEntry(string uid, string data);

    public static void SendPlayerUpdate(Configuration cfg, PlayerState? player)
    {
        if (!cfg.EnableCompanionSync || player == null)
            return;

        var uid = GetUid(player);
        var payload = PackPlayerData(player, cfg);
        LogGeneratedPayload("SendPlayerUpdate", player, uid, payload);
        SendUpdate(cfg.CompanionServerAddress, uid, payload, cfg.CompanionTimeoutMs);
    }

    public static void SendPlayerBankBetUpdate(Configuration cfg, PlayerState? player)
    {
        if (!cfg.EnableCompanionSync || player == null)
            return;

        var uid = GetUid(player);
        var payload = PackPlayerData(player, cfg, true);
        LogGeneratedPayload("SendPlayerBankBetUpdate", player, uid, payload);
        SendUpdate(cfg.CompanionServerAddress, uid, payload, cfg.CompanionTimeoutMs);
    }

    public static void ClearPlayer(Configuration cfg, PlayerState? player)
    {
        if (!cfg.EnableCompanionSync || player == null)
            return;

        SendUpdate(cfg.CompanionServerAddress, GetUid(player), null, cfg.CompanionTimeoutMs);
    }

    public static void SendPlayersUpdate(Configuration cfg, IEnumerable<PlayerState> players)
    {
        if (!cfg.EnableCompanionSync)
            return;

        var batch = players
            .Where(player => player != null)
            .OrderBy(player => player.IsCurrentTurn ? 1 : 0)
            .ThenBy(player => GetUid(player))
            .Select(player =>
            {
                var uid = GetUid(player);
                var payload = PackPlayerData(player, cfg);
                LogGeneratedPayload("SendPlayersUpdate", player, uid, payload);
                return new CompanionBatchEntry(uid, Convert.ToBase64String(payload));
            })
            .ToList();

        SendBatchUpdate(cfg.CompanionServerAddress, batch, cfg.CompanionTimeoutMs);
    }

    public static byte[] PackPlayerData(PlayerState player, Configuration cfg, bool forceEmptyHand = false)
    {
        var hands = forceEmptyHand || player.Hands.Count == 0
            ? new List<HandState> { new(player.CurrentBet) }
            : player.Hands;

        var buffer = new byte[HeaderSize + (hands.Count * HandSize)];
        WriteInt32(buffer, 0, player.Bank);
        WriteInt32(buffer, 4, player.CurrentBet);
        buffer[8] = PackHeaderStatus(player, cfg, forceEmptyHand);

        for (var i = 0; i < hands.Count; i++)
            PackHandData(hands[i], forceEmptyHand ? 0 : GetHandStatus(player, hands[i], i), buffer, HeaderSize + (i * HandSize));

        return buffer;
    }

    public static byte[] PackHandData(HandState hand, int handStatus)
    {
        var buffer = new byte[HandSize];
        PackHandData(hand, handStatus, buffer, 0);
        return buffer;
    }

    public static string GetUid(PlayerState player)
    {
        if (player.IsDealer)
        {
            var lName = Plugin.PlayerState.CharacterName ?? string.Empty;
            var lWorld = Plugin.PlayerState.HomeWorld.RowId;
            Plugin.Log.Debug($"[CompanionSync] GetUid(dealer) Name='{lName}' HomeWorld={lWorld}");
            return GetUidForSource(lName, lWorld);
        }

        var source = $"{player.Name}|{player.WorldId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return Convert.ToHexString(hash, 0, 16);
    }

    public static int GetUiFlags(PlayerState player, Configuration cfg)
    {
        if (GameEngine.CurrentPhase != GamePhase.PlayersTurn ||
            !player.IsCurrentTurn || player.Hands.Count == 0 ||
            player.CurrentHandIndex < 0 || player.CurrentHandIndex >= player.Hands.Count)
            return 0;

        var hand = player.Hands[player.CurrentHandIndex];
        if (hand.Cards.Count < 2 || hand.IsStand || hand.IsBust || hand.IsNaturalBlackJack || hand.IsCharlie)
            return 0;

        var canSplit = CanSplit(player, hand, cfg);
        if (canSplit)
            return 3;

        var canDoubleDown = CanDoubleDown(player, hand, cfg);
        if (canDoubleDown)
            return 2;

        return 1;
    }

    private static async void SendUpdate(string server, string uid, byte[]? data, int timeoutMs)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(uid))
                return;

            var baseAddress = server.TrimEnd('/');
            await SendGate.WaitAsync().ConfigureAwait(false);
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(Math.Clamp(timeoutMs, 1, 1000)));
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseAddress}/update");
                request.Headers.Add("X-UID", uid);
                request.Headers.Add("X-DATA", data != null ? Convert.ToBase64String(data) : string.Empty);

                using var response = await Client.SendAsync(request, cts.Token).ConfigureAwait(false);
            }
            finally
            {
                SendGate.Release();
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Companion Sync Error: {ex.Message}");
        }
    }

    private static async void SendBatchUpdate(string server, IReadOnlyList<CompanionBatchEntry> entries, int timeoutMs)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(server) || entries.Count == 0)
                return;

            var baseAddress = server.TrimEnd('/');
            await SendGate.WaitAsync().ConfigureAwait(false);
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(Math.Clamp(timeoutMs, 1, 1000)));
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseAddress}/update");
                var json = JsonSerializer.Serialize(entries);
                request.Headers.Add("X-BATCH", Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));

                using var response = await Client.SendAsync(request, cts.Token).ConfigureAwait(false);
            }
            finally
            {
                SendGate.Release();
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Companion Sync Batch Error: {ex.Message}");
        }
    }

    private static void LogGeneratedPayload(string context, PlayerState player, string uid, byte[] payload)
    {
        Plugin.Instance.GetMainWindow().AddDebugLog(
            $"[CompanionSync:{context}] {player.DisplayName} UID={uid} Bytes={payload.Length} Hex={ToHex(payload)} Base64={Convert.ToBase64String(payload)}");
    }

    private static string ToHex(byte[] bytes)
    {
        if (bytes.Length == 0)
            return string.Empty;

        var sb = new StringBuilder(bytes.Length * 3);
        for (var i = 0; i < bytes.Length; i++)
        {
            if (i > 0)
                sb.Append(' ');
            sb.Append(bytes[i].ToString("X2"));
        }
        return sb.ToString();
    }

    private static void PackHandData(HandState hand, int handStatus, byte[] buffer, int offset)
    {
        var cardCount = Math.Min(hand.Cards.Count, MaxCardsPerHand);
        for (var i = 0; i < cardCount; i++)
            WriteBits(buffer, offset, i * 6, 6, MapCardId(hand.Cards[i]));

        WriteBits(buffer, offset, 66, 6, handStatus & 0x3F);
    }

    private static byte PackHeaderStatus(PlayerState player, Configuration cfg, bool forceIdle)
    {
        var gameState = forceIdle ? 0 : GetGameState(player) & 0x07;
        var dealerFlag = IsDealer(player) ? DealerHeaderFlag : 0;
        var result = forceIdle ? 0 : GetResult(player) & 0x03;
        var uiFlags = forceIdle ? 0 : GetUiFlags(player, cfg) & 0x03;
        return (byte)(gameState | dealerFlag | (result << 4) | (uiFlags << 6));
    }

    private static int GetGameState(PlayerState player)
    {
        if (player.Hands.Any(h => h.IsNaturalBlackJack))
            return 14;

        if (player.IsActivePlayer && player.IsCurrentTurn)
            return 12;

        return 0;
    }

    private static int GetResult(PlayerState player)
    {
        if (player.LastRoundResult > 0)
            return 1;
        if (player.LastRoundResult < 0)
            return 2;
        if (player.Hands.Any(h => h.RoundResult == 0 && h.Cards.Count > 0) && GameEngine.CurrentPhase == GamePhase.Payout)
            return 3;

        return 0;
    }

    private static int GetHandStatus(PlayerState player, HandState hand, int handIndex)
    {
        if (player.IsCurrentTurn && player.CurrentHandIndex == handIndex)
            return 1;

        if (hand.IsStand || hand.IsBust || hand.IsNaturalBlackJack || hand.IsCharlie || hand.RoundResult != 0)
            return 2;

        return 0;
    }

    private static bool CanSplit(PlayerState player, HandState hand, Configuration cfg)
    {
        if (hand.Cards.Count != 2 || player.Hands.Count >= cfg.MaxHandsPerPlayer)
            return false;

        if (!cfg.EnableSplit)
            return false;

        return cfg.IdenticalSplitOnly
            ? hand.Cards[0].Value == hand.Cards[1].Value
            : PlayerState.GetCardScoreValue(hand.Cards[0].Value) == PlayerState.GetCardScoreValue(hand.Cards[1].Value);
    }

    private static bool CanDoubleDown(PlayerState player, HandState hand, Configuration cfg)
    {
        if (!cfg.EnableDoubleDown || hand.Cards.Count != 2)
            return false;

        return player.Hands.Count <= 1 || cfg.AllowDoubleDownAfterSplit;
    }

    private static int MapCardId(DeckCard card)
    {
        var suitOffset = card.Suit switch
        {
            CardSuit.Clubs => 0,
            CardSuit.Diamonds => 13,
            CardSuit.Hearts => 26,
            CardSuit.Spades => 39,
            _ => 0
        };

        return suitOffset + Math.Clamp(card.Value, 1, 13);
    }

    private static void WriteInt32(byte[] buffer, int offset, long value)
    {
        var clamped = (int)Math.Clamp(value, int.MinValue, int.MaxValue);
        var bytes = BitConverter.GetBytes(clamped);
        Array.Copy(bytes, 0, buffer, offset, 4);
    }

    private static void WriteBits(byte[] buffer, int offset, int bitOffset, int bitCount, int value)
    {
        for (var i = 0; i < bitCount; i++)
        {
            if (((value >> i) & 1) == 0)
                continue;

            var absoluteBit = bitOffset + i;
            buffer[offset + (absoluteBit / 8)] |= (byte)(1 << (absoluteBit % 8));
        }
    }

    public static string GetUidForSource(string name, uint worldId)
    {
        var source = $"{name}|{worldId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return Convert.ToHexString(hash, 0, 16);
    }

    private static bool IsDealer(PlayerState player)
    {
        return player.IsDealer;
    }
}
