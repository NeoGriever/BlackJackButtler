using System;
using System.Numerics;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Game.ClientState.Objects.SubKinds;
using System.Threading.Tasks;
using BlackJackButtler.Chat;
using BlackJackButtler.Regex;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawRotationButton(string id, Vector2 size)
    {
        var savedDegrees = NormalizeRotationDegrees(_config.InitialViewDirection);
        if (BJBGui.Button($"{MathF.Round(savedDegrees):0}°##rotation_{id}", size))
        {
            var current = Plugin.ObjectTable.LocalPlayer?.Rotation ?? _config.InitialViewDirection;
            _rotationEditDegrees = NormalizeRotationDegrees(current);
            ImGui.OpenPopup($"rotation_editor_{id}");
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Configure saved facing rotation");

        if (!ImGui.BeginPopup($"rotation_editor_{id}")) return;
        ImGui.TextUnformatted("Rotation");
        ImGui.SetNextItemWidth(280f);
        ImGui.SliderFloat("Current rotation##edit", ref _rotationEditDegrees, 0f, 360f, "%.0f°");
        ImGui.TextDisabled("Saved rotation");
        var readOnlyDegrees = NormalizeRotationDegrees(_config.InitialViewDirection);
        ImGui.BeginDisabled();
        ImGui.SetNextItemWidth(280f);
        ImGui.SliderFloat("##saved", ref readOnlyDegrees, 0f, 360f, "%.0f°");
        ImGui.EndDisabled();
        if (BJBGui.Button("OK##rotation_ok"))
        {
            _config.InitialViewDirection = NormalizeRotationDegrees(_rotationEditDegrees) * (MathF.PI / 180f);
            _save();
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (BJBGui.Button("Cancel##rotation_cancel")) ImGui.CloseCurrentPopup();
        ImGui.EndPopup();
    }

    private static float NormalizeRotationDegrees(float radians)
    {
        var degrees = radians * (180f / MathF.PI);
        degrees %= 360f;
        if (degrees < 0f) degrees += 360f;
        return degrees;
    }

    private void DrawProminentPanicButton(string id)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.55f, 0.0f, 0.0f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.75f, 0.0f, 0.0f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.40f, 0.0f, 0.0f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 1f, 0f, 1f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 2.0f);

        if (ImGui.Button($"PANIC##panic_full_{id}", new Vector2(-1f, 0)))
            _panicConfirmStage = 1;

        ImGui.PopStyleVar();
        ImGui.PopStyleColor(5);
    }
}
