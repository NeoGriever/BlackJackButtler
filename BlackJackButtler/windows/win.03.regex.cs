using System;
using System.Numerics;
using System.Linq;
using Dalamud.Bindings.ImGui;
using BlackJackButtler.Regex;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private string _filterRegex = string.Empty;

    private void DrawRegexPage()
    {
        ImGui.TextUnformatted("Regular Expressions");
        ImGui.SameLine();

        var allow = _config.AllowEditingStandardRegex;
        if (ImGui.Checkbox("Allow editing standard", ref allow))
        {
            if (allow && !_config.AllowEditingStandardRegex)
            {
                _showRegexWarningPopup = true;
                ImGui.OpenPopup("bjb.regex.warning");
            }
            else if (!allow && _config.AllowEditingStandardRegex)
            {
                _config.AllowEditingStandardRegex = false;
                _save();
            }
        }

        ImGui.SameLine();
        var io = ImGui.GetIO();
        bool keysDown = io.KeyCtrl && io.KeyShift;

        if (!keysDown) ImGui.BeginDisabled();
        if (keysDown) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0f, 0f, 1f));

        if (BJBGui.Button("Hard Reset Trade-Regex##regex_hard_reset"))
        {
            _openRegexResetPopup = true;
            ImGui.OpenPopup("bjb.regex.hardreset.confirm");
        }

        if (keysDown) ImGui.PopStyleColor();
        if (!keysDown)
        {
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("Hold CTRL + SHIFT to unlock this reset button.");
        }

        if (ImGui.BeginPopupModal("bjb.regex.hardreset.confirm", ref _openRegexResetPopup, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextColored(new Vector4(1, 0, 0, 1), "WARNING: HARD RESET REGEX");
            ImGui.TextUnformatted("This will delete all standard trade-related regex entries and recreate them from defaults.");
            ImGui.TextUnformatted("Your custom manually created regex entries will not be affected.");
            ImGui.Spacing();

            if (BJBGui.Button("Yes", new Vector2(180, 0)))
            {
                _config.ForceResetStandardRegexes();
                _save();
                _openRegexResetPopup = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (BJBGui.Button("Cancel", new Vector2(120, 0)))
            {
                _openRegexResetPopup = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        DrawRegexWarningPopup();

        ImGui.Separator();

        if (BJBGui.Button("+ Add Custom Regex Entry"))
        {
            _config.UserRegexes.Add(new UserRegexEntry { Name = "New User Regex" });
            _save();
        }

        ImGui.SameLine();
        BJBGui.DrawFilterBar("regex", ref _filterRegex, "Search regex name or pattern...");

        ImGui.Spacing();

        for (var i = 0; i < _config.UserRegexes.Count; i++)
        {
            var e = _config.UserRegexes[i];
            bool isStd = IsStandardRegex(e.Name);

            if (!BJBGui.MatchesFilter(_filterRegex, e.Name, e.Patterns)) continue;

            ImGui.PushID(i);

            if (isStd) ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.1f, 0.3f, 0.1f, 1f));

            var headerLabel = isStd ? $"● {e.Name}" : e.Name;
            if (string.IsNullOrWhiteSpace(e.Name)) headerLabel = $"Entry {i + 1}";

            if (!string.IsNullOrEmpty(_filterRegex))
                ImGui.SetNextItemOpen(true, ImGuiCond.Always);

            bool open = ImGui.CollapsingHeader($"{headerLabel}###regex_{i}");

            if (isStd) ImGui.PopStyleColor();

            if (open)
            {
                bool disableEditing = isStd && !_config.AllowEditingStandardRegex;
                if (disableEditing) ImGui.BeginDisabled();

                if (ImGui.Checkbox("##enabled", ref e.Enabled)) _save();

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip("Enabled");

                ImGui.SameLine();

                if (ImGui.Checkbox("##caseSensitive", ref e.CaseSensitive)) { RegexEngine.InvalidateCache(); _save(); }

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip("Case sensitive");

                ImGui.SameLine();

                if (ImGui.Checkbox("##applyToTells", ref e.ApplyToTells)) _save();

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip("Apply this regex to incoming tells");

                var entryName = e.Name ?? "";

                ImGui.SameLine();
                ImGui.SetNextItemWidth(300f);

                if (isStd) ImGui.BeginDisabled();
                if (ImGui.InputText("##entryName", ref entryName, 64))
                {
                    e.Name = entryName;
                    _save();
                }
                if (isStd) ImGui.EndDisabled();

                int modeInt = (int)e.Mode;
                ImGui.SameLine();
                ImGui.SetNextItemWidth(250f);
                if (BJBGui.Combo("##opMode", ref modeInt, "Regex-To-Variable\0Regex-Trigger\0"))
                {
                    e.Mode = (RegexEntryMode)modeInt;
                    _save();
                }

                ImGui.Separator();

                for (int pIdx = 0; pIdx < e.Patterns.Count; pIdx++)
                {
                    ImGui.PushID(pIdx);
                    var pStr = e.Patterns[pIdx] ?? "";

                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 70f);
                    if (ImGui.InputText("##pat", ref pStr, 512))
                    {
                        e.Patterns[pIdx] = pStr;
                        RegexEngine.InvalidateCache();
                        _save();
                    }

                    ImGui.SameLine();
                    if (BJBGui.Button("+"))
                    {
                        e.Patterns.Insert(pIdx + 1, "");
                        _save();
                    }

                    ImGui.SameLine();

                    bool canDeletePattern = e.Patterns.Count > 1;
                    if (!canDeletePattern) ImGui.BeginDisabled();
                    if (BJBGui.Button("X"))
                    {
                        e.Patterns.RemoveAt(pIdx);
                        RegexEngine.InvalidateCache();
                        _save();
                        ImGui.PopID();
                        break;
                    }
                    if (!canDeletePattern) ImGui.EndDisabled();

                    ImGui.PopID();
                }

                ImGui.Spacing();

                if (e.Mode == RegexEntryMode.Trigger)
                {
                    ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), "Action Settings");
                    var action = (int)e.Action;
                    if (BJBGui.Combo(
                        "Trigger Action",
                        ref action,
                        "None\0" +
                        "BetChange\0" +
                        "Auto Hit\0" +
                        "Auto Stand\0" +
                        "Auto DD\0" +
                        "Auto Split\0" +
                        "BankOut\0" +
                        "TradePartner\0" +
                        "TradeGilIn\0" +
                        "TradeGilOut\0" +
                        "TradeCommit\0" +
                        "TradeCancel\0" +
                        "TakeBatch\0" +
                        "DiceRollValue\0" +
                        "HighlightBet\0" +
                        "HighlightPayout\0" +
                        "HighlightAlias\0" +
                        "HighlightPause\0" +
                        "HighlightLeave\0" +
                        "HighlightJoin\0" +
                        "Highlight Hit\0" +
                        "Highlight Stand\0" +
                        "Highlight DD\0" +
                        "Highlight Split\0" +
                        "NextRound\0" +
                        "BankTell\0" +
                        "Own Button\0" +
                        "SetBet\0" +
                        "Invite Nearby\0"
                    ))
                    {
                        e.Action = (RegexAction)action;
                        _save();
                    }

                    if (e.Action == RegexAction.TakeBatch)
                    {
                        var param = e.ActionParam ?? "";
                        if (ImGui.InputText("Target Batch Name", ref param, 64))
                        {
                            e.ActionParam = param;
                            _save();
                        }
                    }

                    if (e.Action == RegexAction.ExecuteOwnButton)
                    {
                        var allGroups = _config.CustomCommandGroups;
                        if (allGroups.Count == 0)
                        {
                            ImGui.TextDisabled("No Own Buttons defined.");
                        }
                        else
                        {
                            var groupNames = allGroups.Select(g => g.Name).ToArray();
                            int selectedIdx = Array.FindIndex(groupNames, n => n.Equals(e.ActionParam, StringComparison.OrdinalIgnoreCase));
                            if (selectedIdx < 0) selectedIdx = 0;
                            ImGui.SetNextItemWidth(300f);
                            if (BJBGui.Combo("Target Button##ownbtn_combo", ref selectedIdx, groupNames, groupNames.Length))
                            {
                                e.ActionParam = groupNames[selectedIdx];
                                _save();
                            }
                        }
                    }
                }

                if (disableEditing) ImGui.EndDisabled();

                if (!isStd)
                {
                    ImGui.Spacing();
                    ImGui.Separator();
                    if (ImGui.GetIO().KeyCtrl)
                    {
                        if (BJBGui.Button("Delete Entry", new Vector2(-1, 0)))
                        {
                            _config.UserRegexes.RemoveAt(i);
                            RegexEngine.InvalidateCache();
                            _save();
                            ImGui.PopID();
                            break;
                        }
                    }
                    else
                    {
                        ImGui.BeginDisabled();
                        BJBGui.Button("Delete (Hold CTRL)", new Vector2(-1, 0));
                        ImGui.EndDisabled();
                    }
                }
            }
            ImGui.PopID();
        }
    }

    private bool IsStandardRegex(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return Configuration.StandardRegexNames.Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    private void DrawRegexWarningPopup()
    {
        if (ImGui.BeginPopupModal("bjb.regex.warning", ref _showRegexWarningPopup, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextColored(new Vector4(1, 0, 0, 1), "WARNING: ADVANCED EDITING");
            ImGui.TextUnformatted("Its strongly recommended to leave the standard regex entries untouched. Only unlock the edit mode on it, if you know, what you're doing!");
            ImGui.Spacing();

            if (BJBGui.Button("Unlock Edit Mode", new Vector2(170, 0)))
            {
                _config.AllowEditingStandardRegex = true;
                _save();
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (BJBGui.Button("Cancel", new Vector2(170, 0)))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }
}
