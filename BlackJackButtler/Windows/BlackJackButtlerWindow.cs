using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public sealed class BlackJackButtlerWindow : Window, IDisposable
{
  private enum Page { Main, Settings }
  private Page _page = Page.Main;

  public BlackJackButtlerWindow() : base("BlackJack Buttler")
  {
    Size = new Vector2(900, 600);
    SizeCondition = ImGuiCond.FirstUseEver;

    // Resizable by default: do not set ImGuiWindowFlags.NoResize
    Flags = ImGuiWindowFlags.None;
  }

  public void Dispose() { }

  public override void Draw()
  {
    var avail = ImGui.GetContentRegionAvail();
    var sidebarWidth = 220f;

    ImGui.BeginChild("bjb.sidebar", new Vector2(sidebarWidth, avail.Y), true);
    ImGui.TextUnformatted("BlackJack Buttler");
    ImGui.Separator();

    NavButton(Page.Main, "Main");
    NavButton(Page.Settings, "Settings");

    ImGui.EndChild();

    ImGui.SameLine();

    ImGui.BeginChild("bjb.content", new Vector2(0, avail.Y), true);

    switch (_page)
    {
      case Page.Main:
      ImGui.TextUnformatted("Main");
      ImGui.Separator();
      ImGui.TextWrapped("Placeholder.");
      break;

      case Page.Settings:
      ImGui.TextUnformatted("Settings");
      ImGui.Separator();
      ImGui.TextWrapped("Placeholder.");
      break;
    }

    ImGui.EndChild();
  }

  public void OpenMain()
  {
      _page = Page.Main;
      IsOpen = true;
  }

  public void OpenSettings()
  {
      _page = Page.Settings;
      IsOpen = true;
  }

  private void NavButton(Page page, string label)
  {
    var selected = _page == page;

    if (selected)
    ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 2f);

    if (ImGui.Button(label, new Vector2(-1, 0)))
    _page = page;

    if (selected)
    ImGui.PopStyleVar();
  }
}
