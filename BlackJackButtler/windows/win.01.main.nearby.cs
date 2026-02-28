using System;
using System.Numerics;
using System.Linq;
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

    private void DrawNearbyPlayersSection()
    {
        if (!_config.ShowNearbyPlayers) return;

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.4f, 0.8f, 1f, 1f), "NEARBY PLAYERS");
        ImGui.SameLine();
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

        var allPlayers = NearbyPlayersManager.GetNearbyPlayers(_config);
        if (allPlayers.Count == 0)
        {
            ImGui.TextColored(NearbyColorWorld, "No nearby players found.");
            return;
        }

        int columns = Math.Clamp(_config.NearbyColumns, 1, 5);
        float availWidth = ImGui.GetContentRegionAvail().X;
        float colWidth = availWidth / columns;
        float rowHeight = ImGui.GetTextLineHeightWithSpacing() + 2f;

        int totalItems = allPlayers.Count;
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

            for (int i = 0; i < allPlayers.Count; i++)
            {
                var p = allPlayers[i];
                int col = i % columns;

                if (col > 0) ImGui.SameLine(col * colWidth);

                bool isFav = _config.NearbyFavorites.Contains(p.FullKey);
                bool outOfRange = !isFav && p.Distance > _config.NearbyDistanceCap;

                ImGui.PushID($"nearby_{i}");

                if (partyFull) ImGui.BeginDisabled();
                if (BJBGui.SmallButton($"J##nearby_join_{i}"))
                {
                    ChatCommandRouter.Send($"/pcmd add {p.Name}", _config, "NearbyJoin");
                }
                if (partyFull) ImGui.EndDisabled();

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
                    var nameColor = outOfRange ? NearbyColorOutOfRange : (isFav ? NearbyColorFavName : NearbyColorName);
                    var worldColor = outOfRange ? NearbyColorOutOfRange : NearbyColorWorld;

                    var dist = Math.Min(p.Distance, 99.9f);
                    ImGui.TextColored(nameColor, $"({dist,4:F1}y)");
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
                    ImGui.SetTooltip($"Click to /tell {p.FullKey}\nDistance: {p.Distance:F1} yalms");
                }

                ImGui.PopID();
            }

            ImGui.PopFont();
        }
        ImGui.EndChild();
    }
}
