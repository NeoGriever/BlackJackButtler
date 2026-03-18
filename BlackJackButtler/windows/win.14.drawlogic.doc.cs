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
        ImGui.SetNextWindowSize(new Vector2(500, 800), ImGuiCond.FirstUseEver);

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
        DocEntry("Draw()", "Render the current shape (clears variables)");
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
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), "Block Iteration");
        ImGui.Separator();
        ImGui.TextDisabled("  IterateHand {");
        ImGui.TextDisabled("    // runs once per hand of the current player");
        ImGui.TextDisabled("  }");
        ImGui.TextDisabled("  IterateCard {");
        ImGui.TextDisabled("    // runs once per card of the current hand");
        ImGui.TextDisabled("  }");
        ImGui.TextDisabled("  Blocks can be nested (IterateHand > IterateCard).");

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), "Variable Functions");
        ImGui.Separator();
        DocEntry("SetVar(\"name\", value)", "Set variable (current scope)");
        DocEntry("UnVar(\"name\")", "Remove variable (current scope)");
        DocEntry("setVarH(\"name\", handIdx, value)", "Set at hand scope");
        DocEntry("unVarH(\"name\", handIdx)", "Remove at hand scope");
        DocEntry("setVarC(\"name\", hIdx, cIdx, value)", "Set at card scope");
        DocEntry("unVarC(\"name\", hIdx, cIdx)", "Remove at card scope");
        ImGui.TextDisabled("  Variables are cleared after each Draw() call.");
        ImGui.TextDisabled("  GetVar fallback: card > hand > player > 0.");

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), "Math Functions (in arguments)");
        ImGui.Separator();
        ImGui.TextDisabled("  Ceil(a), Floor(a), Sin(a), Cos(a), Sqrt(a)");
        ImGui.TextDisabled("  Min(a,b), Max(a,b), Clamp(a,min,max)");
        ImGui.TextDisabled("  Mul(a,b), Div(a,b), Mod(a,b), Plus(a,b), Minus(a,b)");
        ImGui.TextDisabled("  GetVar(\"name\"), getVarH(\"name\",h), getVarC(\"name\",h,c)");
        ImGui.TextDisabled("  Operators: + - * / % and parentheses.");

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
        CopyRow("<NameW>", "Player name@world");
        CopyRow("<score>", "Current hand score");
        CopyRow("<cards>", "Current hand cards string");
        CopyRow("<cardcount>", "Number of cards in hand");
        CopyRow("<bank>", "Player bank (numeric)");
        CopyRow("<BankF>", "Player bank (formatted: 1,000,000)");
        CopyRow("<bet>", "Player bet (numeric)");
        CopyRow("<BetF>", "Player bet (formatted)");
        CopyRow("<MaxBet>", "Effective max bet (numeric)");
        CopyRow("<MaxBetF>", "Effective max bet (formatted)");
        CopyRow("<handindex>", "Current hand index");
        CopyRow("<handcount>", "Number of hands");
        CopyRow("<isdealer>", "1 if dealer, 0 if player");
        CopyRow("<IsPlaying>", "1 if active (not hold/bench)");
        CopyRow("<IsCurrentTurn>", "1 if player's turn");

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), "Config Tokens");
        ImGui.Separator();
        CopyRow("<Scale>", "DrawLogic scale setting");
        CopyRow("<OffsetX>", "DrawLogic X offset");
        CopyRow("<OffsetY>", "DrawLogic Y offset");
        CopyRow("<OffsetZ>", "DrawLogic Z offset");
        CopyRow("<OffsetR>", "DrawLogic rotation offset");

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
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), "Hand Tokens (inside IterateHand)");
        ImGui.Separator();
        CopyRow("<HandIndex>", "Current hand loop index");
        CopyRow("<HandsTotal>", "Total number of hands");
        CopyRow("<HandPoints>", "Best score of this hand");
        CopyRow("<HandPointsB>", "Lower soft-hand score (0 if hard)");
        CopyRow("<HandActive>", "1 if this is the active hand");
        CopyRow("<HandBusted>", "1 if this hand is bust");

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.85f, 0.4f, 1f), "Card Tokens (inside IterateCard)");
        ImGui.Separator();
        CopyRow("<CardIndex>", "Current card loop index");
        CopyRow("<CardsTotal>", "Total cards in this hand");
        CopyRow("<CardNumber>", "Card value (1=A, 2-10, 11=J, 12=Q, 13=K)");
        CopyRow("<CardColor>", "Suit (0=Spades, 1=Clubs, 2=Hearts, 3=Diamonds)");
        CopyRow("<CardColorR>", "Suit color red component");
        CopyRow("<CardColorG>", "Suit color green component");
        CopyRow("<CardColorB>", "Suit color blue component");
        CopyRow("<CardAge>", "0\u21921 over 3 seconds since card was drawn");

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
