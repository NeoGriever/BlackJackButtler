using System;
using System.Numerics;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Game.ClientState.Objects.SubKinds;
using System.Threading.Tasks;
using BlackJackButtler.Chat;
using BlackJackButtler.Regex;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawAliasModal()
    {
        if (_triggerAliasPopup)
        {
            _isAliasModalOpen = true;
            _triggerAliasPopup = false;
            ImGui.SetNextWindowFocus();
        }

        if (!_isAliasModalOpen)
            return;

        var keepOpen = _isAliasModalOpen;
        if (ImGui.Begin(AliasPopupTitle, ref keepOpen, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings))
        {
            if (_editingAliasPlayer == null)
            {
                _isAliasModalOpen = false;
                ImGui.End();
                return;
            }

            var editingPlayer = _editingAliasPlayer;
            ImGui.Text($"Set Alias for: {editingPlayer.Name}");
            ImGui.Spacing();

            ImGui.SetNextItemWidth(250f);
            ImGui.InputText("##alias_input", ref _aliasInputBuffer, 32);

            ImGui.Spacing();
            if (BJBGui.Button("Save", new Vector2(100, 0)))
            {
                var input = _aliasInputBuffer.Trim();
                if (string.IsNullOrWhiteSpace(input) || input.Equals(editingPlayer.Name, StringComparison.OrdinalIgnoreCase))
                    editingPlayer.Alias = string.Empty;
                else
                    editingPlayer.Alias = input;

                _save();
                _editingAliasPlayer = null;
                _isAliasModalOpen = false;
            }
            ImGui.SameLine();
            if (BJBGui.Button("Reset", new Vector2(100, 0)))
            {
                editingPlayer.Alias = string.Empty;
                _aliasInputBuffer = editingPlayer.Name;
                _save();
                _editingAliasPlayer = null;
                _isAliasModalOpen = false;
            }
            ImGui.SameLine();
            if (BJBGui.Button("Cancel", new Vector2(100, 0)))
            {
                _editingAliasPlayer = null;
                _isAliasModalOpen = false;
            }
        }
        ImGui.End();

        if (!keepOpen)
        {
            _editingAliasPlayer = null;
            _isAliasModalOpen = false;
        }
    }

    private void OpenAliasPopupForPlayer(PlayerState p)
    {
        p.HighlightAlias = false;
        _editingAliasPlayer = p;
        _aliasInputBuffer = !string.IsNullOrWhiteSpace(p.Alias) ? p.Alias : p.Name;
        _triggerAliasPopup = true;
        AddDebugLog($"[Alias] Name button clicked for {p.Name}; opening alias editor", false);
    }
}
