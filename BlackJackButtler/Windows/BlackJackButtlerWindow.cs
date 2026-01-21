using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using BlackJackButtler;
using BlackJackButtler.Chat;

namespace BlackJackButtler.Windows;

public sealed class BlackJackButtlerWindow : Window, IDisposable
{
  private enum Page { Main, Regexes, Messages, Settings }
  private Page _page = Page.Main;

  private readonly Configuration _config;
  private readonly Action _save;
  private readonly ChatLogBuffer _chatLog;

  public BlackJackButtlerWindow(Configuration config, Action save, ChatLogBuffer chatLog) : base("BlackJack Buttler")
  {
    _config = config;
    _save = save;
    _chatLog = chatLog;

    Size = new Vector2(900, 600);
    SizeCondition = ImGuiCond.FirstUseEver;
    Flags = ImGuiWindowFlags.None;
  }

  public void Dispose() { }

  public override void Draw()
  {
    var avail = ImGui.GetContentRegionAvail();
    var sidebarWidth = 250f;

    ImGui.BeginChild("bjb.sidebar", new Vector2(sidebarWidth, avail.Y), true);
    ImGui.TextUnformatted("BlackJack Buttler");
    ImGui.Separator();

    NavButton(Page.Main, "Main");
    NavButton(Page.Regexes, "Regular Expressions");
    NavButton(Page.Messages, "Message Batches");
    NavButton(Page.Settings, "Settings");

    ImGui.EndChild();

    ImGui.SameLine();

    ImGui.BeginChild("bjb.content", new Vector2(0, avail.Y), true);

    switch (_page)
    {
      case Page.Main:
      DrawMain();
      break;
      case Page.Regexes:
      DrawRegexes();
      break;
      case Page.Messages:
      DrawMessageBatches();
      break;
      case Page.Settings:
      DrawSettings();
      break;
    }

    ImGui.EndChild();
  }

  public void OpenMain() { _page = Page.Main; IsOpen = true; }
  public void OpenSettings() { _page = Page.Settings; IsOpen = true; }

  private void NavButton(Page page, string label)
  {
    var selected = _page == page;

    if (selected) ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 2f);
    if (ImGui.Button(label, new Vector2(-1, 0))) _page = page;
    if (selected) ImGui.PopStyleVar();
  }

  private void DrawMain()
  {
    ImGui.TextUnformatted("Main");
    ImGui.Separator();
    ImGui.TextWrapped("WIP ...");
  }

  private void DrawSettings()
  {
    ImGui.TextUnformatted("Settings");
    ImGui.Separator();
    ImGui.TextWrapped("WIP ...");
  }

  private void DrawRegexes()
  {
    ImGui.TextUnformatted("Regular Expressions");
    ImGui.Separator();

    if (ImGui.Button("Clear Chat Log"))
    _chatLog.Clear();

    ImGui.SameLine();
    ImGui.TextDisabled("Showing last 20 chat messages (debug).");

    ImGui.Spacing();

    // Scrollbarer Bereich
    ImGui.BeginChild("bjb.chatlog", new Vector2(0, 0), true);

    var entries = _chatLog.Snapshot();
    foreach (var e in entries)
    {
      // Format: [time] [chatType] sender: message
      ImGui.TextUnformatted(
      $"[{e.Timestamp:HH:mm:ss}] [{e.ChatType}] {e.Sender}: {e.Message}"
      );
    }

    ImGui.EndChild();
  }


  private void DrawMessageBatches()
  {
    ImGui.TextUnformatted("Message Batches");
    ImGui.Separator();

    // + Batch
    if (ImGui.Button("+ New Batch"))
    {
      _config.MessageBatches.Add(new MessageBatch());
      _save();
    }

    ImGui.SameLine();
    ImGui.TextDisabled($"Batches: {_config.MessageBatches.Count}");

    ImGui.Spacing();

    for (var i = 0; i < _config.MessageBatches.Count; i++)
    {
      var batch = _config.MessageBatches[i];
      ImGui.PushID(i);

      // Header label
      var headerLabel = string.IsNullOrWhiteSpace(batch.Name) ? $"Batch {i + 1}" : batch.Name;

      // Persisted expand state
      var flags = batch.IsExpanded ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
      ImGui.SetNextItemOpen(batch.IsExpanded, ImGuiCond.Always);
      var open = ImGui.CollapsingHeader(headerLabel);


      if (open != batch.IsExpanded)
      {
        batch.IsExpanded = open;
        _save();
      }

      if (open)
      {
        // Batch name edit
        var name = batch.Name ?? "";
        if (ImGui.InputText("Name", ref name, 128))
        {
          batch.Name = name;
          _save();
        }

        ImGui.Spacing();

        // Messages list
        ImGui.TextUnformatted("Messages");
        ImGui.SameLine();
        if (ImGui.Button("+ Line"))
        {
          batch.Messages.Add(string.Empty);
          _save();
        }

        ImGui.Spacing();

        for (var m = 0; m < batch.Messages.Count; m++)
        {
          ImGui.PushID(m);

          var line = batch.Messages[m] ?? "";

          // Single-line editing (knackig). Wenn du Multi-line willst, sag’s, dann stelle ich um.
          if (ImGui.InputText($"##msg_{m}", ref line, 512))
          {
            batch.Messages[m] = line;
            _save();
          }

          ImGui.SameLine();
          if (ImGui.Button("X"))
          {
            batch.Messages.RemoveAt(m);
            _save();
            ImGui.PopID();
            break; // Liste geändert → Schleife verlassen
          }

          ImGui.PopID();
        }

        ImGui.Spacing();
        ImGui.Separator();

        var io = ImGui.GetIO();

        if (!io.KeyCtrl)
        {
          ImGui.BeginDisabled(true);
          ImGui.Button("Delete Batch (hold CTRL)");
          ImGui.EndDisabled();
          if (ImGui.IsItemHovered())
          ImGui.SetTooltip("Hold CTRL while clicking to delete this batch.");
        }
        else
        {
          if (ImGui.Button("Delete Batch"))
          {
            _config.MessageBatches.RemoveAt(i);
            _save();
            ImGui.PopID();
            break;
          }
        }

      }

      ImGui.PopID();
      ImGui.Spacing();
    }
  }
}
