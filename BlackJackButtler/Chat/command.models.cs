using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BlackJackButtler;

[Serializable]
public class PluginCommand
{
    public bool Enabled = true;
    public string Text = string.Empty;
    public float Delay = 0.5f;
    public int GroupId = 0;  // 0 = no group; same non-zero value = iterative/random group
}

[Serializable]
public class CommandLineGroup
{
    public SelectionMode Mode = SelectionMode.Iterative;
    public int IterativeIndex = 0;

    public PluginCommand? PickNext(List<PluginCommand> candidates)
    {
        var enabled = candidates.Where(c => c.Enabled && !string.IsNullOrWhiteSpace(c.Text)).ToList();
        if (enabled.Count == 0) return null;

        switch (Mode)
        {
            case SelectionMode.Iterative:
                if (IterativeIndex >= enabled.Count) IterativeIndex = 0;
                var cmd = enabled[IterativeIndex];
                IterativeIndex = (IterativeIndex + 1) % enabled.Count;
                return cmd;
            case SelectionMode.Random:
                return enabled[Random.Shared.Next(enabled.Count)];
            default:
                return enabled[0];
        }
    }
}

[Serializable]
public class CommandGroup
{
    public string Name = string.Empty;
    public List<PluginCommand> Commands = new();
    public Dictionary<int, CommandLineGroup> LineGroups = new();

    public bool UseCustomButtonColor = false;
    public bool UseCustomTextColor = false;
    public Vector4 CustomButtonColor = new(0.26f, 0.26f, 0.26f, 1.0f);
    public Vector4 CustomTextColor = new(1.0f, 1.0f, 1.0f, 1.0f);
}
