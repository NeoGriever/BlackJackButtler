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
    private void DrawCommandSelector(string label, ref string value)
    {
        ImGui.PushID(label);
        var idSeparator = label.IndexOf("##", StringComparison.Ordinal);
        var visibleLabel = idSeparator < 0 ? label : label[..idSeparator];
        ImGui.TextUnformatted(visibleLabel);
        ImGui.SameLine(260f);
        var commands = _config.CommandGroups.Select(g => "Commands/" + g.Name)
            .Concat(_config.CustomCommandGroups.Select(g => "Custom Buttons/" + g.Name))
            .Append("Actions/Payout")
            .ToArray();
        var labels = new[] { "None" }.Concat(commands).ToArray();
        int selected = 0;
        for (int i = 0; i < commands.Length; i++)
        {
            var raw = commands[i].Split('/').Last();
            if (raw.Equals(value, StringComparison.OrdinalIgnoreCase)) selected = i + 1;
        }
        ImGui.SetNextItemWidth(260f);
        if (BJBGui.Combo("##v2_command_selector", ref selected, labels, labels.Length))
        {
            value = selected == 0 ? string.Empty : commands[selected - 1].Split('/').Last();
            _save();
        }
        ImGui.PopID();
    }
}
