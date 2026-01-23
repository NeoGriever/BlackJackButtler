using System;

namespace BlackJackButtler;

[Serializable]
public class PluginCommand
{
    public bool Enabled = true;
    public string Text = string.Empty;
    public float Delay = 0.5f;
}

[Serializable]
public class CommandGroup
{
    public string Name = string.Empty;
    public System.Collections.Generic.List<PluginCommand> Commands = new();
}
