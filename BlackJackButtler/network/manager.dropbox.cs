using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BlackJackButtler.Chat;
using ECommons.DalamudServices;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace BlackJackButtler;

public static class DropboxIntegration
{
    private static string _currentTargetName = string.Empty;
    private static bool _isHelperActive = false;
    private static bool _lastFrameTradeOpen = false;

    private static long _payoutStartAmount = 0;
    private static long _lastKnownBank = 0;
    private static DateTime? _autoCloseAt = null;

    public static bool IsDropboxAvailable()
        => Svc.PluginInterface.InstalledPlugins.Any(pl => pl.InternalName == "Dropbox" && pl.IsLoaded);

    public static void PayOut(PlayerState p)
    {
        if (p.Bank <= 0) return;
        if (_isHelperActive) return;
        GameEngine.TargetPlayer(p.Name);

        var cfg = Plugin.Instance.Configuration;
        if (cfg.OpenDropboxInsteadOfTrade && IsDropboxAvailable())
            PayOutViaDropbox(p);
        else
            PayOutViaManualTrade(p);
    }

    private static void PayOutViaDropbox(PlayerState p)
    {
        try
        {
            ClearDropboxInventory();
            SetDropboxItemQuantity(1, false, (int)p.Bank);
            Plugin.Instance.GetMainWindow().AddDebugLog($"[Payout] Dropbox IPC: Gil set to {p.Bank}");
        }
        catch (Exception ex)
        {
            Plugin.Instance.GetMainWindow().AddDebugLog($"[Payout] Dropbox IPC failed: {ex.Message}. Falling back to manual trade.");
            PayOutViaManualTrade(p);
            return;
        }
        ChatCommandRouter.Send("/dropbox", Plugin.Instance.Configuration, "OpenDropbox");
    }

    private static void PayOutViaManualTrade(PlayerState p)
    {
        _currentTargetName = p.Name;
        _payoutStartAmount = p.Bank;
        _lastKnownBank = p.Bank;
        _isHelperActive = true;
        _autoCloseAt = null;

        long bank = p.Bank;
        string clipText = bank >= 1_000_000 ? "1000000" : bank.ToString();
        ImGui.SetClipboardText(clipText);

        Plugin.Instance.GetMainWindow().AddDebugLog($"[Payout] Helper started for {_currentTargetName}, bank={bank}. Clipboard={clipText}");
        ChatCommandRouter.Send("/trade <t>", Plugin.Instance.Configuration, "ManualPayoutInit");
    }

    private static unsafe void ClearDropboxInventory()
    {
        var inv = InventoryManager.Instance();
        if (inv == null) return;

        var seen = new HashSet<(uint, bool)>();
        var types = new[] { InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4 };

        foreach (var type in types)
        {
            var container = inv->GetInventoryContainer(type);
            if (container == null) continue;
            for (int i = 0; i < container->Size; i++)
            {
                var item = container->GetInventorySlot(i);
                if (item == null || item->ItemId == 0) continue;
                bool isHQ = (item->Flags & InventoryItem.ItemFlags.HighQuality) != 0;
                var key = (item->ItemId, isHQ);
                if (!seen.Add(key)) continue;
                SetDropboxItemQuantity(item->ItemId, isHQ, 0);
            }
        }
    }

    private static void SetDropboxItemQuantity(uint itemId, bool isHQ, int quantity)
    {
        var sub = Svc.PluginInterface.GetIpcSubscriber<uint, bool, int, object>("Dropbox.SetItemQuantity");
        sub.InvokeAction(itemId, isHQ, quantity);
    }

    public static void Update()
    {
        if (!_isHelperActive) return;

        if (_autoCloseAt.HasValue && DateTime.UtcNow >= _autoCloseAt.Value)
        {
            Plugin.Instance.GetMainWindow().AddDebugLog("[Payout] Auto-close timer expired. Closing helper.");
            Reset();
            return;
        }

        var p = Plugin.Instance.GetMainWindow().GetPlayers().FirstOrDefault(x =>
            x.Name.Equals(_currentTargetName, StringComparison.OrdinalIgnoreCase));

        if (p != null)
        {
            long currentBank = p.Bank;

            if (currentBank <= 0 && !_autoCloseAt.HasValue)
            {
                _autoCloseAt = DateTime.UtcNow.AddSeconds(4);
                Plugin.Instance.GetMainWindow().AddDebugLog("[Payout] Bank reached 0. Starting 4s auto-close timer.");
            }
            else if (currentBank > 0 && _autoCloseAt.HasValue)
            {
                _autoCloseAt = null;
            }

            if (currentBank != _lastKnownBank)
            {
                _lastKnownBank = currentBank;
                if (currentBank > 0)
                {
                    string clipText = currentBank >= 1_000_000 ? "1000000" : currentBank.ToString();
                    ImGui.SetClipboardText(clipText);
                }
            }
        }

        bool isTradeOpen = Svc.GameGui.GetAddonByName("Trade") != nint.Zero;
        if (_lastFrameTradeOpen && !isTradeOpen)
        {
            if (p != null && p.Bank > 0)
            {
                Plugin.Instance.GetMainWindow().AddDebugLog("[Payout] Trade closed, bank remaining. Re-opening trade...");
                ChatCommandRouter.Send("/trade <t>", Plugin.Instance.Configuration, "ManualPayoutNext");
            }
        }
        _lastFrameTradeOpen = isTradeOpen;
    }

    public static void DrawHelperWindow()
    {
        if (!_isHelperActive) return;

        var mainWindow = Plugin.Instance.GetMainWindow();
        var rect = mainWindow.GetWindowRect();
        if (rect.Size.X > 0)
            ImGui.SetNextWindowPos(new Vector2(rect.Pos.X + rect.Size.X + 10, rect.Pos.Y), ImGuiCond.Appearing);

        ImGui.SetNextWindowSize(new Vector2(280, 200), ImGuiCond.FirstUseEver);
        if (ImGui.Begin($"Payout Helper: {_currentTargetName}###bjb_payout_helper", ref _isHelperActive, ImGuiWindowFlags.NoCollapse))
        {
            var p = mainWindow.GetPlayers().FirstOrDefault(x =>
                x.Name.Equals(_currentTargetName, StringComparison.OrdinalIgnoreCase));

            long currentBank = p?.Bank ?? 0;

            ImGui.TextColored(new Vector4(1, 0.8f, 0, 1), $"Remaining: {currentBank:N0} Gil");
            ImGui.Spacing();

            if (_payoutStartAmount > 0)
            {
                float progress = 1.0f - ((float)currentBank / _payoutStartAmount);
                ImGui.ProgressBar(Math.Clamp(progress, 0f, 1f), new Vector2(-1, 0), $"{(int)(progress * 100)}%");
            }

            ImGui.Spacing();
            ImGui.TextDisabled("Clipboard auto-updated");
            ImGui.Spacing();

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
        _payoutStartAmount = 0;
        _lastKnownBank = 0;
        _autoCloseAt = null;
    }
}
