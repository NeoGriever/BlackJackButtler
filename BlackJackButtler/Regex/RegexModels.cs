using System;

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
    PartyJoin,
    PartyLeave,
    PartyDisband
}

[Serializable]
public sealed class UserRegexEntry
{
    public bool Enabled = true;
    public RegexEntryMode Mode = RegexEntryMode.SetVariable;
    public RegexAction Action = RegexAction.None;
    public string ActionParam = "";
    public string Name = "new_variable";
    public string Pattern = "";
    public bool CaseSensitive = false;
}
