using System;

namespace BlackJackButtler.Regex;

// Das hat in meinem letzten Snippet gefehlt:
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
    TakeBatch
}

[Serializable]
public sealed class UserRegexEntry
{
    public bool Enabled = true;
    public RegexEntryMode Mode = RegexEntryMode.SetVariable;
    public RegexAction Action = RegexAction.None;
    public string ActionParam = "";
    public string Name = "new_variable"; // Wird für SetVariable genutzt
    public string Pattern = "";
    public bool CaseSensitive = false;
}
