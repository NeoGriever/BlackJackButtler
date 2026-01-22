using System.Numerics;
using Dalamud.Bindings.ImGui;
using BlackJackButtler.Regex;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawRegexPage()
    {
        ImGui.TextUnformatted("Regular Expressions");
        ImGui.Separator();

        // --- Standard Algorithms ---
        ImGui.TextUnformatted("Standard Algorithms");
        var allow = _config.AllowEditingStandardRegex;
        if (ImGui.Checkbox("Allow editing standard regular expressions", ref allow))
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

        DrawRegexWarningPopup();

        var standardPattern = @"(\d+)\s*$";
        ImGui.BeginDisabled(!_config.AllowEditingStandardRegex);
        ImGui.InputText("Dice value pattern", ref standardPattern, 128, ImGuiInputTextFlags.ReadOnly);
        ImGui.EndDisabled();

        ImGui.BulletText("1 → 11 | 2–9 → 2–9 | 10–13 → 10");
        ImGui.Separator();

        // --- Custom Entries ---
        if (ImGui.Button("+ Add Regex Entry"))
        {
            _config.UserRegexes.Add(new UserRegexEntry());
            _save();
        }

        ImGui.Spacing();

        for (var i = 0; i < _config.UserRegexes.Count; i++)
        {
            var e = _config.UserRegexes[i];
            ImGui.PushID(i);

            if (ImGui.CollapsingHeader(string.IsNullOrWhiteSpace(e.Name) ? $"Entry {i + 1}" : e.Name))
            {
                // 1. Meta-Einstellungen (Immer oben)
                if (ImGui.Checkbox("Enabled", ref e.Enabled)) _save();
                ImGui.SameLine();
                if (ImGui.Checkbox("Case Sensitive", ref e.CaseSensitive)) _save();

                // Name ist wichtig für den Header und die Variable
                var entryName = e.Name ?? "";
                ImGui.SetNextItemWidth(300f);
                if (ImGui.InputText("Entry Name / Variable Name", ref entryName, 64))
                {
                    e.Name = entryName;
                    _save();
                }

                // 2. Modus-Wahl
                int modeInt = (int)e.Mode;
                ImGui.SetNextItemWidth(200f);
                if (ImGui.Combo("Operation Mode", ref modeInt, "Regex-To-Variable\0Regex-Trigger\0"))
                {
                    e.Mode = (RegexEntryMode)modeInt;
                    _save();
                }

                ImGui.Separator();

                // 3. Das Pattern
                var pat = e.Pattern ?? "";
                if (ImGui.InputTextMultiline("Pattern (Regex)", ref pat, 1024, new Vector2(-1, 60)))
                {
                    e.Pattern = pat;
                    _save();
                }

                ImGui.Spacing();

                // 4. Modus-spezifische Details
                if (e.Mode == RegexEntryMode.SetVariable)
                {
                    ImGui.TextColored(new Vector4(0.4f, 0.8f, 1f, 1f), "Info: Set Variable Mode");
                    ImGui.TextWrapped("The match of this regex will be stored in the variable name defined above.");
                }
                else if (e.Mode == RegexEntryMode.Trigger)
                {
                    ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), "Action Settings");
                    var action = (int)e.Action;
                    if (ImGui.Combo("Trigger Action", ref action, "None\0BetChange\0Hit\0Stand\0DD\0Split\0BankOut\0TakeBatch\0"))
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

                // 5. Löschen (Ganz unten)
                ImGui.Spacing();
                ImGui.Separator();
                if (ImGui.GetIO().KeyCtrl)
                {
                    if (ImGui.Button("Delete (Hold CTRL)", new Vector2(-1, 0)))
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
            ImGui.PopID();
        }
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
