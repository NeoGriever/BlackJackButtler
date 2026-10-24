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
    private void DrawNearbySoundSettings(string id)
    {
        if (_config.EnsureNearbyAlertSoundEntriesMigration()) _save();

        DrawSettingsToggle("Player entering area sound trigger", $"{id}_sound_enabled", ref _config.NearbyAlertEnabled);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Play a sound when a new player enters your nearby area.");

        ImGui.TextUnformatted("Volume");
        ImGui.SameLine(260f);
        ImGui.SetNextItemWidth(220f);
        if (BJBGui.SliderFloat($"##{id}_sound_volume", ref _config.NearbyAlertVolume, 0f, 100f, "%.0f%%"))
        {
            _config.NearbyAlertVolume = Math.Clamp(_config.NearbyAlertVolume, 0f, 100f);
            _save();
        }

        DrawNearbyCooldownEditor(id);

        ImGui.TextUnformatted("Mode");
        ImGui.SameLine(260f);
        var mode = (int)_config.NearbyAlertSoundMode;
        DrawEnumButtons(
            $"sound_mode_{id}",
            ref mode,
            new[] { "Iterative", "Random", "First only" },
            index =>
            {
                _config.NearbyAlertSoundMode = (NearbyAlertSoundMode)index;
                _save();
            }
        );

        Header("Files");
        DrawNearbySoundEntries(id);
    }

    private void DrawNearbyCooldownEditor(string id)
    {
        ImGui.TextUnformatted("Cooldown");
        ImGui.SameLine(260f);
        if (_nearbyCooldownEditMode)
        {
            ImGui.SetNextItemWidth(110f);
            ImGui.InputText($"##{id}_cooldown_input", ref _nearbyCooldownInput, 16, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.EnterReturnsTrue);
            var confirm = ImGui.IsItemDeactivatedAfterEdit() || BJBGui.SmallButton($"OK##{id}_cooldown_ok");
            if (confirm && float.TryParse(_nearbyCooldownInput, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                _config.NearbyAlertCooldown = Math.Clamp(value, 0.02f, 30f);
                _nearbyCooldownEditMode = false;
                _save();
            }
            ImGui.SameLine();
            if (BJBGui.SmallButton($"Cancel##{id}_cooldown_cancel")) _nearbyCooldownEditMode = false;
            return;
        }

        ImGui.SetNextItemWidth(220f);
        if (BJBGui.SliderFloat($"##{id}_sound_cooldown", ref _config.NearbyAlertCooldown, 0.02f, 30f, "%.2fs"))
        {
            _config.NearbyAlertCooldown = Math.Clamp(_config.NearbyAlertCooldown, 0.02f, 30f);
            _save();
        }
        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            _nearbyCooldownInput = _config.NearbyAlertCooldown.ToString("0.##", CultureInfo.InvariantCulture);
            _nearbyCooldownEditMode = true;
        }
    }

    private void DrawNearbySoundEntries(string id)
    {
        if (_config.EnsureNearbyAlertSoundEntriesMigration()) _save();

        var removeIndex = -1;
        for (var i = 0; i < _config.NearbyAlertSoundEntries.Count; i++)
        {
            var entry = _config.NearbyAlertSoundEntries[i];
            ImGui.PushID($"{id}_sound_{i}");
            if (BJBGui.SmallButton("X##delete")) removeIndex = i;
            ImGui.SameLine();
            if (ImGui.Checkbox("##enabled", ref entry.Enabled)) SaveNearbySoundEntries();
            ImGui.SameLine();
            ImGui.SetNextItemWidth(104f);
            if (BJBGui.SliderFloat("##volume", ref entry.Volume, 0f, 100f, "%.0f%%"))
            {
                entry.Volume = Math.Clamp(entry.Volume, 0f, 100f);
                SaveNearbySoundEntries();
            }
            ImGui.SameLine();
            ImGui.TextUnformatted(entry.Path);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(entry.Path);
            ImGui.PopID();
        }

        if (removeIndex >= 0)
        {
            _config.NearbyAlertSoundEntries.RemoveAt(removeIndex);
            SaveNearbySoundEntries();
        }

        if (BJBGui.SmallButton($"Select file ...##{id}_add_sound"))
        {
            _fileDialogManager.OpenFileDialog(
                "Select Sound File",
                "Audio{.wav,.mp3,.ogg}",
                (ok, path) =>
                {
                    if (!ok || string.IsNullOrWhiteSpace(path)
                        || _config.NearbyAlertSoundEntries.Any(entry => entry.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
                        return;
                    _config.NearbyAlertSoundEntries.Add(new NearbyAlertSoundEntry { Path = path, Enabled = true, Volume = 100f });
                    SaveNearbySoundEntries();
                }
            );
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton($"Test##{id}_test_sound"))
        {
            var played = NearbyAlertManager.PlayTestSound(_config, out var status);
            _nearbySoundTestStatus = status;
            _nearbySoundTestFailed = !played;
        }
        if (!string.IsNullOrEmpty(_nearbySoundTestStatus))
        {
            ImGui.SameLine();
            ImGui.TextColored(
                _nearbySoundTestFailed ? new Vector4(1f, 0.4f, 0.35f, 1f) : new Vector4(0.45f, 0.9f, 0.5f, 1f),
                _nearbySoundTestStatus
            );
        }
    }

    private void SaveNearbySoundEntries()
    {
        _config.NearbyAlertSoundEntriesMigrated = true;
        _config.SyncLegacyNearbyAlertSoundFiles();
        _save();
    }
}
