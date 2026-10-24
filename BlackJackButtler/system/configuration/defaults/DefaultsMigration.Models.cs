using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BlackJackButtler.Regex;
using Newtonsoft.Json;

namespace BlackJackButtler;

public static partial class DefaultsMigration
{
    [Serializable]
    public class SnapshotCommandDto
    {
        public string Text { get; set; } = "";
        public float Delay { get; set; }
    }

    [Serializable]
    public class SnapshotRegexDto
    {
        public string Name { get; set; } = "";
        public List<string> Patterns { get; set; } = new();
        public string Action { get; set; } = "";
    }
}
