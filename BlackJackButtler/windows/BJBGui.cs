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

    public static bool InputInt(string label, ref int v, int step)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.InputInt(label, ref v, step);
        ImGui.PopStyleColor();
        return r;
    }

    public static bool InputLong(string label, ref long v, long step, long step_fast)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.InputLong(label, ref v, step, step_fast);
        ImGui.PopStyleColor();
        return r;
    }

    public static bool InputFloat(string label, ref float v, float step, float step_fast, string format)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.InputFloat(label, ref v, step, step_fast, format);
        ImGui.PopStyleColor();
        return r;
    }

    public static bool DragInt(string label, ref int v, float speed, int min, int max, string format)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.DragInt(label, ref v, speed, min, max, format);
        ImGui.PopStyleColor();
        return r;
    }

    public static bool SliderFloat(string label, ref float v, float v_min, float v_max, string format)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.SliderFloat(label, ref v, v_min, v_max, format);
        ImGui.PopStyleColor();
        return r;
    }

    public static bool DragFloat(string label, ref float v, float v_speed, float v_min, float v_max, string format)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.DragFloat(label, ref v, v_speed, v_min, v_max, format);
        ImGui.PopStyleColor();
        return r;
    }

    public static bool Combo(string label, ref int current_item, string items_separated_by_zeros)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.Combo(label, ref current_item, items_separated_by_zeros);
        ImGui.PopStyleColor();
        return r;
    }

    public static bool Combo(string label, ref int current_item, string[] items, int items_count)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.Combo(label, ref current_item, items, items_count);
        ImGui.PopStyleColor();
        return r;
    }
}
