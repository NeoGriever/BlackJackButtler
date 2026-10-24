using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BlackJackButtler.Regex;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawPaddingFields(string idSuffix, ref float paddingH, ref float paddingV)
    {
        bool synced = Math.Abs(paddingH - paddingV) < 0.001f;

        ImGui.TextUnformatted("All");
        ImGui.SameLine(300f);
        ImGui.SetNextItemWidth(200f);
        if (!synced) ImGui.BeginDisabled();
        float allVal = synced ? paddingH : 0f;
        string allDisplay = synced ? "%.1f" : "—";
        if (ImGui.DragFloat($"##pad_all{idSuffix}", ref allVal, 0.5f, 0f, 50f, allDisplay))
        {
            allVal = Math.Clamp(allVal, 0f, 50f);
            paddingH = allVal;
            paddingV = allVal;
            _save();
        }
        if (!synced) ImGui.EndDisabled();

        ImGui.Spacing();
        ImGui.TextUnformatted("Horizontal");
        ImGui.SameLine(300f);
        ImGui.SetNextItemWidth(200f);
        if (ImGui.DragFloat($"##pad_h{idSuffix}", ref paddingH, 0.5f, 0f, 50f, "%.1f"))
        {
            paddingH = Math.Clamp(paddingH, 0f, 50f);
            _save();
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Vertical");
        ImGui.SameLine(300f);
        ImGui.SetNextItemWidth(200f);
        if (ImGui.DragFloat($"##pad_v{idSuffix}", ref paddingV, 0.5f, 0f, 50f, "%.1f"))
        {
            paddingV = Math.Clamp(paddingV, 0f, 50f);
            _save();
        }
    }

}
