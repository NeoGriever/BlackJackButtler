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
    private void DrawMainHeader()
    {
        DrawMainHeaderControls();

        if (IsRecognitionActive && !IsLocalPlayerPartyLeader())
        {
            var leaderName = GetPartyLeaderName();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.6f, 0.2f, 1.0f));
            ImGui.TextUnformatted($"You're not the group leader. Current leader is {leaderName}");
            ImGui.PopStyleColor();
            if (ImGui.IsItemClicked())
                GameEngine.TargetPlayer(leaderName);
        }

    }

    private const float MainHeaderButtonWidth = 52f;

    private void DrawMainHeaderControls()
    {
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
            if (BJBGui.Button("Clear##v2_clean_residual", new Vector2(MainHeaderButtonWidth, 0)))
            {
                _openCleanDataPopup = true;
                ImGui.OpenPopup("bjb.clean_data.confirm.v2");
            }
            ImGui.PopStyleColor(2);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Clean players and DrawLogic debug data left over while Group Detector is off.");
            DrawMainHeaderSeparator();
        }

        if (_config.EnableAutomation)
        {
            if (_config.ShowAutoPlayerHandButton)
            {
                ImGui.SameLine();
                DrawMainHeaderToggle("APH##v2_aph", ref _config.AutoInitialDeal, "Auto Player Hand", MainHeaderButtonWidth);
            }
            else _config.AutoInitialDeal = false;

            if (_config.ShowAutoDealerDrawButton)
            {
                ImGui.SameLine();
                DrawMainHeaderToggle("ADD##v2_add", ref _config.AutoDealerDraw, $"Auto Dealer Draw (until {(_config.DealerSoftRule ? "soft" : "hard")} {_config.DealerDrawsUntil})", MainHeaderButtonWidth);
            }
            else _config.AutoDealerDraw = false;

            if (_config.ShowAutoContinueButton)
            {
                ImGui.SameLine();
                DrawMainHeaderToggle("AC##v2_ac", ref _config.AutoContinue, $"Auto Continue (after {_config.AutoContinueDelay:0}s)", MainHeaderButtonWidth);
            }
            else _config.AutoContinue = false;

            bool hasAutoTriggers = _config.UserRegexes.Any(
                r =>
                    r.Enabled && r.Mode == RegexEntryMode.Trigger &&
                    (r.Action == RegexAction.WantHit || r.Action == RegexAction.WantStand || r.Action == RegexAction.WantDD || r.Action == RegexAction.AutoTripleDown || r.Action == RegexAction.WantSplit)
            );
            if (hasAutoTriggers && _config.ShowAutoRunButton)
            {
                ImGui.SameLine();
                bool prevAutoRun = _config.AutoRun;
                DrawMainHeaderToggle("AR##v2_ar", ref _config.AutoRun, "Auto Run", MainHeaderButtonWidth);
                if (_config.AutoRun != prevAutoRun)
                    Plugin.Instance.ResetAutoActionState(cancelCurrentGroup: !_config.AutoRun);
            }
            else _config.AutoRun = false;
        }

        DrawMainHeaderSeparator();
        ImGui.SameLine();
        bool canTell = GameEngine.CanAcceptInterRoundDetectors();
        if (!canTell) ImGui.BeginDisabled();
        if (BJBGui.Button("BNK##v2_bnk", new Vector2(MainHeaderButtonWidth, 0)))
        {
            var snapshot = _players.Where(p => p.IsActivePlayer && !p.IsOnHold).ToList();
            BankTellQueueManager.EnqueueMany(snapshot, _config, "MainV2All");
        }
        if (!canTell) ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip("Bank /tell cycle");

        ImGui.SameLine();
        if (CommandExecutor.IsRunning)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.0f, 0.0f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.1f, 0.1f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.5f, 0.0f, 0.0f, 1.0f));
            if (BJBGui.Button("STOP##v2_stop", new Vector2(MainHeaderButtonWidth, 0)))
                CommandExecutor.CancelCurrentGroup();
            ImGui.PopStyleColor(3);
        }
        else
        {
            ImGui.BeginDisabled();
            ImGui.Button("STOP##v2_stop_dis", new Vector2(MainHeaderButtonWidth, 0));
            ImGui.EndDisabled();
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip("Stop currently running commands");

        DrawMainHeaderSeparator();
        ImGui.SameLine();
        bool tblOn = _config.TablePopout;
        if (tblOn) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.5f, 0.0f, 1.0f));
        if (BJBGui.Button("Tbl##v2_tbl", new Vector2(MainHeaderButtonWidth, 0), tblOn ? BJBGui.OrangeHighlightTextColor : BJBGui.ButtonTextColor))
        {
            _config.TablePopout = !_config.TablePopout;
            if (_tablePopoutWindow != null) _tablePopoutWindow.IsOpen = _config.TablePopout;
            _save();
        }
        if (tblOn) ImGui.PopStyleColor();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(
            tblOn ? "Table is open in popup — click to close" : "Open dealer/player table as popup window"
        );

        ImGui.SameLine();
        DrawRotationButton("v2", new Vector2(MainHeaderButtonWidth, 0));

        if (_config.ShowNearbyPlayers)
        {
            ImGui.SameLine();
            bool nbyOn = _config.NearbyPopout;
            if (nbyOn) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.5f, 0.0f, 1.0f));
            if (BJBGui.Button("Nby##v2_nby", new Vector2(MainHeaderButtonWidth, 0), nbyOn ? BJBGui.OrangeHighlightTextColor : BJBGui.ButtonTextColor))
            {
                _config.NearbyPopout = !_config.NearbyPopout;
                if (_nearbyPopoutWindow != null) _nearbyPopoutWindow.IsOpen = _config.NearbyPopout;
                _save();
            }
            if (nbyOn) ImGui.PopStyleColor();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(
                nbyOn ? "Nearby Players is open in popup — click to close" : "Open Nearby Players as popup window"
            );

            ImGui.SameLine();
            if (BJBGui.Button("CFG##v2_nearby_cfg", new Vector2(MainHeaderButtonWidth, 0)))
                _showNearbySettingsWindow = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Detection Radius and Nearby Players configuration");

            ImGui.SameLine();
            DrawMainHeaderToggle(
                "STI##v2_nearby_sticky",
                ref _config.NearbySticky,
                "Sticky sorting: keep the current Nearby Players order while enabled",
                MainHeaderButtonWidth
            );
        }

        if (io.KeyCtrl && io.KeyShift)
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.5f, 0.0f, 0.0f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.65f, 0.0f, 0.0f, 1.0f));
            if (BJBGui.Button("PANIC##v2_panic", new Vector2(MainHeaderButtonWidth, 0)))
                _panicConfirmStage = 1;
            ImGui.PopStyleColor(2);
        }
    }

    private void DrawMainHeaderToggle(string id, ref bool value, string tooltip, float width)
    {
        bool wasOn = value;
        if (wasOn) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.5f, 0.0f, 1.0f));
        if (BJBGui.Button(id, new Vector2(width, 0), wasOn ? BJBGui.OrangeHighlightTextColor : BJBGui.ButtonTextColor))
        {
            value = !value;
            _save();
        }
        if (wasOn) ImGui.PopStyleColor();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);
    }

    private static void DrawMainHeaderSeparator()
    {
        ImGui.SameLine(0f, 7f);
        var start = ImGui.GetCursorScreenPos();
        var height = ImGui.GetFrameHeight();
        var color = ImGui.GetColorU32(new Vector4(0.45f, 0.45f, 0.45f, 0.8f));
        ImGui.GetWindowDrawList().AddLine(
            new Vector2(
                start.X,
                start.Y + 2f
            ),
            new Vector2(
                start.X,
                start.Y + height - 2f
            ),
            color,
            1f
        );
        ImGui.Dummy(new Vector2(1f, height));
    }
}
