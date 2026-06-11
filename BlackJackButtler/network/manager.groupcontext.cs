using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using static ECommons.GenericHelpers;

namespace BlackJackButtler;

public sealed record GroupMemberSnapshot(
    string Source,
    int GroupIndex,
    int MemberIndex,
    nint Address,
    string Name,
    uint WorldId,
    string WorldName,
    ulong ContentId,
    bool IsPartyLeader);

public static class GroupContextManager
{
    private const int PartySlots = 8;
    private const int AllianceSlots = 20;
    private const int CrossRealmGroupSlots = 6;
    private const int CrossRealmMemberSlots = 8;

    private static readonly object Gate = new();
    private static IReadOnlyList<GroupMemberSnapshot> _members = Array.Empty<GroupMemberSnapshot>();
    private static bool _rawIsAlliance;
    private static bool _nativeIsAlliance;
    private static bool _crossRealmActive;
    private static bool _crossRealmAlliance;
    private static bool _authoritative;
    private static byte _nativeMemberCount;
    private static byte _nativeAllianceFlags;
    private static byte _crossRealmGroupCount;
    private static byte _crossRealmLocalGroupIndex;
    private static nint _crossRealmProxyAddress;
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

    public static bool IsSnapshotAuthoritative()
    {
        lock (Gate)
            return _authoritative;
    }

    public static int CurrentMemberCount()
    {
        lock (Gate)
            return _members.Count;
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

        var crossRealmMembers = ReadCrossRealmMembers(
            out var crossRealmActive,
            out var crossRealmAlliance,
            out var crossRealmGroupCount,
            out var crossRealmLocalGroupIndex,
            out var crossRealmProxyAddress);
        var nativeMembers = crossRealmActive
            ? new List<GroupMemberSnapshot>()
            : ReadNativeMembers(nativeIsAlliance, nativeMemberCount, partyLength, strictValidation);

        bool groupDisbandedHint;
        IReadOnlyList<GroupMemberSnapshot> previousMembers;
        lock (Gate)
        {
            if (crossRealmMembers.Count > 0 || nativeMembers.Count > 0)
                _groupDisbandedHint = false;
            groupDisbandedHint = _groupDisbandedHint;
            previousMembers = _members;
        }

        IReadOnlyList<GroupMemberSnapshot> snapshots;
        bool authoritative;
        string decisionReason;

        if (groupDisbandedHint)
        {
            snapshots = Array.Empty<GroupMemberSnapshot>();
            authoritative = true;
            decisionReason = "Group disbanded system message";
        }
        else if (crossRealmActive && crossRealmMembers.Count > 0)
        {
            snapshots = Deduplicate(crossRealmMembers);
            authoritative = true;
            decisionReason = crossRealmAlliance
                ? $"InfoProxyCrossRealm alliance ({crossRealmGroupCount} groups)"
                : $"InfoProxyCrossRealm party (local group {crossRealmLocalGroupIndex})";
        }
        else if (crossRealmActive)
        {
            snapshots = previousMembers;
            authoritative = false;
            decisionReason = "InfoProxyCrossRealm active but temporarily empty; retained previous snapshot";
        }
        else
        {
            snapshots = Deduplicate(nativeMembers);
            authoritative = true;
            decisionReason = nativeIsAlliance || rawIsAlliance
                ? "Native GroupManager alliance"
                : snapshots.Count > 0
                    ? $"Dalamud party ({snapshots.Count} members)"
                    : "No active group";
        }

        var detectedAlliance = !groupDisbandedHint &&
            (crossRealmActive ? crossRealmAlliance : nativeIsAlliance || rawIsAlliance);
        string diagnostic;
        lock (Gate)
        {
            _members = snapshots;
            _rawIsAlliance = rawIsAlliance;
            _nativeIsAlliance = nativeIsAlliance;
            _crossRealmActive = crossRealmActive;
            _crossRealmAlliance = crossRealmAlliance;
            _authoritative = authoritative;
            _nativeMemberCount = nativeMemberCount;
            _nativeAllianceFlags = nativeAllianceFlags;
            _crossRealmGroupCount = crossRealmGroupCount;
            _crossRealmLocalGroupIndex = crossRealmLocalGroupIndex;
            _crossRealmProxyAddress = crossRealmProxyAddress;
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
            crossRealmActive,
            crossRealmAlliance,
            authoritative,
            nativeMemberCount,
            nativeAllianceFlags,
            crossRealmGroupCount,
            crossRealmLocalGroupIndex,
            crossRealmProxyAddress,
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
            string.Join(',', snapshots.Select(x =>
                $"{x.Source}:{x.GroupIndex}:{x.MemberIndex}:{x.ContentId}:{x.Name}:{x.WorldId}:{x.IsPartyLeader}")));

        if (!string.Equals(signature, _lastDiagnosticSignature, StringComparison.Ordinal))
        {
            _lastDiagnosticSignature = signature;
            Plugin.Instance.GetMainWindow().AddDebugLog(diagnostic);
        }
    }

    private static bool ContainsAny(string value, params string[] candidates)
        => candidates.Any(candidate =>
            value.Contains(candidate, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<GroupMemberSnapshot> GetCurrentMembers(Configuration config)
    {
        lock (Gate)
            return _members.ToList();
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
            return $"Mode={(_detectedAlliance ? "Alliance" : "Party")} | " +
                   $"Source={(_crossRealmActive ? "CrossRealm" : "Native")} | " +
                   $"Authoritative={_authoritative} | Reason='{_decisionReason}' | Members={_members.Count}";
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

    private static List<GroupMemberSnapshot> ReadNativeMembers(
        bool nativeIsAlliance,
        byte nativeMemberCount,
        int partyLength,
        bool strictValidation)
    {
        var snapshots = new List<GroupMemberSnapshot>(PartySlots + AllianceSlots);
        var partySlotCount = Math.Min(PartySlots, Math.Max((int)nativeMemberCount, partyLength));
        for (var i = 0; i < partySlotCount; i++)
            TryAddNativeMember(snapshots, "Party", 0, i, alliance: false);

        if (nativeIsAlliance)
        {
            for (var i = 0; i < AllianceSlots; i++)
                TryAddNativeMember(snapshots, "Alliance", (i / PartySlots) + 1, i, alliance: true);
        }

        if (strictValidation && !nativeIsAlliance)
            return snapshots.Where(x => x.MemberIndex < partySlotCount).ToList();

        return snapshots;
    }

    private static void TryAddNativeMember(
        ICollection<GroupMemberSnapshot> snapshots,
        string source,
        int groupIndex,
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

            snapshots.Add(new GroupMemberSnapshot(
                source,
                groupIndex,
                slot,
                address,
                name,
                member.World.RowId,
                member.World.ValueNullable?.Name.ToString() ?? ResolveWorldName(member.World.RowId),
                member.ContentId,
                !alliance && (uint)slot == Plugin.PartyList.PartyLeaderIndex));
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug($"[GroupContext] Failed to read {source} slot {slot}: {ex.Message}");
        }
    }

    private static unsafe List<GroupMemberSnapshot> ReadCrossRealmMembers(
        out bool active,
        out bool alliance,
        out byte groupCount,
        out byte localGroupIndex,
        out nint proxyAddress)
    {
        var snapshots = new List<GroupMemberSnapshot>(24);
        active = false;
        alliance = false;
        groupCount = 0;
        localGroupIndex = 0;
        proxyAddress = 0;

        try
        {
            var proxy = InfoProxyCrossRealm.Instance();
            proxyAddress = (nint)proxy;
            if (proxy == null)
                return snapshots;

            var proxyReportsActive = proxy->IsInCrossRealmParty;
            active = proxyReportsActive;
            alliance = active && proxy->IsInAllianceRaid;
            groupCount = (byte)Math.Min((int)proxy->GroupCount, CrossRealmGroupSlots);
            localGroupIndex = proxy->LocalPlayerGroupIndex;
            if (!proxyReportsActive && groupCount == 0)
                return snapshots;

            for (var groupIndex = 0; groupIndex < groupCount; groupIndex++)
            {
                var group = proxy->CrossRealmGroups[groupIndex];
                var memberCount = Math.Min((int)group.GroupMemberCount, CrossRealmMemberSlots);
                for (var memberIndex = 0; memberIndex < memberCount; memberIndex++)
                {
                    var member = group.GroupMembers[memberIndex];
                    var name = member.NameString;
                    if (string.IsNullOrWhiteSpace(name) || member.HomeWorld < 0)
                        continue;

                    snapshots.Add(new GroupMemberSnapshot(
                        "CrossRealm",
                        groupIndex,
                        memberIndex,
                        0,
                        name,
                        (uint)member.HomeWorld,
                        ResolveWorldName((uint)member.HomeWorld),
                        member.ContentId,
                        member.IsPartyLeader));
                }
            }

            active = active || snapshots.Count > 1;
            alliance = active && proxy->IsInAllianceRaid;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "[GroupContext] Failed to read InfoProxyCrossRealm");
        }

        return snapshots;
    }

    private static List<GroupMemberSnapshot> Deduplicate(IEnumerable<GroupMemberSnapshot> members)
    {
        return members
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.ContentId != 0
                ? $"cid:{x.ContentId}"
                : $"name:{x.Name}@{x.WorldId}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
    }

    private static string ResolveWorldName(uint worldId)
    {
        if (worldId == 0)
            return string.Empty;

        return GetRow<World>(worldId)?.Name.ToString() ?? string.Empty;
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
        var slots = _members.Count == 0
            ? "(none)"
            : string.Join("; ", _members.Select(x =>
                $"{x.Source}[{x.GroupIndex}:{x.MemberIndex}]@0x{x.Address:X}=" +
                $"{x.Name}@{x.WorldId}/CID:{x.ContentId}/Leader:{x.IsPartyLeader}"));

        return $"[GroupContext] Mode={(_detectedAlliance ? "Alliance" : "Party")} | " +
               $"Source={(_crossRealmActive ? "CrossRealm" : "Native")} | " +
               $"Authoritative={_authoritative} | Reason='{_decisionReason}' | Members={_members.Count} | " +
               $"CrossRealm=Active:{_crossRealmActive},Alliance:{_crossRealmAlliance}," +
               $"Groups:{_crossRealmGroupCount},LocalGroup:{_crossRealmLocalGroupIndex}," +
               $"Proxy:0x{_crossRealmProxyAddress:X} | " +
               $"Native=PartyLength:{_partyLength},MemberCount:{_nativeMemberCount}," +
               $"Alliance:{_nativeIsAlliance},RawAlliance:{_rawIsAlliance}," +
               $"Flags:0x{_nativeAllianceFlags:X2},Manager:0x{_groupManagerAddress:X}," +
               $"Party:0x{_groupListAddress:X},AllianceList:0x{_allianceListAddress:X} | " +
               $"AllianceAddons=List1:0x{_allianceList1AddonAddress:X}/Visible:{_allianceList1Visible}," +
               $"List2:0x{_allianceList2AddonAddress:X}/Visible:{_allianceList2Visible} | Members: {slots}";
    }
}
