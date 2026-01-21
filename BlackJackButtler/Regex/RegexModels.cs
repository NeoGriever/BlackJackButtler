using System;

namespace BlackJackButtler.Regex;

public enum RegexEntryMode
{
  SetVariable,
  Reaction, // später
}

[Serializable]
public sealed class UserRegexEntry
{
  public bool Enabled { get; set; } = true;
  public RegexEntryMode Mode { get; set; } = RegexEntryMode.SetVariable;

  public string Name { get; set; } = "card";          // Variablenname (für SetVariable)
  public string Pattern { get; set; } = "";           // Regex
  public bool CaseSensitive { get; set; } = false;

  // später:
  // public string TargetBatchId { get; set; } = "";
  // public int DelayTenths { get; set; } = 0;
  // public bool Unique { get; set; } = true;
}
