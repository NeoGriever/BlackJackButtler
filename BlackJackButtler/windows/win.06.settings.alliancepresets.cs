using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private const string StandardMessagePresetName = "Standard Message Preset";
    private const string FastMessagePresetName = "Fast Message Preset";
    private const int FastMessagePresetDefinitionVersion = 1;

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

        var snapshot = name.Equals(FastMessagePresetName, StringComparison.OrdinalIgnoreCase)
            ? CreateIntegratedFastMessagePresetSnapshot()
            : CreateSetupPresetSnapshot(useOriginalDefaults);
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
            BuiltInDefinitionVersion = name.Equals(FastMessagePresetName, StringComparison.OrdinalIgnoreCase)
                ? FastMessagePresetDefinitionVersion
                : 0,
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

    private JObject CreateIntegratedFastMessagePresetSnapshot()
    {
        try
        {
            using var stream = typeof(BlackJackButtlerWindow).Assembly
                .GetManifestResourceStream("BlackJackButtler.FastMessagePreset.json");
            if (stream == null)
                return CreateSetupPresetSnapshot(useOriginalDefaults: false);

            using var reader = new StreamReader(stream);
            var exports = JArray.Parse(reader.ReadToEnd());
            var preset = exports.OfType<JObject>().FirstOrDefault(entry =>
                string.Equals(entry["Name"]?.Value<string>(), FastMessagePresetName,
                    StringComparison.OrdinalIgnoreCase))
                ?? exports.OfType<JObject>().FirstOrDefault();
            var snapshot = preset?["Snapshot"] as JObject;
            if (snapshot != null)
                return (JObject)snapshot.DeepClone();
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Could not load the integrated Fast Message Preset.");
        }

        return CreateSetupPresetSnapshot(useOriginalDefaults: false);
    }

    private void EnsureDefaultPresetDefinitions()
    {
        var standard = _config.Presets.FirstOrDefault(p =>
            p.Name.Equals(StandardMessagePresetName, StringComparison.OrdinalIgnoreCase));
        var fast = _config.Presets.FirstOrDefault(p =>
            p.Name.Equals(FastMessagePresetName, StringComparison.OrdinalIgnoreCase));
        var changed = false;

        if (standard != null && fast != null
            && string.Equals(standard.SnapshotJson, fast.SnapshotJson, StringComparison.Ordinal))
        {
            standard.SnapshotJson = CreateSetupPresetSnapshot(useOriginalDefaults: true)
                .ToString(Formatting.None);
            standard.UpdatedAt = DateTime.UtcNow;
            changed = true;
        }

        if (fast != null && fast.BuiltInDefinitionVersion < FastMessagePresetDefinitionVersion)
        {
            fast.SnapshotJson = CreateIntegratedFastMessagePresetSnapshot().ToString(Formatting.None);
            fast.BuiltInDefinitionVersion = FastMessagePresetDefinitionVersion;
            fast.UpdatedAt = DateTime.UtcNow;
            changed = true;
        }

        if (!changed) return;
        PresetStorage.Save(_config.Presets);
        _save();
    }

    private void EnsureTripleDownPartsInDefaultPresets()
    {
        var defaults = new Configuration
        {
            CommandGroups = DefaultsManager.GetDefaultCommands(),
            MessageBatches = DefaultsManager.GetDefaultMessages(),
            UserRegexes = DefaultsManager.GetDefaultRegex(),
        };

        var commandNames = TripleDownStateSupport.PresetCommandNames;
        var messageNames = TripleDownStateSupport.PresetMessageNames;
        var changed = false;

        foreach (var preset in _config.Presets.Where(p =>
                     p.Name.Equals(StandardMessagePresetName, StringComparison.OrdinalIgnoreCase)
                     || p.Name.Equals(FastMessagePresetName, StringComparison.OrdinalIgnoreCase)))
        {
            Configuration snapshot;
            try
            {
                snapshot = JsonConvert.DeserializeObject<Configuration>(preset.SnapshotJson) ?? new Configuration();
            }
            catch
            {
                continue;
            }

            var presetChanged = false;
            foreach (var name in commandNames)
            {
                if (snapshot.CommandGroups.Any(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;
                var source = defaults.CommandGroups.FirstOrDefault(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (source == null) continue;
                snapshot.CommandGroups.Add(TripleDownStateSupport.Clone(source));
                presetChanged = true;
            }

            foreach (var name in messageNames)
            {
                if (snapshot.MessageBatches.Any(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;
                var source = defaults.MessageBatches.FirstOrDefault(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (source == null) continue;
                snapshot.MessageBatches.Add(TripleDownStateSupport.Clone(source));
                presetChanged = true;
            }

            if (!snapshot.UserRegexes.Any(r => r.Name.Equals("Triple Down", StringComparison.OrdinalIgnoreCase)))
            {
                var source = defaults.UserRegexes.FirstOrDefault(r => r.Name.Equals("Triple Down", StringComparison.OrdinalIgnoreCase));
                if (source != null)
                {
                    snapshot.UserRegexes.Add(TripleDownStateSupport.Clone(source));
                    presetChanged = true;
                }
            }

            if (!presetChanged) continue;
            preset.SnapshotJson = JsonConvert.SerializeObject(snapshot, Formatting.None);
            preset.UpdatedAt = DateTime.UtcNow;
            changed = true;
        }

        if (!changed) return;
        PresetStorage.Save(_config.Presets);
        _save();
    }
}
