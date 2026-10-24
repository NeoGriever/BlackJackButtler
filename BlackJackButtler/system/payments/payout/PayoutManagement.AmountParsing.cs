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
    public static bool TryParseWithdrawAmount(string rawAmount, long availableBank, out long amount)
    {
        amount = 0;
        var text = (rawAmount ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(text) || text.Contains('-'))
            return false;

        if (text is "all" or "everything")
        {
            amount = availableBank;
            return amount > 0;
        }

        var multiplier = 1L;
        var hasSuffix = false;
        if (text.EndsWith("k", StringComparison.Ordinal))
        {
            multiplier = 1_000L;
            hasSuffix = true;
            text = text[..^1].Trim();
        }
        else if (text.EndsWith("m", StringComparison.Ordinal))
        {
            multiplier = 1_000_000L;
            hasSuffix = true;
            text = text[..^1].Trim();
        }

        if (text.Any(ch => !char.IsDigit(ch) && ch != '.' && ch != ',' && !char.IsWhiteSpace(ch))
            || !TryParsePayoutNumber(text, hasSuffix, out var parsed))
            return false;

        amount = SafeMultiply(parsed, multiplier);
        return amount > 0;
    }

    private static bool TryParsePayoutNumber(string text, bool hasSuffix, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (!hasSuffix)
        {
            var digits = new string(text.Where(char.IsDigit).ToArray());
            return !string.IsNullOrWhiteSpace(digits)
                && decimal.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        var normalized = NormalizeSuffixedPayoutNumber(text);
        return !string.IsNullOrWhiteSpace(normalized)
            && decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    private static string NormalizeSuffixedPayoutNumber(string text)
    {
        var token = new string(text.Where(ch => char.IsDigit(ch) || ch == '.' || ch == ',').ToArray());
        if (string.IsNullOrWhiteSpace(token))
            return string.Empty;

        var lastDot = token.LastIndexOf('.');
        var lastComma = token.LastIndexOf(',');
        var separatorIndex = Math.Max(lastDot, lastComma);
        if (separatorIndex < 0)
            return new string(token.Where(char.IsDigit).ToArray());

        var hasMixedSeparators = lastDot >= 0 && lastComma >= 0;
        var fractionLength = token.Length - separatorIndex - 1;
        var treatAsDecimal = hasMixedSeparators || fractionLength is > 0 and <= 2;
        if (!treatAsDecimal)
            return new string(token.Where(char.IsDigit).ToArray());

        var whole = new string(token[..separatorIndex].Where(char.IsDigit).ToArray());
        var fraction = new string(token[(separatorIndex + 1)..].Where(char.IsDigit).ToArray());
        return string.IsNullOrEmpty(whole) ? $"0.{fraction}" : $"{whole}.{fraction}";
    }

    private static long SafeMultiply(decimal value, long multiplier)
    {
        try
        {
            var result = value * multiplier;
            return result > long.MaxValue ? long.MaxValue : decimal.ToInt64(decimal.Truncate(result));
        }
        catch (OverflowException)
        {
            return long.MaxValue;
        }
    }
}
