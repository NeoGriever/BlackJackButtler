using System;
using System.Numerics;
using System.Linq;
using Dalamud.Bindings.ImGui;
using BlackJackButtler.Regex;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
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

        if (ImGui.Button("Hard Reset Trade-Regex##regex_hard_reset"))
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

            if (ImGui.Button("Yes", new Vector2(180, 0)))
            {
                _config.ForceResetStandardRegexes();
                _save();
                _openRegexResetPopup = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120, 0)))
            {
                _openRegexResetPopup = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        DrawRegexWarningPopup();

        ImGui.Separator();

        if (ImGui.Button("+ Add Custom Regex Entry"))
        {
            _config.UserRegexes.Add(new UserRegexEntry { Name = "New User Regex" });
            _save();
        }

        ImGui.Spacing();

        for (var i = 0; i < _config.UserRegexes.Count; i++)
        {
            var e = _config.UserRegexes[i];
            bool isStd = IsStandardRegex(e.Name);

            ImGui.PushID(i);

            if (isStd) ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.1f, 0.3f, 0.1f, 1f));

            var headerLabel = isStd ? $"● {e.Name}" : e.Name;
            if (string.IsNullOrWhiteSpace(e.Name)) headerLabel = $"Entry {i + 1}";

            bool open = ImGui.CollapsingHeader(headerLabel);

            if (isStd) ImGui.PopStyleColor();

            if (open)
            {
                bool disableEditing = isStd && !_config.AllowEditingStandardRegex;
                if (disableEditing) ImGui.BeginDisabled();

                if (ImGui.Checkbox("Enabled", ref e.Enabled)) _save();
                ImGui.SameLine();
                if (ImGui.Checkbox("Case Sensitive", ref e.CaseSensitive)) _save();

                var entryName = e.Name ?? "";
                ImGui.SetNextItemWidth(300f);

                if (isStd) ImGui.BeginDisabled();
                if (ImGui.InputText("Entry Name / Variable Name", ref entryName, 64))
                {
                    e.Name = entryName;
                    _save();
                }
                if (isStd) ImGui.EndDisabled();

                int modeInt = (int)e.Mode;
                ImGui.SetNextItemWidth(200f);
                if (ImGui.Combo("Operation Mode", ref modeInt, "Regex-To-Variable\0Regex-Trigger\0"))
                {
                    e.Mode = (RegexEntryMode)modeInt;
                    _save();
                }

                ImGui.Separator();

                var pat = e.Pattern ?? "";
                if (ImGui.InputTextMultiline("Pattern (Regex)", ref pat, 1024, new Vector2(-1, 60)))
                {
                    e.Pattern = pat;
                    _save();
                }

                ImGui.Spacing();

                if (e.Mode == RegexEntryMode.SetVariable)
                {
                    ImGui.TextColored(new Vector4(0.4f, 0.8f, 1f, 1f), "Info: Set Variable Mode");
                    ImGui.TextWrapped("The match of this regex will be stored in the variable name defined above.");
                }
                else if (e.Mode == RegexEntryMode.Trigger)
                {
                    ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), "Action Settings");
                    var action = (int)e.Action;
                    if (ImGui.Combo(
                        "Trigger Action",
                        ref action,
                        "None\0" +
                        "BetChange\0" +
                        "Hit\0" +
                        "Stand\0" +
                        "DD\0" +
                        "Split\0" +
                        "BankOut\0" +
                        "TradePartner\0" +
                        "TradeGilIn\0" +
                        "TradeGilOut\0" +
                        "TradeCommit\0" +
                        "TradeCancel\0" +
                        "TakeBatch\0" +
                        "DiceRollValue\0"
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
                }

                if (disableEditing) ImGui.EndDisabled();

                if (!isStd)
                {
                    ImGui.Spacing();
                    ImGui.Separator();
                    if (ImGui.GetIO().KeyCtrl)
                    {
                        if (ImGui.Button("Delete Entry", new Vector2(-1, 0)))
                        {
                            _config.UserRegexes.RemoveAt(i);
                            _save();
                            ImGui.PopID();
                            break;
                        }
                    }
                    else
                    {
                        ImGui.BeginDisabled();
                        ImGui.Button("Delete (Hold CTRL)", new Vector2(-1, 0));
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
        return Configuration.DefaultTradeRegexes.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private void DrawRegexWarningPopup()
    {
        if (ImGui.BeginPopupModal("bjb.regex.warning", ref _showRegexWarningPopup, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextColored(new Vector4(1, 0, 0, 1), "WARNING: ADVANCED EDITING");
            ImGui.TextUnformatted("Its strongly recommended to leave the standard regex entries untouched. Only unlock the edit mode on it, if you know, what you're doing!");
            ImGui.Spacing();

            if (ImGui.Button("Unlock Edit Mode", new Vector2(170, 0)))
            {
                _config.AllowEditingStandardRegex = true;
                _save();
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(170, 0)))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }
}
