using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private const string StandardMessagePresetName = "Standard Message Preset";
    private const string FastMessagePresetName = "Fast Message Preset";

    private bool HasPreset(string name) =>
        _config.Presets.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private bool HasAllDefaultPresets() =>
        HasPreset(StandardMessagePresetName) && HasPreset(FastMessagePresetName);

    private void CreateMissingDefaultPresets()
    {
        CreateSetupPreset(StandardMessagePresetName, useOriginalDefaults: true);
        CreateSetupPreset(FastMessagePresetName, useOriginalDefaults: false);
    }

    private void CreateSetupPreset(string name, bool useOriginalDefaults)
    {
        if (HasPreset(name))
            return;

        var snapshot = CreateSetupPresetSnapshot(useOriginalDefaults);
        var now = DateTime.UtcNow;
        var sortOrder = _config.Presets.Count == 0
            ? 0
            : _config.Presets.Max(p => p.SortOrder) + 1;

        _config.Presets.Add(new PresetEntry
        {
            Name = name,
            PresetId = Guid.NewGuid().ToString("N"),
            ApplySettings = false,
            ApplyCommands = true,
            ApplyMessages = true,
            ApplyRegexes = true,
            ApplyMessagesDefault = true,
            ApplyMessagesCustom = true,
            ApplyStandardCommands = true,
            ApplyOwnButtons = true,
            ApplySettingsGeneral = false,
            ApplySettingsAutomation = false,
            ApplySettingsRules = false,
            ApplySettingsBetting = false,
            ApplySettingsTimeDelay = false,
            ApplySettingsMessageSettings = false,
            ApplySettingsNearbyPlayers = false,
            ApplySettingsVisual = false,
            ApplySettingsSystem = false,
            ApplyDrawLogic = false,
            CommandsCheckboxMigrated = true,
            SettingsCategoryMigrated = true,
            MessagesCategoryMigrated = true,
            CreatedAt = now,
            UpdatedAt = now,
            SortOrder = sortOrder,
            SnapshotJson = snapshot.ToString(Formatting.None),
        });

        _config.PresetsMigrated = true;
        PresetStorage.Save(_config.Presets);
        _save();
    }

    private JObject CreateSetupPresetSnapshot(bool useOriginalDefaults)
    {
        var snapshotConfig = JsonConvert.DeserializeObject<Configuration>(
            JObject.FromObject(_config).ToString(Formatting.None)) ?? new Configuration();
        snapshotConfig.Presets.Clear();
        if (useOriginalDefaults)
            DefaultsMigration.SeedAllDefaults(snapshotConfig);

        var snapshot = JObject.FromObject(snapshotConfig);
        snapshot.Remove("Presets");
        return snapshot;
    }

    private void EnsureDefaultPresetDefinitions()
    {
        var standard = _config.Presets.FirstOrDefault(p =>
            p.Name.Equals(StandardMessagePresetName, StringComparison.OrdinalIgnoreCase));
        var fast = _config.Presets.FirstOrDefault(p =>
            p.Name.Equals(FastMessagePresetName, StringComparison.OrdinalIgnoreCase));
        var changed = false;

        foreach (var preset in new[] { standard, fast }.Where(preset => preset != null).Cast<PresetEntry>())
        {
            if (!preset.ApplyRegexes) { preset.ApplyRegexes = true; changed = true; }
            if (!preset.ApplyMessagesDefault) { preset.ApplyMessagesDefault = true; changed = true; }
            if (!preset.ApplyMessagesCustom) { preset.ApplyMessagesCustom = true; changed = true; }
            if (!preset.ApplyStandardCommands) { preset.ApplyStandardCommands = true; changed = true; }
            if (!preset.ApplyOwnButtons) { preset.ApplyOwnButtons = true; changed = true; }
            if (!preset.ApplyCommands) { preset.ApplyCommands = true; changed = true; }
            if (!preset.ApplyMessages) { preset.ApplyMessages = true; changed = true; }
        }

        if (standard != null && fast != null
            && string.Equals(standard.SnapshotJson, fast.SnapshotJson, StringComparison.Ordinal))
        {
            standard.SnapshotJson = CreateSetupPresetSnapshot(useOriginalDefaults: true)
                .ToString(Formatting.None);
            standard.UpdatedAt = DateTime.UtcNow;
            changed = true;
        }

        if (!changed) return;
        PresetStorage.Save(_config.Presets);
        _save();
    }
}
