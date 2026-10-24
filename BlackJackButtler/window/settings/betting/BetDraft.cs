using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using BlackJackButtler.Chat;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private static string GetDefaultVipName(int level) => level <= 0 ? "VIP" : $"VIP {level}";

    private void EnsureBetDraft()
    {
        if (_betDraftEntries != null) return;
        if (_config.EnsureBetLimitEntriesMigration()) _save();
        var source = _config.BetLimitEntries.Count > 0 ? _config.BetLimitEntries : BuildBetEntriesFromLegacy();
        _betDraftEntries = source.Select(CloneBetEntry).ToList();
    }

    private List<BetLimitEntry> BuildBetEntriesFromLegacy()
    {
        var list = new List<BetLimitEntry>
        {
            new() { Active = true, Kind = BetLimitEntryKind.MinBet, Name = "Min", Amount = _config.MinBet },
            new() { Active = true, Kind = BetLimitEntryKind.Normal, Name = "Max", Amount = _config.MaxBet }
        };
        for (int i = 0; i < _config.VipBetTiers.Count && i < 9; i++)
            list.Add(new BetLimitEntry { Active = true, Kind = BetLimitEntryKind.Vip, VipLevel = i + 1, Name = _config.VipBetTiers[i].Name, Amount = _config.VipBetTiers[i].MaxBet });
        return list;
    }

    private static BetLimitEntry CloneBetEntry(BetLimitEntry e) => new()
    {
        Active = e.Active,
        Kind = e.Kind,
        VipLevel = e.VipLevel,
        Name = e.Name,
        Amount = e.Amount,
    };

}
