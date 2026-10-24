using System;
using System.Linq;
using Rx = System.Text.RegularExpressions.Regex;
using RxOptions = System.Text.RegularExpressions.RegexOptions;
using BlackJackButtler.Chat;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.ChatMethods;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using ModularTrade;
using static ECommons.GenericHelpers;

namespace BlackJackButtler.ModularTrade;

internal unsafe sealed class BjbDalamudTradeAdapter : ITradeGameAdapter
{
    private static readonly Rx[] GermanSuccessPatterns =
    {
        CreateSystemMessagePattern("Der Handel wurde abgeschlossen\\."),
    };

    private static readonly Rx[] EnglishSuccessPatterns =
    {
        CreateSystemMessagePattern("Trade complete\\."),
    };

    private static readonly Rx[] GermanCancelPatterns =
    {
        CreateSystemMessagePattern("Der Handel wurde abgebrochen\\."),
    };

    private static readonly Rx[] EnglishCancelPatterns =
    {
        CreateSystemMessagePattern("Trade canceled\\."),
    };

    private static readonly Rx[] FrenchSuccessPatterns = Array.Empty<Rx>();
    private static readonly Rx[] FrenchCancelPatterns = Array.Empty<Rx>();
    private static readonly Rx[] JapaneseSuccessPatterns = Array.Empty<Rx>();
    private static readonly Rx[] JapaneseCancelPatterns = Array.Empty<Rx>();

    private GameTradeOutcome _outcome;
    private string _expectedRecipient = string.Empty;
    private nint _targetAddress;
    private DateTimeOffset _awaitingOutcomeSince;

    public BjbDalamudTradeAdapter()
    {
        Plugin.ChatGui.ChatMessage += OnChatMessage;
    }

    public DateTimeOffset Now => DateTimeOffset.UtcNow;

    public bool IsTradeOpen => Svc.Condition[ConditionFlag.TradeOpen]
        || (TryGetAddonByName<AtkUnitBase>("Trade", out var addon) && IsAddonReady(addon));

    public bool IsNumericInputOpen => TryGetAddonByName<AtkUnitBase>("InputNumeric", out var addon)
        && IsAddonReady(addon);

    public bool IsTradeUiReady => TryGetAddonByName<AtkUnitBase>("Trade", out var addon)
        && IsAddonReady(addon);

    public bool IsTradeButtonEnabled
    {
        get
        {
            if (!TryGetAddonByName<AtkUnitBase>("Trade", out var addon) || !IsAddonReady(addon))
                return false;

            var button = GetTradeButton(addon);
            return button is not null && button->IsEnabled;
        }
    }

    public bool IsFinalTradeDialogVisible => FindTradeConfirmationDialog() is not null;

    public bool HasSufficientGil(int amount)
    {
        var inventory = InventoryManager.Instance();
        if (inventory is null)
            return false;

        var currency = inventory->GetInventoryContainer(InventoryType.Currency);
        return currency is not null && currency->GetInventorySlot(0)->Quantity >= amount;
    }

    public bool IsCorrectRecipient(string recipient)
    {
        var target = Plugin.TargetManager.Target as IPlayerCharacter;
        return target is not null
            && target.Address == _targetAddress
            && string.Equals(new Sender(target).ToString(), recipient, StringComparison.Ordinal);
    }

    public bool FindAndTargetPlayer(string recipient)
    {
        var target = Plugin.ObjectTable
            .OfType<IPlayerCharacter>()
            .FirstOrDefault(
                player => player.IsTargetable
                    && string.Equals(new Sender(player).ToString(), recipient, StringComparison.Ordinal)
            );
        if (target is null)
        {
            Trace($"FindAndTargetPlayer: recipient '{recipient}' is not targetable.");
            return false;
        }

        _expectedRecipient = recipient;
        _targetAddress = target.Address;
        if (Plugin.TargetManager.Target?.Address != target.Address)
        {
            Trace($"FindAndTargetPlayer: targeting {recipient} at 0x{target.Address:X}.");
            Plugin.TargetManager.Target = target;
            Plugin.TargetManager.FocusTarget = target;
            return false;
        }

        Trace($"FindAndTargetPlayer: {recipient} is selected.");
        return true;
    }

    public bool TrySendTradeRequest()
    {
        if (!IsCorrectRecipient(_expectedRecipient))
        {
            Trace("TrySendTradeRequest: target verification failed.");
            return false;
        }

        Trace($"TrySendTradeRequest: /trade -> {_expectedRecipient}.");
        ChatCommandRouter.Send("/trade", Plugin.Instance.Configuration, "ModularTrade");
        return true;
    }

    public bool TryOpenGilInput()
    {
        if (!TryGetAddonByName<AtkUnitBase>("Trade", out var addon) || !IsAddonReady(addon))
            return false;

        Trace("TryOpenGilInput: firing direct Trade callback [Int:2, Undefined:0].");
        return FireCallbackIntAndZero(addon, 2);
    }

    public bool TrySetGil(int amount)
    {
        if (amount is <= 0 or > TradePlanner.GameMaximumGilPerTrade)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (!TryGetAddonByName<AtkUnitBase>("InputNumeric", out var addon) || !IsAddonReady(addon))
            return false;

        Trace($"TrySetGil: firing direct InputNumeric callback with {amount}.");
        FireCallbackInt(addon, amount);
        return true;
    }

    public bool TryClickTradeButton()
    {
        if (!TryGetAddonByName<AtkUnitBase>("Trade", out var addon) || !IsAddonReady(addon))
            return false;

        var button = GetTradeButton(addon);
        if (button is null || !button->IsEnabled)
            return false;

        Trace("TryClickTradeButton: firing direct Trade confirm callback [Int:0, Undefined:0].");
        return FireCallbackIntAndZero(addon, 0);
    }

    public bool TryConfirmFinalDialog()
    {
        var dialog = FindTradeConfirmationDialog();
        if (dialog is null)
            return false;

        _awaitingOutcomeSince = Now;
        Trace("TryConfirmFinalDialog: firing direct SelectYesno callback [Int:0].");
        return FireCallbackInt(dialog, 0);
    }

    public void AbortOpenTrade()
    {
        if (!IsTradeOpen)
            return;

        var inventory = InventoryManager.Instance();
        if (inventory is null)
        {
            Trace("AbortOpenTrade: InventoryManager is unavailable; could not refuse the open trade.");
            return;
        }

        Trace("AbortOpenTrade: calling InventoryManager.RefuseTrade() for the failed pre-confirmation session.");
        inventory->RefuseTrade();
    }

    public GameTradeOutcome ConsumeOutcome()
    {
        var outcome = _outcome;
        _outcome = GameTradeOutcome.None;
        return outcome;
    }

    public void ResetObservedOutcome()
    {
        _outcome = GameTradeOutcome.None;
        _awaitingOutcomeSince = DateTimeOffset.MinValue;
        Trace("ResetObservedOutcome.");
    }

    public void Log(string message, Exception? error = null)
    {
#if DEBUG
        if (Plugin.Instance.Configuration.DebugTradeLogging)
            Plugin.Instance.GetMainWindow().AddDebugLog($"[ModularTrade] {message}{(error is null ? string.Empty : $": {error.Message}")}");
#endif
        Trace($"Engine: {message}{(error is null ? string.Empty : $": {error}")}");
    }

    public void Dispose()
    {
        Plugin.ChatGui.ChatMessage -= OnChatMessage;
    }

    private static bool FireCallbackInt(AtkUnitBase* addon, int value)
    {
        var values = stackalloc AtkValue[1];
        values[0] = new AtkValue
        {
            Type = AtkValueType.Int,
            Int = value,
        };
        addon->FireCallback(1, values, true);
        return true;
    }

    private static bool FireCallbackIntAndZero(AtkUnitBase* addon, int value)
    {
        var values = stackalloc AtkValue[2];
        values[0] = new AtkValue
        {
            Type = AtkValueType.Int,
            Int = value,
        };
        values[1] = new AtkValue { Type = 0, Int = 0 };
        addon->FireCallback(2, values, true);
        return true;
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        if (message.LogKind != XivChatType.SystemMessage || _awaitingOutcomeSince == DateTimeOffset.MinValue)
            return;

        var text = message.Message.TextValue?.Trim();
        if (string.IsNullOrEmpty(text))
            return;

        Trace($"Trade system message observed: '{text}'.");

        if (MatchesTradeSystemMessage(text, GermanSuccessPatterns, EnglishSuccessPatterns, FrenchSuccessPatterns, JapaneseSuccessPatterns))
        {
            _outcome = GameTradeOutcome.Succeeded;
            Trace("Trade system message matched Success.");
        }
        else if (MatchesTradeSystemMessage(text, GermanCancelPatterns, EnglishCancelPatterns, FrenchCancelPatterns, JapaneseCancelPatterns))
        {
            _outcome = GameTradeOutcome.Cancelled;
            Trace("Trade system message matched Cancel.");
        }
    }

    private static Rx CreateSystemMessagePattern(string pattern)
        => new($"^{pattern}$", RxOptions.Compiled | RxOptions.CultureInvariant);

    private static bool MatchesTradeSystemMessage(string text, params Rx[][] patternSets)
        => patternSets.SelectMany(patternSet => patternSet).Any(pattern => pattern.IsMatch(text));

    private static void Trace(string message)
    {
#if DEBUG
        if (Plugin.Instance.Configuration.DebugTradeLogging)
            Plugin.Log.Debug($"[ModularTrade.Adapter] {message}");
#endif
    }

    private static AtkComponentButton* GetTradeButton(AtkUnitBase* addon)
    {
        if (addon->UldManager.NodeListCount <= 3 || addon->UldManager.NodeList[3] is null)
            return null;

        var componentNode = addon->UldManager.NodeList[3]->GetAsAtkComponentNode();
        return componentNode is null ? null : (AtkComponentButton*)componentNode->Component;
    }

    private static AtkUnitBase* FindTradeConfirmationDialog()
    {
        var expectedText = Svc.Data.GetExcelSheet<Addon>()?.GetRow(102223).Text.ToString();
        if (string.IsNullOrWhiteSpace(expectedText))
            return null;

        for (var index = 1; index < 100; index++)
        {
            var addonHandle = Plugin.GameGui.GetAddonByName("SelectYesno", index);
            var addon = (AtkUnitBase*)addonHandle.Address;
            if (addon is null)
                break;
            if (!IsAddonReady(addon) || addon->UldManager.NodeListCount <= 15 || addon->UldManager.NodeList[15] is null)
                continue;

            var textNode = addon->UldManager.NodeList[15]->GetAsAtkTextNode();
            if (textNode is null)
                continue;

            var text = textNode->NodeText.ToString();
            if (string.Equals(text, expectedText, StringComparison.Ordinal))
                return addon;
        }

        return null;
    }
}
