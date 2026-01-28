using System;
using System.Numerics;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private bool _showDDMoneyPopup = false;
    private PlayerState? _ddPopupPlayer = null;
    private long _ddPopupMissingAmount = 0;
    private long _ddPopupInitialBank = 0;
    private DateTime _ddPopupOpenTime = DateTime.MinValue;

    public void OpenDDMoneyPopup(PlayerState player, long missingAmount)
    {
        _ddPopupPlayer = player;
        _ddPopupMissingAmount = missingAmount;
        _ddPopupInitialBank = player.Bank;
        _ddPopupOpenTime = DateTime.Now;
        _showDDMoneyPopup = true;

        AddDebugLog($"[DoubleDown] Waiting for {player.DisplayName} to provide {missingAmount:N0} Gil", false);
    }

    public void CloseDDMoneyPopup()
    {
        _showDDMoneyPopup = false;
        _ddPopupPlayer = null;
        _ddPopupMissingAmount = 0;
        _ddPopupInitialBank = 0;
    }

    private void DrawDDMoneyPopup()
    {
        if (!_showDDMoneyPopup || _ddPopupPlayer == null)
            return;

        long bankIncrease = _ddPopupPlayer.Bank - _ddPopupInitialBank;
        bool hasEnoughMoney = bankIncrease >= _ddPopupMissingAmount;

        if (hasEnoughMoney)
        {
            var playerToProcess = _ddPopupPlayer;
            var currentConfig = _config;
            var currentPlayers = _players;

            AddDebugLog($"[DoubleDown] {playerToProcess?.DisplayName} payment verified. Processing...");

            CloseDDMoneyPopup();

            if (playerToProcess != null)
            {
                Task.Run(async () => {
                    await Task.Delay(50);
                    GameEngine.ContinueDDAfterPayment(playerToProcess, currentConfig, currentPlayers);
                });
            }
            return;
        }

        ImGui.SetNextWindowSize(new Vector2(420, 0), ImGuiCond.Always);
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

        if (ImGui.Begin("Double Down Payment Required###bjb_dd_popup", ref _showDDMoneyPopup, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.PushFont(ImGui.GetFont());
            ImGui.SetWindowFontScale(1.3f);
            ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.0f, 1.0f), "Insufficient Funds for Double Down");
            ImGui.SetWindowFontScale(1.0f);
            ImGui.PopFont();

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "Player:");
            ImGui.SameLine();
            ImGui.TextUnformatted(_ddPopupPlayer.DisplayName);

            ImGui.Spacing();

            ImGui.TextColored(new Vector4(1.0f, 0.3f, 0.3f, 1.0f), "Missing:");
            ImGui.SameLine();
            ImGui.TextUnformatted($"{_ddPopupMissingAmount:N0} Gil");

            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 50);
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button($"{FontAwesomeIcon.CommentDots.ToIconString()}##tell_dd"))
            {
                SendPaymentTell(_ddPopupPlayer, (_ddPopupMissingAmount - bankIncrease), "Double Down");
            }
            ImGui.PopFont();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Send /tell to player");

            ImGui.Spacing();

            ImGui.TextColored(new Vector4(0.5f, 1.0f, 0.5f, 1.0f), "Current Bank:");
            ImGui.SameLine();
            ImGui.TextUnformatted($"{_ddPopupPlayer.Bank:N0} Gil");

            if (bankIncrease > 0)
            {
                ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.3f, 1.0f), "Received:");
                ImGui.SameLine();
                ImGui.TextUnformatted($"{bankIncrease:N0} Gil");

                long stillNeeded = _ddPopupMissingAmount - bankIncrease;
                ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.0f, 1.0f), "Still needed:");
                ImGui.SameLine();
                ImGui.TextUnformatted($"{stillNeeded:N0} Gil");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            var waitTime = (DateTime.Now - _ddPopupOpenTime).TotalSeconds;
            var t = TimeSpan.FromSeconds(waitTime);
            ImGui.TextWrapped("Waiting for player to trade the required amount...");
            ImGui.TextDisabled($"Time elapsed: {t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}");

            ImGui.Spacing();
            ImGui.Spacing();

            float progress = Math.Min(1.0f, (float)bankIncrease / _ddPopupMissingAmount);
            ImGui.ProgressBar(progress, new Vector2(-1, 0), $"{progress * 100:F0}%");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));

            if (ImGui.Button("Cancel Double Down", new Vector2(-1, 40))) // Text korrigiert
            {
                AddDebugLog($"[DD] Cancelled for {_ddPopupPlayer.DisplayName} - insufficient funds", false);
                CloseDDMoneyPopup();
            }

            ImGui.PopStyleColor(2);

            ImGui.End();
        }
        else
        {
            CloseDDMoneyPopup();
        }
    }
}
