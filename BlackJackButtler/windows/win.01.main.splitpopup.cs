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
    private bool _showSplitMoneyPopup = false;
    private PlayerState? _splitPopupPlayer = null;
    private long _splitPopupMissingAmount = 0;
    private long _splitPopupInitialBank = 0;
    private DateTime _splitPopupOpenTime = DateTime.MinValue;

    public void OpenSplitMoneyPopup(PlayerState player, long missingAmount)
    {
        _splitPopupPlayer = player;
        _splitPopupMissingAmount = missingAmount;
        _splitPopupInitialBank = player.Bank;
        _splitPopupOpenTime = DateTime.Now;
        _showSplitMoneyPopup = true;

        AddDebugLog($"[Split] Waiting for {player.DisplayName} to provide {missingAmount:N0} Gil", false);
    }

    public void CloseSplitMoneyPopup()
    {
        _showSplitMoneyPopup = false;
        _splitPopupPlayer = null;
        _splitPopupMissingAmount = 0;
        _splitPopupInitialBank = 0;
    }

    private void DrawSplitMoneyPopup()
    {
        if (!_showSplitMoneyPopup || _splitPopupPlayer == null)
            return;

        long bankIncrease = _splitPopupPlayer.Bank - _splitPopupInitialBank;
        bool hasEnoughMoney = bankIncrease >= _splitPopupMissingAmount;

        if (hasEnoughMoney)
        {
            var playerToProcess = _splitPopupPlayer;
            var currentConfig = _config;
            var currentPlayers = _players;

            AddDebugLog($"[Split] {playerToProcess?.DisplayName} payment verified. Processing...");

            CloseSplitMoneyPopup();

            if (playerToProcess != null)
            {
                Task.Run(async () => {
                    await Task.Delay(50);
                    GameEngine.ContinueSplitAfterPayment(playerToProcess, currentConfig, currentPlayers);
                });
            }
            return;
        }

        ImGui.SetNextWindowSize(new Vector2(420, 0), ImGuiCond.Always);
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

        if (ImGui.Begin("Split Payment Required###bjb_split_popup", ref _showSplitMoneyPopup, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.PushFont(ImGui.GetFont());
            ImGui.SetWindowFontScale(1.3f);
            ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.0f, 1.0f), "Insufficient Funds");
            ImGui.SetWindowFontScale(1.0f);
            ImGui.PopFont();

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextColored(new Vector4(0.8f, 0.8f, 1.0f, 1.0f), "Player:");
            ImGui.SameLine();
            ImGui.TextUnformatted(_splitPopupPlayer.DisplayName);

            ImGui.Spacing();

            ImGui.TextColored(new Vector4(1.0f, 0.3f, 0.3f, 1.0f), "Missing:");
            ImGui.SameLine();
            ImGui.TextUnformatted($"{_splitPopupMissingAmount:N0} Gil");

            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 50);
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button($"{FontAwesomeIcon.CommentDots.ToIconString()}##tell_split"))
            {
                SendPaymentTell(_splitPopupPlayer, (_splitPopupMissingAmount - bankIncrease), "Split");
            }
            ImGui.PopFont();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Send /tell to player");

            ImGui.Spacing();

            ImGui.TextColored(new Vector4(0.5f, 1.0f, 0.5f, 1.0f), "Current Bank:");
            ImGui.SameLine();
            ImGui.TextUnformatted($"{_splitPopupPlayer.Bank:N0} Gil");

            if (bankIncrease > 0)
            {
                ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.3f, 1.0f), "Received:");
                ImGui.SameLine();
                ImGui.TextUnformatted($"{bankIncrease:N0} Gil");

                long stillNeeded = _splitPopupMissingAmount - bankIncrease;
                ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.0f, 1.0f), "Still needed:");
                ImGui.SameLine();
                ImGui.TextUnformatted($"{stillNeeded:N0} Gil");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            var waitTime = (DateTime.Now - _splitPopupOpenTime).TotalSeconds;
            var t = TimeSpan.FromSeconds(waitTime);
            ImGui.TextWrapped("Waiting for player to trade the required amount...");
            ImGui.TextDisabled($"Time elapsed: {t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}");

            ImGui.Spacing();
            ImGui.Spacing();

            float progress = Math.Min(1.0f, (float)bankIncrease / _splitPopupMissingAmount);
            ImGui.ProgressBar(progress, new Vector2(-1, 0), $"{progress * 100:F0}%");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1.0f, 0.3f, 0.3f, 1.0f));

            if (ImGui.Button("Cancel Split", new Vector2(-1, 40)))
            {
                AddDebugLog($"[Split] Cancelled for {_splitPopupPlayer.DisplayName} - insufficient funds", false);
                CloseSplitMoneyPopup();
            }

            ImGui.PopStyleColor(2);

            ImGui.End();
        }
        else
        {
            CloseSplitMoneyPopup();
        }
    }
}
