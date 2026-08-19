using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace BlackJackButtler.Windows;

public sealed class Version3MigrationWindow : Window
{
    private readonly Configuration _config;
    private readonly Action _save;
    private int _language;

    private static readonly string[] LanguageLabels = { "English", "Deutsch", "Français", "中文" };

    private static readonly NoticeText[] Texts =
    {
        new(
            "Moving to Version 3",
            "This update begins the transition to a more stable Version 3. The legacy Classic and Version 2 views will be retired in a future release. This gives the settings a clearer, more reliable structure, makes support easier, and reduces both plugin size and loading time.\n\nWe ask everyone who has not yet changed to Version 3 to do so now.\n\nWould you like to change to Version 3?",
            "Yes", "No"),
        new(
            "Umstellung auf Version 3",
            "Dieses Update beginnt die Umstellung auf eine stabilere Version 3. Die veralteten Ansichten Classic und Version 2 werden in einer zukünftigen Version entfernt. Dadurch werden die Einstellungen übersichtlicher und zuverlässiger, der Support bei Fragen zum Plugin wird stabiler und Ladezeit sowie Größe des Plugins werden reduziert.\n\nWir möchten alle Nutzer bitten, die noch nicht auf Version 3 gewechselt haben, dies jetzt zu tun.\n\nMöchtest du auf Version 3 wechseln?",
            "Ja", "Nein"),
        new(
            "Passage à la version 3",
            "Cette mise à jour commence la transition vers une version 3 plus stable. Les vues héritées Classic et Version 2 seront retirées dans une prochaine version. Les réglages seront ainsi plus clairs et plus fiables, l'assistance sera plus simple et le plugin sera plus léger et plus rapide à charger.\n\nNous invitons tous les utilisateurs qui n'ont pas encore adopté la version 3 à le faire maintenant.\n\nVoulez-vous passer à la version 3 ?",
            "Oui", "Non"),
        new(
            "切换到版本 3",
            "此次更新开启了向更稳定的版本 3 迁移的过程。旧版 Classic 和版本 2 视图将在未来的版本中移除。这样可以让设置布局更清晰、更可靠，便于提供插件支持，同时减少插件体积和加载时间。\n\n我们希望尚未切换到版本 3 的所有用户现在进行切换。\n\n要切换到版本 3 吗？",
            "是", "否"),
    };

    public Version3MigrationWindow(Configuration config, Action save)
        : base("Changing to Version 3 for everyone###BJBVersion3Migration",
               ImGuiWindowFlags.NoCollapse)
    {
        _config = config;
        _save = save;
        Size = new Vector2(560f, 390f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        var text = Texts[_language];

        for (var i = 0; i < LanguageLabels.Length; i++)
        {
            if (i > 0) ImGui.SameLine();
            var selected = _language == i;
            if (selected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.12f, 0.43f, 0.78f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.18f, 0.52f, 0.9f, 1f));
            }
            if (ImGui.Button($"{LanguageLabels[i]}##version3_notice_language_{i}"))
                _language = i;
            if (selected)
                ImGui.PopStyleColor(2);
        }

        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextUnformatted(text.Heading);
        ImGui.Spacing();

        if (ImGui.BeginChild("##version3_notice_text", new Vector2(0f, -54f), true))
            ImGui.TextWrapped(text.Body);
        ImGui.EndChild();

        ImGui.Spacing();
        if (ImGui.Button($"{text.Yes}##version3_notice_yes", new Vector2(120f, 32f)))
        {
            _config.MainViewVersion = 3;
            _config.Version3MigrationNoticeOpened = true;
            _save();
            IsOpen = false;
        }
        ImGui.SameLine();
        if (ImGui.Button($"{text.No}##version3_notice_no", new Vector2(120f, 32f)))
        {
            _config.Version3MigrationNoticeOpened = true;
            _save();
            IsOpen = false;
        }
    }

    private sealed record NoticeText(string Heading, string Body, string Yes, string No);
}
