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
    public void TryAutoActivateTradingPlayer(string partnerName)
    {
        if (!_config.AutoActivateTradingPlayers || !IsRecognitionActive || string.IsNullOrWhiteSpace(partnerName))
            return;

        SyncParty();

        var player = TradeManager.ResolvePlayer(partnerName, _players);
        if (player == null || !player.IsInParty)
            return;

        var memberKey = GetGroupMemberKey(player.Name, player.WorldId);
        if (!_newTradingPlayerKeys.Remove(memberKey))
            return;

        if (player.IsActivePlayer)
        {
            AddDebugLog($"[TradeAutoActivate] Consumed new status for already active player {player.DisplayName}", false);
            return;
        }

        ActivatePlayer(player);
        AddDebugLog($"[TradeAutoActivate] Activated newly joined trading player {player.DisplayName}", false);
    }

    private static string GetGroupMemberKey(string name, uint worldId)
        => $"{name.Trim()}@{worldId}";

    public void SyncParty()
    {
        if (Plugin.IsDebugMode) return;
        GroupContextManager.Refresh(_config);
        if (!GroupContextManager.IsSnapshotAuthoritative())
        {
            AddFullDebugLog(
                $"[GroupContext] Sync skipped while snapshot is non-authoritative: " +
                GroupContextManager.GetRoutingSummary(_config)
            );
            return;
        }

        _dealer.IsDealer = true;
        _dealer.IsActivePlayer = true;
        foreach (var p in _players)
        {
            p.IsDealer = false;
            if (!p.IsImaginaryPlayer)
                p.IsInParty = false;
        }

        var allianceMode = GroupContextManager.IsAllianceMode(_config);
        if (allianceMode && !_lastAllianceMode)
            JoinQueueManager.Clear();
        _lastAllianceMode = allianceMode;
        var members = GroupContextManager.GetCurrentMembers(_config);
        var localName = Plugin.PlayerState.CharacterName ?? Plugin.ObjectTable.LocalPlayer?.Name.TextValue ?? string.Empty;
        var localWorldId = Plugin.PlayerState.HomeWorld.RowId;
        var localContentId = Plugin.PlayerState.ContentId;
        var currentGroupMemberKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(localName))
        {
            if (_config.EnableCompanionSync)
            {
                var localUid = CompanionSyncManager.GetUidForSource(localName, localWorldId);
                if (CompanionSyncManager.GetUid(_dealer) != localUid)
                    CompanionSyncManager.ClearPlayer(_config, _dealer);
            }

            _dealer.Name = localName;
            _dealer.WorldId = localWorldId;
            _dealer.IsDealer = true;
            _dealer.IsActivePlayer = true;
            _dealer.IsInParty = true;

            var localPlayerDuplicates = _players
                .Where(p => IsLocalPluginUser(p.Name, p.WorldId, 0, localName, localWorldId, localContentId))
                .ToList();
            foreach (var duplicate in localPlayerDuplicates)
            {
                CompanionSyncManager.ClearPlayer(_config, duplicate);
                _players.Remove(duplicate);
            }

            if (localPlayerDuplicates.Count > 0)
                AddDebugLog($"[GroupContext] Removed {localPlayerDuplicates.Count} local-user player duplicate(s); dealer is {localName}@{localWorldId}");
        }

        foreach (var member in members)
        {
            var name = member.Name;
            if (string.IsNullOrEmpty(name)) continue;

            if (IsLocalPluginUser(
                name,
                member.WorldId,
                member.ContentId,
                localName,
                localWorldId,
                localContentId
            ))
                continue;

            uint homeWorldId = member.WorldId;
            foreach (var obj in Plugin.ObjectTable)
            {
                if (obj is IPlayerCharacter pc
                    && pc.Name.TextValue.Equals(name, StringComparison.OrdinalIgnoreCase)
                    && (homeWorldId == 0 || pc.HomeWorld.RowId == homeWorldId))
                {
                    homeWorldId = pc.HomeWorld.RowId;
                    break;
                }
            }

            var memberKey = GetGroupMemberKey(name, homeWorldId);
            currentGroupMemberKeys.Add(memberKey);
            if (_hasGroupMembershipBaseline && !_knownGroupMemberKeys.Contains(memberKey))
            {
                _newTradingPlayerKeys.Add(memberKey);
                AddDebugLog($"[TradeAutoActivate] Marked newly joined member {name}@{homeWorldId}", false);
            }

            var existing = _players.FirstOrDefault(
                x =>
                    x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                    && (x.WorldId == 0 || homeWorldId == 0 || x.WorldId == homeWorldId)
            );
            if (existing != null)
            {
                existing.IsDealer = false;
                existing.IsInParty = true;
                if (existing.WorldId != homeWorldId)
                {
                    CompanionSyncManager.ClearPlayer(_config, existing);
                    existing.WorldId = homeWorldId;
                }
            }
            else
            {
                _players.Add(
                    new PlayerState
                    {
                        Name = name,
                        WorldId = homeWorldId,
                        IsDealer = false,
                        IsInParty = true,
                    }
                );
            }
        }

        _newTradingPlayerKeys.IntersectWith(currentGroupMemberKeys);
        _knownGroupMemberKeys.Clear();
        _knownGroupMemberKeys.UnionWith(currentGroupMemberKeys);
        _hasGroupMembershipBaseline = true;

        foreach (var p in _players.Where(x => !x.IsImaginaryPlayer))
        {
            if (!p.IsInParty)
            {
                string worldName = VipManager.ResolveWorldName(p.WorldId);
                if (!string.IsNullOrEmpty(worldName) && VipManager.GetPlayerTier(p.Name, worldName) > 0)
                    VipManager.SetPlayerTier(p.Name, worldName, 0);
            }
        }

        foreach (var leaver in _players.Where(x => !x.IsImaginaryPlayer && !x.IsInParty && !x.IsActivePlayer && x.Bank > 0).ToList())
        {
            if (StatsManager.IsRunning)
            {
                StatsManager.AddTip(leaver.Bank);
                AddDebugLog($"[Party] {leaver.DisplayName} left with {leaver.Bank} gil — added as tip.", false);
            }
            leaver.Bank = 0;
            CompanionSyncManager.SendPlayerBankBetUpdate(_config, leaver);
        }

        var zeroBankLeavers = _players
            .Where(x => !x.IsImaginaryPlayer && !x.IsInParty && x.Bank == 0)
            .ToList();
        foreach (var leaver in zeroBankLeavers.Where(x => x.IsActivePlayer))
            GameEngine.DeactivatePlayerMidRound(leaver, _players, _config);

        var zeroBankLeaverIds = zeroBankLeavers
            .Select(x => x.UIID)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        RemovePlayersWithCompanionErase(
            x =>
                !x.IsImaginaryPlayer
                && !x.IsInParty
                && !x.IsActivePlayer
                && (x.Bank == 0 || zeroBankLeaverIds.Contains(x.UIID))
        );
    }

    private void SetGroupDetectorActive(bool active)
    {
        var context = active ? "Activate" : "Deactivate";
        ValidatePlayersAgainstCurrentGroup(context);

        IsRecognitionActive = active;
        if (active)
        {
            StatsLogManager.OnGroupDetectorStarted();
            SyncParty();
            ViewDirectionManager.CaptureCurrentRotation(_config);
            _groupDetectorActivatedAt = StatsManager.IsRunning ? null : DateTime.Now;
            if (UserStatisticsManager.HasSessions())
                _triggerUserStatsSessionPrompt = true;
            else
            {
                UserStatisticsManager.StartSession();
                SaveSessionFromUI();
            }
        }
        else
        {
            _triggerUserStatsSessionPrompt = false;
            _userStatsSessionPromptOpen = false;
            UserStatisticsManager.StopSession();
            StatsLogManager.OnGroupDetectorStopped();
            if (!StatsManager.IsRunning)
                SessionManager.ClearSession();
            AddDebugLog(
                StatsManager.IsRunning ? "[SessionManager] Session retained for active stats (Group Detector deactivated)" : "[SessionManager] Session cleared (Group Detector deactivated)",
                false
            );
            if (StatsManager.IsRunning)
                SaveSessionFromUI();
            _groupDetectorActivatedAt = null;
        }

        Plugin.Instance.UpdateEventHooks();
    }
}
