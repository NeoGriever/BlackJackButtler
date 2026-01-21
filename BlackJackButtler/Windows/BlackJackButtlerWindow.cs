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
  private string _partyDump = string.Empty;


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

  private bool _showRegexWarningPopup;
  private bool _pendingEnableStandardEdit;

  private void DrawRegexes()
  {
    ImGui.TextUnformatted("Regular Expressions");
    ImGui.Separator();

    // --- Standard Regex (locked) ---
    ImGui.TextUnformatted("Standard Algorithms");
    ImGui.Spacing();

    // Checkbox "unlock" mit Warn-Popup
    var allow = _config.AllowEditingStandardRegex;
    if (ImGui.Checkbox("Allow editing standard regular expressions", ref allow))
    {
      if (allow && !_config.AllowEditingStandardRegex)
      {
        // erst Popup zeigen, noch nicht freischalten
        _pendingEnableStandardEdit = true;
        _showRegexWarningPopup = true;
        ImGui.OpenPopup("bjb.regex.warning");
      }
      else if (!allow && _config.AllowEditingStandardRegex)
      {
        _config.AllowEditingStandardRegex = false;
        _save();
      }
    }

    if (ImGui.BeginPopupModal("bjb.regex.warning", ref _showRegexWarningPopup, ImGuiWindowFlags.AlwaysAutoResize))
    {
      ImGui.TextWrapped("Editing the regular expression is for advanced users. Please make sure, that you know, what you're doing.");
      ImGui.Spacing();

      if (ImGui.Button("I understand"))
      {
        if (_pendingEnableStandardEdit)
        {
          _config.AllowEditingStandardRegex = true;
          _save();
        }
        _pendingEnableStandardEdit = false;
        _showRegexWarningPopup = false;
        ImGui.CloseCurrentPopup();
      }

      ImGui.SameLine();

      if (ImGui.Button("Cancel"))
      {
        _pendingEnableStandardEdit = false;
        _showRegexWarningPopup = false;
        ImGui.CloseCurrentPopup();
      }

      ImGui.EndPopup();
    }

    // Standard: Dice->Card mapping Anzeige (editierbar erst bei Unlock)
    ImGui.Separator();
    ImGui.TextUnformatted("Dice → Card value (Blackjack)");
    ImGui.TextDisabled("Applied only to your own dice events.");

    // Wir lassen das Pattern fürs Erste fest (UI-Text), später kannst du es als editierbares Pattern hinterlegen.
    var standardPattern = @"(\d+)\s*$";
    ImGui.BeginDisabled(!_config.AllowEditingStandardRegex);
    ImGui.InputText("Dice value pattern", ref standardPattern, 128, ImGuiInputTextFlags.ReadOnly);
    ImGui.EndDisabled();

    ImGui.Spacing();
    ImGui.TextUnformatted("Mapping:");
    ImGui.BulletText("1 → 11");
    ImGui.BulletText("2–9 → 2–9");
    ImGui.BulletText("10–13 → 10");

    ImGui.Spacing();
    ImGui.Separator();

    // --- User Regexes ---
    ImGui.TextUnformatted("Custom Regex Entries");
    ImGui.Spacing();

    if (ImGui.Button("+ Add Regex Entry"))
    {
      _config.UserRegexes.Add(new BlackJackButtler.Regex.UserRegexEntry());
      _save();
    }

    ImGui.SameLine();
    ImGui.TextDisabled($"Entries: {_config.UserRegexes.Count}");

    ImGui.Spacing();

    for (var i = 0; i < _config.UserRegexes.Count; i++)
    {
      var e = _config.UserRegexes[i];
      ImGui.PushID(i);

      var header = string.IsNullOrWhiteSpace(e.Name) ? $"Entry {i + 1}" : e.Name;
      if (ImGui.CollapsingHeader(header, ImGuiTreeNodeFlags.DefaultOpen))
      {
        var enabled = e.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled))
        {
          e.Enabled = enabled;
          _save();
        }

        // Mode (nur SetVariable jetzt sinnvoll)
        var mode = (int)e.Mode;
        if (ImGui.Combo("Mode", ref mode, "Set Variable\0Reaction (later)\0"))
        {
          e.Mode = (BlackJackButtler.Regex.RegexEntryMode)mode;
          _save();
        }

        // Variable name
        var name = e.Name ?? "";
        if (ImGui.InputText("Name", ref name, 64))
        {
          e.Name = name;
          _save();
        }

        // Case sensitive
        var cs = e.CaseSensitive;
        if (ImGui.Checkbox("Case sensitive", ref cs))
        {
          e.CaseSensitive = cs;
          _save();
        }

        // Pattern
        var pat = e.Pattern ?? "";
        if (ImGui.InputTextMultiline("Pattern", ref pat, 2048, new Vector2(-1, 80)))
        {
          e.Pattern = pat;
          _save();
        }

        ImGui.Spacing();

        // Delete (CTRL required)
        var io = ImGui.GetIO();
        if (!io.KeyCtrl)
        {
          ImGui.BeginDisabled(true);
          ImGui.Button("Delete (hold CTRL)");
          ImGui.EndDisabled();
        }
        else
        {
          if (ImGui.Button("Delete"))
          {
            _config.UserRegexes.RemoveAt(i);
            _save();
            ImGui.PopID();
            break;
          }
        }
      }

      ImGui.PopID();
    }
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
