using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using BlackJackButtler.Chat;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void ExecuteNearbyCommand(NearbyPlayerInfo p, string commandName, string context)
    {
        if (string.IsNullOrWhiteSpace(commandName)) return;

        var currentTarget = Plugin.TargetManager.Target;
        string previousName = currentTarget?.Name.TextValue ?? string.Empty;
        string previousWorld = currentTarget is Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter pc ? pc.HomeWorld.Value.Name.ToString() : string.Empty;

        GameActionQueueManager.Enqueue(
            $"{context}:{p.FullKey}",
            async () =>
            {
                try
                {
                    GameEngine.TargetPlayer(p.Name, p.World);
                    var targetName = string.IsNullOrWhiteSpace(p.World) ? p.Name : $"{p.Name}@{p.World}";
                    await CommandExecutor.ExecuteGroup(commandName, targetName, _config);
                }
                finally
                {
                    if (!string.IsNullOrWhiteSpace(previousName))
                        GameEngine.TargetPlayer(previousName, previousWorld);
                }
            },
            $"NearbyCommand:{p.FullKey}"
        );
    }
}
