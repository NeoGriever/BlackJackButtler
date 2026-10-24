using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using BlackJackButtler.Regex;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawBettingSettings()
    {
        if (_config.EnsureBetLimitEntriesMigration()) _save();

        if (ImGui.BeginTable("bjb_bet_entries_v3", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Active", ImGuiTableColumnFlags.WidthFixed, 96f);
            ImGui.TableSetupColumn("Kind", ImGuiTableColumnFlags.WidthFixed, 100f);
            ImGui.TableSetupColumn("VIP", ImGuiTableColumnFlags.WidthFixed, 116f);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 200f);
            ImGui.TableSetupColumn("Amount", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, 17f);
            ImGui.TableHeadersRow();

            for (var i = 0; i < _config.BetLimitEntries.Count; i++)
            {
                var entry = _config.BetLimitEntries[i];
                ImGui.PushID($"v3_bet_entry_{i}");
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                if (BJBOnOffSwitch.Draw("active", ref entry.Active, 48f)) CommitBetEntries();

                ImGui.TableNextColumn();
                if (entry.Kind == BetLimitEntryKind.MinBet)
                {
                    ImGui.TextDisabled("Minimum");
                }
                else
                {
                    var normal = entry.Kind == BetLimitEntryKind.Normal;
                    var previousVipLevel = entry.VipLevel;
                    if (BJBOnOffSwitch.Draw("kind", ref normal, "NRM", "VIP", 42f))
                    {
                        if (normal)
                        {
                            var previousDefault = GetDefaultVipName(previousVipLevel);
                            entry.Kind = BetLimitEntryKind.Normal;
                            entry.VipLevel = 0;
                            if (string.IsNullOrWhiteSpace(entry.Name)
                                || entry.Name.Equals("VIP", StringComparison.OrdinalIgnoreCase)
                                || entry.Name.Equals(previousDefault, StringComparison.OrdinalIgnoreCase))
                                entry.Name = "Max";
                        }
                        else
                        {
                            entry.Kind = BetLimitEntryKind.Vip;
                            entry.VipLevel = GetNextVipLevel(_config.BetLimitEntries);
                            if (string.IsNullOrWhiteSpace(entry.Name)
                                || entry.Name.Equals("Max", StringComparison.OrdinalIgnoreCase))
                                entry.Name = GetDefaultVipName(entry.VipLevel);
                        }
                        CommitBetEntries();
                    }
                }

                ImGui.TableNextColumn();
                if (entry.Kind != BetLimitEntryKind.Vip)
                    ImGui.TextDisabled("-");
                else
                {
                    ImGui.SetNextItemWidth(-1f);
                    if (BJBGui.InputInt("##vip_level", ref entry.VipLevel, 1))
                    {
                        entry.VipLevel = Math.Clamp(entry.VipLevel, 1, 99);
                        CommitBetEntries();
                    }
                }

                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.InputText("##name", ref entry.Name, 64))
                {
                    if (string.IsNullOrWhiteSpace(entry.Name))
                        entry.Name = entry.Kind switch
                        {
                            BetLimitEntryKind.MinBet => "Min",
                            BetLimitEntryKind.Normal => "Max",
                            _ => "VIP",
                        };
                    CommitBetEntries();
                }

                ImGui.TableNextColumn();
                var ringOptions = GetMoneyRingOptions();
                ImGui.SetNextItemWidth(
                    ringOptions.Length > 0 ? Math.Max(32f, ImGui.GetContentRegionAvail().X - 18f - ImGui.GetStyle().ItemSpacing.X) : -1f
                );
                if (BJBGui.InputLongFormatted("##amount", ref entry.Amount))
                {
                    entry.Amount = Math.Clamp(entry.Amount, 1, 1_000_000_000);
                    CommitBetEntries();
                }
                if (ringOptions.Length > 0)
                {
                    ImGui.SameLine();
                    var selection = _moneyRing.Draw($"bet_limit_{i}", ringOptions);
                    if (selection.HasValue)
                    {
                        entry.Amount = Math.Clamp(selection.Value.ApplyTo(entry.Amount), 1, 1_000_000_000);
                        CommitBetEntries();
                    }
                }

                ImGui.TableNextColumn();
                if (entry.Kind == BetLimitEntryKind.MinBet) ImGui.BeginDisabled();
                if (BJBGui.SmallButton("X##delete"))
                {
                    _config.BetLimitEntries.RemoveAt(i);
                    CommitBetEntries();
                    ImGui.PopID();
                    break;
                }
                if (entry.Kind == BetLimitEntryKind.MinBet) ImGui.EndDisabled();

                ImGui.PopID();
            }
            ImGui.EndTable();
        }

        if (BJBGui.Button("Add Entry##v3_add_bet_entry"))
        {
            _config.BetLimitEntries.Add(CreateNextBetLimitEntry(_config.BetLimitEntries));
            CommitBetEntries();
        }

        Header("Betting Presets");
        foreach (var preset in _config.BettingPresets.ToList())
            DrawBettingPreset(preset);

        if (BJBGui.Button("Save as preset##v3_save_bet_preset"))
        {
            _config.BettingPresets.Add(
                new BettingPreset
                {
                    Name = GetNextBettingPresetName(),
                    Entries = _config.BetLimitEntries.Select(CloneBetEntry).ToList(),
                }
            );
            _save();
        }
    }

    private void DrawBettingPreset(BettingPreset preset)
    {
        ImGui.PushID($"v3_betting_preset_{preset.Name}");
        if (preset.Color.HasValue)
            ImGui.PushStyleColor(ImGuiCol.Button, preset.Color.Value);
        if (BJBGui.Button($"{FormatBettingPreset(preset)}##load"))
        {
            var armed = _armedBetPresetName.Equals(preset.Name, StringComparison.Ordinal)
                && DateTime.UtcNow <= _armedBetPresetUntil;
            if (armed)
            {
                _config.BetLimitEntries = preset.Entries.Select(CloneBetEntry).ToList();
                _config.BetLimitEntriesMigrated = true;
                CommitBetEntries();
                _armedBetPresetName = string.Empty;
            }
            else
            {
                _armedBetPresetName = preset.Name;
                _armedBetPresetUntil = DateTime.UtcNow.AddSeconds(2);
            }
        }
        if (preset.Color.HasValue) ImGui.PopStyleColor();
        ImGui.SameLine();
        if (BJBGui.SmallButton("*##color"))
            _editingBetPresetColor = _editingBetPresetColor == preset.Name ? string.Empty : preset.Name;
        ImGui.SameLine();
        if (BJBGui.SmallButton("X##delete"))
        {
            _config.BettingPresets.Remove(preset);
            if (_armedBetPresetName == preset.Name) _armedBetPresetName = string.Empty;
            _save();
            ImGui.PopID();
            return;
        }

        if (_armedBetPresetName == preset.Name && DateTime.UtcNow <= _armedBetPresetUntil)
            ImGui.TextDisabled("Click again to load this preset");

        ImGui.SetNextItemWidth(180f);
        if (ImGui.InputText("Name##rename", ref preset.Name, 64)) _save();

        if (_editingBetPresetColor == preset.Name)
        {
            var color = preset.Color ?? new Vector4(0.26f, 0.46f, 0.72f, 1f);
            if (ImGui.ColorEdit4("Color##edit", ref color))
            {
                color.W = 1f;
                preset.Color = color;
                _save();
            }
            ImGui.SameLine();
            if (BJBGui.SmallButton("Reset Color##reset"))
            {
                preset.Color = null;
                _save();
            }
        }
        ImGui.PopID();
    }

    private void CommitBetEntries()
    {
        _config.EnsureBetLimitEntriesMigration();
        var minimum = _config.BetLimitEntries.FirstOrDefault(entry => entry.Kind == BetLimitEntryKind.MinBet);
        if (minimum == null)
        {
            minimum = new BetLimitEntry { Active = true, Kind = BetLimitEntryKind.MinBet, Name = "Min", Amount = _config.MinBet };
            _config.BetLimitEntries.Insert(0, minimum);
        }
        minimum.VipLevel = 0;
        minimum.Name = string.IsNullOrWhiteSpace(minimum.Name) ? "Min" : minimum.Name;
        _config.MinBet = Math.Max(1, minimum.Amount);

        var normal = _config.BetLimitEntries.LastOrDefault(entry => entry.Kind == BetLimitEntryKind.Normal);
        if (normal != null)
        {
            normal.VipLevel = 0;
            normal.Name = string.IsNullOrWhiteSpace(normal.Name) ? "Max" : normal.Name;
            _config.MaxBet = Math.Max(1, normal.Amount);
        }

        _config.VipBetTiers = _config.BetLimitEntries
            .Where(entry => entry.Active && entry.Kind == BetLimitEntryKind.Vip && entry.VipLevel > 0)
            .GroupBy(entry => entry.VipLevel)
            .OrderBy(group => group.Key)
            .Select(
                group =>
                {
                    var entry = group.Last();
                    return new VipBetTier { Name = string.IsNullOrWhiteSpace(entry.Name) ? "VIP" : entry.Name, MaxBet = entry.Amount };
                }
            )
            .ToList();
        _config.BetLimitEntriesMigrated = true;
        _betDraftEntries = null;
        _save();
    }

    private BetLimitEntry CreateNextBetLimitEntry(IReadOnlyCollection<BetLimitEntry> entries)
    {
        if (entries.Count == 0)
        {
            return new BetLimitEntry
            {
                Active = true,
                Kind = BetLimitEntryKind.MinBet,
                Name = "Min",
                Amount = _config.MinBet,
            };
        }

        if (entries.Count == 1)
        {
            return new BetLimitEntry
            {
                Active = false,
                Kind = BetLimitEntryKind.Normal,
                Name = "Max",
                Amount = _config.MaxBet,
            };
        }

        var nextVipLevel = GetNextVipLevel(entries);
        return new BetLimitEntry
        {
            Active = false,
            Kind = BetLimitEntryKind.Vip,
            VipLevel = nextVipLevel,
            Name = GetDefaultVipName(nextVipLevel),
            Amount = _config.MaxBet,
        };
    }

    private static int GetNextVipLevel(IEnumerable<BetLimitEntry> entries) => Math.Max(
        1,
        entries
            .Where(entry => entry.Kind == BetLimitEntryKind.Vip)
            .Select(entry => entry.VipLevel)
            .DefaultIfEmpty(0)
            .Max() + 1
    );

    private string GetNextBettingPresetName()
    {
        const string root = "New Preset";
        if (_config.BettingPresets.All(preset => !preset.Name.Equals(root, StringComparison.OrdinalIgnoreCase)))
            return root;
        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{root} {suffix}";
            if (_config.BettingPresets.All(preset => !preset.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
        }
    }

    private static string FormatBettingPreset(BettingPreset preset)
    {
        var entries = preset.Entries
            .OrderBy(entry => entry.Kind == BetLimitEntryKind.MinBet ? 0 : entry.Kind == BetLimitEntryKind.Normal ? 1 : 2)
            .ThenBy(entry => entry.VipLevel)
            .Select(entry => $"{(string.IsNullOrWhiteSpace(entry.Name) ? entry.Kind.ToString() : entry.Name)}: {entry.Amount}");
        return string.Join(", ", entries);
    }
}
