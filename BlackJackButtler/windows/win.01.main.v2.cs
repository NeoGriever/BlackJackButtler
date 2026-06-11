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
        bool compact = IsV2SuperCompact();
        if (compact)
        {
            ImGui.SetWindowFontScale(0.9f);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(1.5f, 0.8f));
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(1.5f, 0.8f));
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(1.5f, 0.8f));
        }

        DrawMainHeaderV2();
        ImGui.Spacing();
        DrawCustomButtonBarV2();
        DrawAutoContinueBarV2();

        if (!_config.TablePopout)
        {
            ImGui.Spacing();
            DrawDealerPanelV2();
            ImGui.Spacing();
            DrawPlayersPanelV2();
        }

        if (!_config.NearbyPopout)
            DrawNearbyPlayersSection(true);

        DrawMainSharedPopupsV2();

        if (compact)
        {
            ImGui.PopStyleVar(3);
            ImGui.SetWindowFontScale(1.0f);
        }
    }

    private bool IsV2SuperCompact() => _config.MainViewVersion == 2 && _config.MainViewV2SuperCompact;

    private void DrawMainHeaderV2()
    {
        DrawCompactHeaderRowV2();

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
            && (!IsLocalPlayerPartyLeader() || GroupContextManager.CurrentMemberCount() == 0))
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

    private const float _v2BtnW = 52f;

    private void DrawCompactHeaderRowV2()
    {
        // Status-Indikator oben rechts
        var phaseText = GameEngine.CurrentPhase.ToString();
        var phaseSize = ImGui.CalcTextSize(phaseText);
        var savedCursor = ImGui.GetCursorPos();
        float rightPadding = 10f;
        ImGui.SetCursorPos(new Vector2(ImGui.GetWindowWidth() - phaseSize.X - rightPadding, savedCursor.Y + 2f));
        ImGui.TextColored(new Vector4(1f, 0.65f, 0.2f, 1f), phaseText);
        ImGui.SetCursorPos(savedCursor);

        var io = ImGui.GetIO();
        if (io.KeyCtrl && io.KeyShift)
        {
            DrawProminentPanicButton("v2_header");
            return;
        }

        // Open / Closed
        bool isOn = IsRecognitionActive;
        if (isOn)
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.15f, 0.55f, 0.15f, 1f));
        else
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.30f, 0.30f, 0.30f, 1f));
        if (BJBGui.Button(isOn ? "Open##v2_recon" : "Closed##v2_recon", new Vector2(64f, 0)))
        {
            SetGroupDetectorActive(!IsRecognitionActive);
        }
        ImGui.PopStyleColor();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(isOn ? "Group Detector: ON — click to deactivate" : "Group Detector: OFF — click to activate");

        // Clear (nur bei Residual-Daten)
        bool hasResidualData =
            _players.Any(p => !p.IsDebugPlayer)
            || _dealer.Hands.Any(h => h.Cards.Count > 0)
            || DrawLogicDebugManager.DebugHands.Any(h => h.Cards.Count > 0)
            || DrawLogicDebugManager.ValidScriptCache.Count > 0;
        if (!IsRecognitionActive && hasResidualData)
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.55f, 0.30f, 0.05f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.75f, 0.40f, 0.10f, 1f));
            if (BJBGui.Button("Clear##v2_clean_residual", new Vector2(_v2BtnW, 0)))
            {
                _openCleanDataPopup = true;
                ImGui.OpenPopup("bjb.clean_data.confirm.v2");
            }
            ImGui.PopStyleColor(2);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Clean players and DrawLogic debug data left over while Group Detector is off.");
            DrawCompactSeparatorV2();
        }

        // Automation-Buttons (nur wenn Automation aktiviert)
        if (_config.EnableAutomation)
        {
            if (_config.ShowAutoPlayerHandButton)
            {
                ImGui.SameLine();
                DrawCompactToggleV2("APH##v2_aph", ref _config.AutoInitialDeal, "Auto Player Hand", _v2BtnW);
            }
            else _config.AutoInitialDeal = false;

            if (_config.ShowAutoDealerDrawButton)
            {
                ImGui.SameLine();
                DrawCompactToggleV2("ADD##v2_add", ref _config.AutoDealerDraw,
                    $"Auto Dealer Draw (until {(_config.DealerSoftRule ? "soft" : "hard")} {_config.DealerDrawsUntil})", _v2BtnW);
            }
            else _config.AutoDealerDraw = false;

            if (_config.ShowAutoContinueButton)
            {
                ImGui.SameLine();
                DrawCompactToggleV2("AC##v2_ac", ref _config.AutoContinue,
                    $"Auto Continue (after {_config.AutoContinueDelay:0}s)", _v2BtnW);
            }
            else _config.AutoContinue = false;

            bool hasAutoTriggers = _config.UserRegexes.Any(r =>
                r.Enabled && r.Mode == RegexEntryMode.Trigger &&
                (r.Action == RegexAction.WantHit || r.Action == RegexAction.WantStand ||
                 r.Action == RegexAction.WantDD || r.Action == RegexAction.WantSplit));
            if (hasAutoTriggers && _config.ShowAutoRunButton)
            {
                ImGui.SameLine();
                bool prevAutoRun = _config.AutoRun;
                DrawCompactToggleV2("AR##v2_ar", ref _config.AutoRun, "Auto Run", _v2BtnW);
                if (_config.AutoRun != prevAutoRun)
                    Plugin.Instance.ResetAutoActionState(cancelCurrentGroup: !_config.AutoRun);
            }
            else _config.AutoRun = false;
        }

        // BNK
        DrawCompactSeparatorV2();
        ImGui.SameLine();
        bool canTell = GameEngine.CanAcceptInterRoundDetectors();
        if (!canTell) ImGui.BeginDisabled();
        if (BJBGui.Button("BNK##v2_bnk", new Vector2(_v2BtnW, 0)))
        {
            var snapshot = _players.Where(p => p.IsActivePlayer && !p.IsOnHold).ToList();
            BankTellQueueManager.EnqueueMany(snapshot, _config, "MainV2All");
        }
        if (!canTell) ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip("Bank /tell cycle");

        // STOP
        ImGui.SameLine();
        if (CommandExecutor.IsRunning)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.0f, 0.0f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.1f, 0.1f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.5f, 0.0f, 0.0f, 1.0f));
            if (BJBGui.Button("STOP##v2_stop", new Vector2(_v2BtnW, 0)))
                CommandExecutor.CancelCurrentGroup();
            ImGui.PopStyleColor(3);
        }
        else
        {
            ImGui.BeginDisabled();
            ImGui.Button("STOP##v2_stop_dis", new Vector2(_v2BtnW, 0));
            ImGui.EndDisabled();
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip("Stop currently running commands");

        // Table Popout — Snapshot VOR dem Button, damit Push/Pop immer übereinstimmen
        DrawCompactSeparatorV2();
        ImGui.SameLine();
        bool tblOn = _config.TablePopout;
        if (tblOn) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.5f, 0.0f, 1.0f));
        if (BJBGui.Button("Tbl##v2_tbl", new Vector2(_v2BtnW, 0)))
        {
            _config.TablePopout = !_config.TablePopout;
            if (_tablePopoutWindow != null) _tablePopoutWindow.IsOpen = _config.TablePopout;
            _save();
        }
        if (tblOn) ImGui.PopStyleColor();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(tblOn
            ? "Table is open in popup — click to close"
            : "Open dealer/player table as popup window");

        // Nearby Popout — gleicher Snapshot-Fix
        ImGui.SameLine();
        bool nbyOn = _config.NearbyPopout;
        if (nbyOn) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.5f, 0.0f, 1.0f));
        if (BJBGui.Button("Nby##v2_nby", new Vector2(_v2BtnW, 0)))
        {
            _config.NearbyPopout = !_config.NearbyPopout;
            if (_nearbyPopoutWindow != null) _nearbyPopoutWindow.IsOpen = _config.NearbyPopout;
            _save();
        }
        if (nbyOn) ImGui.PopStyleColor();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(nbyOn
            ? "Nearby Players is open in popup — click to close"
            : "Open Nearby Players as popup window");

        ImGui.SameLine();
        if (BJBGui.Button("CFG##v2_nearby_cfg", new Vector2(_v2BtnW, 0)))
            _showNearbySettingsWindow = true;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Detection Radius and Nearby Players configuration");

        ImGui.SameLine();
        DrawCompactToggleV2(
            "STI##v2_nearby_sticky",
            ref _config.NearbySticky,
            "Sticky sorting: keep the current Nearby Players order while enabled",
            _v2BtnW);

        // PANIC (nur bei Ctrl+Shift)
        if (io.KeyCtrl && io.KeyShift)
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.5f, 0.0f, 0.0f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.65f, 0.0f, 0.0f, 1.0f));
            if (BJBGui.Button("PANIC##v2_panic", new Vector2(_v2BtnW, 0)))
                _panicConfirmStage = 1;
            ImGui.PopStyleColor(2);
        }
    }

    private void DrawCompactToggleV2(string id, ref bool value, string tooltip, float width)
    {
        bool wasOn = value;
        if (wasOn) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.5f, 0.0f, 1.0f));
        if (BJBGui.Button(id, new Vector2(width, 0)))
        {
            value = !value;
            _save();
        }
        if (wasOn) ImGui.PopStyleColor();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);
    }

    private static void DrawCompactSeparatorV2()
    {
        ImGui.SameLine(0f, 7f);
        var start = ImGui.GetCursorScreenPos();
        var height = ImGui.GetFrameHeight();
        var color = ImGui.GetColorU32(new Vector4(0.45f, 0.45f, 0.45f, 0.8f));
        ImGui.GetWindowDrawList().AddLine(
            new Vector2(start.X, start.Y + 2f),
            new Vector2(start.X, start.Y + height - 2f),
            color,
            1f);
        ImGui.Dummy(new Vector2(1f, height));
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
        if (BJBGui.Button("Clear##v2_clean_residual_legacy", new Vector2(130, 0)))
        {
            _openCleanDataPopup = true;
            ImGui.OpenPopup("bjb.clean_data.confirm.v2");
        }
        ImGui.PopStyleColor(2);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Clean players and DrawLogic debug data left over while Group Detector is off.");
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

    internal void DrawDealerPanelV2(string idSuffix = "")
    {
        if (ImGui.BeginTable($"bjb_dealer_table_v2{idSuffix}", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
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

    internal void DrawPlayersPanelV2(string idSuffix = "")
    {
        _partyDissolved = _players.Count > 0 && !_players.Any(x => x.IsInParty);
        const int columnCount = 10;
        if (ImGui.BeginTable($"bjb_main_table_v2{idSuffix}", columnCount, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
        {
            SetupTableColumns();
            DrawPlayerTableHeaders();

            var playerSnapshot = _players.ToList();
            foreach (var player in playerSnapshot)
            {
                ImGui.TableNextRow();
                ApplyPlayerRowBackground(player);
                DrawPlayerRow(player, false);
            }
            ImGui.EndTable();
        }
    }

    internal void TriggerPanicStage1() => _panicConfirmStage = 1;

    internal void DrawMainSharedPopupsV2()
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
            RemovePlayersWithCompanionErase(p => !p.IsDebugPlayer);
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
