using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using BlackJackButtler.Chat;
using BlackJackButtler.Regex;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawMainPageV2()
    {
        DrawMainHeaderV2();
        ImGui.Spacing();
        DrawCustomButtonBarV2();
        DrawAutoContinueBarV2();

        ImGui.Spacing();
        DrawDealerPanelV2();

        ImGui.Spacing();
        DrawPlayersPanelV2();

        DrawNearbyPlayersSection(true);
        DrawMainSharedPopupsV2();
    }

    private void DrawMainHeaderV2()
    {
        ImGui.TextColored(new Vector4(1f, 0.65f, 0.2f, 1f), $"Phase: {GameEngine.CurrentPhase}");
        ImGui.SameLine();
        ImGui.TextDisabled("Main View V2");

        DrawSessionControlsV2();
        DrawAutomationControlsV2();
        DrawRoundUtilityControlsV2();

        if (IsRecognitionActive && !IsLocalPlayerPartyLeader())
        {
            var leaderName = GetPartyLeaderName();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.6f, 0.2f, 1.0f));
            ImGui.TextUnformatted($"You're not the group leader. Current leader is {leaderName}");
            ImGui.PopStyleColor();
            if (ImGui.IsItemClicked())
                GameEngine.TargetPlayer(leaderName);
        }

        if (IsRecognitionActive && _config.CurrentLevel == UserLevel.Dev
            && (!IsLocalPlayerPartyLeader() || Plugin.PartyList.Length == 0))
        {
            if (BJBGui.SmallButton("Activate debug mode"))
            {
                IsRecognitionActive = false;
                SessionManager.ClearSession();
                EnableDebugMode();
                Plugin.Instance.UpdateEventHooks();
            }
        }
    }

    private void DrawSessionControlsV2()
    {
        ImGui.Spacing();
        ImGui.TextDisabled("Session");
        ImGui.SameLine();

        var reconText = IsRecognitionActive ? "Group Detector: ON" : "Group Detector: OFF";
        if (BJBGui.Button(reconText, new Vector2(170, 0)))
        {
            IsRecognitionActive = !IsRecognitionActive;
            if (IsRecognitionActive)
            {
                SyncParty();
                ViewDirectionManager.CaptureCurrentRotation(_config);
                _groupDetectorActivatedAt = StatsManager.IsRunning ? null : DateTime.Now;
            }
            else
            {
                SessionManager.ClearSession();
                _players.RemoveAll(p => !p.IsActivePlayer && p.Bank == 0);
                AddDebugLog("[SessionManager] Session cleared (Group Detector deactivated)", false);
                _groupDetectorActivatedAt = null;
            }

            Plugin.Instance.UpdateEventHooks();
        }

        if (!IsRecognitionActive)
            DrawCleanDataButtonV2();

        if (IsRecognitionActive && !StatsManager.IsRunning && _groupDetectorActivatedAt.HasValue)
        {
            double elapsed = (DateTime.Now - _groupDetectorActivatedAt.Value).TotalSeconds;
            if (elapsed < 30)
            {
                ImGui.SameLine();
                int secondsLeft = 30 - (int)elapsed;
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.1f, 0.5f, 0.1f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.15f, 0.65f, 0.15f, 1f));
                if (BJBGui.Button($"Start Bank ({secondsLeft}s)##v2_groupdetect_startbank", new Vector2(150, 0)))
                {
                    StatsManager.StartSession();
                    _save();
                    _groupDetectorActivatedAt = null;
                }
                ImGui.PopStyleColor(2);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Start the stats session with the current bank.");
            }
            else
            {
                _groupDetectorActivatedAt = null;
            }
        }
    }

    private void DrawCleanDataButtonV2()
    {
        bool hasResidualData =
            _players.Any(p => !p.IsDebugPlayer)
            || _dealer.Hands.Any(h => h.Cards.Count > 0)
            || DrawLogicDebugManager.DebugHands.Any(h => h.Cards.Count > 0)
            || DrawLogicDebugManager.ValidScriptCache.Count > 0;
        if (!hasResidualData) return;

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.55f, 0.30f, 0.05f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.75f, 0.40f, 0.10f, 1f));
        if (BJBGui.Button("Clean Data##v2_clean_residual", new Vector2(130, 0)))
        {
            _openCleanDataPopup = true;
            ImGui.OpenPopup("bjb.clean_data.confirm.v2");
        }
        ImGui.PopStyleColor(2);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Clean players and DrawLogic debug data left over while Group Detector is off.");
    }

    private void DrawAutomationControlsV2()
    {
        ImGui.Spacing();
        ImGui.TextDisabled("Automation");
        if (!_config.EnableAutomation)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("disabled");
            return;
        }

        bool anyButton = false;
        if (_config.ShowAutoPlayerHandButton)
        {
            ImGui.SameLine();
            DrawToggleButtonV2("Player Hand", ref _config.AutoInitialDeal, "Automatically deals initial player hands.");
            anyButton = true;
        }
        else _config.AutoInitialDeal = false;

        if (_config.ShowAutoDealerDrawButton)
        {
            ImGui.SameLine();
            DrawToggleButtonV2("Dealer Draw", ref _config.AutoDealerDraw,
                $"Dealer draws until {(_config.DealerSoftRule ? "soft" : "hard")} {_config.DealerDrawsUntil}.");
            anyButton = true;
        }
        else _config.AutoDealerDraw = false;

        if (_config.ShowAutoContinueButton)
        {
            ImGui.SameLine();
            DrawToggleButtonV2("Auto Continue", ref _config.AutoContinue,
                $"Starts the next round after {_config.AutoContinueDelay:0}s of no chat activity.");
            anyButton = true;
        }
        else _config.AutoContinue = false;

        bool hasAutoTriggers = _config.UserRegexes.Any(r =>
            r.Enabled && r.Mode == RegexEntryMode.Trigger &&
            (r.Action == RegexAction.WantHit || r.Action == RegexAction.WantStand ||
             r.Action == RegexAction.WantDD || r.Action == RegexAction.WantSplit));
        if (hasAutoTriggers && _config.ShowAutoRunButton)
        {
            ImGui.SameLine();
            DrawToggleButtonV2("Auto Run", ref _config.AutoRun,
                "Executes player action triggers automatically.");
            anyButton = true;
        }
        else _config.AutoRun = false;

        if (!anyButton)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("no visible buttons");
        }
    }

    private void DrawToggleButtonV2(string label, ref bool value, string tooltip)
    {
        if (value) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.5f, 0.0f, 1.0f));
        if (BJBGui.Button($"{(value ? "ON" : "OFF")} {label}##v2_toggle_{label}"))
        {
            value = !value;
            _save();
        }
        if (value) ImGui.PopStyleColor();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);
    }

    private void DrawRoundUtilityControlsV2()
    {
        ImGui.Spacing();
        ImGui.TextDisabled("Round Tools");
        ImGui.SameLine();

        DrawBankTellAllButtonV2();
        ImGui.SameLine();
        DrawStopButtonV2();
        DrawNotepadButtonV2();
        ImGui.SameLine();
        if (ImGui.Checkbox("Bank input##v2_enable_bank_input", ref _config.EnableBankInput)) _save();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Enable Bank input");

        var io = ImGui.GetIO();
        if (io.KeyCtrl && io.KeyShift)
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.5f, 0.0f, 0.0f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.65f, 0.0f, 0.0f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));
            if (ImGui.Button("PANIC##v2_panic_btn", new Vector2(70, 0)))
                _panicConfirmStage = 1;
            ImGui.PopStyleColor(3);
        }
    }

    private void DrawBankTellAllButtonV2()
    {
        var tellPhase = GameEngine.CurrentPhase;
        bool canTell = (tellPhase == GamePhase.Waiting || tellPhase == GamePhase.Payout) && !CommandExecutor.IsRunning;
        if (!canTell) ImGui.BeginDisabled();
        if (BJBGui.Button("Bank /tell##v2_banktell_all"))
        {
            var snapshot = _players.Where(p => p.IsActivePlayer && !p.IsOnHold).ToList();
            var dealerName = _dealer.Name;
            Task.Run(async () =>
            {
                foreach (var p in snapshot)
                {
                    GameEngine.TargetPlayer(p.Name);
                    VariableManager.SetPlayerVariables(p);
                    await CommandExecutor.ExecuteGroup("BankTell", p.DisplayName, _config);
                }
                GameEngine.TargetPlayer(dealerName);
            });
        }
        if (!canTell) ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Post bank/bet info for all active players to party chat");
    }

    private void DrawStopButtonV2()
    {
        if (!CommandExecutor.IsRunning)
        {
            ImGui.TextDisabled("STOP");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("No command group is running.");
            return;
        }

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.0f, 0.0f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.1f, 0.1f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.5f, 0.0f, 0.0f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));
        if (ImGui.Button("STOP##v2_stop_commands"))
            CommandExecutor.CancelCurrentGroup();
        ImGui.PopStyleColor(4);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Stop currently running commands");
    }

    private void DrawNotepadButtonV2()
    {
        if (_notepadWindow.IsOpen)
        {
            if (BJBGui.Button("X##v2_notepad_close_btn", new Vector2(ImGui.GetFrameHeight(), 0)))
                _notepadWindow.IsOpen = false;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Close Notepad");
            return;
        }

        ImGui.PushFont(UiBuilder.IconFont);
        if (BJBGui.Button(FontAwesomeIcon.StickyNote.ToIconString() + "##v2_notepad_btn", new Vector2(ImGui.GetFrameHeight(), 0)))
        {
            if (!_notepadLoaded) { _notepadLoaded = true; _notepadWindow.LoadContent(); }
            _notepadWindow.IsOpen = true;
        }
        ImGui.PopFont();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Open Notepad");
    }

    private void DrawCustomButtonBarV2()
    {
        if (_config.CustomButtonOrder.Count == 0 && _config.CustomCommandGroups.Count == 0) return;

        ImGui.TextDisabled("Custom Actions");
        ImGui.SameLine();
        DrawCustomButtonBar();
    }

    private void DrawAutoContinueBarV2()
    {
        if (!_config.AutoContinue || !Plugin.AutoContinueActive) return;

        float barHeight = _config.AutoContinueBarHeight;
        if (_config.AutoContinueBarShowText && barHeight < 20f) barHeight = 20f;

        float elapsed = (float)Plugin.AutoContinueElapsedSeconds;
        float progress = Math.Clamp(elapsed / _config.AutoContinueDelay, 0f, 1f);

        var cursorPos = ImGui.GetCursorScreenPos();
        float availWidth = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();

        drawList.AddRectFilled(cursorPos,
            new Vector2(cursorPos.X + availWidth, cursorPos.Y + barHeight),
            ImGui.GetColorU32(new Vector4(0.15f, 0.15f, 0.15f, 1f)));
        drawList.AddRectFilled(cursorPos,
            new Vector2(cursorPos.X + availWidth * progress, cursorPos.Y + barHeight),
            ImGui.GetColorU32(_config.AutoContinueBarColor));

        if (_config.AutoContinueBarShowText)
        {
            float remaining = Math.Max(0f, _config.AutoContinueDelay - elapsed);
            string text = $"{remaining:F0}s";
            var textSize = ImGui.CalcTextSize(text);
            drawList.AddText(
                new Vector2(cursorPos.X + (availWidth - textSize.X) * 0.5f,
                            cursorPos.Y + (barHeight - textSize.Y) * 0.5f),
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), text);
        }

        ImGui.Dummy(new Vector2(availWidth, barHeight));
    }

    private void DrawDealerPanelV2()
    {
        ImGui.TextColored(new Vector4(1f, 0.65f, 0.2f, 1f), "Table");
        ImGui.Separator();
        if (ImGui.BeginTable("bjb_dealer_table_v2", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 170);
            ImGui.TableSetupColumn("Cards", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            ImGui.TableSetupColumn("Points", ImGuiTableColumnFlags.WidthFixed, 65);
            ImGui.TableSetupColumn("Dealer Controls", ImGuiTableColumnFlags.WidthFixed, 240);
            ImGui.TableHeadersRow();
            ImGui.TableNextRow();
            DrawDealerRow();
            ImGui.EndTable();
        }
    }

    private void DrawPlayersPanelV2()
    {
        _partyDissolved = _players.Count > 0 && !_players.Any(x => x.IsInParty);
        if (ImGui.BeginTable("bjb_main_table_v2", 10, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
        {
            SetupTableColumns();
            ImGui.TableHeadersRow();

            var playerSnapshot = _players.ToList();
            foreach (var player in playerSnapshot)
            {
                ImGui.TableNextRow();
                DrawPlayerRow(player, false);
            }
            ImGui.EndTable();
        }
    }

    private void DrawMainSharedPopupsV2()
    {
        if (_triggerAliasPopup)
        {
            ImGui.OpenPopup("bjb_alias_popup");
            _isAliasModalOpen = true;
            _triggerAliasPopup = false;
        }

        DrawAliasModal();
        DrawCleanDataPopupV2();
        DrawPanicPopupsV2();
    }

    private void DrawCleanDataPopupV2()
    {
        if (!ImGui.BeginPopupModal("bjb.clean_data.confirm.v2", ref _openCleanDataPopup, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        ImGui.TextColored(new Vector4(1f, 0.7f, 0.2f, 1f), "Clean residual session data?");
        ImGui.TextUnformatted("This will clear:");
        ImGui.BulletText("All recognized players (non-debug)");
        ImGui.BulletText("Dealer hand");
        ImGui.BulletText("DrawLogic debug hands and script cache");
        ImGui.Spacing();
        if (BJBGui.Button("Yes, clean##v2_clean_yes", new Vector2(160, 0)))
        {
            Chat.GameLog.PushSnapshot(_players, _dealer, GameEngine.CurrentPhase, "CleanData");
            _players.RemoveAll(p => !p.IsDebugPlayer);
            _dealer.Hands.Clear();
            DrawLogicDebugManager.Reset();
            Regex.RegexEngine.ClearNextRoundVotes();
            _save();
            _openCleanDataPopup = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (BJBGui.Button("Cancel##v2_clean_cancel", new Vector2(120, 0)))
        {
            _openCleanDataPopup = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    private void DrawPanicPopupsV2()
    {
        if (_panicConfirmStage == 1)
            ImGui.OpenPopup("panic_confirm_1_v2");

        bool panicOpen1 = true;
        if (ImGui.BeginPopupModal("panic_confirm_1_v2", ref panicOpen1,
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
        {
            ImGui.TextUnformatted("Are you sure?");
            ImGui.TextUnformatted("This will stop this round and jump back to the round start.");
            ImGui.Spacing();
            if (ImGui.Button("Yes##v2_panic1_yes"))
            {
                _panicConfirmStage = 2;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("No##v2_panic1_no"))
            {
                _panicConfirmStage = 0;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
        else if (_panicConfirmStage == 1)
            _panicConfirmStage = 0;

        if (_panicConfirmStage == 2)
            ImGui.OpenPopup("panic_confirm_2_v2");

        bool panicOpen2 = true;
        if (ImGui.BeginPopupModal("panic_confirm_2_v2", ref panicOpen2,
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
        {
            ImGui.TextColored(new Vector4(1, 0.3f, 0.3f, 1), "Are you REALLY sure?");
            ImGui.TextUnformatted("This is only a rescue option if the round is stuck.");
            ImGui.Spacing();
            if (ImGui.Button("Yes, PANIC##v2_panic2_yes"))
            {
                ExecutePanic();
                _panicConfirmStage = 0;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("No##v2_panic2_no"))
            {
                _panicConfirmStage = 0;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
        else if (_panicConfirmStage == 2)
            _panicConfirmStage = 0;
    }
}
