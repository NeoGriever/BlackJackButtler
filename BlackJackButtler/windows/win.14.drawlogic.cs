using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private bool _showDrawLogicDoc = false;
    private string _newDrawLogicName = "";
    private string _lastDlScriptHash = "";
    private readonly Dictionary<int, bool> _dlEditOpen = new();
    private readonly Dictionary<int, string> _dlEditBuffer = new();
    private readonly Dictionary<int, string> _dlEditStatus = new();

    private void EnsureDrawLogicSeed()
    {
        if (_config.DrawLogicSeeded) return;
        _config.DrawLogicSeeded = true;
        if (_config.DrawLogicEntries.Count == 0)
        {
            var path = DrawLogicScriptManager.CreateDefaultScript();
            _config.DrawLogicEntries.Add(new DrawLogicEntry
            {
                Name = "Visualize Cards",
                IsIterate = true,
                IsActive = true,
                ScriptPath = path,
            });
            _config.DrawLogicStartEntry = "Visualize Cards";
        }
        _save();
    }

    private void DrawDrawLogicPage()
    {
        EnsureDrawLogicSeed();

        ImGui.TextUnformatted("Draw Logic");
        ImGui.SameLine();
        if (BJBGui.SmallButton("?##drawlogic_doc")) _showDrawLogicDoc = !_showDrawLogicDoc;
        ImGui.SameLine();
        if (BJBGui.SmallButton("Debug##drawlogic_dbg")) Plugin.Instance.OpenDrawLogicDebug();
        ImGui.Separator();
        // ImGui.TextDisabled("Scriptable world-drawing system. Define draw commands that execute per frame.");
        // ImGui.TextDisabled("When 'Iterate' is on, the script runs once per active player + dealer.");
        ImGui.Spacing();

        var entries = _config.DrawLogicEntries;
        var names = new List<string> { "(None)" };
        names.AddRange(entries.Select(e => e.Name));

        int currentIdx = string.IsNullOrEmpty(_config.DrawLogicStartEntry)
            ? 0
            : names.IndexOf(_config.DrawLogicStartEntry);
        if (currentIdx < 0) currentIdx = 0;

        ImGui.SetNextItemWidth(300f);
        if (BJBGui.Combo("Start Entry##drawlogic_start", ref currentIdx,
            string.Join('\0', names) + '\0'))
        {
            _config.DrawLogicStartEntry = currentIdx == 0 ? "" : names[currentIdx];
            _save();
        }

        ImGui.SameLine();
        ImGui.Checkbox("Debug Mode##dl_debug_toggle", ref DrawLogicDebugManager.IsActive);

        if (DrawLogicDebugManager.IsActive)
            DrawDebugHandsPanel();

        ImGui.Spacing();

        ImGui.SetNextItemWidth(300f);
        ImGui.InputText("##new_drawlogic_name", ref _newDrawLogicName, 64);
        ImGui.SameLine();
        bool canAdd = !string.IsNullOrWhiteSpace(_newDrawLogicName)
            && !entries.Any(e => e.Name.Equals(_newDrawLogicName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!canAdd) ImGui.BeginDisabled();
        if (BJBGui.Button("+ New Entry"))
        {
            var newPath = DrawLogicScriptManager.CreateNewFile(entries.Count);
            entries.Add(new DrawLogicEntry { Name = _newDrawLogicName.Trim(), ScriptPath = newPath });
            _newDrawLogicName = "";
            _save();
        }
        if (!canAdd) ImGui.EndDisabled();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        int toRemove = -1;
        int swapA = -1, swapB = -1;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            ImGui.PushID($"drawlogic_{i}");

            bool open = ImGui.CollapsingHeader($"{entry.Name}###dl_{i}", ImGuiTreeNodeFlags.DefaultOpen);

            if (open)
            {
                var name = entry.Name;
                ImGui.SetNextItemWidth(300f);
                if (ImGui.InputText("Name##dl_name", ref name, 64))
                {
                    var trimmed = name.Trim();
                    bool nameValid = !string.IsNullOrWhiteSpace(trimmed)
                        && !entries.Where((e, idx) => idx != i)
                            .Any(e => e.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
                    if (nameValid)
                    {
                        if (_config.DrawLogicStartEntry == entry.Name)
                            _config.DrawLogicStartEntry = trimmed;
                        entry.Name = trimmed;
                        _save();
                    }
                }

                var iterate = entry.IsIterate;
                if (ImGui.Checkbox("Iterate (run per player)##dl_iter", ref iterate))
                {
                    entry.IsIterate = iterate;
                    _save();
                }

                ImGui.SameLine();
                var active = entry.IsActive;
                if (ImGui.Checkbox("Active##dl_active", ref active))
                {
                    entry.IsActive = active;
                    _save();
                }

                var scriptPath = entry.ScriptPath;
                ImGui.SetNextItemWidth(300f);
                ImGui.InputText($"##dl_path_{i}", ref scriptPath, 256, ImGuiInputTextFlags.ReadOnly);
                ImGui.SameLine();
                if (BJBGui.SmallButton($"...##dl_browse_{i}"))
                    ImGui.OpenPopup($"dl_pathpopup_{i}");
                ImGui.SameLine();
                if (BJBGui.SmallButton($"Reload##dl_reload_{i}"))
                {
                    entry.Script = DrawLogicScriptManager.ReloadScript(entry.ScriptPath);
                }
                ImGui.SameLine();
                var ctrlForDefault = ImGui.GetIO().KeyCtrl;
                if (!ctrlForDefault) ImGui.BeginDisabled();
                if (BJBGui.SmallButton($"Create Default##dl_default_{i}"))
                {
                    var defaultPath = DrawLogicScriptManager.CreateDefaultScript();
                    entry.ScriptPath = defaultPath;
                    entry.Script = DrawLogicScriptManager.ReloadScript(defaultPath);
                    _save();
                }
                if (!ctrlForDefault)
                {
                    ImGui.EndDisabled();
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        ImGui.SetTooltip("Hold CTRL to create/overwrite default script.");
                }

                ImGui.SameLine();
                bool autoReloadAvail = DrawLogicDebugManager.IsActive;
                if (!autoReloadAvail) ImGui.BeginDisabled();
                var autoReload = entry.AutoReload;
                if (ImGui.Checkbox($"Auto-Reload##dl_autoreload_{i}", ref autoReload))
                {
                    if (autoReload)
                    {
                        foreach (var other in entries) other.AutoReload = false;
                        entry.AutoReload = true;
                        DrawLogicScriptManager.SetAutoReload(entry.ScriptPath);
                    }
                    else
                    {
                        entry.AutoReload = false;
                        DrawLogicScriptManager.ClearAutoReload();
                    }
                    _save();
                }
                if (!autoReloadAvail)
                {
                    ImGui.EndDisabled();
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        ImGui.SetTooltip("Enable Debug Mode to use Auto-Reload.");
                }

                if (ImGui.BeginPopup($"dl_pathpopup_{i}"))
                {
                    ImGui.TextUnformatted("Script Path (relative):");
                    var editPath = entry.ScriptPath;
                    ImGui.SetNextItemWidth(300f);
                    if (ImGui.InputText($"##dl_pathedit_{i}", ref editPath, 256, ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        entry.ScriptPath = editPath.Trim();
                        _save();
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }

                bool editOpen = ImGui.CollapsingHeader($"Edit##dl_edit_hdr_{i}");
                bool wasEditOpen = _dlEditOpen.GetValueOrDefault(i, false);
                if (editOpen && !wasEditOpen)
                {
                    var loaded = string.IsNullOrEmpty(entry.ScriptPath)
                        ? entry.Script ?? string.Empty
                        : DrawLogicScriptManager.ReadScript(entry.ScriptPath) ?? string.Empty;
                    _dlEditBuffer[i] = loaded;
                    _dlEditStatus[i] = string.Empty;
                }
                else if (!editOpen && wasEditOpen)
                {
                    _dlEditBuffer.Remove(i);
                    _dlEditStatus.Remove(i);
                }
                _dlEditOpen[i] = editOpen;

                if (editOpen)
                {
                    var buf = _dlEditBuffer.GetValueOrDefault(i, string.Empty);
                    if (ImGui.InputTextMultiline($"##dl_edit_text_{i}", ref buf, 65536, new Vector2(-1, 280)))
                        _dlEditBuffer[i] = buf;

                    bool hasPath = !string.IsNullOrEmpty(entry.ScriptPath);
                    string saveLabel = hasPath ? $"Save to file##dl_save_{i}" : $"Save As...##dl_save_{i}";
                    if (BJBGui.Button(saveLabel))
                    {
                        if (hasPath)
                        {
                            try
                            {
                                DrawLogicScriptManager.WriteScript(entry.ScriptPath, _dlEditBuffer.GetValueOrDefault(i, string.Empty));
                                entry.Script = _dlEditBuffer.GetValueOrDefault(i, string.Empty);
                                _dlEditStatus[i] = $"Saved to {entry.ScriptPath} at {DateTime.Now:HH:mm:ss}";
                                _save();
                            }
                            catch (Exception ex)
                            {
                                _dlEditStatus[i] = $"Save failed: {ex.Message}";
                            }
                        }
                        else
                        {
                            int captured = i;
                            var baseDir = DrawLogicScriptManager.GetBaseDir();
                            _fileDialogManager.SaveFileDialog(
                                "Save DrawLogic Script",
                                "Text Files{.txt}",
                                $"new_script_{captured}.txt",
                                ".txt",
                                (success, fullPath) =>
                                {
                                    if (!success || string.IsNullOrEmpty(fullPath)) return;
                                    try
                                    {
                                        string relPath;
                                        if (!string.IsNullOrEmpty(baseDir) && fullPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
                                            relPath = Path.GetRelativePath(baseDir, fullPath).Replace('\\', '/');
                                        else
                                            relPath = fullPath;

                                        File.WriteAllText(fullPath, _dlEditBuffer.GetValueOrDefault(captured, string.Empty));
                                        entry.ScriptPath = relPath;
                                        entry.Script = _dlEditBuffer.GetValueOrDefault(captured, string.Empty);
                                        _dlEditStatus[captured] = $"Saved as {relPath}";
                                        _save();
                                    }
                                    catch (Exception ex)
                                    {
                                        _dlEditStatus[captured] = $"Save failed: {ex.Message}";
                                    }
                                },
                                baseDir);
                        }
                    }
                    ImGui.SameLine();
                    if (BJBGui.SmallButton($"Reload from file##dl_edit_reload_{i}"))
                    {
                        if (!string.IsNullOrEmpty(entry.ScriptPath))
                            _dlEditBuffer[i] = DrawLogicScriptManager.ReloadScript(entry.ScriptPath) ?? string.Empty;
                    }

                    var status = _dlEditStatus.GetValueOrDefault(i, string.Empty);
                    if (!string.IsNullOrEmpty(status))
                    {
                        ImGui.SameLine();
                        ImGui.TextColored(new Vector4(0.6f, 0.9f, 0.6f, 1f), status);
                    }
                }

                bool isFirst = i == 0;
                bool isLast = i == entries.Count - 1;

                if (isFirst) ImGui.BeginDisabled();
                if (BJBGui.SmallButton("Up##dl_up")) { swapA = i - 1; swapB = i; }
                if (isFirst) ImGui.EndDisabled();

                ImGui.SameLine();
                if (isLast) ImGui.BeginDisabled();
                if (BJBGui.SmallButton("Down##dl_down")) { swapA = i; swapB = i + 1; }
                if (isLast) ImGui.EndDisabled();

                ImGui.SameLine();
                var io = ImGui.GetIO();
                bool ctrlHeld = io.KeyCtrl;
                if (!ctrlHeld) ImGui.BeginDisabled();
                if (ctrlHeld) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0f, 0f, 1f));
                if (BJBGui.SmallButton("Delete##dl_del")) toRemove = i;
                if (ctrlHeld) ImGui.PopStyleColor();
                if (!ctrlHeld)
                {
                    ImGui.EndDisabled();
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        ImGui.SetTooltip("Hold CTRL to delete.");
                }
            }

            ImGui.PopID();
            ImGui.Spacing();
        }

        if (swapA >= 0 && swapB >= 0 && swapB < entries.Count)
        {
            (entries[swapA], entries[swapB]) = (entries[swapB], entries[swapA]);
            _save();
        }

        if (toRemove >= 0)
        {
            var deletedEntry = entries[toRemove];
            DrawLogicScriptManager.SoftDeleteFile(deletedEntry.ScriptPath);
            entries.RemoveAt(toRemove);
            if (_config.DrawLogicStartEntry == deletedEntry.Name)
                _config.DrawLogicStartEntry = "";
            _save();
        }
    }

    private void ExecuteDrawLogic()
    {
        if (string.IsNullOrEmpty(_config.DrawLogicStartEntry)) return;
        if (_config.DrawLogicEntries.Count == 0) return;

        var players = _players;
        var dealer = _dealer;

        if (DrawLogicDebugManager.IsActive)
        {
            var (vPlayers, vDealer) = DrawLogicDebugManager.BuildVirtualPlayers();
            players = vPlayers;
            dealer = vDealer;
        }
        else if (GameEngine.CurrentPhase == GamePhase.Waiting)
        {
            players = players.Select(p => { var c = p.Clone(); c.Hands.Clear(); return c; }).ToList();
            if (dealer != null)
            {
                dealer = dealer.Clone();
                dealer.Hands.Clear();
            }
        }

        if (dealer == null)
            return;

        foreach (var e in _config.DrawLogicEntries)
        {
            if (!string.IsNullOrEmpty(e.ScriptPath))
            {
                if (e.AutoReload)
                    DrawLogicScriptManager.CheckAndApplyFileChange(e);
                else
                    e.Script = DrawLogicScriptManager.ReadScript(e.ScriptPath);
            }
        }

        var currentHash = string.Join("|", _config.DrawLogicEntries.Select(e => e.Script?.Length.ToString() ?? ""));
        if (currentHash != _lastDlScriptHash)
        {
            if (!string.IsNullOrEmpty(_lastDlScriptHash))
                DrawLogicInterpreter.TriggerDebugCapture();
            _lastDlScriptHash = currentHash;
        }

        try
        {
            DrawLogicInterpreter.ExecuteStartEntry(
                _config.DrawLogicEntries, _config.DrawLogicStartEntry,
                players, dealer, _config);

            foreach (var e in _config.DrawLogicEntries)
                DrawLogicDebugManager.ValidScriptCache[e.Name] = e.Script;
        }
        catch
        {
            var fallback = _config.DrawLogicEntries.Select(e =>
            {
                if (DrawLogicDebugManager.ValidScriptCache.TryGetValue(e.Name, out var cached))
                    return new DrawLogicEntry { Name = e.Name, Script = cached, IsIterate = e.IsIterate, IsActive = e.IsActive };
                return new DrawLogicEntry { Name = e.Name, Script = "", IsIterate = e.IsIterate, IsActive = e.IsActive };
            }).ToList();

            try
            {
                DrawLogicInterpreter.ExecuteStartEntry(
                    fallback, _config.DrawLogicStartEntry,
                    players, dealer, _config);
            }
            catch { }
        }
    }

    private void DrawDebugHandsPanel()
    {
        ImGui.BeginChild("dl_debug_hands", new Vector2(-1, 250), true);

        for (int h = 0; h < DrawLogicDebugManager.DebugHands.Count; h++)
        {
            var hand = DrawLogicDebugManager.DebugHands[h];
            ImGui.PushID($"dbg_hand_{h}");

            ImGui.TextUnformatted($"Hand {h + 1}");
            ImGui.SameLine();
            if (BJBGui.SmallButton("+ Card##dbg_add"))
                DrawLogicDebugManager.AddRandomCard(hand);

            if (DrawLogicDebugManager.DebugHands.Count > 1)
            {
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.15f, 0.15f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.2f, 0.2f, 1.0f));
                if (BJBGui.SmallButton("X##dbg_del_hand"))
                {
                    DrawLogicDebugManager.DebugHands.RemoveAt(h);
                    ImGui.PopStyleColor(2);
                    ImGui.PopID();
                    break;
                }
                ImGui.PopStyleColor(2);
            }

            int removeCard = -1;
            for (int c = 0; c < hand.Cards.Count; c++)
            {
                var card = hand.Cards[c];
                Vector4 color = (card.Suit == CardSuit.Diamonds || card.Suit == CardSuit.Hearts)
                    ? new Vector4(1, 0.3f, 0.3f, 1)
                    : new Vector4(0.9f, 0.9f, 0.9f, 1);
                ImGui.TextColored(color, card.ToString());
                ImGui.SameLine(0, 2);

                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.5f, 0.1f, 0.1f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.7f, 0.15f, 0.15f, 1.0f));
                if (BJBGui.SmallButton($"X##dbg_del_c{c}"))
                    removeCard = c;
                ImGui.PopStyleColor(2);
                ImGui.SameLine(0, 8);
            }
            if (hand.Cards.Count > 0) ImGui.NewLine();

            if (removeCard >= 0)
                hand.Cards.RemoveAt(removeCard);

            DrawDebugHandScore(hand);

            ImGui.PopID();
            ImGui.Spacing();
        }

        if (BJBGui.SmallButton("+ Add Hand##dbg_add_hand"))
            DrawLogicDebugManager.DebugHands.Add(new DebugHand());

        ImGui.EndChild();
    }

    private static void DrawDebugHandScore(DebugHand hand)
    {
        if (hand.Cards.Count == 0) return;

        int total = 0, aces = 0;
        foreach (var c in hand.Cards)
        {
            if (c.Value == 1) { total += 1; aces++; }
            else if (c.Value >= 10) total += 10;
            else total += c.Value;
        }
        int min = total;
        int? max = (aces > 0 && total + 10 <= 21) ? total + 10 : null;
        int best = (max.HasValue && max.Value <= 21) ? max.Value : min;

        ImGui.Text("Score:");
        ImGui.SameLine();

        if (best > 21)
            ImGui.TextColored(new Vector4(1.0f, 0.2f, 0.2f, 1.0f), $"BUST ({best})");
        else if (best == 21 && hand.Cards.Count == 2)
            ImGui.TextColored(new Vector4(0.0f, 1.0f, 0.0f, 1.0f), "BJ (Natural)");
        else if (best == 21)
            ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.0f, 1.0f), "21");
        else if (max.HasValue)
            ImGui.Text($"{min}/{max.Value}");
        else
            ImGui.Text($"{min}");
    }
}
