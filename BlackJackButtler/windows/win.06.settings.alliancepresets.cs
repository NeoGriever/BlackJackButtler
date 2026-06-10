using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private const string AllianceInviteGroupName = "Alliance Invite";
    private const string StandardMessagePresetName = "Standard Message Preset";
    private const string FastMessagePresetName = "Fast Message Preset";

    private void DrawSettingsTab_Alliance()
    {
        if (!ImGui.BeginTabItem("Alliance"))
            return;

        ImGui.Spacing();
        DrawSettingsAllianceBody();
        ImGui.EndTabItem();
    }

    private void DrawSettingsAllianceBody()
    {
        ImGui.TextWrapped(
            "Alliance detection and runtime routing are always active. Party chat and dice commands are routed once per command chain.");
        ImGui.Spacing();

        DrawCommandSelector("Alliance Nearby J Command", ref _config.AllianceNearbyCommandName);

        ImGui.Spacing();
        if (BJBGui.Button("Create Alliance Invite Button"))
        {
            EnsureAllianceInviteGroup();
            _page = Page.Commands;
            _pendingCommandsTab = "OwnButtons";
            _pendingOwnButtonGroupName = AllianceInviteGroupName;
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(
                "Creates a hidden Alliance Invite command once and opens it under Commands > Own Buttons.");
    }

    private void EnsureAllianceInviteGroup()
    {
        var existing = _config.CustomCommandGroups.FirstOrDefault(g =>
            g.Name.Equals(AllianceInviteGroupName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            return;

        _config.CustomCommandGroups.Add(new CommandGroup
        {
            Name = AllianceInviteGroupName,
            IsActive = true,
            IsVisible = false,
            Commands =
            {
                new PluginCommand
                {
                    Text = "/tell <.> You can join through the PF. Use the code 2121",
                    Delay = 0.5f,
                    FixedDelay = true,
                },
            },
        });
        _save();
    }

    private void DrawSettingsTab_PresetSetup()
    {
        if (!ImGui.BeginTabItem("Preset Setup"))
            return;

        ImGui.Spacing();
        DrawSettingsPresetSetupBody();
        ImGui.EndTabItem();
    }

    private void DrawSettingsPresetSetupBody()
    {
        ImGui.TextWrapped(
            "Creates reusable presets from the current configuration. Existing presets with the same name are kept unchanged.");
        ImGui.Spacing();

        var hasStandard = HasPreset(StandardMessagePresetName);
        if (hasStandard) ImGui.BeginDisabled();
        if (BJBGui.Button("Create Standard Message Preset"))
            CreateSetupPreset(StandardMessagePresetName, fast: false);
        if (hasStandard) ImGui.EndDisabled();

        ImGui.SameLine();
        var hasFast = HasPreset(FastMessagePresetName);
        if (hasFast) ImGui.BeginDisabled();
        if (BJBGui.Button("Create Fast Message Preset"))
            CreateSetupPreset(FastMessagePresetName, fast: true);
        if (hasFast) ImGui.EndDisabled();
    }

    private bool HasPreset(string name) =>
        _config.Presets.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private void CreateSetupPreset(string name, bool fast)
    {
        if (HasPreset(name))
            return;

        var snapshot = JObject.FromObject(_config);
        snapshot.Remove("Presets");
        var now = DateTime.UtcNow;
        var sortOrder = _config.Presets.Count == 0
            ? 0
            : _config.Presets.Max(p => p.SortOrder) + 1;

        _config.Presets.Add(new PresetEntry
        {
            Name = name,
            PresetId = Guid.NewGuid().ToString("N"),
            ApplySettings = false,
            ApplyCommands = fast,
            ApplyMessages = true,
            ApplyRegexes = fast,
            ApplyMessagesDefault = true,
            ApplyMessagesCustom = true,
            ApplyStandardCommands = fast,
            ApplyOwnButtons = fast,
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
}
