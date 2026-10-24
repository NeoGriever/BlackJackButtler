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
    private void DrawNearbyIgnoreWindow()
    {
        if (!_showNearbyIgnoreWindow) return;

        ImGui.SetNextWindowSize(new Vector2(460f, 420f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Nearby Auto-Act Ignore List###bjb_nearby_ignore", ref _showNearbyIgnoreWindow, ImGuiWindowFlags.NoCollapse))
        {
            ImGui.End();
            return;
        }

        ImGui.TextUnformatted("Add manually");
        ImGui.SetNextItemWidth(210f);
        ImGui.InputText("Player##nearby_ignore_player", ref _nearbyIgnoreNameInput, 64);
        ImGui.SameLine();

        ImGui.SetNextItemWidth(150f);
        var worldBefore = _nearbyIgnoreWorldInput;
        if (DateTime.Now < _nearbyWorldInputBlockedUntil)
            ImGui.BeginDisabled();
        if (ImGui.InputText("World##nearby_ignore_world", ref _nearbyIgnoreWorldInput, 64))
            TryAutocompleteNearbyIgnoreWorld(worldBefore);
        if (DateTime.Now < _nearbyWorldInputBlockedUntil)
            ImGui.EndDisabled();

        ImGui.SameLine();
        if (BJBGui.SmallButton("+##nearby_ignore_manual_add"))
            AddNearbyIgnoreEntry(_nearbyIgnoreNameInput, _nearbyIgnoreWorldInput, true);

        ImGui.Separator();
        ImGui.TextUnformatted("Nearby players");
        var nearby = NearbyPlayersManager.GetNearbyPlayers(_config)
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.World, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (ImGui.BeginChild("nearby_ignore_nearby_list", new Vector2(-1, 140f), true))
        {
            foreach (var p in nearby)
            {
                ImGui.PushID($"nearby_ignore_add_{p.FullKey}");
                if (BJBGui.SmallButton("+"))
                    AddNearbyIgnoreEntry(p.Name, p.World, false);
                ImGui.SameLine();
                ImGui.TextUnformatted($"{p.Name}@{p.World}");
                ImGui.PopID();
            }
        }
        ImGui.EndChild();

        ImGui.Separator();
        ImGui.TextUnformatted("Ignored");
        var entries = _config.NearbyAutoActIgnoreList
            .OrderBy(ParseIgnoreName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(ParseIgnoreWorld, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ImGui.BeginChild("nearby_ignore_entries", new Vector2(-1, 140f), true))
        {
            foreach (var entry in entries)
            {
                ImGui.PushID($"nearby_ignore_remove_{entry}");
                if (BJBGui.SmallButton("X"))
                {
                    _config.NearbyAutoActIgnoreList.RemoveAll(x => x.Equals(entry, StringComparison.OrdinalIgnoreCase));
                    _save();
                    ImGui.PopID();
                    break;
                }
                ImGui.SameLine();
                ImGui.TextUnformatted(FormatIgnoreEntry(entry));
                ImGui.PopID();
            }
        }
        ImGui.EndChild();

        ImGui.End();
    }

    private void TryAutocompleteNearbyIgnoreWorld(string previous)
    {
        if (_nearbyIgnoreWorldInput.Equals(previous, StringComparison.Ordinal))
            return;

        if (WorldNameManager.TryCompletePrefix(_nearbyIgnoreWorldInput, out var completed)
            && !completed.Equals(_nearbyIgnoreWorldInput, StringComparison.Ordinal))
        {
            _nearbyIgnoreWorldInput = completed;
            _nearbyWorldInputBlockedUntil = DateTime.Now.AddSeconds(1);
        }
    }

    private void AddNearbyIgnoreEntry(string name, string world, bool clearInputs)
    {
        var cleanName = name.Trim();
        var cleanWorld = NormalizeWorldName(world.Trim());
        if (string.IsNullOrWhiteSpace(cleanName) || string.IsNullOrWhiteSpace(cleanWorld))
            return;

        var key = $"{cleanName}@{cleanWorld}";
        if (!_config.NearbyAutoActIgnoreList.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            _config.NearbyAutoActIgnoreList.Add(key);
            _config.NearbyAutoActIgnoreList.Sort(StringComparer.OrdinalIgnoreCase);
            _save();
        }

        if (clearInputs)
        {
            _nearbyIgnoreNameInput = string.Empty;
            _nearbyIgnoreWorldInput = string.Empty;
        }
    }

    private static string NormalizeWorldName(string world)
    {
        var exact = WorldNameManager.SortedWorldNames.FirstOrDefault(w => w.Equals(world, StringComparison.OrdinalIgnoreCase));
        return exact ?? world;
    }

    private static string ParseIgnoreName(string entry)
    {
        var at = entry.IndexOf('@');
        return at < 0 ? entry : entry[..at];
    }

    private static string ParseIgnoreWorld(string entry)
    {
        var at = entry.IndexOf('@');
        return at < 0 ? string.Empty : entry[(at + 1)..];
    }

    private static string FormatIgnoreEntry(string entry)
    {
        var name = ParseIgnoreName(entry);
        var world = ParseIgnoreWorld(entry);
        return string.IsNullOrWhiteSpace(world) ? name : $"{name}@{world}";
    }
}
