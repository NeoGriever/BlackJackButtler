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
    private void ValidatePlayersAgainstCurrentGroup(string context)
    {
        GroupContextManager.Refresh(_config, strictValidation: true);
        var members = GroupContextManager.GetCurrentMembers(_config);
        var memberKeys = members
            .Select(member => $"{member.Name}@{member.WorldId}")
            .ToList();
        var before = _players
            .Select(player => $"{player.Name}@{player.WorldId}[Bank={player.Bank:N0},Active={player.IsActivePlayer}]")
            .ToList();

        if (!GroupContextManager.IsSnapshotAuthoritative())
        {
            AddDebugLog(
                $"[GroupDetector:{context}] Validation skipped for non-authoritative snapshot | " +
                $"{GroupContextManager.GetRoutingSummary(_config)} | " +
                $"Detected=[{string.Join("; ", memberKeys)}] | Before=[{string.Join("; ", before)}]"
            );
            AddFullDebugLog(
                $"[GroupDetector:{context}] Full context: {GroupContextManager.GetRoutingDiagnostic(_config)}"
            );
            return;
        }

        var removedRealPlayers = _players
            .Where(player => !player.IsImaginaryPlayer)
            .Where(
                player => !members.Any(
                    member =>
                        member.Name.Equals(player.Name, StringComparison.OrdinalIgnoreCase)
                        && (member.WorldId == 0 || player.WorldId == 0 || member.WorldId == player.WorldId)
                )
            )
            .ToList();
        var removed = removedRealPlayers
            .Concat(
                _players.Where(
                    player => player.IsImaginaryPlayer
                        && (removedRealPlayers.Any(realPlayer => PlayerIdentityManager.References(player, realPlayer))
                            || PlayerIdentityManager.GetReferencedPlayer(_players, player) == null)
                )
            )
            .ToList();

        foreach (var player in removed)
        {
            CompanionSyncManager.ClearPlayer(_config, player);
            _players.Remove(player);
        }

        AddDebugLog(
            $"[GroupDetector:{context}] Strict validation | {GroupContextManager.GetRoutingSummary(_config)} | " +
            $"Detected=[{string.Join("; ", memberKeys)}] | Before=[{string.Join("; ", before)}] | " +
            $"Removed=[{string.Join("; ", removed.Select(player => $"{player.Name}@{player.WorldId}"))}] | " +
            $"Remaining={_players.Count}"
        );
        AddFullDebugLog(
            $"[GroupDetector:{context}] Full context: {GroupContextManager.GetRoutingDiagnostic(_config)}"
        );
    }

    private static bool IsLocalPluginUser(
        string candidateName,
        uint candidateWorldId,
        ulong candidateContentId,
        string localName,
        uint localWorldId,
        ulong localContentId
    )
    {
        if (localContentId != 0 && candidateContentId != 0)
            return candidateContentId == localContentId;

        if (!candidateName.Equals(localName, StringComparison.OrdinalIgnoreCase))
            return false;

        return candidateWorldId == 0
            || localWorldId == 0
            || candidateWorldId == localWorldId;
    }
}
