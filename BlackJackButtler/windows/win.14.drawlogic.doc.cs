using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawDrawLogicDocPanel()
    {
        if (!_showDrawLogicDoc) return;

        ImGui.SetNextWindowPos(
            new Vector2(_lastWindowPos.X + _lastWindowSize.X + 10, _lastWindowPos.Y),
            ImGuiCond.Appearing);
        ImGui.SetNextWindowSize(new Vector2(460, 620), ImGuiCond.FirstUseEver);

        if (!ImGui.Begin("Draw Logic Reference###bjb_drawlogic_doc", ref _showDrawLogicDoc,
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings))
        {
            ImGui.End();
            return;
        }

        ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), "Drawing Functions");
        ImGui.Separator();
        DocEntry("BeginShape(x, y, z)", "Start a new shape at world position");
        DocEntry("SetDrawColor(r, g, b, a)", "Set draw color (0.0\u20131.0)");
        DocEntry("BeginPath()", "Start a new path");
        DocEntry("MoveTo(x, y, z)", "Move to position (local offset)");
        DocEntry("LineTo(x, y, z)", "Draw line to position (local offset)");
        DocEntry("EndPath()", "Finish path (open)");
        DocEntry("ClosePath()", "Finish path (closed polygon)");
        DocEntry("FinishShape()", "Finalize shape");

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), "Shape Functions");
        ImGui.Separator();
        DocEntry("Draw()", "Render the current shape");
        DocEntry("Move(x, y, z)", "Translate current shape");
        DocEntry("Rotate(angle)", "Rotate shape (radians)");
        DocEntry("RotateTowards(x, z)", "Rotate towards world X/Z");
        DocEntry("SetLineThickness(t)", "Set line thickness (pixels)");

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), "Control Functions");
        ImGui.Separator();
        DocEntry("CallDrawLogic(\"name\")", "Call another entry");
        DocEntry("CallDrawLogic(\"name\", x, y, z)", "Call with custom position");
        ImGui.TextDisabled("  Iterate entries iterate over all players.");
        ImGui.TextDisabled("  Non-iterate entries use current context.");

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), "Position Tokens");
        ImGui.Separator();
        CopyRow("<pos>.x", "Player X world position");
        CopyRow("<pos>.y", "Player Y world position");
        CopyRow("<pos>.z", "Player Z world position");
        CopyRow("<rotation>", "Player rotation (radians)");

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), "Player Tokens");
        ImGui.Separator();
        CopyRow("<name>", "Player display name");
        CopyRow("<score>", "Current hand score");
        CopyRow("<cards>", "Current hand cards string");
        CopyRow("<cardcount>", "Number of cards in hand");
        CopyRow("<bank>", "Player bank amount");
        CopyRow("<bet>", "Player current bet");
        CopyRow("<handindex>", "Current hand index");
        CopyRow("<handcount>", "Number of hands");
        CopyRow("<isdealer>", "1 if dealer, 0 if player");

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), "State Flags (1 = true, 0 = false)");
        ImGui.Separator();
        CopyRow("<focused>", "Player's turn");
        CopyRow("<nearby>", "Within distance cap");
        CopyRow("<visible>", "WorldToScreen visible");
        CopyRow("<online>", "In ObjectTable");
        CopyRow("<ingroup>", "In party");
        CopyRow("<groupexists>", "Party exists");

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), "Game State (1 = true, 0 = false)");
        ImGui.Separator();
        CopyRow("<isbust>", "Hand is bust");
        CopyRow("<isstand>", "Hand is standing");
        CopyRow("<isblackjack>", "Natural blackjack");
        CopyRow("<isdone>", "Player is done");
        CopyRow("<isdd>", "Double down active");

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), "Arithmetic");
        ImGui.Separator();
        ImGui.TextDisabled("Supports + - * / and parentheses in arguments.");
        ImGui.TextDisabled("Example: <pos>.x + 0.5 * <focused>");
        ImGui.TextDisabled("Bool flags are 1/0, usable in math expressions.");
        ImGui.TextDisabled("// at line start = comment (skipped).");

        ImGui.End();
    }

    private static void DocEntry(string signature, string description)
    {
        if (BJBGui.SmallButton($"Copy##{signature}"))
            ImGui.SetClipboardText(signature);
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.7f, 0.9f, 1f, 1f), signature);
        ImGui.SameLine();
        ImGui.TextDisabled($"- {description}");
    }
}
