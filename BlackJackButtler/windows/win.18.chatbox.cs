using System;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface.Windowing;
using BlackJackButtler.Chat;

namespace BlackJackButtler.Windows;

public class ChatBoxWindow : Window
{
    private readonly ChatLogBuffer _chatLog;
    private readonly Configuration _config;

    private string _input = string.Empty;
    private bool _autoScroll = true;
    private int _lastSeenCount = 0;
    private bool _focusInputNext = false;

    private static readonly Vector4 DiceHighlight = new(1.0f, 0.85f, 0.30f, 1.0f);
    private static readonly Vector4 MetaDim = new(0.55f, 0.55f, 0.55f, 1.0f);
    private static readonly Vector4 TextDefault = new(0.95f, 0.95f, 0.95f, 1.0f);

    public ChatBoxWindow(ChatLogBuffer chatLog, Configuration config) : base("BJB Messenger###bjb_chatbox")
    {
        _chatLog = chatLog;
        _config = config;
        Size = new Vector2(420, 520);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(280, 240),
            MaximumSize = new Vector2(1600, 1600),
        };
    }

    public override void Draw()
    {
        if (BJBGui.SmallButton("Clear##bjb_chat_clear")) _chatLog.Clear();
        ImGui.SameLine();
        if (BJBGui.SmallButton("Copy##bjb_chat_copy")) CopyVisibleToClipboard();
        ImGui.SameLine();
        ImGui.Checkbox("Auto-Scroll##bjb_chat_auto", ref _autoScroll);
        ImGui.SameLine();
        ImGui.TextDisabled($"Buffer: {_chatLog.Snapshot().Count}");

        ImGui.Separator();

        var inputBarHeight = ImGui.GetFrameHeightWithSpacing() + 4f;
        if (ImGui.BeginChild("bjb_chat_scroll", new Vector2(-1, -inputBarHeight), true))
        {
            var snapshot = _chatLog.Snapshot();
            var visible = snapshot.Where(m => ChatLogBuffer.IsPartyChatType(m.ChatType) || m.IsDice).ToList();

            foreach (var m in visible)
            {
                ImGui.TextColored(MetaDim, $"[{m.Timestamp:HH:mm}]");
                ImGui.SameLine();
                if (m.GroupIndexNumber > 0)
                {
                    ImGui.TextColored(MetaDim, $"[{m.GroupIndexNumber}]");
                    ImGui.SameLine();
                }

                var nameText = string.IsNullOrEmpty(m.Name) ? "?" : m.Name;
                ImGui.PushStyleColor(ImGuiCol.Text, m.ColorU32);
                ImGui.TextUnformatted($"{nameText}:");
                ImGui.PopStyleColor();
                ImGui.SameLine();

                if (m.IsDice)
                    ImGui.TextColored(DiceHighlight, m.Message);
                else
                    ImGui.TextColored(TextDefault, m.Message);
            }

            if (_autoScroll && visible.Count != _lastSeenCount)
            {
                if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 20f || _lastSeenCount == 0)
                    ImGui.SetScrollHereY(1.0f);
                _lastSeenCount = visible.Count;
            }
        }
        ImGui.EndChild();

        if (_focusInputNext)
        {
            ImGui.SetKeyboardFocusHere();
            _focusInputNext = false;
        }

        var sendWidth = 70f;
        var inputWidth = ImGui.GetContentRegionAvail().X - sendWidth - 8f;
        ImGui.SetNextItemWidth(inputWidth);
        var submitted = ImGui.InputText(
            "##bjb_chat_input",
            ref _input,
            500,
            ImGuiInputTextFlags.EnterReturnsTrue
        );
        ImGui.SameLine();
        var clicked = BJBGui.Button("Send##bjb_chat_send");

        if ((submitted || clicked) && !string.IsNullOrWhiteSpace(_input))
        {
            var text = _input.Trim();
            ChatCommandRouter.Send($"/p {text}", _config, "ChatBox");
            _input = string.Empty;
            _focusInputNext = true;
        }
    }

    private void CopyVisibleToClipboard()
    {
        var snapshot = _chatLog.Snapshot();
        var visible = snapshot.Where(m => ChatLogBuffer.IsPartyChatType(m.ChatType) || m.IsDice).ToList();
        if (visible.Count == 0)
        {
            ImGui.SetClipboardText("(BJB Messenger: no messages)");
            return;
        }

        var sb = new StringBuilder(visible.Count * 64);
        sb.AppendLine($"=== BJB Messenger Log ===");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Entries: {visible.Count}");
        sb.AppendLine($"=========================");
        foreach (var m in visible)
        {
            var tag = m.GroupIndexNumber > 0 ? $"[{m.GroupIndexNumber}] " : "";
            sb.AppendLine($"[{m.Timestamp:HH:mm}] {tag}{m.Name}: {m.Message}");
        }
        ImGui.SetClipboardText(sb.ToString());
    }
}
