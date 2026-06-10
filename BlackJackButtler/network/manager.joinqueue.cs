using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using BlackJackButtler.Chat;
using ECommons.DalamudServices;

namespace BlackJackButtler;

public sealed class JoinQueueEntry
{
    public string Name = string.Empty;
    public string World = string.Empty;
    public string FullKey => $"{Name}@{World}";
    public DateTime EnqueuedAt;
    public DateTime? OutOfRangeSince;
}

public static class JoinQueueManager
{
    private static readonly List<JoinQueueEntry> _queue = new();

    public static IReadOnlyList<JoinQueueEntry> Queue => _queue;
    public static int Count => _queue.Count;
    public static bool IsProcessing => _state != InviteState.Idle;

    private enum InviteState { Idle, Targeting, VerifyTarget, SendingInvite, Restoring }

    private static InviteState _state = InviteState.Idle;
    private static DateTime _stateEnteredAt;
    private static string _previousTargetName = string.Empty;
    private static string _currentInviteName = string.Empty;
    private static int _lastKnownPartySize;
    private static DateTime _leaveTimestamp = DateTime.MinValue;

    public static void Enqueue(string name, string world)
    {
        if (_queue.Any(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(world) || string.IsNullOrWhiteSpace(e.World) || e.World.Equals(world, StringComparison.OrdinalIgnoreCase)))) return;
        _queue.Add(new JoinQueueEntry
        {
            Name = name,
            World = world,
            EnqueuedAt = DateTime.Now,
        });
    }

    public static void Dequeue(string name, string world)
    {
        _queue.RemoveAll(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(world) || string.IsNullOrWhiteSpace(e.World) || e.World.Equals(world, StringComparison.OrdinalIgnoreCase)));
    }

    public static void Clear()
    {
        _queue.Clear();
        if (_state != InviteState.Idle)
        {
            _state = InviteState.Idle;
            _currentInviteName = string.Empty;
        }
    }

    public static bool IsQueued(string name, string world)
    {
        return _queue.Any(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(world) || string.IsNullOrWhiteSpace(e.World) || e.World.Equals(world, StringComparison.OrdinalIgnoreCase)));
    }

    public static void Tick(Configuration cfg)
    {
        if (GroupContextManager.IsAllianceMode(cfg))
            return;

        var partySize = Plugin.PartyList.Length;
        if (partySize < _lastKnownPartySize && _queue.Count > 0)
            _leaveTimestamp = DateTime.Now;
        _lastKnownPartySize = partySize;

        UpdateOutOfRange(cfg);
        ProcessStateMachine(cfg);
    }

    private static void UpdateOutOfRange(Configuration cfg)
    {
        for (int i = _queue.Count - 1; i >= 0; i--)
        {
            var entry = _queue[i];

            if (NearbyPlayersManager.IsPlayerInRange(entry.Name, entry.World, cfg))
            {
                entry.OutOfRangeSince = null;
            }
            else
            {
                entry.OutOfRangeSince ??= DateTime.Now;

                if (!cfg.NoAutoDequeue && (DateTime.Now - entry.OutOfRangeSince.Value).TotalSeconds >= 90)
                {
                    var window = Plugin.Instance.GetMainWindow();
                    window.AddDebugLog($"[JoinQueue] Auto-dequeue: {entry.FullKey} (out of range 90s)");
                    _queue.RemoveAt(i);
                }
            }
        }
    }

    private static void ProcessStateMachine(Configuration cfg)
    {
        double elapsed = (DateTime.Now - _stateEnteredAt).TotalSeconds;

        switch (_state)
        {
            case InviteState.Idle:
                if (_queue.Count == 0) return;
                if (Plugin.PartyList.Length >= 8) return;
                if (GameEngine.CurrentPhase is not (GamePhase.Waiting or GamePhase.Payout)) return;
                if (CommandExecutor.IsRunning || CommandExecutor.IsFollowUpPending) return;
                if ((DateTime.Now - _leaveTimestamp).TotalSeconds < 5.0) return;

                var first = _queue[0];
                if (first.OutOfRangeSince != null) return;

                var target = FindPlayerObject(first.Name, first.World);
                if (target == null)
                {
                    var window = Plugin.Instance.GetMainWindow();
                    window.AddDebugLog($"[JoinQueue] Player not found, removing: {first.FullKey}");
                    _queue.RemoveAt(0);
                    return;
                }

                _currentInviteName = first.FullKey;
                var currentTarget = Plugin.TargetManager.Target;
                _previousTargetName = currentTarget is IPlayerCharacter pc
                    ? $"{pc.Name.TextValue}@{pc.HomeWorld.Value.Name}"
                    : string.Empty;

                Svc.Framework.RunOnTick(() => { Plugin.TargetManager.Target = target; });

                _state = InviteState.Targeting;
                _stateEnteredAt = DateTime.Now;
                break;

            case InviteState.Targeting:
                if (elapsed < 1.0) return;
                _state = InviteState.VerifyTarget;
                _stateEnteredAt = DateTime.Now;
                break;

            case InviteState.VerifyTarget:
                var currentTgt = Plugin.TargetManager.Target;
                if (currentTgt is IPlayerCharacter tgtPc)
                {
                    var tgtKey = $"{tgtPc.Name.TextValue}@{tgtPc.HomeWorld.Value.Name}";
                    var currentParts = _currentInviteName.Split('@');
                    var currentName = currentParts.Length > 0 ? currentParts[0] : _currentInviteName;
                    var currentWorld = currentParts.Length > 1 ? currentParts[1] : string.Empty;
                    if (tgtPc.Name.TextValue.Equals(currentName, StringComparison.OrdinalIgnoreCase)
                        && (string.IsNullOrWhiteSpace(currentWorld) || tgtKey.Equals(_currentInviteName, StringComparison.OrdinalIgnoreCase)))
                    {
                        ChatCommandRouter.Send("/pcmd add <t>", cfg, "JoinQueue");
                        _state = InviteState.SendingInvite;
                        _stateEnteredAt = DateTime.Now;

                        var window = Plugin.Instance.GetMainWindow();
                        window.AddDebugLog($"[JoinQueue] Invited: {_currentInviteName}");
                        _queue.RemoveAll(e => e.FullKey == _currentInviteName);
                        break;
                    }
                }

                var window2 = Plugin.Instance.GetMainWindow();
                window2.AddDebugLog($"[JoinQueue] Target verify failed for: {_currentInviteName}");
                _queue.RemoveAll(e => e.FullKey == _currentInviteName);
                _state = InviteState.Restoring;
                _stateEnteredAt = DateTime.Now;
                break;

            case InviteState.SendingInvite:
                if (elapsed < 0.3) return;
                RestorePreviousTarget();
                _state = InviteState.Restoring;
                _stateEnteredAt = DateTime.Now;
                break;

            case InviteState.Restoring:
                if (elapsed < 0.3) return;
                _state = InviteState.Idle;
                _currentInviteName = string.Empty;
                break;
        }
    }

    private static void RestorePreviousTarget()
    {
        if (string.IsNullOrEmpty(_previousTargetName))
        {
            Svc.Framework.RunOnTick(() => { Plugin.TargetManager.Target = null; });
            return;
        }

        var parts = _previousTargetName.Split('@');
        if (parts.Length != 2) return;

        var obj = FindPlayerObject(parts[0], parts[1]);
        Svc.Framework.RunOnTick(() => { Plugin.TargetManager.Target = obj; });
    }

    private static IPlayerCharacter? FindPlayerObject(string name, string world)
    {
        foreach (var obj in Plugin.ObjectTable)
        {
            if (obj.ObjectKind != ObjectKind.Pc) continue;
            if (obj is not IPlayerCharacter pc) continue;
            if (pc.Name.TextValue == name
                && (string.IsNullOrWhiteSpace(world) || pc.HomeWorld.Value.Name.ToString() == world))
                return pc;
        }
        return null;
    }
}
