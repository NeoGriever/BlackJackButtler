using System;
using System.Numerics;
using System.Linq;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawMessagesPage()
    {
        ImGui.TextUnformatted("Message Batches");
        ImGui.Separator();

        var hideStd = _config.HideStandardBatches;
        if (ImGui.Checkbox("Hide standard batches", ref hideStd)) { _config.HideStandardBatches = hideStd; _save(); }

        ImGui.SameLine();
        var io = ImGui.GetIO();
        bool keysDown = io.KeyCtrl && io.KeyShift;

        if (!keysDown) ImGui.BeginDisabled();
        if (keysDown) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0f, 0f, 1f));

        if (ImGui.Button("Hard Reset Standard Batches##hard_reset"))
        {
            _openForceDefaultsPopup = true;
            ImGui.OpenPopup("bjb.restore.confirm");
        }

        if (keysDown) ImGui.PopStyleColor();
        if (!keysDown)
        {
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Hold CTRL + SHIFT to unlock this button.");
        }

        if (ImGui.BeginPopupModal("bjb.restore.confirm", ref _openForceDefaultsPopup, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextColored(new Vector4(1, 0, 0, 1), "WARNING: HARD RESET");
            ImGui.TextUnformatted("This will delete all standard batches and recreate them.");
            ImGui.Spacing();

            if (ImGui.Button("Yes, do it", new Vector2(120, 0)))
            {
                _config.ForceResetStandardBatches();
                _save();
                _openForceDefaultsPopup = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                _openForceDefaultsPopup = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        if (ImGui.Button("+ New Batch")) { _config.MessageBatches.Add(new MessageBatch()); _save(); }

        for (int i = 0; i < _config.MessageBatches.Count; i++)
        {
            var batch = _config.MessageBatches[i];
            bool isStd = IsStandardBatch(batch.Name);
            if (isStd && _config.HideStandardBatches) continue;

            ImGui.PushID(i);
            if (isStd) ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.1f, 0.3f, 0.1f, 1f));

            bool open = ImGui.CollapsingHeader(isStd ? $"● {batch.Name}" : batch.Name);

            ImGui.SameLine(ImGui.GetContentRegionAvail().X - 120);
            ImGui.SetNextItemWidth(120);
            int mode = (int)batch.Mode;
            if (ImGui.Combo($"##mode_{batch.Name}", ref mode, "Random\0First\0Iterative\0")) { batch.Mode = (SelectionMode)mode; _save(); }

            if (isStd) ImGui.PopStyleColor();

            if (open)
            {
                if (!isStd) { var n = batch.Name; if (ImGui.InputText("Batch Name", ref n, 64)) { batch.Name = n; _save(); } }

                for (int m = 0; m < batch.Messages.Count; m++)
                {
                    var msg = batch.Messages[m];
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 40);
                    if (ImGui.InputText($"##msg_{batch.Name}_{m}", ref msg, 256)) { batch.Messages[m] = msg; _save(); }
                    ImGui.SameLine();
                    if (ImGui.Button($"X##{batch.Name}_{m}")) { batch.Messages.RemoveAt(m); _save(); break; }
                }
                if (ImGui.Button("+ Line")) { batch.Messages.Add(""); _save(); }

                if (!isStd)
                {
                    ImGui.Spacing();
                    ImGui.Separator();

                    if (io.KeyCtrl)
                    {
                        if (ImGui.Button("Delete (Hold CTRL)", new Vector2(-1, 0)))
                        {
                            _config.MessageBatches.RemoveAt(i);
                            _save();
                            ImGui.PopID();
                            break;
                        }
                    }
                    else
                    {
                        ImGui.BeginDisabled();
                        ImGui.Button("Delete (Hold CTRL)", new Vector2(-1, 0));
                        ImGui.EndDisabled();
                    }
                }
            }

            ImGui.PopID();
        }
    }

    private bool IsStandardBatch(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return Configuration.StandardBatchNames.Contains(name, StringComparer.OrdinalIgnoreCase);
    }
}
