using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public class ImportantNoticeWindow : Window
{
    private readonly Configuration _config;
    private readonly Action _save;
    private DateTime _openedAt = DateTime.MinValue;
    private int _selectedLang = 0;

    private static readonly string[] LangLabels = { "English", "Deutsch", "\u65e5\u672c\u8a9e" };

    private static readonly string TextEnglish =
        "After receiving reports that some people have been blaming and even verbally attacking other Blackjack dealers " +
        "for not using the BlackJack Buttler, I need to make one thing clear:\n" +
        "\n" +
        "This is not okay. If this happens again anywhere, I will personally speak to everyone involved. " +
        "Everyone has the right to use whatever plugin they want \u2014 or no plugin at all.\n" +
        "\n" +
        "The BlackJack Buttler is a helper. It is a tool meant to give people the option to get started with " +
        "Blackjack dealing. But everyone is different. If someone feels more comfortable using macros instead, " +
        "that is their choice.\n" +
        "\n" +
        "If something like this happens again and crosses the line, I will remove that person's access to the plugin. " +
        "I will also personally investigate every single incident and listen to both sides before making a decision.\n" +
        "\n" +
        "So please stay respectful and calm.\n" +
        "\n" +
        "That is all.";

    private static readonly string TextDeutsch =
        "Nachdem mich Berichte erreicht haben, dass einige Personen andere Blackjack-Dealer kritisieren oder sogar " +
        "verbal angreifen, nur weil sie den BlackJack Buttler nicht benutzen, m\u00f6chte ich eines klarstellen:\n" +
        "\n" +
        "Das ist nicht in Ordnung. Sollte so etwas irgendwo noch einmal vorkommen, werde ich mit allen Beteiligten " +
        "pers\u00f6nlich sprechen. Jeder hat das Recht, jedes beliebige Plugin zu nutzen \u2014 oder auch ganz darauf zu verzichten.\n" +
        "\n" +
        "Der BlackJack Buttler ist ein Hilfsmittel. Ein Werkzeug, das Menschen die M\u00f6glichkeit geben soll, in das " +
        "Blackjack-Dealing einzusteigen. Aber jeder ist anders. Wenn sich jemand mit Makros wohler f\u00fchlt, dann ist " +
        "das dessen Entscheidung.\n" +
        "\n" +
        "Wenn so etwas erneut passiert und eine Grenze \u00fcberschritten wird, werde ich der betreffenden Person den " +
        "Zugriff auf das Plugin entziehen. Au\u00dferdem werde ich jeden einzelnen Vorfall pers\u00f6nlich pr\u00fcfen und mir immer " +
        "beide Seiten anh\u00f6ren, bevor ich eine Entscheidung treffe.\n" +
        "\n" +
        "Bitte bleibt also respektvoll und ruhig.\n" +
        "\n" +
        "Das ist alles.";

    private static readonly string TextJapanese =
        "BlackJack Buttler \u3092\u4f7f\u7528\u3057\u3066\u3044\u306a\u3044\u3053\u3068\u3092\u7406\u7531\u306b\u3001\u4ed6\u306e\u30d6\u30e9\u30c3\u30af\u30b8\u30e3\u30c3\u30af\u30c7\u30a3\u30fc\u30e9\u30fc\u3092\u975e\u96e3\u3057\u305f\u308a\u3001" +
        "\u8a00\u8449\u3067\u653b\u6483\u3057\u305f\u308a\u3059\u308b\u4eba\u304c\u3044\u308b\u3068\u3044\u3046\u5831\u544a\u3092\u53d7\u3051\u307e\u3057\u305f\u3002\n" +
        "\u305d\u306e\u4ef6\u306b\u3064\u3044\u3066\u3001\u306f\u3063\u304d\u308a\u304a\u4f1d\u3048\u3057\u307e\u3059\u3002\n" +
        "\n" +
        "\u305d\u306e\u3088\u3046\u306a\u884c\u70ba\u306f\u8a31\u5bb9\u3067\u304d\u307e\u305b\u3093\u3002\u4eca\u5f8c\u3001\u540c\u3058\u3053\u3068\u304c\u3069\u3053\u304b\u3067\u518d\u3073\u8d77\u304d\u305f\u5834\u5408\u3001\u95a2\u4fc2\u8005\u5168\u54e1\u3068\u79c1\u304c\u76f4\u63a5\u8a71\u3092\u3057\u307e\u3059\u3002\n" +
        "\u3069\u306e\u30d7\u30e9\u30b0\u30a4\u30f3\u3092\u4f7f\u3046\u304b\u3001\u3042\u308b\u3044\u306f\u30d7\u30e9\u30b0\u30a4\u30f3\u3092\u4f7f\u308f\u306a\u3044\u304b\u306f\u3001\u3059\u3079\u3066\u672c\u4eba\u306e\u81ea\u7531\u3067\u3059\u3002\n" +
        "\n" +
        "BlackJack Buttler \u306f\u88dc\u52a9\u30c4\u30fc\u30eb\u3067\u3059\u3002\n" +
        "\u30d6\u30e9\u30c3\u30af\u30b8\u30e3\u30c3\u30af\u306e\u30c7\u30a3\u30fc\u30ea\u30f3\u30b0\u306b\u8e0f\u307f\u51fa\u3059\u305f\u3081\u306e\u9078\u629e\u80a2\u3092\u63d0\u4f9b\u3059\u308b\u305f\u3081\u306e\u3082\u306e\u3067\u3059\u3002\n" +
        "\u3057\u304b\u3057\u3001\u4eba\u305d\u308c\u305e\u308c\u3084\u308a\u65b9\u306f\u9055\u3044\u307e\u3059\u3002\u30de\u30af\u30ed\u306e\u65b9\u304c\u4f7f\u3044\u3084\u3059\u3044\u3068\u611f\u3058\u308b\u306e\u3067\u3042\u308c\u3070\u3001\u305d\u308c\u3082\u672c\u4eba\u306e\u9078\u629e\u3067\u3059\u3002\n" +
        "\n" +
        "\u4eca\u5f8c\u3053\u306e\u3088\u3046\u306a\u3053\u3068\u304c\u518d\u3073\u8d77\u3053\u308a\u3001\u5ea6\u3092\u8d8a\u3059\u3088\u3046\u3067\u3042\u308c\u3070\u3001\u305d\u306e\u4eba\u7269\u306e\u30d7\u30e9\u30b0\u30a4\u30f3\u5229\u7528\u6a29\u9650\u3092\u53d6\u308a\u6d88\u3057\u307e\u3059\u3002\n" +
        "\u307e\u305f\u3001\u3059\u3079\u3066\u306e\u4ef6\u306b\u3064\u3044\u3066\u79c1\u81ea\u8eab\u304c\u78ba\u8a8d\u3057\u3001\u5224\u65ad\u3092\u4e0b\u3059\u524d\u306b\u5fc5\u305a\u53cc\u65b9\u306e\u8a71\u3092\u805e\u304d\u307e\u3059\u3002\n" +
        "\n" +
        "\u3069\u3046\u304b\u843d\u3061\u7740\u3044\u3066\u3001\u4e92\u3044\u306b\u656c\u610f\u3092\u6301\u3063\u3066\u63a5\u3057\u3066\u304f\u3060\u3055\u3044\u3002\n" +
        "\n" +
        "\u4ee5\u4e0a\u3067\u3059\u3002";

    public ImportantNoticeWindow(Configuration config, Action save)
        : base("Important notice!###BJBImportantNotice",
               ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse)
    {
        _config = config;
        _save = save;
        Size = new Vector2(500, 600);
        SizeCondition = ImGuiCond.Always;
        RespectCloseHotkey = false;
    }

    public override void PreDraw()
    {
        var bg = ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg];
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(bg.X, bg.Y, bg.Z, 0.95f));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor();
    }

    public override void OnClose()
    {
        if (!_config.ImportantNoticeAcknowledged)
            IsOpen = true;
    }

    public override void Draw()
    {
        if (_openedAt == DateTime.MinValue)
            _openedAt = DateTime.Now;

        var viewport = ImGui.GetMainViewport();
        var workPos = viewport.WorkPos;
        var workSize = viewport.WorkSize;
        var winSize = ImGui.GetWindowSize();
        ImGui.SetWindowPos(new Vector2(
            workPos.X + workSize.X - winSize.X - 25,
            workPos.Y + workSize.Y - winSize.Y - 25));

        for (int i = 0; i < LangLabels.Length; i++)
        {
            if (i > 0) ImGui.SameLine();
            bool selected = _selectedLang == i;
            if (selected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.5f, 0.8f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.4f, 0.6f, 0.9f, 1f));
            }
            if (ImGui.Button(LangLabels[i], new Vector2(0, 28)))
                _selectedLang = i;
            if (selected)
                ImGui.PopStyleColor(2);
        }

        ImGui.Separator();

        var text = _selectedLang switch
        {
            1 => TextDeutsch,
            2 => TextJapanese,
            _ => TextEnglish,
        };

        if (ImGui.BeginChild("##notice_text", new Vector2(-1, -60), false))
        {
            ImGui.TextWrapped(text);
        }
        ImGui.EndChild();

        ImGui.Separator();
        ImGui.Spacing();

        var elapsed = (DateTime.Now - _openedAt).TotalSeconds;
        int remaining = Math.Max(0, 30 - (int)elapsed);

        if (remaining > 0)
        {
            ImGui.BeginDisabled();
            ImGui.Button($"Please wait ... {remaining} ...", new Vector2(-1, 40));
            ImGui.EndDisabled();
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.7f, 0.2f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.8f, 0.3f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.15f, 0.6f, 0.15f, 1f));
            if (ImGui.Button("Okay, i understand", new Vector2(-1, 40)))
            {
                _config.ImportantNoticeAcknowledged = true;
                _save();
                IsOpen = false;
            }
            ImGui.PopStyleColor(3);
        }
    }
}
