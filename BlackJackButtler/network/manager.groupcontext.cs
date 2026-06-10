using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Party;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Component.GUI;
using static ECommons.GenericHelpers;

namespace BlackJackButtler;

public static class GroupContextManager
{
    private const int PartySlots = 8;
    private const int AllianceSlots = 20;

    private sealed record MemberSnapshot(
        string Source,
        int Slot,
        nint Address,
        IPartyMember Member,
        string Name,
        uint WorldId,
        ulong ContentId);

    private static readonly object Gate = new();
    private static IReadOnlyList<MemberSnapshot> _members = Array.Empty<MemberSnapshot>();
    private static bool _rawIsAlliance;
    private static bool _nativeIsAlliance;
    private static byte _nativeMemberCount;
    private static byte _nativeAllianceFlags;
    private static nint _allianceList1AddonAddress;
    private static nint _allianceList2AddonAddress;
    private static bool _allianceList1Visible;
    private static bool _allianceList2Visible;
    private static bool _detectedAlliance;
    private static string _decisionReason = "Not refreshed";
    private static bool _groupDisbandedHint;
    private static int _partyLength;
    private static nint _groupManagerAddress;
    private static nint _groupListAddress;
    private static nint _allianceListAddress;
    private static string _lastDiagnosticSignature = string.Empty;

    public static bool IsAllianceMode(Configuration config)
    {
        lock (Gate)
            return _detectedAlliance;
    }

    public static void ObserveSystemMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var normalized = message.Trim();
        lock (Gate)
        {
            if (ContainsAny(
                    normalized,
                    "Deine Gruppe wurde aufgelöst",
                    "Die Allianz für die Gruppensuche wurde aufgelöst",
                    "Your party has been disbanded",
                    "The alliance has been disbanded",
                    "The party finder alliance has been disbanded"))
            {
                _groupDisbandedHint = true;
                return;
            }

            if (ContainsAny(
                    normalized,
                    "Du bist einer Allianz beigetreten",
                    "Du hast eine weltenübergreifende Gruppe gebildet",
                    "You have joined an alliance",
                    "You have formed a cross-world party",
                    "You have joined a party"))
            {
                _groupDisbandedHint = false;
            }
        }
    }

    public static void Refresh(Configuration config, bool strictValidation = false)
    {
        var snapshots = new List<MemberSnapshot>(PartySlots + AllianceSlots);
        var rawIsAlliance = Plugin.PartyList.IsAlliance;
        var partyLength = Plugin.PartyList.Length;
        var groupManagerAddress = Plugin.PartyList.GroupManagerAddress;
        var groupListAddress = Plugin.PartyList.GroupListAddress;
        var allianceListAddress = Plugin.PartyList.AllianceListAddress;
        var nativeIsAlliance = false;
        byte nativeMemberCount = 0;
        byte nativeAllianceFlags = 0;
        ReadNativeGroupState(ref nativeIsAlliance, ref nativeMemberCount, ref nativeAllianceFlags);
        ReadAllianceAddonState(
            out var allianceList1AddonAddress,
            out var allianceList1Visible,
            out var allianceList2AddonAddress,
            out var allianceList2Visible);

        for (var i = 0; i < PartySlots; i++)
            TryAddMember(snapshots, "Party", i, alliance: false);

        for (var i = 0; i < AllianceSlots; i++)
            TryAddMember(snapshots, "Alliance", i, alliance: true);

        snapshots = snapshots
            .GroupBy(x => x.ContentId != 0
                ? $"cid:{x.ContentId}"
                : $"name:{x.Name}@{x.WorldId}")
            .Select(x => x.First())
            .ToList();

        var confirmedPartySlotCount = Math.Min(
            PartySlots,
            Math.Max((int)nativeMemberCount, partyLength));
        if (strictValidation || !nativeIsAlliance)
        {
            snapshots = snapshots
                .Where(x => x.Source != "Party" || x.Slot < confirmedPartySlotCount)
                .ToList();
        }

        var allianceMemberCount = snapshots.Count(x => x.Source == "Alliance");
        bool groupDisbandedHint;
        lock (Gate)
        {
            if (nativeMemberCount > 0 || partyLength > 0 || rawIsAlliance || nativeIsAlliance || allianceMemberCount > 0)
                _groupDisbandedHint = false;
            groupDisbandedHint = _groupDisbandedHint;
        }

        if (groupDisbandedHint)
        {
            snapshots.Clear();
            allianceMemberCount = 0;
        }

        var allianceAddonExists = allianceList1AddonAddress != 0 || allianceList2AddonAddress != 0;
        var confirmedNormalParty = !nativeIsAlliance
            && !rawIsAlliance
            && allianceMemberCount == 0
            && nativeMemberCount > 0
            && partyLength > 0;

        var detectedAlliance = !groupDisbandedHint && !confirmedNormalParty
            && (allianceAddonExists || nativeIsAlliance || rawIsAlliance || allianceMemberCount > 0);
        var decisionReason = groupDisbandedHint
                ? "Group disbanded system message"
                : confirmedNormalParty
                ? $"Confirmed normal party (NativeMemberCount={nativeMemberCount}, PartyList.Length={partyLength})"
                : nativeIsAlliance
                    ? "Native GroupManager IsAlliance"
                    : rawIsAlliance
                        ? "Dalamud PartyList.IsAlliance"
                        : allianceMemberCount > 0
                            ? $"Populated alliance slots ({allianceMemberCount})"
                            : allianceAddonExists
                                ? "Alliance UI addon fallback"
                                : "No alliance signal";

        string diagnostic;
        lock (Gate)
        {
            _members = snapshots;
            _rawIsAlliance = rawIsAlliance;
            _nativeIsAlliance = nativeIsAlliance;
            _nativeMemberCount = nativeMemberCount;
            _nativeAllianceFlags = nativeAllianceFlags;
            _allianceList1AddonAddress = allianceList1AddonAddress;
            _allianceList2AddonAddress = allianceList2AddonAddress;
            _allianceList1Visible = allianceList1Visible;
            _allianceList2Visible = allianceList2Visible;
            _detectedAlliance = detectedAlliance;
            _decisionReason = decisionReason;
            _partyLength = partyLength;
            _groupManagerAddress = groupManagerAddress;
            _groupListAddress = groupListAddress;
            _allianceListAddress = allianceListAddress;
            diagnostic = BuildDiagnosticLocked();
        }

        var signature = string.Join('|',
            rawIsAlliance,
            nativeIsAlliance,
            nativeMemberCount,
            nativeAllianceFlags,
            allianceList1AddonAddress,
            allianceList1Visible,
            allianceList2AddonAddress,
            allianceList2Visible,
            detectedAlliance,
            decisionReason,
            partyLength,
            groupManagerAddress,
            groupListAddress,
            allianceListAddress,
            strictValidation,
            string.Join(',', snapshots.Select(x => $"{x.Source}:{x.Slot}:{x.ContentId}:{x.Name}:{x.WorldId}")));

        if (!string.Equals(signature, _lastDiagnosticSignature, StringComparison.Ordinal))
        {
            _lastDiagnosticSignature = signature;
            Plugin.Instance.GetMainWindow().AddDebugLog(diagnostic);
        }
    }

    private static bool ContainsAny(string value, params string[] candidates)
        => candidates.Any(candidate =>
            value.Contains(candidate, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<IPartyMember> GetCurrentMembers(Configuration config)
    {
        lock (Gate)
        {
            var includeAlliance = _detectedAlliance;
            return _members
                .Where(x => x.Source == "Party" || includeAlliance)
                .Select(x => x.Member)
                .ToList();
        }
    }

    public static string GetRoutingDiagnostic(Configuration config)
    {
        lock (Gate)
            return BuildDiagnosticLocked();
    }

    public static string GetRoutingSummary(Configuration config)
    {
        lock (Gate)
        {
            var partyMembers = _members.Count(x => x.Source == "Party");
            var allianceMembers = _members.Count(x => x.Source == "Alliance");
            return $"Mode={(_detectedAlliance ? "Alliance" : "Party")} | " +
                   $"Reason='{_decisionReason}' | PartySlots={partyMembers} | AllianceSlots={allianceMembers}";
        }
    }

    public static Task<bool> CaptureAllianceModeAsync(Configuration config, string context)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Plugin.Framework.RunOnTick(() =>
        {
            try
            {
                Refresh(config);
                var alliance = IsAllianceMode(config);
                Plugin.Instance.GetMainWindow().AddDebugLog(
                    $"[GroupRoute:{context}] {GetRoutingSummary(config)}");
                tcs.TrySetResult(alliance);
            }
            catch (Exception ex)
            {
                Plugin.Instance.GetMainWindow().AddDebugLog(
                    $"[GroupRoute:{context}] Detection failed: {ex.GetType().Name} - {ex.Message}");
                tcs.TrySetResult(false);
            }
        });
        return tcs.Task;
    }

    private static void TryAddMember(
        ICollection<MemberSnapshot> snapshots,
        string source,
        int slot,
        bool alliance)
    {
        try
        {
            var address = alliance
                ? Plugin.PartyList.GetAllianceMemberAddress(slot)
                : Plugin.PartyList.GetPartyMemberAddress(slot);
            if (address == 0)
                return;

            var member = alliance
                ? Plugin.PartyList.CreateAllianceMemberReference(address)
                : Plugin.PartyList.CreatePartyMemberReference(address);
            var name = member?.Name.TextValue ?? string.Empty;
            if (member == null || string.IsNullOrWhiteSpace(name))
                return;

            snapshots.Add(new MemberSnapshot(
                source,
                slot,
                address,
                member,
                name,
                member.World.RowId,
                member.ContentId));
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug($"[GroupContext] Failed to read {source} slot {slot}: {ex.Message}");
        }
    }

    private static unsafe void ReadNativeGroupState(
        ref bool isAlliance,
        ref byte memberCount,
        ref byte allianceFlags)
    {
        try
        {
            var manager = GroupManager.Instance();
            if (manager == null)
                return;

            var group = manager->GetGroupWithCheck(false);
            if (group == null)
                group = &manager->MainGroup;
            if (group == null)
                return;

            isAlliance = group->IsAlliance;
            memberCount = group->MemberCount;
            allianceFlags = group->AllianceFlags;
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug($"[GroupContext] Failed to read native GroupManager state: {ex.Message}");
        }
    }

    private static unsafe void ReadAllianceAddonState(
        out nint list1Address,
        out bool list1Visible,
        out nint list2Address,
        out bool list2Visible)
    {
        list1Address = 0;
        list1Visible = false;
        list2Address = 0;
        list2Visible = false;

        if (TryGetAddonByName<AtkUnitBase>("_AllianceList1", out var list1))
        {
            list1Address = (nint)list1;
            list1Visible = list1->IsVisible;
        }

        if (TryGetAddonByName<AtkUnitBase>("_AllianceList2", out var list2))
        {
            list2Address = (nint)list2;
            list2Visible = list2->IsVisible;
        }
    }

    private static string BuildDiagnosticLocked()
    {
        var partyMembers = _members.Where(x => x.Source == "Party").ToList();
        var allianceMembers = _members.Where(x => x.Source == "Alliance").ToList();
        var slots = _members.Count == 0
            ? "(none)"
            : string.Join("; ", _members.Select(x =>
                $"{x.Source}[{x.Slot}]@0x{x.Address:X}={x.Name}@{x.WorldId}/CID:{x.ContentId}"));

        return $"[GroupContext] Mode={(_detectedAlliance ? "Alliance" : "Party")} | " +
               $"Reason='{_decisionReason}' | PartySlots={partyMembers.Count} | " +
               $"AllianceSlots={allianceMembers.Count} | Members: {slots}";
    }
}
