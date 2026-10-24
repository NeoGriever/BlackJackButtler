using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using BlackJackButtler.Chat;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawNearbySettingsWindow()
    {
        if (!_showNearbySettingsWindow) return;

        ImGui.SetNextWindowSize(new Vector2(420f, 0f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Nearby Settings###bjb_nearby_settings", ref _showNearbySettingsWindow, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse))
        {
            ImGui.End();
            return;
        }

        ImGui.TextUnformatted("Range");
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 8f);
        if (_nearbyDistSliderEditMode)
        {
            if (ImGui.InputFloat("##dist_input", ref _config.NearbyDistanceCap, 0f, 0f, "%.2f yalms", ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll))
            {
                _config.NearbyDistanceCap = MathF.Max(0f, _config.NearbyDistanceCap);
                _save();
                NearbyPlayersManager.InvalidateCache();
                _nearbyDistSliderEditMode = false;
            }
            if (!ImGui.IsItemActive() && !ImGui.IsItemFocused())
                _nearbyDistSliderEditMode = false;
        }
        else
        {
            float sliderVal = Math.Min(_config.NearbyDistanceCap, 99.99f);
            if (BJBGui.SliderFloat("##nearby_dist_slider", ref sliderVal, 0f, 99.99f, "%.2f yalms"))
            {
                _config.NearbyDistanceCap = sliderVal;
                _save();
                NearbyPlayersManager.InvalidateCache();
            }
            _distSliderHovered = ImGui.IsItemHovered();
            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                _nearbyDistSliderEditMode = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Range 0.00–99.99 yalms.\nDouble-click to enter any value.");
        }

        ImGui.Separator();
        DrawNearbyOnOff("Foot Numbers", "nearby_foot_numbers", ref _config.NearbyShowFootNumbers);
        DrawNearbyOnOff("Always Show Range", "nearby_always_show_circle", ref _config.NearbyAlwaysShowCircle);
        DrawPartyNearbyJCommandSelector();
        DrawCommandSelector("Alliance Nearby J Command", ref _config.AllianceNearbyCommandName);

        ImGui.Separator();
        ImGui.TextUnformatted("Area Shape");
        ImGui.SameLine(160f);
        var circle = _config.NearbyShape == NearbyShapeMode.Circle;
        if (BJBOnOffSwitch.Draw("nearby_shape", ref circle, "Circle", "Rect", 52f))
        {
            _config.NearbyShape = circle ? NearbyShapeMode.Circle : NearbyShapeMode.Rectangle;
            _save();
            NearbyPlayersManager.InvalidateCache();
        }

        DrawNearbyOffsetInput("Offset X##nearby_offset_x", ref _config.NearbyOffsetX, "nearby_offset_x_reset");
        DrawNearbyOffsetInput("Offset Z##nearby_offset_z", ref _config.NearbyOffsetZ, "nearby_offset_z_reset");

        if (_config.NearbyShape == NearbyShapeMode.Rectangle)
        {
            ImGui.SetNextItemWidth(130f);
            if (BJBGui.DragFloat("Aspect ratio##nearby_rect_aspect", ref _config.NearbyRectangleAspectRatio, 0.01f, 0.1f, 10f, "%.2f"))
            {
                _config.NearbyRectangleAspectRatio = Math.Clamp(_config.NearbyRectangleAspectRatio, 0.1f, 10f);
                _save();
                NearbyPlayersManager.InvalidateCache();
            }
            ImGui.SameLine();
            if (BJBGui.SmallButton("Reset##nearby_rect_aspect_reset"))
            {
                _config.NearbyRectangleAspectRatio = 1f;
                _save();
                NearbyPlayersManager.InvalidateCache();
            }

            ImGui.SetNextItemWidth(130f);
            if (BJBGui.DragFloat("Rotation##nearby_rect_rotation", ref _config.NearbyRectangleRotation, 0.5f, -180f, 180f, "%.1f deg"))
            {
                _config.NearbyRectangleRotation = Math.Clamp(_config.NearbyRectangleRotation, -180f, 180f);
                _save();
                NearbyPlayersManager.InvalidateCache();
            }
            ImGui.SameLine();
            if (BJBGui.SmallButton("Reset##nearby_rect_rotation_reset"))
            {
                _config.NearbyRectangleRotation = 0f;
                _save();
                NearbyPlayersManager.InvalidateCache();
            }
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Fixed World Position");
        ImGui.SameLine(160f);
        bool fixedPosition = _config.NearbyUseFixedPosition;
        if (BJBOnOffSwitch.Draw("nearby_fixed_position", ref fixedPosition, 38f))
        {
            _config.NearbyUseFixedPosition = fixedPosition;
            if (fixedPosition)
                NearbyPlayersManager.CaptureFixedCenter(_config);
            _save();
            NearbyPlayersManager.InvalidateCache();
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton("Capture##nearby_fixed_capture"))
        {
            NearbyPlayersManager.CaptureFixedCenter(_config);
            _save();
            NearbyPlayersManager.InvalidateCache();
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton("Reset##nearby_fixed_reset"))
        {
            _config.NearbyFixedCenterCaptured = false;
            _config.NearbyFixedCenterX = 0f;
            _config.NearbyFixedCenterY = 0f;
            _config.NearbyFixedCenterZ = 0f;
            _save();
            NearbyPlayersManager.InvalidateCache();
        }

        ImGui.Separator();
        DrawNearbyAutoActSettings();

        ImGui.End();
        DrawNearbyIgnoreWindow();
    }

    private void DrawNearbyOffsetInput(string label, ref float value, string resetId)
    {
        ImGui.SetNextItemWidth(130f);
        if (BJBGui.DragFloat(label, ref value, 0.05f, -100f, 100f, "%.2f"))
        {
            _save();
            NearbyPlayersManager.InvalidateCache();
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton($"Reset##{resetId}"))
        {
            value = 0f;
            _save();
            NearbyPlayersManager.InvalidateCache();
        }
    }

    private void DrawNearbyAutoActSettings()
    {
        ImGui.TextUnformatted("Auto Act");
        ImGui.SameLine(160f);
        if (BJBOnOffSwitch.Draw("nearby_auto_act", ref _config.NearbyAutoActEnabled, 38f)) _save();
        ImGui.SameLine();
        DrawNearbyAutoActCommandSelector();

        ImGui.TextUnformatted("Timeout");
        ImGui.SameLine(160f);
        ImGui.SetNextItemWidth(120f);
        if (BJBGui.DragFloat("##nearby_autoact_timeout", ref _config.NearbyAutoActTimeoutMinutes, 1f, 1f, 1440f, "%.0f"))
        {
            _config.NearbyAutoActTimeoutMinutes = MathF.Round(Math.Clamp(_config.NearbyAutoActTimeoutMinutes, 1f, 1440f));
            _save();
        }
        ImGui.SameLine();
        ImGui.TextUnformatted("Minutes");
        ImGui.SameLine();
        if (BJBGui.SmallButton("Reset##nearby_autoact_timeout_reset"))
        {
            _config.NearbyAutoActTimeoutMinutes = 120f;
            _save();
        }

        ImGui.TextUnformatted("Ignore list");
        ImGui.SameLine();
        if (BJBGui.SmallButton("Edit##nearby_autoact_ignore_edit"))
            _showNearbyIgnoreWindow = true;
        ImGui.TextDisabled($"{_config.NearbyAutoActIgnoreList.Count} entries");
    }

    private void DrawNearbyOnOff(string label, string id, ref bool value)
    {
        ImGui.TextUnformatted(label);
        ImGui.SameLine(160f);
        if (BJBOnOffSwitch.Draw(id, ref value, 38f)) _save();
    }

    private void DrawPartyNearbyJCommandSelector(float labelColumn = 160f)
    {
        ImGui.TextUnformatted("Party Nearby J Command");
        ImGui.SameLine(labelColumn);
        var selected = (int)_config.PartyNearbyJCommand;
        ImGui.SetNextItemWidth(Math.Max(120f, ImGui.GetContentRegionAvail().X));
        if (!BJBGui.Combo("##party_nearby_j_command", ref selected, "None\0Party Invite\0")) return;
        _config.PartyNearbyJCommand = (PartyNearbyJCommandMode)selected;
        if (_config.PartyNearbyJCommand == PartyNearbyJCommandMode.None)
            JoinQueueManager.Clear();
        _save();
    }

    private void DrawNearbyAutoActCommandSelector()
    {
        var commands = _config.CommandGroups.Select(group => "Commands/" + group.Name)
            .Concat(_config.CustomCommandGroups.Select(group => "Custom Buttons/" + group.Name))
            .Append("Actions/Payout")
            .ToArray();
        var labels = new[] { "None" }.Concat(commands).ToArray();
        var selected = 0;
        for (var i = 0; i < commands.Length; i++)
        {
            var raw = commands[i].Split('/').Last();
            if (raw.Equals(_config.NearbyAutoActCommandName, StringComparison.OrdinalIgnoreCase))
                selected = i + 1;
        }

        ImGui.SetNextItemWidth(Math.Max(110f, ImGui.GetContentRegionAvail().X));
        if (!BJBGui.Combo("##nearby_autoact_command", ref selected, labels, labels.Length)) return;
        _config.NearbyAutoActCommandName = selected == 0 ? string.Empty : commands[selected - 1].Split('/').Last();
        _save();
    }
}
