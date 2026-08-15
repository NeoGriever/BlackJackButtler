using System;

namespace BlackJackButtler;

public static class ImaginaryPlayerBankManager
{
    private const long HalfTransferGranularity = 1_000;

    public static bool TryTransferToImaginaryPlayer(
        PlayerState realPlayer,
        string parameter,
        Configuration config,
        out string result)
    {
        var window = Plugin.Instance.GetMainWindow();
        var players = window.GetPlayers();
        var sourcePlayer = realPlayer.IsImaginaryPlayer
            ? PlayerIdentityManager.GetReferencedPlayer(players, realPlayer) ?? realPlayer
            : realPlayer;
        var imaginaryPlayer = PlayerIdentityManager.GetImaginaryPlayer(players, sourcePlayer);
        if (imaginaryPlayer == null)
        {
            result = $"{sourcePlayer.DisplayName} has no imaginary player";
            return false;
        }

        var token = (parameter ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(token))
        {
            result = "missing transfer amount";
            return false;
        }

        var isNegative = token.StartsWith("-", StringComparison.Ordinal);
        if (isNegative)
            token = token[1..].Trim();

        long amount;
        bool transferToImaginaryPlayer;
        switch (token)
        {
            case "half":
            case "50%":
                if (isNegative)
                {
                    result = "half and 50% only split the real player's bank";
                    return false;
                }

                amount = (Math.Max(0, sourcePlayer.Bank) / 2 / HalfTransferGranularity)
                    * HalfTransferGranularity;
                transferToImaginaryPlayer = true;
                break;

            case "min":
            case "max":
                if (isNegative)
                {
                    result = "min and max only transfer from the real player to the imaginary player";
                    return false;
                }

                var targetBank = token == "min" ? config.MinBet : config.MaxBet;
                amount = Math.Max(0, targetBank - imaginaryPlayer.Bank);
                transferToImaginaryPlayer = true;
                break;

            default:
                if (!PayoutManagement.TryParseWithdrawAmount(token, long.MaxValue, out amount))
                {
                    result = $"invalid transfer amount '{parameter}'";
                    return false;
                }

                transferToImaginaryPlayer = !isNegative;
                break;
        }

        var sender = transferToImaginaryPlayer ? sourcePlayer : imaginaryPlayer;
        var receiver = transferToImaginaryPlayer ? imaginaryPlayer : sourcePlayer;
        amount = Math.Min(amount, Math.Max(0, sender.Bank));
        if (receiver.Bank >= 0)
            amount = Math.Min(amount, long.MaxValue - receiver.Bank);

        if (amount <= 0)
        {
            result = transferToImaginaryPlayer
                ? $"no Gil available to move from {sourcePlayer.DisplayName} to {imaginaryPlayer.DisplayName}"
                : $"no Gil available to move from {imaginaryPlayer.DisplayName} to {sourcePlayer.DisplayName}";
            return false;
        }

        var sourceBefore = sourcePlayer.Bank;
        var imaginaryBefore = imaginaryPlayer.Bank;
        sender.Bank -= amount;
        receiver.Bank += amount;

        ActivityLogManager.LogBankChange(sourcePlayer.DisplayName, sourceBefore, sourcePlayer.Bank);
        ActivityLogManager.LogBankChange(imaginaryPlayer.DisplayName, imaginaryBefore, imaginaryPlayer.Bank);
        CompanionSyncManager.SendPlayerBankBetUpdate(config, sourcePlayer);
        CompanionSyncManager.SendPlayerBankBetUpdate(config, imaginaryPlayer);
        SessionManager.SaveSession(players, window.GetDealer(), GameEngine.CurrentPhase, window.IsRecognitionActive);

        result = $"{amount:N0} Gil: {sourcePlayer.DisplayName} {sourceBefore:N0} → {sourcePlayer.Bank:N0}, " +
            $"{imaginaryPlayer.DisplayName} {imaginaryBefore:N0} → {imaginaryPlayer.Bank:N0}";
        return true;
    }
}
