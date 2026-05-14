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

    private void DrawNearbyPlayersSection(bool version2 = false)
    {
        if (!_config.ShowNearbyPlayers) return;

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.4f, 0.8f, 1f, 1f), "NEARBY PLAYERS");
        ImGui.SameLine();

        if (JoinQueueManager.Count > 0)
        {
            ImGui.TextColored(new Vector4(1f, 0.8f, 0f, 1f), $"Queue: {JoinQueueManager.Count}");
            ImGui.SameLine();
            if (BJBGui.SmallButton("Clear##joinqueue")) JoinQueueManager.Clear();
            ImGui.SameLine();
        }

        ImGui.TextColored(NearbyColorWorld, "(click name to /tell)");
        ImGui.SameLine(ImGui.GetContentRegionAvail().X - 50f);
        if (ImGui.Checkbox("Sticky", ref _config.NearbySticky)) _save();

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (BJBGui.SliderFloat("##nearby_dist_cap", ref _config.NearbyDistanceCap, 2.0f, 100.0f, "%.1f yalms"))
        {
            _config.NearbyDistanceCap = MathF.Round(_config.NearbyDistanceCap, 1);
            _config.NearbyDistanceCap = Math.Clamp(_config.NearbyDistanceCap, 2.0f, 100.0f);
            _save();
        }
        _distSliderHovered = ImGui.IsItemHovered();

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
        if (version2)
            sorted = NearbyNumberManager.ApplyAndSort(sorted);

        if (sorted.Count == 0)
        {
            ImGui.TextColored(NearbyColorWorld, "No nearby players found.");
            DrawDistanceCircle();
            return;
        }

        if (version2)
            NearbyNumberManager.DrawFootNumbers(sorted);

        int columns = version2 ? 1 : Math.Clamp(_config.NearbyColumns, 1, 5);
        float availWidth = ImGui.GetContentRegionAvail().X;
        float colWidth = availWidth / columns;
        float rowHeight = ImGui.GetTextLineHeightWithSpacing() + 2f;

        int totalItems = sorted.Count;
        int totalRows = (int)Math.Ceiling(totalItems / (double)columns);
        int visibleRows = Math.Clamp(totalRows, 3, 15);
        float childHeight = visibleRows * rowHeight + 8f;

        NearbyPlayersManager.PauseSorting = _config.NearbySticky;

        bool partyFull = Plugin.PartyList.Length >= 8;

        if (ImGui.BeginChild("bjb_nearby_scroll", new Vector2(availWidth, childHeight), true))
        {
            ImGui.PushFont(UiBuilder.MonoFont);

            if (ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows))
                NearbyPlayersManager.PauseSorting = true;

            for (int i = 0; i < sorted.Count; i++)
            {
                var p = sorted[i];
                int col = i % columns;

                if (col > 0) ImGui.SameLine(col * colWidth);

                bool isFav = _config.NearbyFavorites.Contains(p.FullKey);
                bool isQueued = JoinQueueManager.IsQueued(p.Name, p.World);
                bool outOfRange = !isFav && !isQueued && p.Distance > _config.NearbyDistanceCap;

                ImGui.PushID($"nearby_{i}");

                if (isQueued)
                {
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
                    if (partyFull) ImGui.BeginDisabled();
                    if (BJBGui.SmallButton($"J##nearby_join_{i}"))
                        JoinQueueManager.Enqueue(p.Name, p.World);
                    if (partyFull) ImGui.EndDisabled();
                }

                ImGui.SameLine(0, 4);

                if (version2)
                {
                    ImGui.TextColored(new Vector4(1f, 0.85f, 0.25f, 1f), $"{NearbyNumberManager.GetNumber(p.FullKey),2}");
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Stable nearby number");
                    ImGui.SameLine(0, 4);
                }

                if (version2 && !string.IsNullOrWhiteSpace(_config.NearbyQuestionCommandName))
                {
                    bool canRunQuestion = !CommandExecutor.IsRunning;
                    if (!canRunQuestion) ImGui.BeginDisabled();
                    if (BJBGui.SmallButton($"?##nearby_question_{i}"))
                        ExecuteNearbyQuestionCommand(p);
                    if (!canRunQuestion) ImGui.EndDisabled();
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        ImGui.SetTooltip($"Run {_config.NearbyQuestionCommandName} for {p.FullKey}");
                    ImGui.SameLine(0, 4);
                }

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
                    var nameColor = outOfRange ? NearbyColorOutOfRange
                        : isQueued ? NearbyColorQueuedName
                        : isFav ? NearbyColorFavName
                        : NearbyColorName;
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

    private void ExecuteNearbyQuestionCommand(NearbyPlayerInfo p)
    {
        if (string.IsNullOrWhiteSpace(_config.NearbyQuestionCommandName)) return;

        var currentTarget = Plugin.TargetManager.Target;
        string previousName = currentTarget?.Name.TextValue ?? string.Empty;
        string previousWorld = currentTarget is Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter pc
            ? pc.HomeWorld.Value.Name.ToString()
            : string.Empty;

        GameEngine.TargetPlayer(p.Name, p.World);
        Task.Run(async () =>
        {
            await CommandExecutor.ExecuteGroup(_config.NearbyQuestionCommandName, p.Name, _config);
            if (!string.IsNullOrWhiteSpace(previousName))
                GameEngine.TargetPlayer(previousName, previousWorld);
        });
    }

    private void DrawDistanceCircle()
    {
        if (!_distSliderHovered && !(_config.NearbyAlwaysShowCircle && IsRecognitionActive)) return;
        var local = Plugin.ObjectTable.LocalPlayer;
        if (local == null) return;

        var center = local.Position;
        float radius = _config.NearbyDistanceCap;
        const int segments = 64;

        var worldPoints = new Vector3[segments];
        var screenPoints = new Vector2[segments];
        var visible = new bool[segments];

        for (int i = 0; i < segments; i++)
        {
            float angle = 2f * MathF.PI * i / segments;
            worldPoints[i] = new Vector3(
                center.X + radius * MathF.Cos(angle),
                center.Y,
                center.Z + radius * MathF.Sin(angle));
            visible[i] = Plugin.GameGui.WorldToScreen(worldPoints[i], out screenPoints[i]);
        }

        var drawList = ImGui.GetBackgroundDrawList();
        var color = ImGui.GetColorU32(new Vector4(1f, 0.85f, 0f, 0.5f));

        var stroke = new List<Vector2>();

        for (int i = 0; i < segments; i++)
        {
            int j = (i + 1) % segments;

            if (visible[i] && visible[j])
            {
                if (stroke.Count == 0) stroke.Add(screenPoints[i]);
                stroke.Add(screenPoints[j]);
            }
            else if (!visible[i] && !visible[j])
            {
                FlushStroke(drawList, stroke, color);
            }
            else if (visible[i] && !visible[j])
            {
                if (stroke.Count == 0) stroke.Add(screenPoints[i]);
                var edge = FindEdgePoint(worldPoints[i], worldPoints[j]);
                if (edge.HasValue) stroke.Add(edge.Value);
                FlushStroke(drawList, stroke, color);
            }
            else
            {
                FlushStroke(drawList, stroke, color);
                var edge = FindEdgePoint(worldPoints[j], worldPoints[i]);
                if (edge.HasValue) stroke.Add(edge.Value);
                stroke.Add(screenPoints[j]);
            }
        }

        FlushStroke(drawList, stroke, color);
    }

    private static Vector2? FindEdgePoint(Vector3 visibleWorld, Vector3 invisibleWorld)
    {
        var a = visibleWorld;
        var b = invisibleWorld;
        for (int k = 0; k < 6; k++)
        {
            var mid = (a + b) * 0.5f;
            if (Plugin.GameGui.WorldToScreen(mid, out _))
                a = mid;
            else
                b = mid;
        }
        return Plugin.GameGui.WorldToScreen(a, out var result) ? result : null;
    }

    private static void FlushStroke(ImDrawListPtr drawList, List<Vector2> stroke, uint color)
    {
        if (stroke.Count >= 2)
        {
            for (int i = 0; i < stroke.Count - 1; i++)
                drawList.AddLine(stroke[i], stroke[i + 1], color, 2f);
        }
        stroke.Clear();
    }
}
