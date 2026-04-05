using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public class UpdatePopupWindow : Window
{
    private readonly Configuration _config;
    private readonly Action _save;
    private bool _triggerMigratePopup = false;

    private static readonly string CurrentChangelog =
        "v1.7.0.0\n" +
        "\n" +
        "- Fix: Natural BlackJack, Dirty\n" +
        "  BlackJack and Charlie notifications\n" +
        "  now trigger correctly\n" +
        "- Config migration now runs\n" +
        "  automatically at startup\n" +
        "\n" +
        "IMPORTANT: Please click the\n" +
        "'Migrate Configurations' button\n" +
        "below to receive the updated\n" +
        "commands and messages for Charlie,\n" +
        "Natural BlackJack and Dirty\n" +
        "BlackJack.\n";

    public UpdatePopupWindow(Configuration config, Action save)
        : base("The BlackJack Buttler has learned something new###BJBUpdatePopup",
               ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse)
    {
        _config = config;
        _save = save;
        Size = new Vector2(300, 380);
        SizeCondition = ImGuiCond.Always;
    }

    public override void PreDraw()
    {
        var viewport = ImGui.GetMainViewport();
        var center = viewport.GetCenter();
        ImGui.SetNextWindowPos(new Vector2(center.X - 150, center.Y - 190), ImGuiCond.Appearing);
    }

    public override void Draw()
    {
        ImGui.PushFont(UiBuilder.MonoFont);

        if (_triggerMigratePopup)
        {
            ImGui.OpenPopup("bjb.migrate.confirm");
            _triggerMigratePopup = false;
        }

        if (ImGui.BeginChild("##changelog_content", new Vector2(-1, -150), false))
        {
            ImGui.TextWrapped(CurrentChangelog);
            ImGui.EndChild();
        }

        ImGui.Separator();
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.5f, 0.8f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.6f, 0.9f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.15f, 0.4f, 0.7f, 1f));

        if (ImGui.Button("Migrate Configurations", new Vector2(-1, 0)))
        {
            _triggerMigratePopup = true;
        }

        ImGui.PopStyleColor(3);

        ImGui.TextWrapped("Recommended: Updates Charlie, Natural BlackJack and Dirty BlackJack commands and messages.");

        ImGui.Spacing();

        var open = true;
        if (ImGui.BeginPopupModal("bjb.migrate.confirm", ref open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextWrapped(
                "This will update/replace/create the specific commands " +
                "and messages for Charlie, Natural BlackJack and " +
                "Dirty BlackJack. It will NOT replace modified messages.");
            ImGui.Spacing();

            if (BJBGui.Button("Yes"))
            {
                if (DefaultsMigration.MigrateNotifyGroups(_config))
                    _save();
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (BJBGui.Button("Cancel"))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.85f, 0.35f, 0.1f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.95f, 0.45f, 0.15f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.75f, 0.25f, 0.05f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.95f, 0.6f, 1f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.5f);
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.15f, 0.1f, 0.05f, 1f));

        var coffeeSize = ImGui.CalcTextSize("Buy me a coffee");
        if (ImGui.Button("Buy me a coffee", new Vector2(coffeeSize.X + 20, 0)))
        {
            Dalamud.Utility.Util.OpenLink("https://buymeacoffee.com/mindconstructor");
        }

        ImGui.PopStyleColor(5);
        ImGui.PopStyleVar();

        ImGui.Spacing();

        if (BJBGui.Button("Dismiss"))
        {
            IsOpen = false;
        }
        ImGui.SameLine();
        if (BJBGui.Button("Don't show it again"))
        {
            _config.DisableUpdatePopup = true;
            _save();
            IsOpen = false;
        }

        ImGui.PopFont();
    }
}
