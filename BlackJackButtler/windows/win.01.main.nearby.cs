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
    private string _nearbyIgnoreNameInput = string.Empty;
    private string _nearbyIgnoreWorldInput = string.Empty;
    private DateTime _nearbyWorldInputBlockedUntil = DateTime.MinValue;

    internal void DrawNearbyPlayersSection(bool version2 = false)
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

        if (BJBGui.SmallButton("Cfg##nearby_settings"))
            _showNearbySettingsWindow = true;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Nearby settings (range, shape, etc.)");

        DrawNearbySettingsWindow();

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
        if (version2)
            sorted = NearbyNumberManager.ApplyAndSort(sorted);

        if (sorted.Count == 0)
        {
            ImGui.TextColored(NearbyColorWorld, "No nearby players found.");
            DrawDistanceCircle();
            return;
        }

        if (version2 && _config.NearbyShowFootNumbers)
            NearbyNumberManager.DrawFootNumbers(sorted);

        int columns = Math.Clamp(_config.NearbyColumns, 1, 5);
        float availWidth = ImGui.GetContentRegionAvail().X;
        float colWidth = availWidth / columns;
        float rowHeight = ImGui.GetTextLineHeightWithSpacing() + 2f;

        int totalItems = sorted.Count;
        int totalRows = (int)Math.Ceiling(totalItems / (double)columns);
        float availHeight = ImGui.GetContentRegionAvail().Y;
        float childHeight;
        if (availHeight > rowHeight * 4)
        {
            // Im Popout-Fenster oder wenn genug Platz: verfügbare Höhe nutzen
            childHeight = availHeight - 4f;
        }
        else
        {
            int visibleRows = Math.Clamp(totalRows, 3, 15);
            childHeight = visibleRows * rowHeight + 8f;
        }

        bool partyFull = Plugin.PartyList.Length >= 8;

        if (ImGui.BeginChild("bjb_nearby_scroll", new Vector2(availWidth, childHeight), true))
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

        var area = NearbyPlayersManager.GetArea(_config);
        if (_config.NearbyShape == NearbyShapeMode.Rectangle)
        {
            DrawDistanceRectangle(area);
            return;
        }

        var center = area.Center;
        float radius = area.Radius;
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

    private void DrawDistanceRectangle(NearbyArea area)
    {
        var halfX = area.Radius * Math.Clamp(area.AspectRatio, 0.1f, 10f);
        var halfZ = area.Radius;
        var cos = MathF.Cos(area.RotationRadians);
        var sin = MathF.Sin(area.RotationRadians);
        var localCorners = new[]
        {
            new Vector2(-halfX, -halfZ),
            new Vector2( halfX, -halfZ),
            new Vector2( halfX,  halfZ),
            new Vector2(-halfX,  halfZ),
        };

        var screenPoints = new Vector2[4];
        var visible = new bool[4];
        for (int i = 0; i < localCorners.Length; i++)
        {
            var c = localCorners[i];
            var x = c.X * cos - c.Y * sin;
            var z = c.X * sin + c.Y * cos;
            var world = new Vector3(area.Center.X + x, area.Center.Y, area.Center.Z + z);
            visible[i] = Plugin.GameGui.WorldToScreen(world, out screenPoints[i]);
        }

        var drawList = ImGui.GetBackgroundDrawList();
        var color = ImGui.GetColorU32(new Vector4(1f, 0.85f, 0f, 0.5f));
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4;
            if (visible[i] && visible[j])
                drawList.AddLine(screenPoints[i], screenPoints[j], color, 2f);
        }
    }

    private void DrawNearbySettingsWindow()
    {
        if (!_showNearbySettingsWindow) return;

        ImGui.SetNextWindowSize(new Vector2(420f, 0f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Nearby Settings###bjb_nearby_settings", ref _showNearbySettingsWindow,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse))
        {
            ImGui.End();
            return;
        }

        // Distanz-Slider (Eingabefeld-Slider-Mix)
        ImGui.TextUnformatted("Range");
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 8f);
        if (_nearbyDistSliderEditMode)
        {
            if (ImGui.InputFloat("##dist_input", ref _config.NearbyDistanceCap, 0f, 0f, "%.2f yalms",
                ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll))
            {
                _config.NearbyDistanceCap = MathF.Max(0f, _config.NearbyDistanceCap);
                _save();
                NearbyPlayersManager.InvalidateCache();
                _nearbyDistSliderEditMode = false;
            }
            if (!ImGui.IsItemActive() && !ImGui.IsItemFocused())
                _nearbyDistSliderEditMode = false;
        }
        else
        {
            float sliderVal = Math.Min(_config.NearbyDistanceCap, 99.99f);
            if (BJBGui.SliderFloat("##nearby_dist_slider", ref sliderVal, 0f, 99.99f, "%.2f yalms"))
            {
                _config.NearbyDistanceCap = sliderVal;
                _save();
                NearbyPlayersManager.InvalidateCache();
            }
            _distSliderHovered = ImGui.IsItemHovered();
            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                _nearbyDistSliderEditMode = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Range 0.00–99.99 yalms.\nDouble-click to enter any value.");
        }

        ImGui.Separator();
        if (ImGui.Checkbox("Foot numbers", ref _config.NearbyShowFootNumbers)) _save();

        ImGui.Separator();
        ImGui.TextUnformatted("Area");
        int shape = (int)_config.NearbyShape;
        ImGui.SetNextItemWidth(180f);
        if (BJBGui.Combo("Shape##nearby_shape", ref shape, "Circle\0Rectangle\0"))
        {
            _config.NearbyShape = (NearbyShapeMode)shape;
            _save();
            NearbyPlayersManager.InvalidateCache();
        }

        DrawNearbyOffsetInput("Offset X##nearby_offset_x", ref _config.NearbyOffsetX, "nearby_offset_x_reset");
        DrawNearbyOffsetInput("Offset Z##nearby_offset_z", ref _config.NearbyOffsetZ, "nearby_offset_z_reset");

        if (_config.NearbyShape == NearbyShapeMode.Rectangle)
        {
            ImGui.SetNextItemWidth(130f);
            if (BJBGui.DragFloat("Aspect ratio##nearby_rect_aspect", ref _config.NearbyRectangleAspectRatio, 0.01f, 0.1f, 10f, "%.2f"))
            {
                _config.NearbyRectangleAspectRatio = Math.Clamp(_config.NearbyRectangleAspectRatio, 0.1f, 10f);
                _save();
                NearbyPlayersManager.InvalidateCache();
            }
            ImGui.SameLine();
            if (BJBGui.SmallButton("Reset##nearby_rect_aspect_reset"))
            {
                _config.NearbyRectangleAspectRatio = 1f;
                _save();
                NearbyPlayersManager.InvalidateCache();
            }

            ImGui.SetNextItemWidth(130f);
            if (BJBGui.DragFloat("Rotation##nearby_rect_rotation", ref _config.NearbyRectangleRotation, 0.5f, -180f, 180f, "%.1f deg"))
            {
                _config.NearbyRectangleRotation = Math.Clamp(_config.NearbyRectangleRotation, -180f, 180f);
                _save();
                NearbyPlayersManager.InvalidateCache();
            }
            ImGui.SameLine();
            if (BJBGui.SmallButton("Reset##nearby_rect_rotation_reset"))
            {
                _config.NearbyRectangleRotation = 0f;
                _save();
                NearbyPlayersManager.InvalidateCache();
            }
        }

        ImGui.Separator();
        bool fixedPosition = _config.NearbyUseFixedPosition;
        if (ImGui.Checkbox("Fixed world position", ref fixedPosition))
        {
            _config.NearbyUseFixedPosition = fixedPosition;
            if (fixedPosition)
                NearbyPlayersManager.CaptureFixedCenter(_config);
            _save();
            NearbyPlayersManager.InvalidateCache();
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton("Capture##nearby_fixed_capture"))
        {
            NearbyPlayersManager.CaptureFixedCenter(_config);
            _save();
            NearbyPlayersManager.InvalidateCache();
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton("Reset##nearby_fixed_reset"))
        {
            _config.NearbyFixedCenterCaptured = false;
            _config.NearbyFixedCenterX = 0f;
            _config.NearbyFixedCenterY = 0f;
            _config.NearbyFixedCenterZ = 0f;
            _save();
            NearbyPlayersManager.InvalidateCache();
        }

        ImGui.Separator();
        DrawNearbyAutoActSettings();

        ImGui.End();
        DrawNearbyIgnoreWindow();
    }

    private void DrawNearbyOffsetInput(string label, ref float value, string resetId)
    {
        ImGui.SetNextItemWidth(130f);
        if (BJBGui.DragFloat(label, ref value, 0.05f, -100f, 100f, "%.2f"))
        {
            _save();
            NearbyPlayersManager.InvalidateCache();
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton($"Reset##{resetId}"))
        {
            value = 0f;
            _save();
            NearbyPlayersManager.InvalidateCache();
        }
    }

    private void DrawNearbyAutoActSettings()
    {
        ImGui.TextUnformatted("Auto-Acting");
        if (ImGui.Checkbox("Enable Auto-Acting", ref _config.NearbyAutoActEnabled)) _save();

        DrawCommandSelector("Command##nearby_autoact_command", ref _config.NearbyAutoActCommandName);

        ImGui.SetNextItemWidth(150f);
        if (BJBGui.DragFloat("Timeout (minutes)##nearby_autoact_timeout", ref _config.NearbyAutoActTimeoutMinutes, 1f, 1f, 1440f, "%.0f"))
        {
            _config.NearbyAutoActTimeoutMinutes = MathF.Round(Math.Clamp(_config.NearbyAutoActTimeoutMinutes, 1f, 1440f));
            _save();
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton("Reset##nearby_autoact_timeout_reset"))
        {
            _config.NearbyAutoActTimeoutMinutes = 120f;
            _save();
        }

        ImGui.TextUnformatted("Ignore list");
        ImGui.SameLine();
        if (BJBGui.SmallButton("Edit##nearby_autoact_ignore_edit"))
            _showNearbyIgnoreWindow = true;
        ImGui.TextDisabled($"{_config.NearbyAutoActIgnoreList.Count} entries");
    }

    private void DrawNearbyIgnoreWindow()
    {
        if (!_showNearbyIgnoreWindow) return;

        ImGui.SetNextWindowSize(new Vector2(460f, 420f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Nearby Auto-Act Ignore List###bjb_nearby_ignore", ref _showNearbyIgnoreWindow,
                ImGuiWindowFlags.NoCollapse))
        {
            ImGui.End();
            return;
        }

        ImGui.TextUnformatted("Add manually");
        ImGui.SetNextItemWidth(210f);
        ImGui.InputText("Player##nearby_ignore_player", ref _nearbyIgnoreNameInput, 64);
        ImGui.SameLine();

        ImGui.SetNextItemWidth(150f);
        var worldBefore = _nearbyIgnoreWorldInput;
        if (DateTime.Now < _nearbyWorldInputBlockedUntil)
            ImGui.BeginDisabled();
        if (ImGui.InputText("World##nearby_ignore_world", ref _nearbyIgnoreWorldInput, 64))
            TryAutocompleteNearbyIgnoreWorld(worldBefore);
        if (DateTime.Now < _nearbyWorldInputBlockedUntil)
            ImGui.EndDisabled();

        ImGui.SameLine();
        if (BJBGui.SmallButton("+##nearby_ignore_manual_add"))
            AddNearbyIgnoreEntry(_nearbyIgnoreNameInput, _nearbyIgnoreWorldInput, true);

        ImGui.Separator();
        ImGui.TextUnformatted("Nearby players");
        var nearby = NearbyPlayersManager.GetNearbyPlayers(_config)
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.World, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (ImGui.BeginChild("nearby_ignore_nearby_list", new Vector2(-1, 140f), true))
        {
            foreach (var p in nearby)
            {
                ImGui.PushID($"nearby_ignore_add_{p.FullKey}");
                if (BJBGui.SmallButton("+"))
                    AddNearbyIgnoreEntry(p.Name, p.World, false);
                ImGui.SameLine();
                ImGui.TextUnformatted($"{p.Name}@{p.World}");
                ImGui.PopID();
            }
        }
        ImGui.EndChild();

        ImGui.Separator();
        ImGui.TextUnformatted("Ignored");
        var entries = _config.NearbyAutoActIgnoreList
            .OrderBy(ParseIgnoreName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(ParseIgnoreWorld, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ImGui.BeginChild("nearby_ignore_entries", new Vector2(-1, 140f), true))
        {
            foreach (var entry in entries)
            {
                ImGui.PushID($"nearby_ignore_remove_{entry}");
                if (BJBGui.SmallButton("X"))
                {
                    _config.NearbyAutoActIgnoreList.RemoveAll(x => x.Equals(entry, StringComparison.OrdinalIgnoreCase));
                    _save();
                    ImGui.PopID();
                    break;
                }
                ImGui.SameLine();
                ImGui.TextUnformatted(FormatIgnoreEntry(entry));
                ImGui.PopID();
            }
        }
        ImGui.EndChild();

        ImGui.End();
    }

    private void TryAutocompleteNearbyIgnoreWorld(string previous)
    {
        if (_nearbyIgnoreWorldInput.Equals(previous, StringComparison.Ordinal))
            return;

        if (WorldNameManager.TryCompletePrefix(_nearbyIgnoreWorldInput, out var completed)
            && !completed.Equals(_nearbyIgnoreWorldInput, StringComparison.Ordinal))
        {
            _nearbyIgnoreWorldInput = completed;
            _nearbyWorldInputBlockedUntil = DateTime.Now.AddSeconds(1);
        }
    }

    private void AddNearbyIgnoreEntry(string name, string world, bool clearInputs)
    {
        var cleanName = name.Trim();
        var cleanWorld = NormalizeWorldName(world.Trim());
        if (string.IsNullOrWhiteSpace(cleanName) || string.IsNullOrWhiteSpace(cleanWorld))
            return;

        var key = $"{cleanName}@{cleanWorld}";
        if (!_config.NearbyAutoActIgnoreList.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            _config.NearbyAutoActIgnoreList.Add(key);
            _config.NearbyAutoActIgnoreList.Sort(StringComparer.OrdinalIgnoreCase);
            _save();
        }

        if (clearInputs)
        {
            _nearbyIgnoreNameInput = string.Empty;
            _nearbyIgnoreWorldInput = string.Empty;
        }
    }

    private static string NormalizeWorldName(string world)
    {
        var exact = WorldNameManager.SortedWorldNames.FirstOrDefault(w => w.Equals(world, StringComparison.OrdinalIgnoreCase));
        return exact ?? world;
    }

    private static string ParseIgnoreName(string entry)
    {
        var at = entry.IndexOf('@');
        return at < 0 ? entry : entry[..at];
    }

    private static string ParseIgnoreWorld(string entry)
    {
        var at = entry.IndexOf('@');
        return at < 0 ? string.Empty : entry[(at + 1)..];
    }

    private static string FormatIgnoreEntry(string entry)
    {
        var name = ParseIgnoreName(entry);
        var world = ParseIgnoreWorld(entry);
        return string.IsNullOrWhiteSpace(world) ? name : $"{name}@{world}";
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
