using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace BlackJackButtler.Regex;

public enum RegexEntryMode
{
    SetVariable,
    Trigger
}

public enum RegexAction
{
    None,
    BetInformationChange,
    WantHit,
    WantStand,
    WantDD,
    WantSplit,
    BankOut,
    TradePartner,
    TradeGilIn,
    TradeGilOut,
    TradeCommit,
    TradeCancel,
    TakeBatch,
    DiceRollValue,
    HighlightBet,
    HighlightPayout,
    HighlightAlias,
    HighlightPause,
    HighlightLeave,
    HighlightJoin,
    HighlightHit,
    HighlightStand,
    HighlightDD,
    HighlightSplit,
    NextRound,
    BankTell,
    ExecuteOwnButton,
    SetBet,
    InviteNearby,
    Payout,
    Withdraw,
}

[Flags]
public enum RegexChatSource
{
    Party = 1 << 0,
    Tell = 1 << 1,
    Say = 1 << 2,
    System = 1 << 3,
}

[Serializable]
public sealed class UserRegexEntry
{
    public bool Enabled = true;
    public RegexEntryMode Mode = RegexEntryMode.SetVariable;
    public RegexAction Action = RegexAction.None;
    public string ActionParam = "";
    public string Name = "new_entry";
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<string> Patterns = new() { "" };
    public bool CaseSensitive = false;
    public bool ApplyToTells = false;
    public RegexChatSource Sources = RegexChatSource.Party;
}
