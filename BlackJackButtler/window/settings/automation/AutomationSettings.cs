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
    private void DrawAutomationSettings()
    {
        ImGui.TextUnformatted("Enable:");

        DrawAutomationToggle(
            "Message Reaction",
            ref _config.AutoRun,
            "message_reaction",
            enabled =>
            {
                _config.ShowAutoRunButton = true;
                if (enabled) _config.EnableAutomation = true;
                Plugin.Instance.ResetAutoActionState(cancelCurrentGroup: !enabled);
            },
            drawAfter: () =>
            {
                if (BJBGui.Button("Create standard Automation Regex Entries"))
                    InsertRegularGameplayRegexEntries();
            }
        );

        DrawAutomationToggle(
            "Dealer Draw",
            ref _config.AutoDealerDraw,
            "dealer_draw",
            enabled =>
            {
                _config.ShowAutoDealerDrawButton = true;
                if (enabled) _config.EnableAutomation = true;
            }
        );
        DrawAutomationToggle(
            "Player Draw",
            ref _config.AutoInitialDeal,
            "player_draw",
            enabled =>
            {
                _config.ShowAutoPlayerHandButton = true;
                if (enabled) _config.EnableAutomation = true;
            }
        );
        DrawAutomationToggle("Auto Activate Trading Players", ref _config.AutoActivateTradingPlayers, "auto_activate_trading_players", _ => { });
        DrawAutomationToggle(
            "Continue after",
            ref _config.AutoContinue,
            "auto_continue",
            enabled =>
            {
                _config.ShowAutoContinueButton = true;
                if (enabled) _config.EnableAutomation = true;
            },
            drawAfter: () =>
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100f);
                if (ImGui.InputFloat("##v3_auto_continue_delay", ref _config.AutoContinueDelay, 0f, 0f, "%.0f"))
                {
                    _config.AutoContinueDelay = Math.Clamp(MathF.Round(_config.AutoContinueDelay), 10f, 300f);
                    _save();
                }
                ImGui.SameLine();
                ImGui.TextUnformatted("Seconds");
            }
        );

        DrawAutoContinueMinimumPlayersSelector("v3_auto_continue_min_players");

        ImGui.Spacing();
        DrawCommandSelector("Command after Bet-Change", ref _config.AutoBetPostCommandName);
        DrawCommandSelector("Command on insufficient Bank", ref _config.InsufficientBetCommandName);

        ImGui.TextUnformatted("Command Speed");
        ImGui.SameLine(260f);
        ImGui.SetNextItemWidth(220f);
        if (BJBGui.SliderFloat("##v3_command_speed", ref _config.CommandSpeedMultiplier, 0.1f, 4f, "%.2fx"))
        {
            _config.CommandSpeedMultiplier = Math.Clamp((float)Math.Round(_config.CommandSpeedMultiplier / 0.05f) * 0.05f, 0.1f, 4f);
            _save();
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton("Reset##v3_command_speed"))
        {
            _config.CommandSpeedMultiplier = 1f;
            _save();
        }

        ImGui.TextUnformatted("Recall Unlock");
        ImGui.SameLine(260f);
        ImGui.SetNextItemWidth(220f);
        if (BJBGui.SliderFloat("##v3_recall_unlock", ref _config.RecallUnlockSeconds, 1f, 60f, "%.0fs"))
        {
            _config.RecallUnlockSeconds = Math.Clamp(MathF.Round(_config.RecallUnlockSeconds), 1f, 60f);
            _save();
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton("Reset##v3_recall_unlock"))
        {
            _config.RecallUnlockSeconds = 20f;
            _save();
        }

        DrawPlayerSoundSettings();
    }

    private void DrawAutomationToggle(string label, ref bool value, string id, Action<bool> changed, Action? drawAfter = null)
    {
        if (BJBOnOffSwitch.Draw(id, ref value))
        {
            changed(value);
            _save();
        }
        ImGui.SameLine();
        ImGui.TextUnformatted(label);
        drawAfter?.Invoke();
    }

    private void InsertRegularGameplayRegexEntries()
    {
        var entries = new[]
        {
            CreateGameplayRegex("Draw", true, RegexChatSource.Party, RegexAction.WantHit, "^hit\\!*$", "^hit me\\!*$", "^h\\!*$"),
            CreateGameplayRegex("Stand", true, RegexChatSource.Party, RegexAction.WantStand, "^stand\\!*$", "^stando\\!*$", "^standy\\!*$", "^stay\\!*$", "^s\\!*$"),
            CreateGameplayRegex("Double Down", true, RegexChatSource.Party, RegexAction.WantDD, "^dd\\!*$", "^double down\\!*$"),
            CreateGameplayRegex("Triple Down", true, RegexChatSource.Party, RegexAction.AutoTripleDown, "^td$", "^triple down$"),
            CreateGameplayRegex("Split", true, RegexChatSource.Party, RegexAction.WantSplit, "^split\\!*$"),
            CreateGameplayRegex("Ready", false, RegexChatSource.Party | RegexChatSource.Tell | RegexChatSource.Say, RegexAction.NextRound, "^ready\\!*$", "^i\\'m ready\\!*$", "^r\\!*$"),
            CreateGameplayRegex("Set Bet", false, RegexChatSource.Party | RegexChatSource.Tell, RegexAction.SetBet, "^bet\\s+((?:\\d[\\d.,]*\\s*[km]?)|max|all|full|min)$"),
            CreateGameplayRegex("Bank Tell", false, RegexChatSource.Party | RegexChatSource.Tell | RegexChatSource.Say, RegexAction.BankTell, "^bank\\?*$", "^what\\'*s my bank\\?*$", "^what is my bank\\?*$"),
            CreateGameplayRegex("Withdraw", false, RegexChatSource.Party, RegexAction.Withdraw, "^withdraw\\s+((?:\\d[\\d.,]*\\s*[km]?)|all|everything)$"),
        };

        var changed = false;
        foreach (var entry in entries)
        {
            if (_config.UserRegexes.Any(existing => existing.Name.Equals(entry.Name, StringComparison.OrdinalIgnoreCase)))
                continue;
            _config.UserRegexes.Add(entry);
            changed = true;
        }

        if (!changed) return;
        RegexEngine.InvalidateCache();
        _save();
    }

    private static UserRegexEntry CreateGameplayRegex(string name, bool enabled, RegexChatSource sources, RegexAction action, params string[] patterns)
        => new()
        {
            Name = name,
            Enabled = enabled,
            CaseSensitive = false,
            Sources = sources,
            Mode = RegexEntryMode.Trigger,
            Action = action,
            Patterns = new List<string>(patterns),
        };
}
