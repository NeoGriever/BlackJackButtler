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
    private DateTime? _groupDetectorActivatedAt;

    private void DrawMainPage()
    {
        if (_config.MainViewVersion == 2)
        {
            DrawMainPageV2();
            return;
        }

        DrawMainHeader();
        ImGui.Separator();
        DrawCustomButtonBar();

        ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), $"DEALER - Phase: {GameEngine.CurrentPhase}");

        if (ImGui.BeginTable("bjb_dealer_table", 4, ImGuiTableFlags.Borders))
        {

            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 150);
            ImGui.TableSetupColumn("Cards", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            ImGui.TableSetupColumn("Points", ImGuiTableColumnFlags.WidthFixed, 55);
            ImGui.TableSetupColumn("Controls", ImGuiTableColumnFlags.WidthFixed, 350);

            ImGui.TableNextRow();
            DrawDealerRow();
            ImGui.EndTable();
        }

        if (_config.AutoContinue && Plugin.AutoContinueActive)
        {
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

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.4f, 0.8f, 1, 1), "PLAYERS");
        ImGui.SameLine();
        {
            bool canTell = GameEngine.CanAcceptInterRoundDetectors();
            if (!canTell) ImGui.BeginDisabled();
            if (BJBGui.SmallButton("Bank /tell"))
            {
                var snapshot = _players.Where(p => p.IsActivePlayer && !p.IsOnHold).ToList();
                BankTellQueueManager.EnqueueMany(snapshot, _config, "MainV1All");
            }
            if (!canTell) ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("Post bank/bet info for all active players to party chat");

            if (CommandExecutor.IsRunning)
            {
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.0f, 0.0f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.1f, 0.1f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.5f, 0.0f, 0.0f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));
                if (ImGui.Button("STOP##stop_commands"))
                    CommandExecutor.CancelCurrentGroup();
                ImGui.PopStyleColor(4);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Stop currently running commands");
            }
        }
        {
            float checkboxSize = ImGui.GetFrameHeight();
            float spacing = ImGui.GetStyle().ItemSpacing.X;
            float rightEdge = ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX();

            var io = ImGui.GetIO();
            bool showPanic = io.KeyCtrl && io.KeyShift;
            float panicWidth = showPanic ? 60f : 0f;
            float panicOffset = showPanic ? panicWidth + spacing : 0f;

            if (!_notepadWindow.IsOpen)
            {
                ImGui.SameLine(rightEdge - checkboxSize - spacing - checkboxSize - panicOffset);
                ImGui.PushFont(UiBuilder.IconFont);
                if (BJBGui.Button(FontAwesomeIcon.StickyNote.ToIconString() + "##notepad_btn", new Vector2(checkboxSize, checkboxSize)))
                {
                    if (!_notepadLoaded) { _notepadLoaded = true; _notepadWindow.LoadContent(); }
                    _notepadWindow.IsOpen = true;
                }
                ImGui.PopFont();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Open Notepad");
            }

            ImGui.SameLine(rightEdge - checkboxSize - panicOffset);
            if (ImGui.Checkbox("##enable_bank_input", ref _config.EnableBankInput)) _save();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Enable Bank input");

            if (showPanic)
            {
                ImGui.SameLine(rightEdge - panicWidth);

                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.5f, 0.0f, 0.0f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.65f, 0.0f, 0.0f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.4f, 0.0f, 0.0f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));
                ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 1f, 0f, 1f));
                ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 2.0f);

                if (ImGui.Button("PANIC##panic_btn", new Vector2(panicWidth, checkboxSize)))
                {
                    _panicConfirmStage = 1;
                }

                ImGui.PopStyleVar();
                ImGui.PopStyleColor(5);
            }
        }
        if (ImGui.BeginTable("bjb_main_table", 10, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
        {
            SetupTableColumns();
            ImGui.TableHeadersRow();

            _partyDissolved = _players.Count > 0 && !_players.Any(x => x.IsInParty);
            var playerSnapshot = _players.ToList();
            foreach (var player in playerSnapshot)
            {
                ImGui.TableNextRow();
                ApplyPlayerRowBackground(player);
                DrawPlayerRow(player, false);
            }
            ImGui.EndTable();
        }

        DrawNearbyPlayersSection();

        if (_triggerAliasPopup)
        {
            ImGui.OpenPopup("bjb_alias_popup");
            _isAliasModalOpen = true;
            _triggerAliasPopup = false;
        }

        DrawAliasModal();

        if (_panicConfirmStage == 1)
            ImGui.OpenPopup("panic_confirm_1");

        bool panicOpen1 = true;
        if (ImGui.BeginPopupModal("panic_confirm_1", ref panicOpen1,
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
        {
            ImGui.TextUnformatted("Are you sure?");
            ImGui.TextUnformatted("This will stop this round and jump back to the round start.");
            ImGui.Spacing();
            if (ImGui.Button("Yes##panic1_yes"))
            {
                _panicConfirmStage = 2;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("No##panic1_no"))
            {
                _panicConfirmStage = 0;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
        else if (_panicConfirmStage == 1)
            _panicConfirmStage = 0;

        if (_panicConfirmStage == 2)
            ImGui.OpenPopup("panic_confirm_2");

        bool panicOpen2 = true;
        if (ImGui.BeginPopupModal("panic_confirm_2", ref panicOpen2,
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
        {
            ImGui.TextColored(new Vector4(1, 0.3f, 0.3f, 1), "Are you REALLY sure?");
            ImGui.TextUnformatted("This is only a rescue option if the round is stuck.");
            ImGui.Spacing();
            if (ImGui.Button("Yes, PANIC##panic2_yes"))
            {
                ExecutePanic();
                _panicConfirmStage = 0;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("No##panic2_no"))
            {
                _panicConfirmStage = 0;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
        else if (_panicConfirmStage == 2)
            _panicConfirmStage = 0;
    }

    private void DrawDealerRow()
    {
        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(0.05f, 0.05f, 0.05f, 1f)));

        ImGui.PushID("dealer_row");

        ImGui.TableNextColumn();
        var nameColor = new Vector4(1f, 1f, 0.2f, 1f);
        ImGui.TextColored(nameColor, _dealer.DisplayName);

        ImGui.TableNextColumn();
        DrawMultiHandCards(_dealer);

        ImGui.TableNextColumn();
        DrawMultiHandPoints(_dealer);

        ImGui.TableNextColumn();
        DrawDealerControls();

        ImGui.PopID();
    }

    private void DrawAliasModal()
    {
        if (ImGui.BeginPopupModal("bjb_alias_popup", ref _isAliasModalOpen, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (_editingAliasPlayer == null)
            {
                _isAliasModalOpen = false;
                ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
                return;
            }

            ImGui.Text($"Set Alias for: {_editingAliasPlayer.Name}");
            ImGui.Spacing();

            ImGui.SetNextItemWidth(250f);
            ImGui.InputText("##alias_input", ref _aliasInputBuffer, 32);

            ImGui.Spacing();
            if (BJBGui.Button("Save", new Vector2(120, 0)))
            {
                var input = _aliasInputBuffer.Trim();
                if (string.IsNullOrWhiteSpace(input) || input.Equals(_editingAliasPlayer.Name, StringComparison.OrdinalIgnoreCase))
                    _editingAliasPlayer.Alias = string.Empty;
                else
                    _editingAliasPlayer.Alias = input;

                _editingAliasPlayer = null;
                _isAliasModalOpen = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (BJBGui.Button("Cancel", new Vector2(120, 0)))
            {
                _editingAliasPlayer = null;
                _isAliasModalOpen = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    private void SetupTableColumns()
    {
        ImGui.TableSetupColumn("V", ImGuiTableColumnFlags.WidthFixed, 25);
        ImGui.TableSetupColumn("A", ImGuiTableColumnFlags.WidthFixed, 25);
        ImGui.TableSetupColumn("J", ImGuiTableColumnFlags.WidthFixed, 25);
        ImGui.TableSetupColumn("R", ImGuiTableColumnFlags.WidthFixed, 25);
        ImGui.TableSetupColumn("P", ImGuiTableColumnFlags.WidthFixed, 25);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 130);
        ImGui.TableSetupColumn("Bank", ImGuiTableColumnFlags.WidthStretch, 1.0f);
        ImGui.TableSetupColumn("Bet", ImGuiTableColumnFlags.WidthStretch, 1.0f);
        ImGui.TableSetupColumn("Cards", ImGuiTableColumnFlags.WidthFixed, 100);
        ImGui.TableSetupColumn("Points", ImGuiTableColumnFlags.WidthFixed, 55);
        ImGui.TableSetupColumn("Controls", ImGuiTableColumnFlags.WidthFixed, 200);
    }

    private void DrawMainHeader()
    {
        var io = ImGui.GetIO();
        bool canStop = io.KeyCtrl && io.KeyShift;

        var recon_text = IsRecognitionActive ? "● Group Detector" : "○ Group Detector";
        var auto_deal_text = _config.AutoInitialDeal ? "● Auto Player Hand" : "○ Auto Player Hand";
        var auto_dealer_text = _config.AutoDealerDraw ? "● Auto Dealer Draw" : "○ Auto Dealer Draw";

        if (BJBGui.Button(recon_text, new Vector2(200, 0)))
        {
            IsRecognitionActive = !IsRecognitionActive;
            if (IsRecognitionActive)
            {
                SyncParty();
                ViewDirectionManager.CaptureCurrentRotation(_config);
                _groupDetectorActivatedAt = StatsManager.IsRunning ? null : DateTime.Now;
            }

            if (!IsRecognitionActive)
            {
                if (!StatsManager.IsRunning)
                    SessionManager.ClearSession();
                RemovePlayersWithCompanionErase(p => !p.IsActivePlayer && p.Bank == 0);
                AddDebugLog(StatsManager.IsRunning
                    ? "[SessionManager] Session retained for active stats (Group Detector deactivated)"
                    : "[SessionManager] Session cleared (Group Detector deactivated)", false);
                if (StatsManager.IsRunning)
                    SaveSessionFromUI();
                _groupDetectorActivatedAt = null;
            }

            Plugin.Instance.UpdateEventHooks();
        }

        if (!IsRecognitionActive)
        {
            bool hasResidualData =
                _players.Any(p => !p.IsDebugPlayer)
                || _dealer.Hands.Any(h => h.Cards.Count > 0)
                || DrawLogicDebugManager.DebugHands.Any(h => h.Cards.Count > 0)
                || DrawLogicDebugManager.ValidScriptCache.Count > 0;
            if (hasResidualData)
            {
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.55f, 0.30f, 0.05f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.75f, 0.40f, 0.10f, 1f));
                if (BJBGui.Button("Clean Data##clean_residual", new Vector2(140, 0)))
                {
                    _openCleanDataPopup = true;
                    ImGui.OpenPopup("bjb.clean_data.confirm");
                }
                ImGui.PopStyleColor(2);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Group Detector is off but session data remains. Click to clean players and DrawLogic debug data.");
            }
        }

        if (ImGui.BeginPopupModal("bjb.clean_data.confirm", ref _openCleanDataPopup, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextColored(new Vector4(1f, 0.7f, 0.2f, 1f), "Clean residual session data?");
            ImGui.TextUnformatted("This will clear:");
            ImGui.BulletText("All recognized players (non-debug)");
            ImGui.BulletText("Dealer hand");
            ImGui.BulletText("DrawLogic debug hands and script cache");
            ImGui.Spacing();
            if (BJBGui.Button("Yes, clean", new Vector2(160, 0)))
            {
                Chat.GameLog.PushSnapshot(_players, _dealer, GameEngine.CurrentPhase, "CleanData");
                RemovePlayersWithCompanionErase(p => !p.IsDebugPlayer);
                _dealer.Hands.Clear();
                DrawLogicDebugManager.Reset();
                Regex.RegexEngine.ClearNextRoundVotes();
                _save();
                _openCleanDataPopup = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (BJBGui.Button("Cancel", new Vector2(120, 0)))
            {
                _openCleanDataPopup = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        if (IsRecognitionActive && !StatsManager.IsRunning && _groupDetectorActivatedAt.HasValue)
        {
            double elapsed = (DateTime.Now - _groupDetectorActivatedAt.Value).TotalSeconds;
            if (elapsed < 30)
            {
                ImGui.SameLine();
                int secondsLeft = 30 - (int)elapsed;
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.1f, 0.5f, 0.1f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.15f, 0.65f, 0.15f, 1f));
                if (BJBGui.Button($"Start Bank ({secondsLeft}s)##groupdetect_startbank", new Vector2(160, 0)))
                {
                    StatsManager.StartSession();
                    SaveSessionFromUI();
                    _groupDetectorActivatedAt = null;
                }
                ImGui.PopStyleColor(2);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Start the stats session with the current bank.\nAvailable for 30 seconds after activating Group Detector.");
            }
            else
            {
                _groupDetectorActivatedAt = null;
            }
        }

        ImGui.SameLine();

        bool autoDealActive = _config.AutoInitialDeal;
        if (autoDealActive) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.5f, 0.0f, 1.0f));

        if (BJBGui.Button(auto_deal_text))
        {
            var newValue = !_config.AutoInitialDeal;
            _config.AutoInitialDeal = newValue;
            Plugin.Instance.ResetAutoActionState(cancelCurrentGroup: !newValue);
            _save();
        }

        if (autoDealActive) ImGui.PopStyleColor();


        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Automatically deals the initial hand to players during the Initial Deal phase.");

        ImGui.SameLine();

        bool autoDealerActive = _config.AutoDealerDraw;
        if (autoDealerActive) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.5f, 0.0f, 1.0f));

        if (BJBGui.Button(auto_dealer_text))
        {
            var newValue = !_config.AutoDealerDraw;
            _config.AutoDealerDraw = newValue;
            Plugin.Instance.ResetAutoActionState(cancelCurrentGroup: !newValue);
            _save();
        }

        if (autoDealerActive) ImGui.PopStyleColor();

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"Dealer draws until {(_config.DealerSoftRule ? "soft" : "hard")} {_config.DealerDrawsUntil}, then stands.");

        ImGui.SameLine();

        var auto_continue_text = _config.AutoContinue ? "● Auto Continue" : "○ Auto Continue";
        bool autoContinueActive = _config.AutoContinue;
        if (autoContinueActive) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.5f, 0.0f, 1.0f));

        if (BJBGui.Button(auto_continue_text))
        {
            var newValue = !_config.AutoContinue;
            _config.AutoContinue = newValue;
            Plugin.Instance.ResetAutoActionState(cancelCurrentGroup: !newValue);
            _save();
        }

        if (autoContinueActive) ImGui.PopStyleColor();

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"Automatically starts the next round after {_config.AutoContinueDelay:0}s of no chat activity.");

        bool hasAutoTriggers = _config.UserRegexes.Any(r =>
            r.Enabled && r.Mode == RegexEntryMode.Trigger &&
            (r.Action == RegexAction.WantHit || r.Action == RegexAction.WantStand ||
             r.Action == RegexAction.WantDD || r.Action == RegexAction.WantSplit));

        if (hasAutoTriggers)
        {
            ImGui.SameLine();

            var auto_run_text = _config.AutoRun ? "● Auto Run" : "○ Auto Run";
            bool autoRunActive = _config.AutoRun;
            if (autoRunActive) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.5f, 0.0f, 1.0f));

            if (BJBGui.Button(auto_run_text))
            {
                var newValue = !_config.AutoRun;
                _config.AutoRun = newValue;
                Plugin.Instance.ResetAutoActionState(cancelCurrentGroup: !newValue);
                _save();
            }

            if (autoRunActive) ImGui.PopStyleColor();

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("When ON, player action triggers (Hit/Stand/DD/Split) execute automatically.\nWhen OFF, they highlight the corresponding button instead.");
        }

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

    private void DrawCustomButtonBar()
    {
        if (_config.CustomButtonOrder.Count == 0 && _config.CustomCommandGroups.Count == 0) return;

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
        bool isRunning = CommandExecutor.IsRunning;
        if (isRunning) ImGui.BeginDisabled();

        bool prevWasButton = false;

        for (int i = 0; i < _config.CustomButtonOrder.Count; i++)
        {
            var entry = _config.CustomButtonOrder[i];

            if (entry == "---")
            {
                if (prevWasButton)
                {
                    bool hasButtonAfter = false;
                    for (int j = i + 1; j < _config.CustomButtonOrder.Count; j++)
                    {
                        if (_config.CustomButtonOrder[j] == "---") continue;
                        if (_config.CustomCommandGroups.Any(g => g.Name == _config.CustomButtonOrder[j]))
                        { hasButtonAfter = true; break; }
                    }
                    if (!hasButtonAfter) continue;
                    prevWasButton = false;
                }
                continue;
            }

            var group = _config.CustomCommandGroups.FirstOrDefault(g => g.Name == entry);
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
            var buttonSize = _config.ButtonBarFixedWidth
                ? new Vector2(_config.ButtonBarFixedWidthValue, 0)
                : Vector2.Zero;

            if (colorPushCount > 0 && group.UseCustomTextColor)
                clicked = buttonSize.X > 0
                    ? ImGui.Button($"{displayLabel}##{idSuffix}_{i}", buttonSize)
                    : ImGui.Button($"{displayLabel}##{idSuffix}_{i}");
            else
                clicked = buttonSize.X > 0
                    ? BJBGui.Button($"{displayLabel}##{idSuffix}_{i}", buttonSize)
                    : BJBGui.Button($"{displayLabel}##{idSuffix}_{i}");

            if (colorPushCount > 0) ImGui.PopStyleColor(colorPushCount);

            ImGui.PopStyleVar();
            if (scale != 1.0f) ImGui.SetWindowFontScale(1.0f);
            if (useMono) ImGui.PopFont();

            if (clicked)
            {
                var targetName = _players.FirstOrDefault(p => p.IsCurrentTurn)?.DisplayName
                              ?? _dealer.DisplayName;
                var groupName = group.Name;
                Task.Run(() => CommandExecutor.ExecuteGroup(groupName, targetName, _config));
            }

            prevWasButton = true;
        }

        if (isRunning) ImGui.EndDisabled();
    }

    private bool IsLocalPlayerPartyLeader()
    {
        var localName = Plugin.ObjectTable.LocalPlayer?.Name.TextValue;
        if (string.IsNullOrEmpty(localName) || Plugin.PartyList.Length == 0)
            return true;
        var leader = Plugin.PartyList[(int)Plugin.PartyList.PartyLeaderIndex];
        return leader != null
            && string.Equals(leader.Name.TextValue, localName, StringComparison.Ordinal);
    }

    private string GetPartyLeaderName()
    {
        if (Plugin.PartyList.Length == 0) return string.Empty;
        var leader = Plugin.PartyList[(int)Plugin.PartyList.PartyLeaderIndex];
        return leader?.Name.TextValue ?? string.Empty;
    }

    private void DrawPlayerRow(PlayerState p, bool isDealer)
    {
        uint bgColor;
        if (!p.IsActivePlayer) bgColor = ImGui.GetColorU32(new Vector4(0, 0, 0, 1));
        else if (p.IsCurrentTurn) bgColor = ImGui.GetColorU32(new Vector4(0.0f, 0.25f, 0.0f, 1f));
        else if (p.IsOnHold) bgColor = ImGui.GetColorU32(new Vector4(0.15f, 0.15f, 0.15f, 1f));
        else bgColor = ImGui.GetColorU32(new Vector4(0.0f, 0.0f, 0.2f, 1f));

        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, bgColor);
        ImGui.PushID(p.Name);

        ImGui.TableNextColumn();
        if (p.IsActivePlayer && _config.VipBetTiers.Count > 0)
        {
            string worldName = VipManager.ResolveWorldName(p.WorldId);
            int currentTier = VipManager.GetPlayerTier(p.Name, worldName);

            bool isVip = currentTier > 0;
            var btnColor = isVip
                ? new Vector4(1.0f, 0.6f, 0.0f, 1f)
                : new Vector4(0.35f, 0.22f, 0.08f, 1f);
            var txtColor = isVip
                ? new Vector4(0.8f, 0.1f, 0.1f, 1f)
                : new Vector4(0.5f, 0.5f, 0.5f, 1f);

            ImGui.PushStyleColor(ImGuiCol.Button, btnColor);
            ImGui.PushStyleColor(ImGuiCol.Text, txtColor);

            string vipLabel = isVip && currentTier <= _config.VipBetTiers.Count
                ? $"{currentTier}##vip_{p.UIID}"
                : $"V##vip_{p.UIID}";
            if (BJBGui.Button(vipLabel))
            {
                VipManager.CycleTier(p.Name, worldName, _config.VipBetTiers.Count);
            }

            ImGui.PopStyleColor(2);

            if (ImGui.IsItemHovered())
            {
                if (isVip && currentTier <= _config.VipBetTiers.Count)
                    ImGui.SetTooltip($"{_config.VipBetTiers[currentTier - 1].Name} (Max: {_config.VipBetTiers[currentTier - 1].MaxBet:N0})\nClick to cycle tier");
                else
                    ImGui.SetTooltip("Click to cycle tier");
            }
        }

        ImGui.TableNextColumn();
        if (p.IsActivePlayer) {
            bool hlAlias = p.HighlightAlias;
            bool clickedAlias = hlAlias
                ? BJBGui.ButtonHighlighted($"A##alias_btn_{p.UIID}", _config.HighlightColor, _config.HighlightTextColor)
                : BJBGui.Button($"A##alias_btn_{p.UIID}");
            if (clickedAlias) {
                p.HighlightAlias = false;
                _editingAliasPlayer = p;
                _aliasInputBuffer = !string.IsNullOrWhiteSpace(p.Alias) ? p.Alias : p.Name;
                _triggerAliasPopup = true;
            }
        }

        ImGui.TableNextColumn();
        if (!p.IsActivePlayer) {
            bool hlJoin = p.HighlightJoin;
            bool clickedJoin = hlJoin
                ? BJBGui.ButtonHighlighted($">##{p.UIID}", new Vector2(-1, 0), _config.HighlightColor, _config.HighlightTextColor)
                : BJBGui.Button($">##{p.UIID}", new Vector2(-1, 0));
            if (clickedJoin)
            {
                p.HighlightJoin = false;
                p.IsActivePlayer = true;
                var joinPhase = GameEngine.CurrentPhase;
                if (joinPhase == GamePhase.InitialDeal || joinPhase == GamePhase.PlayersTurn || joinPhase == GamePhase.DealerTurn)
                    p.JoinedMidRound = true;
                ActivityLogManager.LogPlayerJoin(p.DisplayName);
                RoundLogManager.RecordPlayerJoin(p.DisplayName);
                SaveSessionFromUI();
                CompanionSyncManager.SendPlayerBankBetUpdate(_config, p);
            }
        }
        else {
            bool hlLeave = p.HighlightLeave;
            bool removeRowAfterLeave = false;
            bool clickedLeave = hlLeave
                ? BJBGui.ButtonHighlighted($"X##{p.UIID}", new Vector2(-1, 0), _config.HighlightColor, _config.HighlightTextColor)
                : BJBGui.Button($"X##{p.UIID}", new Vector2(-1, 0));
            if (clickedLeave) {
                p.HighlightLeave = false;
                var leavePhase = GameEngine.CurrentPhase;
                if (leavePhase == GamePhase.InitialDeal || leavePhase == GamePhase.PlayersTurn || leavePhase == GamePhase.DealerTurn)
                {
                    GameEngine.DeactivatePlayerMidRound(p, _players, _config);
                }
                else
                {
                    if (p.Bank == 0) ActivityLogManager.LogPlayerLeave(p.DisplayName);
                    RoundLogManager.RecordPlayerLeave(p.DisplayName);
                    p.IsActivePlayer = false;
                    p.IsCurrentTurn = false;
                    p.ReadySkip = false;
                }
                p.CurrentBet = 0;
                SaveSessionFromUI();
                if (TryRemoveEmptyInactiveDebugPlayer(p))
                    removeRowAfterLeave = true;
                else
                    CompanionSyncManager.SendPlayerBankBetUpdate(_config, p);
            }
            if (removeRowAfterLeave) { ImGui.PopID(); return; }
        }

        // R — Auto-Ready Spalte
        ImGui.TableNextColumn();
        if (p.IsActivePlayer && !p.IsOnHold && !p.IsOnBench
            && GameEngine.CanAcceptInterRoundDetectors())
        {
            int activeCountR = _players.Count(pl => pl.IsActivePlayer && !pl.IsOnHold);
            bool canToggleR = activeCountR >= 2;
            if (!canToggleR) ImGui.BeginDisabled();
            bool wasReadyR = p.ReadySkip;
            if (wasReadyR) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.8f, 0.2f, 1f));
            if (BJBGui.SmallButton($"R##{p.UIID}_readyskip_col"))
            {
                p.ReadySkip = !p.ReadySkip;
                _save();
                if (p.ReadySkip) RegexEngine.CheckAutoReadyStart(_players, _config);
            }
            if (wasReadyR) ImGui.PopStyleColor();
            if (!canToggleR) ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip(canToggleR
                    ? (p.ReadySkip ? "Ready-skip ON: Auto-counted as voted" : "Click to auto-count as voted for next round")
                    : "Need 2+ active players to enable ready-skip");
        }

        // P — Pause/Hold Spalte
        ImGui.TableNextColumn();
        if (p.IsActivePlayer) {
            var currentPhase = GameEngine.CurrentPhase;
            bool isActiveRound = currentPhase == GamePhase.InitialDeal || currentPhase == GamePhase.PlayersTurn;

            bool isDisabled = false;
            if (p.IsOnBench)
            {
                isDisabled = (currentPhase == GamePhase.DealerTurn || currentPhase == GamePhase.Payout);
            }
            else if (p.IsOnHold)
            {
                isDisabled = false;
            }
            else
            {
                if (isActiveRound)
                    isDisabled = !GameEngine.CanMovePlayerToBench(p, _players);
            }

            if (isDisabled) ImGui.BeginDisabled();

            int colorsPushed = 0;
            bool holdHighlighted = false;
            if (p.IsOnBench)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.5f, 0.0f, 1f));
                colorsPushed = 1;
            }
            else if (p.IsOnHold)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1f));
                colorsPushed = 1;
            }
            else if (p.HighlightPause)
            {
                holdHighlighted = true;
            }

            bool clickedHold = holdHighlighted
                ? BJBGui.ButtonHighlighted($"H##hold_{p.UIID}", _config.HighlightColor, _config.HighlightTextColor)
                : BJBGui.Button($"H##hold_{p.UIID}");
            if (clickedHold)
            {
                p.HighlightPause = false;

                if (p.IsOnBench)
                {
                    if (currentPhase != GamePhase.DealerTurn && currentPhase != GamePhase.Payout)
                        GameEngine.MovePlayerFromBench(p);
                }
                else if (p.IsOnHold)
                {
                    if (isActiveRound)
                    {
                        p.IsOnHold = false;
                        p.IsOnBench = true;
                        p.BenchedAt = DateTime.UtcNow;
                        p.WasOnHoldThisRound = true;
                    }
                    else
                    {
                        p.IsOnHold = false;
                    }
                }
                else
                {
                    if (isActiveRound && GameEngine.CanMovePlayerToBench(p, _players))
                    {
                        bool wasCurrentTurn = p.IsCurrentTurn;
                        GameEngine.MovePlayerToBench(p, _players);
                        if (wasCurrentTurn)
                            GameEngine.NextTurn(_players, _config);
                    }
                    else
                    {
                        p.IsOnHold = !p.IsOnHold;
                        if (p.IsOnHold) p.ReadySkip = false;
                    }
                }
                _save();
            }

            if (colorsPushed > 0) ImGui.PopStyleColor(colorsPushed);
            if (isDisabled) ImGui.EndDisabled();

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                if (p.IsOnBench)
                    ImGui.SetTooltip("On bench - Click to return");
                else if (p.IsOnHold && isActiveRound)
                    ImGui.SetTooltip("On hold - Click to join via bench (late entry)");
                else if (p.IsOnHold)
                    ImGui.SetTooltip("On hold - Click to reactivate for next round");
                else if (isActiveRound)
                    ImGui.SetTooltip("Click to move to bench (pause during round)");
                else
                    ImGui.SetTooltip("Click to hold (skip next round)");
            }
        }

        ImGui.TableNextColumn();
        Vector4 nameColor;
        if (p.IsCurrentTurn)
            nameColor = new Vector4(1f, 1f, 0.2f, 1f);
        else if (p.IsActivePlayer && !p.IsOnHold
                 && (p.ReadySkip || RegexEngine.HasPlayerVoted(p.Name))
                 && GameEngine.CanAcceptInterRoundDetectors())
            nameColor = new Vector4(0.2f, 1f, 0.2f, 1f);
        else
            nameColor = new Vector4(1, 1, 1, 1);
        ImGui.TextColored(nameColor, p.DisplayName);
        // Ready-skip wurde in die R-Spalte verschoben

        ImGui.TableNextColumn();
        {
            bool canTellPlayer = GameEngine.CanAcceptInterRoundDetectors();
            float tButtonWidth = 25f;
            float heartButtonWidth = 25f;
            float mButtonWidth = 25f;
            float spacing = ImGui.GetStyle().ItemSpacing.X;

            long bankBefore = p.Bank;
            bool removeRowAfterBankEdit = false;
            if (!_config.EnableBankInput) ImGui.BeginDisabled();
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - tButtonWidth - heartButtonWidth - mButtonWidth - spacing * 3);
            if (BJBGui.InputLong($"##bank_{p.UIID}", ref p.Bank, 1000, 10000))
            {
                _bankChanged.Add(p.UIID);
                _save();
            }
            if (ImGui.IsItemActivated()) _bankSnapshot[p.UIID] = bankBefore;
            if (ImGui.IsItemDeactivated())
            {
                bool bankChanged = _bankChanged.Remove(p.UIID);
                if (_bankSnapshot.TryGetValue(p.UIID, out long oldBank))
                {
                    if (bankChanged && p.Bank != oldBank)
                        ActivityLogManager.LogBankChange(p.DisplayName, oldBank, p.Bank);
                    _bankSnapshot.Remove(p.UIID);
                }
                if (bankChanged)
                {
                    SaveSessionFromUI();
                    if (TryRemoveEmptyInactiveDebugPlayer(p))
                        removeRowAfterBankEdit = true;
                    else
                        CompanionSyncManager.SendPlayerBankBetUpdate(_config, p);
                }
            }
            if (!_config.EnableBankInput) ImGui.EndDisabled();
            if (removeRowAfterBankEdit) { ImGui.PopID(); return; }

            ImGui.SameLine();
            if (!canTellPlayer) ImGui.BeginDisabled();
            bool hlTell = p.HighlightTell && canTellPlayer;
            bool clickedTell = hlTell
                ? BJBGui.SmallButtonHighlighted($"T##tell_{p.UIID}", _config.HighlightColor, _config.HighlightTextColor)
                : BJBGui.SmallButton($"T##tell_{p.UIID}");
            if (clickedTell)
            {
                p.HighlightTell = false;
                BankTellQueueManager.Enqueue(p, _config, "PlayerButton");
            }
            if (!canTellPlayer) ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("Post bank/bet info for this player to party chat");

            ImGui.SameLine();
            bool hasUndo = _bankToTipUndo.TryGetValue(p.UIID, out var undoEntry)
                           && (DateTime.Now - undoEntry.clickedAt).TotalSeconds < 10;
            if (hasUndo)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.5f, 0.0f, 1f));
                if (BJBGui.SmallButton($"U##heart_{p.UIID}"))
                {
                    p.Bank = undoEntry.amount;
                    StatsManager.AddTip(-undoEntry.amount);
                    _bankToTipUndo.Remove(p.UIID);
                    SaveSessionFromUI();
                    CompanionSyncManager.SendPlayerBankBetUpdate(_config, p);
                }
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                {
                    int remaining = Math.Max(0, 10 - (int)(DateTime.Now - undoEntry.clickedAt).TotalSeconds);
                    ImGui.SetTooltip($"Undo Bank→Tip ({remaining}s)");
                }
            }
            else
            {
                _bankToTipUndo.Remove(p.UIID);
                bool canHeart = StatsManager.IsRunning && p.Bank > 0;
                bool ctrlDown = ImGui.GetIO().KeyCtrl;
                if (!canHeart || !ctrlDown) ImGui.BeginDisabled();
                ImGui.PushFont(UiBuilder.IconFont);
                if (BJBGui.SmallButton(FontAwesomeIcon.Heart.ToIconString() + $"##heart_{p.UIID}"))
                {
                    long amount = p.Bank;
                    StatsManager.AddTip(amount);
                    p.Bank = 0;
                    _bankToTipUndo[p.UIID] = (amount, DateTime.Now);
                    SaveSessionFromUI();
                    CompanionSyncManager.SendPlayerBankBetUpdate(_config, p);
                }
                ImGui.PopFont();
                if (!canHeart || !ctrlDown) ImGui.EndDisabled();
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    if (!StatsManager.IsRunning)
                        ImGui.SetTooltip("Start a stats session first");
                    else if (!ctrlDown)
                        ImGui.SetTooltip("Hold CTRL to transfer bank to tips");
                    else
                        ImGui.SetTooltip("Transfer entire bank to tips");
                }
            }

            ImGui.SameLine();
            if (BJBGui.SmallButton($"M##maxbet_{p.UIID}"))
            {
                p.CurrentBet = Math.Min(p.GetEffectiveMaxBet(_config), p.Bank);
                _save();
                SaveSessionFromUI();
                CompanionSyncManager.SendPlayerBankBetUpdate(_config, p);
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"Set bet to lower value of max bet ({p.GetEffectiveMaxBet(_config):N0}) or bank ({p.Bank:N0})");
        }

        ImGui.TableNextColumn();
        long effectiveMaxBet = p.GetEffectiveMaxBet(_config);
        bool betOutOfRange = p.CurrentBet < _config.MinBet || p.CurrentBet > effectiveMaxBet;
        if (betOutOfRange)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
            if (BJBGui.SmallButton($"!##bet_warn_{p.UIID}"))
            {
                _page = Page.Settings;
                _pendingSettingsFocus = p.CurrentBet < _config.MinBet ? "min_bet" : "max_bet";
                _pendingSettingsTab = "Betting";
            }
            ImGui.PopStyleColor();
            if (ImGui.IsItemHovered())
            {
                if (p.CurrentBet < _config.MinBet)
                    ImGui.SetTooltip($"Bet is below minimum ({_config.MinBet:N0})");
                else
                {
                    var vipName = p.GetVipTierName(_config);
                    if (!string.IsNullOrEmpty(vipName))
                        ImGui.SetTooltip($"Bet is above {vipName} maximum ({effectiveMaxBet:N0})");
                    else
                        ImGui.SetTooltip($"Bet is above maximum ({effectiveMaxBet:N0})");
                }
            }
            ImGui.SameLine();
        }
        bool hlBet = p.HighlightBet;
        if (hlBet)
        {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, _config.HighlightColor);
            ImGui.PushStyleColor(ImGuiCol.Text, _config.HighlightTextColor);
        }
        long betBefore = p.CurrentBet;
        ImGui.SetNextItemWidth(-1);
        // Use raw ImGui.InputLong when highlighted so HighlightTextColor is not overridden internally
        if (hlBet ? ImGui.InputLong($"##bet_{p.UIID}", ref p.CurrentBet, 500, 5000) : BJBGui.InputLong($"##bet_{p.UIID}", ref p.CurrentBet, 500, 5000))
        {
            p.HighlightBet = false;
            _betChanged.Add(p.UIID);
            _save();
        }
        if (ImGui.IsItemActivated()) _betSnapshot[p.UIID] = betBefore;
        if (ImGui.IsItemDeactivated())
        {
            bool betChanged = _betChanged.Remove(p.UIID);
            if (_betSnapshot.TryGetValue(p.UIID, out long oldBet))
            {
                if (betChanged && p.CurrentBet != oldBet)
                    ActivityLogManager.LogBetSet(p.DisplayName, p.CurrentBet);
                _betSnapshot.Remove(p.UIID);
            }
            if (betChanged)
            {
                SaveSessionFromUI();
                CompanionSyncManager.SendPlayerBankBetUpdate(_config, p);
            }
        }
        if (hlBet) ImGui.PopStyleColor(2);

        ImGui.TableNextColumn();
        DrawMultiHandCards(p);
        ImGui.TableNextColumn();
        DrawMultiHandPoints(p);
        ImGui.TableNextColumn();
        DrawPlayerControls(p);

        ImGui.PopID();
    }

    private void DrawMultiHandCards(PlayerState p)
    {
        if (p.Hands.Count == 0) { ImGui.Text("-"); return; }

        var startPos = ImGui.GetCursorPos();

        for (int i = 0; i < p.Hands.Count; i++)
        {
            var cards = p.Hands[i].Cards;
            if (cards.Count == 0) {
                if (p.IsCurrentTurn && p.CurrentHandIndex == i) ImGui.TextColored(new Vector4(1, 1, 0, 1), "[-]");
                else ImGui.Text(" - ");
                continue;
            }

            foreach (var card in cards)
            {
                Vector4 color = (card.Suit == CardSuit.Diamonds || card.Suit == CardSuit.Hearts)
                    ? new Vector4(1, 0.3f, 0.3f, 1)
                    : new Vector4(0.9f, 0.9f, 0.9f, 1);

                ImGui.TextColored(color, card.ToString());
                ImGui.SameLine(0, 4);
            }
            ImGui.Text(" ");
        }

        bool isDealer = _dealer != null && ReferenceEquals(p, _dealer);
        if (!isDealer && !p.IsCurrentTurn && !CommandExecutor.IsRunning)
        {
            var endPos = ImGui.GetCursorPos();
            ImGui.SetCursorPos(startPos);
            var regionSize = new Vector2(ImGui.GetColumnWidth(), endPos.Y - startPos.Y);
            ImGui.InvisibleButton($"##cardsclick_{p.UIID}", regionSize);
            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                OpenHandEditPopup(p, p.CurrentHandIndex);
            ImGui.SetCursorPos(endPos);
        }
    }

    private void DrawMultiHandPoints(PlayerState p)
    {
        if (p.Hands.Count == 0) { ImGui.Text("-"); return; }

        for (int i = 0; i < p.Hands.Count; i++)
        {
            var hand = p.Hands[i];
            if (hand.Cards.Count == 0) { ImGui.Text("-"); continue; }

            var (min, max) = p.CalculatePoints(i);
            int best = max.HasValue ? max.Value : min;

            if (best == 21)
            {
                if (hand.IsNaturalBlackJack)
                {
                    ImGui.TextColored(new Vector4(0.0f, 1.0f, 0.0f, 1.0f), "BJ");
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Natural BlackJack (2 Cards)");
                }
                else
                {
                    ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.0f, 1.0f), "BJ");
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Dirty BlackJack (3+ Cards)");
                }
            }
            else if (hand.IsCharlie)
            {
                ImGui.TextColored(new Vector4(0.4f, 1.0f, 0.4f, 1.0f), $"C{best}");
                if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Charlie ({hand.Cards.Count} cards)");
            }
            else if (hand.IsBust)
            {
                var color = new Vector4(1.0f, 0.2f, 0.2f, 1.0f);
                ImGui.TextColored(color, $"{best}");

                Vector2 minPos = ImGui.GetItemRectMin();
                Vector2 maxPos = ImGui.GetItemRectMax();

                float midY = minPos.Y + (maxPos.Y - minPos.Y) * 0.5f;

                ImGui.GetWindowDrawList().AddLine(
                    new Vector2(minPos.X, midY),
                    new Vector2(maxPos.X, midY),
                    ImGui.GetColorU32(color),
                    1.5f
                );

                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Bust!");
            }
            else
            {
                var display = max.HasValue ? $"{min}/{max}" : $"{min}";
                ImGui.Text(display);
            }
        }
    }

    private static readonly Dictionary<string, string> _groupDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Initial", "Auto-deal" },
        { "Hit", "Auto-draw" },
        { "DD", "Auto-DD" },
        { "Split", "Auto-split" },
        { "SplitDraw", "Auto-split-draw" },
        { "Natural BlackJack Notify", "Nat.BJ" },
        { "Dirty BlackJack Notify", "Dirty BJ" },
        { "Charlie Notify", "Charlie" },
    };

    private void DrawEmergencyStopRow(PlayerState p)
    {
        string groupName = CommandExecutor.CurrentGroupName;
        string label = _groupDisplayNames.TryGetValue(groupName, out var display)
            ? display : $"Auto-{groupName}";

        ImGui.TextColored(new Vector4(1f, 0.5f, 0f, 1f), $"{label} ...");

        ImGui.SameLine(ImGui.GetColumnWidth() - 30f);

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.0f, 0.0f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.1f, 0.1f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.5f, 0.0f, 0.0f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));

        if (ImGui.Button($"X##estop_{p.UIID}", new Vector2(25, 0)))
        {
            var snapshotIndex = CommandExecutor.PreActionSnapshotIndex;

            CommandExecutor.CancelCurrentGroup();

            _config.AutoRun = false;
            _config.AutoInitialDeal = false;
            _save();

            if (snapshotIndex >= 0)
            {
                var phase = GameEngine.CurrentPhase;
                Chat.GameLog.ApplySnapshot(snapshotIndex, _players, ref _dealer, ref phase);
                GameEngine.CurrentPhase = phase;
                GameEngine.ClearForcedRecipient();
            }

            var restoredPlayer = _players.FirstOrDefault(pl =>
                pl.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase));
            restoredPlayer?.ResetHighlightsOnceConsistent();

            AddDebugLog($"[EmergencyStop] Aborted '{groupName}' for {p.DisplayName}, state restored, AutoRun disabled", false);
        }

        ImGui.PopStyleColor(4);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Emergency Stop: Abort action, restore state, disable Auto Run");
    }

    private void ClearPlayer(PlayerState p, bool bankToTip)
    {
        if (bankToTip && p.Bank > 0)
            StatsManager.AddTip(p.Bank);
        p.Bank = 0;
        p.CurrentBet = 0;
        if (TryRemoveEmptyInactiveDebugPlayer(p))
            return;
        CompanionSyncManager.SendPlayerBankBetUpdate(_config, p);
    }

    private void DrawPlayerControls(PlayerState p)
    {
        bool showClear = (!p.IsActivePlayer && !p.IsInParty) || _partyDissolved;
        if (showClear && (p.Bank > 0 || p.CurrentBet > 0))
        {
            var io = ImGui.GetIO();
            bool shift = io.KeyShift;
            bool ctrl = io.KeyCtrl;

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.0f, 0.0f, 1f));
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));
            if (BJBGui.SmallButton($"CLEAR##clear_{p.UIID}"))
            {
                if (shift)
                {
                    foreach (var pl in _players.ToList())
                    {
                        bool plClearable = ((!pl.IsActivePlayer && !pl.IsInParty) || _partyDissolved)
                                           && (pl.Bank > 0 || pl.CurrentBet > 0);
                        if (plClearable)
                            ClearPlayer(pl, ctrl);
                    }
                }
                else
                {
                    ClearPlayer(p, ctrl);
                }
            }
            ImGui.PopStyleColor(2);

            if (ImGui.IsItemHovered())
            {
                string tip = ctrl
                    ? "CTRL: Bank \u2192 Tips, Bet = 0"
                    : "Bank = 0, Bet = 0";
                if (shift) tip = "SHIFT: " + tip + " (all clearable)";
                ImGui.SetTooltip(tip);
            }
            return;
        }

        bool isExecutingForThis = CommandExecutor.IsRunning
            && CommandExecutor.CurrentGroupHasDice
            && CommandExecutor.CurrentTargetPlayer.Equals(p.Name, StringComparison.OrdinalIgnoreCase);

        if (isExecutingForThis)
        {
            DrawEmergencyStopRow(p);
            return;
        }

        bool globalLock = CommandExecutor.IsRunning || _showSplitMoneyPopup || _showDDMoneyPopup;
        if (globalLock) ImGui.BeginDisabled();

        if (p.IsCurrentTurn && p.CurrentHandIndex > 0)
            ImGui.Dummy(new Vector2(0, p.CurrentHandIndex * ImGui.GetFrameHeightWithSpacing()));

        InnerPlayerControls(p);

        if (globalLock) ImGui.EndDisabled();

        var phase = GameEngine.CurrentPhase;
        if (phase == GamePhase.Waiting || phase == GamePhase.Payout)
        {
            var currentTarget = GameEngine.GetCurrentTargetName();
            bool isTargeted = p.Name.Equals(currentTarget, StringComparison.OrdinalIgnoreCase);
            if (!isTargeted)
            {
                ImGui.SameLine();
                ImGui.PushFont(UiBuilder.IconFont);
                if (BJBGui.SmallButton($"{FontAwesomeIcon.Crosshairs.ToIconString()}##target_{p.UIID}"))
                {
                    GameEngine.TargetPlayer(p.Name);
                    VariableManager.SetPlayerVariables(p);
                }
                ImGui.PopFont();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Target this player");
            }
        }
    }

    private void InnerPlayerControls(PlayerState p)
    {
        var phase = GameEngine.CurrentPhase;

        if (phase == GamePhase.Payout)
        {
            bool shouldHighlight = p.HighlightPay;
            bool clickedPay = shouldHighlight
                ? BJBGui.SmallButtonHighlighted($"Pay Out##{p.UIID}", _config.HighlightColor, _config.HighlightTextColor)
                : BJBGui.SmallButton($"Pay Out##{p.UIID}");
            if (clickedPay)
            {
                p.HighlightPay = false;
                DropboxIntegration.PayOut(p);
            }
            return;
        }

        if (!p.IsCurrentTurn)
        {
            ImGui.TextDisabled("Waiting for turn ...");
            return;
        }

        if (_showSplitMoneyPopup || _showDDMoneyPopup)
        {
            ImGui.TextColored(new Vector4(1, 0.5f, 0, 1), "Awaiting payment ...");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Buttons are disabled until the payment is processed or cancelled.");
            return;
        }

        if (phase == GamePhase.InitialDeal && !p.HasInitialHandDealt)
        {
            if (_config.AutoInitialDeal)
            {
                ImGui.TextDisabled("Auto...");
            }
            else
            {
                if (BJBGui.SmallButton($"Deal Hand##deal_{p.UIID}"))
                {
                    Task.Run(() => GameEngine.ActionDealHand(p, _config, _players));
                }
            }
            return;
        }

        if (phase == GamePhase.PlayersTurn && p.HasInitialHandDealt)
        {
            if (p.Hands.Count == 0) return;
            var currentHand = p.Hands[p.CurrentHandIndex];
            var (min, max) = p.CalculatePoints(p.CurrentHandIndex);

            bool isSplitHand = p.Hands.Count > 1;

            bool canHit = min < 21 && !currentHand.IsDoubleDown && !currentHand.IsStand;

            bool canDD = canHit && currentHand.Cards.Count == 2;
            if (!_config.EnableDoubleDown) canDD = false;
            if (isSplitHand && !_config.AllowDoubleDownAfterSplit) canDD = false;

            bool canSplit = false;
            if (_config.EnableSplit && canHit && currentHand.Cards.Count == 2 && p.Hands.Count < _config.MaxHandsPerPlayer)
            {
                if (_config.IdenticalSplitOnly)
                canSplit = currentHand.Cards[0].Value == currentHand.Cards[1].Value;
                else
                canSplit = PlayerState.GetCardScoreValue(currentHand.Cards[0].Value) == PlayerState.GetCardScoreValue(currentHand.Cards[1].Value);
            }

            bool canStand = !currentHand.IsStand && !currentHand.IsBust;

            HighlightActionButton(p, "Draw", ref p.HighlightHit, canHit, () =>
            {
                BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, $"PlayerHit:{p.Name}");
                Task.Run(() => GameEngine.ActionHit(p, _config, _players));
            });
            ImGui.SameLine();

            HighlightActionButton(p, "DD", ref p.HighlightDD, canDD, () =>
            {
                BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, $"PlayerDD:{p.Name}");
                Task.Run(() => GameEngine.ActionDD(p, _config, _players));
            });
            ImGui.SameLine();

            HighlightActionButton(p, "Spl", ref p.HighlightSplit, canSplit, () =>
            {
                BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, $"PlayerSplit:{p.Name}");
                Task.Run(() => GameEngine.ActionSplit(p, _config, _players));
            });
            ImGui.SameLine();

            HighlightActionButton(p, "Stand", ref p.HighlightStand, canStand, () =>
            {
                BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, $"PlayerStand:{p.Name}");
                Task.Run(() => GameEngine.ActionStand(p, _config, _players));
            });
            ImGui.SameLine();

            DrawRecallButton(p);
        }
    }

    private void DrawRecallButton(PlayerState p)
    {
        bool hasState = !string.IsNullOrEmpty(CommandExecutor.LastStateGroupName);
        double elapsed = hasState
            ? (DateTime.Now - CommandExecutor.LastStateFiredAt).TotalSeconds
            : 0;
        float unlock = _config.RecallUnlockSeconds;
        bool unlocked = hasState && elapsed >= unlock;

        if (!unlocked) ImGui.BeginDisabled();
        string label;
        if (!hasState) label = "Recall";
        else if (!unlocked) label = $"Recall ({Math.Max(0, (int)Math.Ceiling(unlock - elapsed))}s)";
        else label = "Recall";

        if (BJBGui.Button($"{label}##btn_recall_{p.UIID}"))
        {
            string grp = CommandExecutor.LastStateGroupName;
            string tgt = CommandExecutor.LastStateTargetName;
            Task.Run(() => CommandExecutor.ExecuteGroup(grp, tgt, _config));
        }
        if (!unlocked) ImGui.EndDisabled();
    }

    private void DrawDealerControls()
    {
        bool globalLock = CommandExecutor.IsRunning;
        if (globalLock) ImGui.BeginDisabled();

        InnerDealerControls();

        if (globalLock) ImGui.EndDisabled();
    }

    private void InnerDealerControls()
    {
        var phase = GameEngine.CurrentPhase;

        if (GameEngine.CanAcceptInterRoundDetectors())
        {
            bool hlNewRound = _highlightNewRound;
            bool clickedNewRound = hlNewRound
                ? BJBGui.SmallButtonHighlighted("Start New Round", _config.HighlightColor, _config.HighlightTextColor)
                : BJBGui.SmallButton("Start New Round");
            if (clickedNewRound)
            {
                _highlightNewRound = false;
                BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, "DealStart");
                Task.Run(() => GameEngine.StartInitialDeal(_players, _config));
            }
        }
        else if (phase == GamePhase.DealerTurn)
        {
            if (_config.AutoDealerDraw)
            {
                ImGui.TextDisabled("Auto...");
            }
            else
            {
                if (BJBGui.SmallButton("Hit"))
                {
                    BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, "DealerHit");
                    Task.Run(() => GameEngine.DealerHit(_config, _players));
                }
                ImGui.SameLine();
                if (BJBGui.SmallButton("Stand"))
                {
                    BlackJackButtler.Chat.GameLog.PushSnapshot(_players, _dealer, phase, "DealerStand");
                    Task.Run(async () => {
                        await GameEngine.DealerStand(_config, _players);
                        await GameEngine.EvaluateFinalResults(_players, _dealer, _config);
                    });
                }
            }
        }
        else { ImGui.TextDisabled("Waiting..."); }
    }

    private void HighlightActionButton(PlayerState p, string label, ref bool highlightField, bool enabled, Action onClick)
    {
        if (!enabled) ImGui.BeginDisabled();
        bool shouldHighlight = highlightField && enabled;

        bool clicked = shouldHighlight
            ? BJBGui.ButtonHighlighted($"{label}##btn_{label}_{p.UIID}", _config.HighlightColor, _config.HighlightTextColor)
            : BJBGui.Button($"{label}##btn_{label}_{p.UIID}");

        if (clicked)
        {
            highlightField = false;
            onClick?.Invoke();
        }

        if (!enabled) ImGui.EndDisabled();
    }

    public void SyncParty()
    {
        if (Plugin.IsDebugMode) return;
        foreach (var p in _players) p.IsInParty = false;

        var leaderIndex = Plugin.PartyList.PartyLeaderIndex;

        for (int i = 0; i < Plugin.PartyList.Length; i++)
        {
            var member = Plugin.PartyList[i];
            if (member == null) continue;

            var name = member.Name.TextValue;
            if (string.IsNullOrEmpty(name)) continue;

            if (i == leaderIndex)
            {
                var localName = Plugin.PlayerState.CharacterName ?? name;
                if (string.IsNullOrWhiteSpace(localName)) localName = name;
                var localWorldId = Plugin.PlayerState.HomeWorld.RowId;
                var newUid = CompanionSyncManager.GetUidForSource(localName, localWorldId);
                if (CompanionSyncManager.GetUid(_dealer) != newUid)
                    CompanionSyncManager.ClearPlayer(_config, _dealer);
                _dealer.Name = localName;
                _dealer.WorldId = localWorldId;
                AddDebugLog($"[CompanionSync] Eigene UID: {newUid} (Name={localName} HomeWorld={localWorldId})");
                continue;
            }

            uint homeWorldId = member.World.RowId;
            foreach (var obj in Plugin.ObjectTable)
            {
                if (obj is IPlayerCharacter pc && pc.Name.TextValue == name)
                {
                    homeWorldId = pc.HomeWorld.RowId;
                    break;
                }
            }

            var existing = _players.FirstOrDefault(x => x.Name == name);
            if (existing != null)
            {
                existing.IsInParty = true;
                if (existing.WorldId != homeWorldId)
                {
                    CompanionSyncManager.ClearPlayer(_config, existing);
                    existing.WorldId = homeWorldId;
                }
            }
            else
            {
                _players.Add(new PlayerState { Name = name, WorldId = homeWorldId, IsInParty = true });
            }
        }

        foreach (var p in _players)
        {
            if (!p.IsInParty)
            {
                string worldName = VipManager.ResolveWorldName(p.WorldId);
                if (!string.IsNullOrEmpty(worldName) && VipManager.GetPlayerTier(p.Name, worldName) > 0)
                    VipManager.SetPlayerTier(p.Name, worldName, 0);
            }
        }

        foreach (var leaver in _players.Where(x => !x.IsInParty && !x.IsActivePlayer && x.Bank > 0).ToList())
        {
            if (StatsManager.IsRunning)
            {
                StatsManager.AddTip(leaver.Bank);
                AddDebugLog($"[Party] {leaver.DisplayName} left with {leaver.Bank} gil — added as tip.", false);
            }
            leaver.Bank = 0;
            CompanionSyncManager.SendPlayerBankBetUpdate(_config, leaver);
        }
        RemovePlayersWithCompanionErase(x => !x.IsInParty && !x.IsActivePlayer && x.Bank == 0);
    }

    private void DrawOfflineUnderline()
    {
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        ImGui.GetWindowDrawList().AddLine(new Vector2(min.X, max.Y), new Vector2(max.X, max.Y), ImGui.GetColorU32(new Vector4(1, 0.5f, 0, 1)), 2.0f);
    }

    private void ApplyPlayerRowBackground(PlayerState player)
    {
        if (!GameEngine.IsPlayerUnableToCoverBet(player))
            return;

        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(0.55f, 0.05f, 0.05f, 0.55f)));
        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.GetColorU32(new Vector4(0.55f, 0.05f, 0.05f, 0.55f)));
    }

    private void CreateTestData()
    {
        GenerateRandomDebugPlayers();
    }

    private void GenerateRandomDebugPlayers()
    {
        RemovePlayersWithCompanionErase(p => p.IsDebugPlayer);

        var count = Random.Shared.Next(5, 9);
        var worlds = WorldNameManager.SortedWorldNames.ToArray();
        for (var i = 0; i < count; i++)
        {
            var world = worlds[Random.Shared.Next(worlds.Length)];
            var token = Random.Shared.Next(0x10000000, int.MaxValue).ToString("X8");
            var active = i < 2;
            _players.Add(new PlayerState
            {
                Name = $"Player {token}@{world}",
                WorldId = DebugWorldId(world),
                IsActivePlayer = active,
                IsDebugPlayer = true,
                IsInParty = true,
                IsCurrentTurn = false,
                Bank = active ? Random.Shared.Next(100, 1000) : Random.Shared.Next(50, 501) * 1000L,
                CurrentBet = active ? 100 : Random.Shared.Next(1, 6) * 1000L,
            });
        }

        CompanionSyncManager.SendPlayersUpdate(_config, _players.Where(p => p.IsDebugPlayer));
        GameEngine.SetRuntimeContext(_players, _dealer);
        AddDebugLog($"[DEBUG] Generated {count} random debug players.", false);
        _save();
    }

    private static uint DebugWorldId(string world)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var ch in world)
            {
                hash ^= ch;
                hash *= 16777619;
            }
            return hash == 0 ? 1 : hash;
        }
    }

    private void ExecutePanic()
    {
        if (CommandExecutor.IsRunning)
            CommandExecutor.CancelCurrentGroup();

        foreach (var p in _players)
        {
            p.Hands.Clear();
            p.Hands.Add(new HandState(p.CurrentBet));
            p.CurrentHandIndex = 0;
            p.IsCurrentTurn = false;
            p.HasInitialHandDealt = false;
            p.IsDone = false;
            p.LastRoundResult = 0;
            p.JoinedMidRound = false;
            p.IsOnHold = false;
            p.WasOnHoldThisRound = false;
            p.IsOnBench = false;
            p.ResetHighlightsAll();
        }

        if (_dealer != null)
        {
            _dealer.Hands.Clear();
            _dealer.Hands.Add(new HandState(0));
            _dealer.CurrentHandIndex = 0;
            _dealer.IsCurrentTurn = false;
            _dealer.HasInitialHandDealt = false;
            _dealer.IsDone = false;
            _dealer.ResetHighlightsAll();
        }

        DeckManager.Reshuffle();
        GameEngine.CurrentPhase = GamePhase.Waiting;
        CompanionSyncManager.SendPlayersUpdate(_config, _players);

        AddDebugLog("[PANIC] Round force-aborted. Phase reset to Waiting.", false);
    }

    private void SendPaymentTell(PlayerState p, long amount, string action)
    {
        var batch = _config.MessageBatches.FirstOrDefault(b => b.Name == "Payment Reminder");
        string raw = batch?.GetNextMessage() ?? "Please pay ${missingGil} gil for your ${action}.";

        string tReplacement = !string.IsNullOrWhiteSpace(p.Alias) ? p.Alias : "<t>";

        string processed = raw
            .Replace("<t>", tReplacement)
            .Replace("${missingGil}", amount.ToString("N0"))
            .Replace("${action}", action);

        ChatCommandRouter.Send($"/t <t> {processed}", _config, "PaymentTell");
    }

    private void RemovePlayersWithCompanionErase(Predicate<PlayerState> match)
    {
        for (var i = _players.Count - 1; i >= 0; i--)
        {
            var player = _players[i];
            if (!match(player))
                continue;

            CompanionSyncManager.ClearPlayer(_config, player);
            _players.RemoveAt(i);
        }
    }

    private void ClearPlayersWithCompanionErase()
    {
        foreach (var player in _players)
            CompanionSyncManager.ClearPlayer(_config, player);

        _players.Clear();
    }

    private bool TryRemoveEmptyInactiveDebugPlayer(PlayerState player)
    {
        if (!Plugin.IsDebugMode || !player.IsDebugPlayer || player.IsActivePlayer || player.Bank != 0)
            return false;

        CompanionSyncManager.ClearPlayer(_config, player);
        _players.Remove(player);
        AddDebugLog($"[DEBUG] Removed inactive zero-bank debug player: {player.DisplayName}", false);
        _save();
        return true;
    }
}
