using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using BlackJackButtler;

namespace BlackJackButtler.Windows;

public sealed class BlackJackButtlerWindow : Window, IDisposable
{
  private enum Page { Main, Messages, Settings }
  private Page _page = Page.Main;

  private readonly Configuration _config;
  private readonly Action _save;

  public BlackJackButtlerWindow(Configuration config, Action save) : base("BlackJack Buttler")
  {
    _config = config;
    _save = save;

    Size = new Vector2(900, 600);
    SizeCondition = ImGuiCond.FirstUseEver;
    Flags = ImGuiWindowFlags.None;
  }

  public void Dispose() { }

  public override void Draw()
  {
    var avail = ImGui.GetContentRegionAvail();
    var sidebarWidth = 200f;

    ImGui.BeginChild("bjb.sidebar", new Vector2(sidebarWidth, avail.Y), true);
    ImGui.TextUnformatted("BlackJack Buttler");
    ImGui.Separator();

    NavButton(Page.Main, "Main");
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
    ImGui.TextWrapped("Main Window");
  }

  private void DrawSettings()
  {
    ImGui.TextUnformatted("Settings");
    ImGui.Separator();
    ImGui.TextWrapped("Configurations");
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
      var open = ImGui.CollapsingHeader(headerLabel, flags);

      // Sync expand state (best-effort)
      batch.IsExpanded = open;

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

        // Delete batch
        if (ImGui.Button("Delete Batch"))
        {
          _config.MessageBatches.RemoveAt(i);
          _save();
          ImGui.PopID();
          break;
        }
      }

      ImGui.PopID();
      ImGui.Spacing();
    }
  }
}
