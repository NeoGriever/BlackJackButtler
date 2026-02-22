using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

internal static class BJBGui
{
    public static Vector4 ButtonTextColor = new Vector4(1f, 1f, 1f, 1f);

    public static bool Button(string label)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.Button(label);
        ImGui.PopStyleColor();
        return r;
    }

    public static bool Button(string label, Vector2 size)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.Button(label, size);
        ImGui.PopStyleColor();
        return r;
    }

    public static bool SmallButton(string label)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.SmallButton(label);
        ImGui.PopStyleColor();
        return r;
    }
}
