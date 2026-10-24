using System;
using System.Linq;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private int _moneyRingOptionsFrame = -1;
    private PieAmountOption[] _moneyRingOptions = Array.Empty<PieAmountOption>();

    private PieAmountOption[] GetMoneyRingOptions()
    {
        var frame = ImGui.GetFrameCount();
        if (_moneyRingOptionsFrame == frame) return _moneyRingOptions;
        _moneyRingOptionsFrame = frame;
        _moneyRingOptions = !_config.MoneyRingEnabled || _config.MoneyRingEntries == null
            ? Array.Empty<PieAmountOption>()
            : _config.MoneyRingEntries
                .Where(entry => entry != null && entry.Enabled && entry.Amount > 0)
                .Select(entry => new PieAmountOption(entry.Amount, string.IsNullOrWhiteSpace(entry.Label) ? $"{entry.Amount:N0}" : entry.Label.Trim()))
                .ToArray();
        return _moneyRingOptions;
    }
}
