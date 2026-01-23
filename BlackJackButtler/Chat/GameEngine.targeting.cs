using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static partial class GameEngine
{
    public static void SetForcedRecipient(string? name)
    {
        lock (_ctxLock)
        {
            _forcedRecipientName = name?.Trim() ?? string.Empty;
        }
    }

    public static void ClearForcedRecipient()
    {
        lock (_ctxLock)
        {
            _forcedRecipientName = string.Empty;
        }
    }

    public static string GetCurrentTargetName()
    {
        lock (_ctxLock)
        {
            if (!string.IsNullOrWhiteSpace(_forcedRecipientName))
                return _forcedRecipientName;
        }

        if (_debugMode)
            return _virtualTargetName;

        var real = Plugin.TargetManager.Target?.Name.TextValue ?? string.Empty;
        return !string.IsNullOrWhiteSpace(real) ? real : _virtualTargetName;
    }

    public static void TargetPlayer(string name)
    {
        _virtualTargetName = name;

        if (_debugMode)
            return;

        var obj = Plugin.ObjectTable.FirstOrDefault(o =>
            o.Name.TextValue.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (obj != null)
            Plugin.TargetManager.Target = obj;
    }
}
