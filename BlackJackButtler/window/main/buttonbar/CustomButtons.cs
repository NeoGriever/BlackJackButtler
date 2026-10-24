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
    private void DrawCustomButtonBar()
    {
        if (_config.CustomButtonEntries.Count == 0 && _config.CustomCommandGroups.Count == 0) return;

        if (_config.ButtonBarPopout)
        {
            if (BJBGui.SmallButton("\u2190##popout_close"))
            {
                _config.ButtonBarPopout = false;
                Plugin.Instance.CloseButtonBar();
                _save();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Show buttons here");
            ImGui.Spacing();
            return;
        }

        RenderCustomButtons("main");

        ImGui.SameLine();
        if (BJBGui.SmallButton("\u2192##popout_open"))
        {
            _config.ButtonBarPopout = true;
            Plugin.Instance.OpenButtonBar();
            _save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Pop out buttons");

        ImGui.Spacing();
    }

    internal void RenderCustomButtons(string idSuffix, bool vertical = false)
    {
        EnsureButtonOrderMigration();
        bool isRunning = CommandExecutor.IsRunning;
        if (isRunning) ImGui.BeginDisabled();

        bool prevWasButton = false;

        for (int i = 0; i < _config.CustomButtonEntries.Count; i++)
        {
            var entry = _config.CustomButtonEntries[i];

            if (entry.IsBreak)
            {
                if (!entry.IsVisible) continue;
                if (prevWasButton)
                {
                    bool hasButtonAfter = false;
                    for (int j = i + 1; j < _config.CustomButtonEntries.Count; j++)
                    {
                        var followingEntry = _config.CustomButtonEntries[j];
                        if (followingEntry.IsBreak) continue;
                        var followingGroup = FindCustomButtonGroup(followingEntry);
                        if (followingGroup != null && followingGroup.IsActive && followingGroup.IsVisible)
                        {
                            hasButtonAfter = true;
                            break;
                        }
                    }
                    if (!hasButtonAfter) continue;
                    prevWasButton = false;
                }
                continue;
            }

            var group = FindCustomButtonGroup(entry);
            if (group == null) continue;
            if (!group.IsActive || !group.IsVisible) continue;

            string displayLabel = !string.IsNullOrEmpty(group.ButtonLabel) ? group.ButtonLabel : group.Name;

            if (prevWasButton)
            {
                if (!vertical)
                    ImGui.SameLine();

                bool estMono = group.UseCustomFont ? group.CustomUseMono : _config.CustomButtonUseMono;
                if (estMono) ImGui.PushFont(UiBuilder.MonoFont);
                float estScale = group.UseCustomFontScale ? group.CustomFontScale : _config.CustomButtonFontScale;
                if (estScale != 1.0f) ImGui.SetWindowFontScale(estScale);

                float estPadH = group.UseCustomPadding ? group.CustomPaddingH : _config.CustomButtonPaddingH;
                float textWidth = ImGui.CalcTextSize(displayLabel).X;
                float buttonWidth = textWidth + estPadH * 2 + ImGui.GetStyle().ItemSpacing.X;

                if (estScale != 1.0f) ImGui.SetWindowFontScale(1.0f);
                if (estMono) ImGui.PopFont();

                if (!vertical && buttonWidth > ImGui.GetContentRegionAvail().X)
                    ImGui.NewLine();
            }

            bool useMono = group.UseCustomFont ? group.CustomUseMono : _config.CustomButtonUseMono;
            if (useMono) ImGui.PushFont(UiBuilder.MonoFont);

            float scale = group.UseCustomFontScale ? group.CustomFontScale : _config.CustomButtonFontScale;
            if (scale != 1.0f) ImGui.SetWindowFontScale(scale);

            float padH = group.UseCustomPadding ? group.CustomPaddingH : _config.CustomButtonPaddingH;
            float padV = group.UseCustomPadding ? group.CustomPaddingV : _config.CustomButtonPaddingV;
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(padH, padV));

            int colorPushCount = 0;
            if (group.UseCustomButtonColor)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, group.CustomButtonColor);
                colorPushCount++;
            }
            if (group.UseCustomTextColor)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, group.CustomTextColor);
                colorPushCount++;
            }

            bool clicked;
            var buttonSize = _config.ButtonBarFixedWidth ? new Vector2(_config.ButtonBarFixedWidthValue, 0) : Vector2.Zero;

            if (colorPushCount > 0 && group.UseCustomTextColor)
                clicked = buttonSize.X > 0 ? ImGui.Button($"{displayLabel}##{idSuffix}_{i}", buttonSize) : ImGui.Button($"{displayLabel}##{idSuffix}_{i}");
            else
                clicked = buttonSize.X > 0 ? BJBGui.Button($"{displayLabel}##{idSuffix}_{i}", buttonSize) : BJBGui.Button($"{displayLabel}##{idSuffix}_{i}");

            if (colorPushCount > 0) ImGui.PopStyleColor(colorPushCount);

            ImGui.PopStyleVar();
            if (scale != 1.0f) ImGui.SetWindowFontScale(1.0f);
            if (useMono) ImGui.PopFont();

            if (clicked)
            {
                var targetName = _players.FirstOrDefault(p => p.IsCurrentTurn)?.DisplayName
                              ?? _dealer.DisplayName;
                var groupName = group.Name;
                GameActionQueueManager.Enqueue(
                    $"CustomButton:{groupName}",
                    () => CommandExecutor.ExecuteGroup(
                        groupName,
                        targetName,
                        _config
                    ),
                    $"CustomButton:{groupName}:{targetName}"
                );
            }

            prevWasButton = true;
        }

        if (isRunning) ImGui.EndDisabled();
    }
}
