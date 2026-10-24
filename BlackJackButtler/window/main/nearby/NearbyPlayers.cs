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
    private static readonly Vector4 NearbyColorName = new(1f, 0.65f, 0.2f, 1f);
    private static readonly Vector4 NearbyColorWorld = new(0.6f, 0.6f, 0.6f, 1f);
    private static readonly Vector4 NearbyColorFavName = new(0.4f, 0.9f, 0.5f, 1f);
    private static readonly Vector4 NearbyColorOutOfRange = new(0.35f, 0.35f, 0.35f, 1f);
    private static readonly Vector4 NearbyColorStarFav = new(1f, 0.85f, 0f, 1f);
    private static readonly Vector4 NearbyColorStarNormal = new(0.4f, 0.4f, 0.4f, 1f);
    private static readonly Vector4 NearbyColorQueuedName = new(0.5f, 0.85f, 1f, 1f);

    private bool _distSliderHovered;
    private bool _nearbyListHovered;
    private bool _showNearbySettingsWindow;
    private bool _showNearbyIgnoreWindow;
    private bool _nearbyDistSliderEditMode;
    private bool _nearbyListVisible = true;
    private string _nearbyIgnoreNameInput = string.Empty;
    private string _nearbyIgnoreWorldInput = string.Empty;
    private DateTime _nearbyWorldInputBlockedUntil = DateTime.MinValue;

    internal void DrawNearbyPlayersSection()
    {
        if (!_config.ShowNearbyPlayers)
        {
            _showNearbySettingsWindow = false;
            return;
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.4f, 0.8f, 1f, 1f), "NEARBY PLAYERS");
        if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            _nearbyListVisible = !_nearbyListVisible;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(_nearbyListVisible ? "Click to hide nearby players" : "Click to show nearby players");
        DrawNearbySettingsWindow();
        if (!_nearbyListVisible)
        {
            DrawDistanceCircle();
            return;
        }
        ImGui.SameLine();

        var allianceMode = GroupContextManager.IsAllianceMode(_config);
        if (!allianceMode && JoinQueueManager.Count > 0)
        {
            ImGui.TextColored(new Vector4(1f, 0.8f, 0f, 1f), $"Queue: {JoinQueueManager.Count}");
            ImGui.SameLine();
            if (BJBGui.SmallButton("Clear##joinqueue")) JoinQueueManager.Clear();
            ImGui.SameLine();
        }

        ImGui.TextColored(NearbyColorWorld, "(click name to /tell)");

        NearbyPlayersManager.PauseSorting = _config.NearbySticky || _nearbyListHovered;
        var allPlayers = NearbyPlayersManager.GetNearbyPlayers(_config);

        var queue = JoinQueueManager.Queue;
        var queued = new List<NearbyPlayerInfo>();
        var rest = new List<NearbyPlayerInfo>();
        var seenKeys = new HashSet<string>();

        foreach (var q in queue)
        {
            var match = allPlayers.FirstOrDefault(p => p.FullKey == q.FullKey);
            if (match != null)
            {
                queued.Add(match);
                seenKeys.Add(match.FullKey);
            }
            else
            {
                queued.Add(new NearbyPlayerInfo { Name = q.Name, World = q.World, Distance = float.MaxValue });
                seenKeys.Add(q.FullKey);
            }
        }

        foreach (var p in allPlayers)
            if (!seenKeys.Contains(p.FullKey)) rest.Add(p);

        var sorted = queued.Concat(rest).ToList();
        sorted = NearbyNumberManager.ApplyAndSort(sorted);

        if (sorted.Count == 0)
        {
            ImGui.TextColored(NearbyColorWorld, "No nearby players found.");
            DrawDistanceCircle();
            return;
        }

        if (_config.NearbyShowFootNumbers)
            NearbyNumberManager.DrawFootNumbers(sorted);

        int columns = Math.Clamp(_config.NearbyColumns, 1, 5);
        var availableRegion = ImGui.GetContentRegionAvail();
        if (availableRegion.X <= 1f || availableRegion.Y <= 1f)
        {
            _nearbyListHovered = false;
            NearbyPlayersManager.PauseSorting = _config.NearbySticky;
            ImGui.TextDisabled("Resize the window to show nearby players.");
            DrawDistanceCircle();
            return;
        }

        float availWidth = availableRegion.X;
        float colWidth = availWidth / columns;
        float rowHeight = ImGui.GetTextLineHeightWithSpacing() + 2f;

        int totalItems = sorted.Count;
        int totalRows = (int)Math.Ceiling(totalItems / (double)columns);
        float availHeight = availableRegion.Y;
        float childHeight;
        if (availHeight > rowHeight * 4)
        {
            childHeight = availHeight - 4f;
        }
        else
        {
            int visibleRows = Math.Clamp(totalRows, 3, 15);
            childHeight = visibleRows * rowHeight + 8f;
        }

        bool partyFull = GroupContextManager.CurrentMemberCount() >= 8;

        childHeight = Math.Max(rowHeight, childHeight);
        if (ImGui.BeginChild("bjb_nearby_scroll", new Vector2(Math.Max(1f, availWidth), childHeight), true))
        {
            ImGui.PushFont(UiBuilder.MonoFont);

            _nearbyListHovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows);
            NearbyPlayersManager.PauseSorting = _config.NearbySticky || _nearbyListHovered;

            for (int i = 0; i < sorted.Count; i++)
            {
                var p = sorted[i];
                int col = i % columns;

                if (col > 0) ImGui.SameLine(col * colWidth);

                bool isFav = _config.NearbyFavorites.Contains(p.FullKey);
                bool isQueued = JoinQueueManager.IsQueued(p.Name, p.World);
                bool outOfRange = !isFav && !isQueued && !p.IsInRange;

                ImGui.PushID($"nearby_{i}");

                var drewJoinButton = false;
                if (isQueued)
                {
                    drewJoinButton = true;
                    var entry = JoinQueueManager.Queue.FirstOrDefault(e => e.FullKey == p.FullKey);
                    if (entry?.OutOfRangeSince != null)
                    {
                        double elapsed = (DateTime.Now - entry.OutOfRangeSince.Value).TotalSeconds;
                        int remaining = Math.Max(0, 90 - (int)elapsed);
                        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.15f, 0.15f, 1f));
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.7f, 0.25f, 0.25f, 1f));
                        if (BJBGui.SmallButton($"{remaining}s##nearby_join_{i}"))
                            JoinQueueManager.Dequeue(p.Name, p.World);
                        ImGui.PopStyleColor(2);
                    }
                    else
                    {
                        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1f));
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.3f, 0.3f, 1f));
                        if (BJBGui.SmallButton($"X##nearby_join_{i}"))
                            JoinQueueManager.Dequeue(p.Name, p.World);
                        ImGui.PopStyleColor(2);
                    }
                }
                else
                {
                    if (allianceMode)
                    {
                        drewJoinButton = true;
                        var canRun = !string.IsNullOrWhiteSpace(_config.AllianceNearbyCommandName)
                            && !GameActionQueueManager.IsBusy;
                        if (!canRun) ImGui.BeginDisabled();
                        if (BJBGui.SmallButton($"J##nearby_join_{i}"))
                            ExecuteNearbyCommand(p, _config.AllianceNearbyCommandName, "AllianceNearbyJ");
                        if (!canRun) ImGui.EndDisabled();
                        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                            ImGui.SetTooltip(
                                string.IsNullOrWhiteSpace(_config.AllianceNearbyCommandName) ? "Select an Alliance Nearby J Command in the Nearby Players CFG window" : $"Run {_config.AllianceNearbyCommandName} for {p.FullKey}"
                            );
                    }
                    else if (_config.PartyNearbyJCommand == PartyNearbyJCommandMode.PartyInvite)
                    {
                        drewJoinButton = true;
                        if (partyFull) ImGui.BeginDisabled();
                        if (BJBGui.SmallButton($"J##nearby_join_{i}"))
                            JoinQueueManager.Enqueue(p.Name, p.World);
                        if (partyFull) ImGui.EndDisabled();
                    }
                }

                if (drewJoinButton)
                    ImGui.SameLine(0, 4);

                ImGui.TextColored(new Vector4(1f, 0.85f, 0.25f, 1f), $"{NearbyNumberManager.GetNumber(p.FullKey),2}");
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Stable nearby number");
                ImGui.SameLine(0, 4);

                var starColor = isFav ? NearbyColorStarFav : NearbyColorStarNormal;
                ImGui.TextColored(starColor, isFav ? "\u2605" : "\u2606");
                if (ImGui.IsItemClicked())
                {
                    if (isFav)
                        _config.NearbyFavorites.Remove(p.FullKey);
                    else
                        _config.NearbyFavorites.Add(p.FullKey);
                    _save();
                    NearbyPlayersManager.InvalidateCache();
                }

                ImGui.SameLine(0, 4);

                ImGui.BeginGroup();
                {
                    var nameColor = outOfRange ? NearbyColorOutOfRange : isQueued ? NearbyColorQueuedName : isFav ? NearbyColorFavName : NearbyColorName;
                    var worldColor = outOfRange ? NearbyColorOutOfRange : NearbyColorWorld;

                    if (p.Distance < float.MaxValue)
                    {
                        var dist = Math.Min(p.Distance, 99.9f);
                        ImGui.TextColored(nameColor, $"({dist,4:F1}y)");
                    }
                    else
                    {
                        ImGui.TextColored(NearbyColorOutOfRange, "(--.-y)");
                    }

                    ImGui.SameLine(0, 4);
                    ImGui.TextColored(nameColor, p.Name);
                    ImGui.SameLine(0, 0);
                    ImGui.TextColored(worldColor, $"@{p.World}");
                }
                ImGui.EndGroup();

                if (ImGui.IsItemClicked())
                {
                    ChatCommandRouter.Send($"/tell {p.FullKey} ", _config, "NearbyTell");
                }

                if (ImGui.IsItemHovered())
                {
                    var distText = p.Distance < float.MaxValue ? $"{p.Distance:F1} yalms" : "out of range";
                    ImGui.SetTooltip($"Click to /tell {p.FullKey}\nDistance: {distText}");
                }

                ImGui.PopID();
            }

            ImGui.PopFont();
        }
        ImGui.EndChild();

        DrawDistanceCircle();
    }

}
