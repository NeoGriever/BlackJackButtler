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
}
