using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawPlayerSoundSettings()
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("Player chat sound effects");
        if (ImGui.Checkbox("Enable player sound effects##player_sounds", ref _config.EnablePlayerSoundEffects))
            _save();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Append each player's allowed <se.N> tag to group chat messages addressed to them.");

        EnsureAllowedPlayerSoundEffects();
        if (!ImGui.CollapsingHeader("Available sounds##player_sound_list", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        for (var soundId = 1; soundId <= PlayerSoundEffects.MaxSoundId; soundId++)
        {
            if (soundId > 1 && (soundId - 1) % 4 != 0)
                ImGui.SameLine();

            var allowedIndex = soundId - 1;
            var allowed = _config.AllowedPlayerSoundEffects[allowedIndex];
            ImGui.PushID(soundId);
            if (ImGui.Checkbox("##player_sound_allowed", ref allowed))
            {
                _config.AllowedPlayerSoundEffects[allowedIndex] = allowed;
                _save();
            }

            ImGui.SameLine();
            var numberWidth = ImGui.CalcTextSize("16").X;
            var numberStart = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(numberWidth, ImGui.GetFrameHeight()));
            ImGui.GetWindowDrawList().AddText(new Vector2(numberStart.X + numberWidth - ImGui.CalcTextSize(soundId.ToString()).X, numberStart.Y + (ImGui.GetFrameHeight() - ImGui.GetTextLineHeight()) * 0.5f), ImGui.GetColorU32(ImGuiCol.Text), soundId.ToString());
            ImGui.SameLine();
            if (ImGui.SmallButton("Play##player_sound_preview"))
                PlayerSoundEffects.Preview(soundId);
            ImGui.PopID();
        }
    }

    private void EnsureAllowedPlayerSoundEffects()
    {
        var current = _config.AllowedPlayerSoundEffects;
        if (current != null && current.Length >= PlayerSoundEffects.MaxSoundId)
            return;

        var normalized = Enumerable.Repeat(true, PlayerSoundEffects.MaxSoundId).ToArray();
        if (current != null)
            System.Array.Copy(current, normalized, current.Length);
        _config.AllowedPlayerSoundEffects = normalized;
        _save();
    }
}
