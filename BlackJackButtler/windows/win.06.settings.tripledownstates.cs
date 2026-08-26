using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json;

namespace BlackJackButtler.Windows;

internal static class TripleDownStateSupport
{
    internal readonly record struct StateDefinition(string Name, string Prompt);

    internal static readonly StateDefinition[] States =
    {
        new("StateHSDTS", "[Hit] [Stand] [Double Down] [Triple Down] [Split]"),
        new("StateHSTS", "[Hit] [Stand] [Triple Down] [Split]"),
        new("StateHSDT", "[Hit] [Stand] [Double Down] [Triple Down]"),
        new("StateHST", "[Hit] [Stand] [Triple Down]"),
    };

    internal static readonly string[] PresetCommandNames = States.Select(state => state.Name)
        .Concat(new[] { "TD", "PlayerTDForcedStand" }).ToArray();

    internal static readonly string[] PresetMessageNames =
    {
        "Player State Messages HSDTS", "Player State Messages HSTS",
        "Player State Messages HSDT", "Player State Messages HST",
        "Player TD Messages", "Player TD First Card Messages",
        "Player TD Messages Stand", "Player TD Forced Stand Messages",
    };

    internal static T Clone<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value))!;
}

public partial class BlackJackButtlerWindow
{
    private string _tripleDownMissingStateSignature = string.Empty;
    private bool _tripleDownStateRepairSucceeded;
    private bool _tripleDownStateAdaptFailed;

    private List<TripleDownStateSupport.StateDefinition> GetMissingTripleDownStates() =>
        TripleDownStateSupport.States
            .Where(state => !_config.CommandGroups.Any(group =>
                group.Name.Equals(state.Name, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    private void DrawTripleDownStateRepairNotice()
    {
        if (!_config.EnableTripleDown) return;

        var missing = GetMissingTripleDownStates();
        var signature = string.Join("|", missing.Select(state => state.Name));
        if (!string.Equals(signature, _tripleDownMissingStateSignature, StringComparison.Ordinal))
        {
            _tripleDownMissingStateSignature = signature;
            // Preserve the neutral, disabled success state after this window
            // created the final missing entry. Any later missing state resets it.
            if (missing.Count > 0)
            {
                _tripleDownStateRepairSucceeded = false;
                _tripleDownStateAdaptFailed = false;
            }
        }
        if (missing.Count == 0 && !_tripleDownStateRepairSucceeded) return;

        var needsRepair = missing.Count > 0;
        if (needsRepair) ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.78f, 0.62f, 0.08f, 0.38f));
        ImGui.BeginChild("triple_down_missing_states", new Vector2(0f, 0f), true);
        if (needsRepair)
            ImGui.TextColored(new Vector4(1f, 0.2f, 0.2f, 1f), "Missing state parts.");
        else
            ImGui.TextUnformatted("Triple Down state parts are ready.");

        if (!needsRepair) ImGui.BeginDisabled();
        if (ImGui.Button("Create missing State Commands and switch to them##triple_down_create_states"))
            CreateMissingTripleDownStates(missing, adaptFromStateHs: false);
        if (!needsRepair) ImGui.EndDisabled();

        ImGui.SameLine();
        if (!needsRepair || _tripleDownStateAdaptFailed) ImGui.BeginDisabled();
        if (_tripleDownStateAdaptFailed) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.2f, 0.2f, 1f));
        if (ImGui.Button(_tripleDownStateAdaptFailed
                ? "Sadly, this failed##triple_down_adapt_states"
                : "Try to adapt##triple_down_adapt_states"))
            CreateMissingTripleDownStates(missing, adaptFromStateHs: true);
        if (_tripleDownStateAdaptFailed) ImGui.PopStyleColor();
        if (!needsRepair || _tripleDownStateAdaptFailed) ImGui.EndDisabled();

        ImGui.EndChild();
        if (needsRepair) ImGui.PopStyleColor();
    }

    private void CreateMissingTripleDownStates(
        IReadOnlyCollection<TripleDownStateSupport.StateDefinition> missing,
        bool adaptFromStateHs)
    {
        if (missing.Count == 0) return;

        CommandGroup? stateHs = null;
        if (adaptFromStateHs)
        {
            stateHs = _config.CommandGroups.FirstOrDefault(group =>
                group.Name.Equals("StateHS", StringComparison.OrdinalIgnoreCase));
            if (stateHs == null || !stateHs.Commands.Any(command =>
                    command.Text.Contains("[Hit] [Stand]", StringComparison.Ordinal)))
            {
                _tripleDownStateAdaptFailed = true;
                return;
            }
        }

        var created = new List<string>();
        foreach (var state in missing)
        {
            if (_config.CommandGroups.Any(group => group.Name.Equals(state.Name, StringComparison.OrdinalIgnoreCase)))
                continue;

            CommandGroup group;
            if (stateHs != null)
            {
                group = TripleDownStateSupport.Clone(stateHs);
                group.Id = string.Empty;
                group.Name = state.Name;
                foreach (var command in group.Commands.Where(command =>
                             command.Text.Contains("[Hit] [Stand]", StringComparison.Ordinal)))
                    command.Text = command.Text.Replace("[Hit] [Stand]", state.Prompt, StringComparison.Ordinal);
            }
            else
            {
                group = new CommandGroup
                {
                    Name = state.Name,
                    Commands = new List<PluginCommand>
                    {
                        new() { Text = $"/p {state.Prompt}", Delay = 0.5f },
                    },
                };
            }

            _config.CommandGroups.Add(group);
            created.Add(state.Name);
        }

        if (created.Count == 0) return;
        _tripleDownStateRepairSucceeded = true;
        _tripleDownStateAdaptFailed = false;
        _pendingOpenCommandGroups.UnionWith(created);
        _pendingCommandsTab = "Commands";
        _page = Page.Commands;
        _save();
    }
}
