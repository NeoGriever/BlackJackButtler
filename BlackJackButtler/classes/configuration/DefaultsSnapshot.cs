using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlackJackButtler.Regex;
using Newtonsoft.Json;

namespace BlackJackButtler;

[Serializable]
public class DefaultsSnapshot
{
    public string Version { get; set; } = "";
    public Dictionary<string, List<string>> Messages { get; set; } = new();
    public Dictionary<string, List<DefaultsMigration.SnapshotCommandDto>> Commands { get; set; } = new();
    public List<DefaultsMigration.SnapshotRegexDto> Regex { get; set; } = new();
}
