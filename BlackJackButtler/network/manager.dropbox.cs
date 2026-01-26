using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BlackJackButtler.Chat;
using ECommons.DalamudServices;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler;

public static class DropboxIntegration
{
    private static string _currentTargetName = string.Empty;
    private static List<long> _chunks = new();
    private static List<bool> _chunkDone = new();
    private static bool _isHelperActive = false;
    private static bool _lastFrameTradeOpen = false;

    public static void PayOut(PlayerState p)
    {
        if (p.Bank <= 0) return;
        GameEngine.TargetPlayer(p.Name);
        bool hasDropbox = Svc.PluginInterface.InstalledPlugins
            .Any(pl => pl.InternalName == "Dropbox" && pl.IsLoaded);

        if (hasDropbox)
        {
            Plugin.Instance.GetMainWindow().AddDebugLog($"[Payout] Dropbox detected. Copying {p.Bank} to clipboard.");
            ImGui.SetClipboardText(p.Bank.ToString());
            ChatCommandRouter.Send("/dropbox", Plugin.Instance.Configuration, "OpenDropbox");
            p.Bank = 0;
            Plugin.Instance.Configuration.Save();
        }
        else
        {
            _currentTargetName = p.Name;
            _chunks.Clear();
            _chunkDone.Clear();
            long remaining = p.Bank;
            while (remaining > 0)
            {
                long val = Math.Min(remaining, 1000000);
                _chunks.Add(val);
                _chunkDone.Add(false);
                remaining -= val;
            }
            _isHelperActive = true;
            _lastFrameTradeOpen = false;
            Plugin.Instance.GetMainWindow().AddDebugLog($"[Payout] Helper started for {_currentTargetName} ({_chunks.Count} trades).");
            ChatCommandRouter.Send("/trade <t>", Plugin.Instance.Configuration, "ManualPayoutInit");
        }
    }

    public static void Update()
    {
        if (!_isHelperActive) return;
        bool isTradeOpen = Svc.GameGui.GetAddonByName("Trade") != nint.Zero;
        if (_lastFrameTradeOpen && !isTradeOpen)
        {
            if (_chunkDone.Any(done => !done))
            {
                Plugin.Instance.GetMainWindow().AddDebugLog("[Payout] Trade closed, but chunks remaining. Re-opening...");
                ChatCommandRouter.Send("/trade <t>", Plugin.Instance.Configuration, "ManualPayoutNext");
            }
            else
            {
                Plugin.Instance.GetMainWindow().AddDebugLog("[Payout] All chunks processed. Closing helper.");
                var p = Plugin.Instance.GetMainWindow().GetPlayers().FirstOrDefault(x => x.Name == _currentTargetName);
                if (p != null) p.Bank = 0;
                Reset();
            }
        }

        _lastFrameTradeOpen = isTradeOpen;
    }

    public static void DrawHelperWindow()
    {
        if (!_isHelperActive) return;
        ImGui.SetNextWindowSize(new Vector2(250, 300), ImGuiCond.FirstUseEver);
        if (ImGui.Begin($"Payout Helper: {_currentTargetName}###bjb_payout_helper", ref _isHelperActive, ImGuiWindowFlags.NoCollapse))
        {
            ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), "Remaining Chunks:");
            ImGui.Separator();

            if (ImGui.BeginChild("chunk_list"))
            {
                for (int i = 0; i < _chunks.Count; i++)
                {
                    bool done = _chunkDone[i];

                    if (done) ImGui.BeginDisabled();

                    if (ImGui.Button($"COPY##{i}"))
                    {
                        ImGui.SetClipboardText(_chunks[i].ToString());
                        _chunkDone[i] = true;
                    }
                    ImGui.SameLine();
                    ImGui.TextUnformatted($"{_chunks[i]:N0} Gil");

                    if (done) ImGui.EndDisabled();
                }
                ImGui.EndChild();
            }

            ImGui.Separator();
            if (ImGui.Button("Cancel Payout", new Vector2(-1, 0))) Reset();

            ImGui.End();
        }
        else
        {
            Reset();
        }
    }

    public static void Reset()
    {
        _isHelperActive = false;
        _currentTargetName = string.Empty;
        _chunks.Clear();
        _chunkDone.Clear();
        Plugin.Instance.Configuration.Save();
    }
}
