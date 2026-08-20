using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private string _filterMessages = string.Empty;
    private readonly Dictionary<MessageBatch, MessageBatchTextEditState> _messageTextEditors = new();
    private MessageBatch? _messageTextBackBatch;
    private bool _messageTextBackPopupOpen;

    private sealed class MessageBatchTextEditState
    {
        public string Original = string.Empty;
        public string Draft = string.Empty;
    }

    private void DrawMessagesPage()
    {
        ImGui.TextUnformatted("Message Batches");
        ImGui.SameLine();
        if (BJBGui.SmallButton("?##varref_msg")) _showVarRefPanel = !_showVarRefPanel;
        ImGui.Separator();

        var io = ImGui.GetIO();

        if (ImGui.BeginTabBar("##message_batch_tabs"))
        {
            if (ImGui.BeginTabItem("Standard"))
            {
                BJBGui.DrawFilterBar("messages_standard", ref _filterMessages, "Search batch name or message...");
                ImGui.SameLine();
                DrawRestoreDefaultMessagesButton(io.KeyCtrl && io.KeyShift);
                ImGui.Spacing();
                DrawMessageBatches(standard: true, io.KeyCtrl);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Custom"))
            {
                BJBGui.DrawFilterBar("messages_custom", ref _filterMessages, "Search batch name or message...");
                ImGui.SameLine();
                if (BJBGui.Button("+ New Batch"))
                {
                    _config.MessageBatches.Add(new MessageBatch());
                    _save();
                }
                ImGui.Spacing();
                DrawMessageBatches(standard: false, io.KeyCtrl);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        DrawRestoreDefaultMessagesPopup();

        if (_messageTextBackPopupOpen && _messageTextBackBatch != null)
            ImGui.OpenPopup("bjb_message_text_unsaved");
        DrawMessageTextBackPopup();
    }

    private void DrawRestoreDefaultMessagesButton(bool unlocked)
    {
        if (!unlocked) ImGui.BeginDisabled();
        if (unlocked) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0f, 0f, 1f));

        if (BJBGui.Button("Restore default messages (Hard reset)##hard_reset"))
        {
            _openForceDefaultsPopup = true;
            ImGui.OpenPopup("bjb.restore.confirm");
        }

        if (unlocked) ImGui.PopStyleColor();
        if (!unlocked)
        {
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("Hold CTRL + SHIFT to unlock this button.");
        }
    }

    private void DrawRestoreDefaultMessagesPopup()
    {
        if (!ImGui.BeginPopupModal("bjb.restore.confirm", ref _openForceDefaultsPopup,
                ImGuiWindowFlags.AlwaysAutoResize))
            return;

        ImGui.TextColored(new Vector4(1, 0, 0, 1), "WARNING: HARD RESET");
        ImGui.TextUnformatted("This will delete all standard messages and commands and recreate them.");
        ImGui.TextUnformatted("Choose which defaults pack to restore:");
        ImGui.Spacing();

        if (BJBGui.Button("Use New Defaults (recommended)", new Vector2(260, 0)))
        {
            DefaultsMigration.SeedAllDefaultsFromV2(_config);
            _save();
            _openForceDefaultsPopup = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (BJBGui.Button("Use Old Defaults", new Vector2(160, 0)))
        {
            DefaultsMigration.SeedAllDefaults(_config);
            _save();
            _openForceDefaultsPopup = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (BJBGui.Button("Cancel", new Vector2(120, 0)))
        {
            _openForceDefaultsPopup = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    private void DrawMessageBatches(bool standard, bool ctrlHeld)
    {
        for (var i = 0; i < _config.MessageBatches.Count; i++)
        {
            var batch = _config.MessageBatches[i];
            var isStandard = IsStandardBatch(batch.Name);
            if (isStandard != standard || !BJBGui.MatchesFilter(_filterMessages, batch.Name, batch.Messages))
                continue;

            ImGui.PushID($"batch_{i}");
            if (isStandard) ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.1f, 0.3f, 0.1f, 1f));

            var modeName = batch.Mode switch
            {
                SelectionMode.Random => "Random",
                SelectionMode.First => "First",
                SelectionMode.Iterative => "Iterative",
                _ => "Unknown",
            };

            if (!string.IsNullOrEmpty(_filterMessages))
                ImGui.SetNextItemOpen(true, ImGuiCond.Always);

            var open = ImGui.CollapsingHeader($"{(isStandard ? "● " : "")}{batch.Name} [{modeName}]###batch_header");
            if (isStandard) ImGui.PopStyleColor();

            if (open)
            {
                if (!isStandard)
                {
                    var name = batch.Name;
                    if (ImGui.InputText("Batch Name", ref name, 64))
                    {
                        batch.Name = name;
                        _save();
                    }
                }

                if (_messageTextEditors.TryGetValue(batch, out var textEditor))
                    DrawMessageTextEditor(batch, textEditor);
                else
                    DrawMessageListEditor(batch, isStandard, ctrlHeld, i);
            }

            ImGui.PopID();
            ImGui.Spacing();
        }
    }

    private void DrawMessageListEditor(MessageBatch batch, bool isStandard, bool ctrlHeld, int batchIndex)
    {
        for (var messageIndex = 0; messageIndex < batch.Messages.Count; messageIndex++)
        {
            var message = batch.Messages[messageIndex];
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 35f);
            if (ImGui.InputText($"##msg_{messageIndex}", ref message, 256))
            {
                batch.Messages[messageIndex] = message;
                _save();
            }
            ImGui.SameLine();
            if (BJBGui.Button($"X##msg_{messageIndex}"))
            {
                batch.Messages.RemoveAt(messageIndex);
                if (messageIndex < batch.ADFlags.Count) batch.ADFlags.RemoveAt(messageIndex);
                _save();
                break;
            }
        }

        if (BJBGui.Button("+ Line"))
        {
            batch.Messages.Add(string.Empty);
            _save();
        }
        ImGui.SameLine();
        ImGui.TextUnformatted("Mode:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120f);
        var mode = (int)batch.Mode;
        if (BJBGui.Combo("##mode", ref mode, "Random\0First\0Iterative\0"))
        {
            batch.Mode = (SelectionMode)mode;
            _save();
        }

        ImGui.SameLine();
        if (BJBGui.Button("Text Edit Mode"))
            _messageTextEditors[batch] = new MessageBatchTextEditState
            {
                Original = BuildMessageText(batch),
                Draft = BuildMessageText(batch),
            };

        if (isStandard)
            return;

        ImGui.Spacing();
        ImGui.Separator();
        if (ctrlHeld)
        {
            if (BJBGui.Button("Delete (Hold CTRL)", new Vector2(-1f, 0f)))
            {
                _messageTextEditors.Remove(batch);
                _config.MessageBatches.RemoveAt(batchIndex);
                _save();
            }
        }
        else
        {
            ImGui.BeginDisabled();
            BJBGui.Button("Delete (Hold CTRL)", new Vector2(-1f, 0f));
            ImGui.EndDisabled();
        }
    }

    private void DrawMessageTextEditor(MessageBatch batch, MessageBatchTextEditState state)
    {
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextMultiline("##message_text_editor", ref state.Draft, 16_384, new Vector2(-1f, 220f));
        ImGui.TextDisabled("Prefix a line with [AD] to enable Anti-Double. Use \\[AD] for literal text.");

        if (BJBGui.Button("Save"))
            SaveMessageTextEditor(batch, state);
        ImGui.SameLine();
        if (BJBGui.Button("List Edit Mode"))
        {
            if (string.Equals(state.Original, state.Draft, StringComparison.Ordinal))
            {
                _messageTextEditors.Remove(batch);
            }
            else
            {
                _messageTextBackBatch = batch;
                _messageTextBackPopupOpen = true;
            }
        }
    }

    private void DrawMessageTextBackPopup()
    {
        var batch = _messageTextBackBatch;
        if (!_messageTextBackPopupOpen || batch == null)
            return;

        if (!ImGui.BeginPopupModal("bjb_message_text_unsaved", ref _messageTextBackPopupOpen, ImGuiWindowFlags.AlwaysAutoResize))
            return;

        ImGui.TextUnformatted("Save changes to this message batch?");
        if (BJBGui.Button("Yes"))
        {
            if (_messageTextEditors.TryGetValue(batch, out var state))
                SaveMessageTextEditor(batch, state);
            _messageTextEditors.Remove(batch);
            _messageTextBackBatch = null;
            _messageTextBackPopupOpen = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (BJBGui.Button("No"))
        {
            _messageTextEditors.Remove(batch);
            _messageTextBackBatch = null;
            _messageTextBackPopupOpen = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (BJBGui.Button("Cancel"))
        {
            _messageTextBackPopupOpen = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    private void SaveMessageTextEditor(MessageBatch batch, MessageBatchTextEditState state)
    {
        var messages = new List<string>();
        var adFlags = new List<bool>();
        foreach (var rawLine in state.Draft.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
                continue;

            var line = rawLine;
            var antiDouble = false;
            if (line.StartsWith("\\[AD]", StringComparison.OrdinalIgnoreCase))
                line = line[1..];
            else if (line.StartsWith("[AD]", StringComparison.OrdinalIgnoreCase))
            {
                antiDouble = true;
                line = line[4..];
            }

            if (string.IsNullOrWhiteSpace(line))
                continue;

            messages.Add(line);
            adFlags.Add(antiDouble);
        }

        batch.Messages = messages;
        batch.ADFlags = adFlags;
        state.Draft = BuildMessageText(batch);
        state.Original = state.Draft;
        _save();
    }

    private static string BuildMessageText(MessageBatch batch)
    {
        var lines = new List<string>(batch.Messages.Count);
        for (var index = 0; index < batch.Messages.Count; index++)
        {
            var line = batch.Messages[index] ?? string.Empty;
            if (batch.GetAD(index))
                lines.Add("[AD]" + line);
            else if (line.StartsWith("[AD]", StringComparison.OrdinalIgnoreCase))
                lines.Add("\\" + line);
            else
                lines.Add(line);
        }
        return string.Join("\n", lines);
    }

    private bool IsStandardBatch(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return Configuration.StandardBatchNames.Contains(name, StringComparer.OrdinalIgnoreCase);
    }
}
