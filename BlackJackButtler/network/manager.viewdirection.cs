using System;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using ECommons.DalamudServices;

namespace BlackJackButtler;

public static class ViewDirectionManager
{
    public static void CaptureCurrentRotation(Configuration cfg)
    {
        var lp = Plugin.ObjectTable.LocalPlayer;
        if (lp == null) return;
        cfg.InitialViewDirection = lp.Rotation;
        cfg.Save();
    }

    public static void ApplyViewDirection(Configuration cfg)
    {
        var angle = cfg.InitialViewDirection;
        Svc.Framework.RunOnTick(() =>
        {
            var lp = Plugin.ObjectTable.LocalPlayer;
            if (lp == null) return;
            SetRotation(lp.Address, angle);
        });
    }

    public static void ApplyViewDirectionImmediate(Configuration cfg)
    {
        var lp = Plugin.ObjectTable.LocalPlayer;
        if (lp == null) return;
        SetRotation(lp.Address, cfg.InitialViewDirection);
    }

    public static void TickLookEveryTime(Configuration cfg)
    {
        if (!cfg.LookEveryTime) return;
        var lp = Plugin.ObjectTable.LocalPlayer;
        var target = Plugin.TargetManager.Target;
        if (lp != null && target != null
            && target.GameObjectId == lp.GameObjectId)
        {
            SetRotation(lp.Address, cfg.InitialViewDirection);
        }
    }

    public static bool IsInternalCommand(string text)
    {
        return text.TrimStart().StartsWith("/initialviewdirection", StringComparison.OrdinalIgnoreCase);
    }

    private static unsafe void SetRotation(nint address, float angle)
    {
        ((GameObject*)address)->Rotation = angle;
    }
}
