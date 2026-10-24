using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using BlackJackButtler.Chat;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawEnumButtons(string id, ref int value, string[] labels, Action<int> onChanged, int separateBeforeIndex = -1, bool joined = true)
    {
        if (!joined)
        {
            for (var index = 0; index < labels.Length; index++)
            {
                if (index > 0) ImGui.SameLine();
                var active = value == index;
                if (active) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1f, 0.5f, 0f, 1f));
                if (BJBGui.Button($"{labels[index]}##{id}_{index}", active ? BJBGui.OrangeHighlightTextColor : BJBGui.ButtonTextColor))
                {
                    value = index;
                    onChanged(index);
                }
                if (active) ImGui.PopStyleColor();
            }
            return;
        }

        var style = ImGui.GetStyle();
        var height = ImGui.GetFrameHeight();
        var rounding = Math.Min(Math.Max(4f, style.FrameRounding), height * 0.5f);
        var drawList = ImGui.GetWindowDrawList();

        for (int i = 0; i < labels.Length; i++)
        {
            var startsGroup = i == 0 || i == separateBeforeIndex;
            var endsGroup = i == labels.Length - 1 || i == separateBeforeIndex - 1;
            if (i > 0) ImGui.SameLine(0f, startsGroup ? style.ItemSpacing.X : 0f);

            var textSize = ImGui.CalcTextSize(labels[i]);
            var size = new Vector2(textSize.X + style.FramePadding.X * 2f, height);
            var position = ImGui.GetCursorScreenPos();
            var clicked = ImGui.InvisibleButton($"##{id}_{i}", size);
            var hovered = ImGui.IsItemHovered();
            var held = ImGui.IsItemActive();
            var active = value == i;
            var color = active ? held ? new Vector4(0.9f, 0.4f, 0f, 1f) : hovered ? new Vector4(1f, 0.6f, 0.1f, 1f) : new Vector4(1f, 0.5f, 0f, 1f) : style.Colors[(int)(held ? ImGuiCol.ButtonActive : hovered ? ImGuiCol.ButtonHovered : ImGuiCol.Button)];
            var maximum = position + size;

            drawList.AddRectFilled(position, maximum, ImGui.GetColorU32(color), startsGroup || endsGroup ? rounding : 0f);
            if (startsGroup && !endsGroup)
                drawList.AddRectFilled(new Vector2(maximum.X - rounding, position.Y), maximum, ImGui.GetColorU32(color));
            else if (!startsGroup && endsGroup)
                drawList.AddRectFilled(position, new Vector2(position.X + rounding, maximum.Y), ImGui.GetColorU32(color));

            var textPosition = position + (size - textSize) * 0.5f;
            drawList.AddText(textPosition, ImGui.GetColorU32(active ? BJBGui.OrangeHighlightTextColor : BJBGui.ButtonTextColor), labels[i]);

            if (clicked && value != i)
            {
                value = i;
                onChanged(i);
            }
        }
    }

    private void DrawAutoContinueMinimumPlayersSelector(string id)
    {
        ImGui.TextUnformatted("Minimum players for auto continue:");
        ImGui.SameLine();

        const float width = 48f;
        const float height = 27f;
        const float rounding = 5f;
        var labels = new[] { "1+", "2+", "3+", "4+" };
        var drawList = ImGui.GetWindowDrawList();

        for (var index = 0; index < labels.Length; index++)
        {
            if (index > 0) ImGui.SameLine(0f, 0f);

            var position = ImGui.GetCursorScreenPos();
            var selected = _config.AutoContinueMinimumPlayers == index + 1;
            var hovered = false;
            if (ImGui.InvisibleButton($"##{id}_{index}", new Vector2(width, height)))
            {
                _config.AutoContinueMinimumPlayers = index + 1;
                _save();
            }
            hovered = ImGui.IsItemHovered();

            var color = selected ? new Vector4(1f, 0.5f, 0f, 1f) : ImGui.GetStyle().Colors[(int)(hovered ? ImGuiCol.ButtonHovered : ImGuiCol.Button)];
            var colorU32 = ImGui.GetColorU32(color);
            var maximum = position + new Vector2(width, height);

            if (index == 0 || index == labels.Length - 1)
            {
                drawList.AddRectFilled(position, maximum, colorU32, rounding);
                if (index == 0)
                    drawList.AddRectFilled(position + new Vector2(rounding, 0f), maximum, colorU32);
                else
                    drawList.AddRectFilled(position, maximum - new Vector2(rounding, 0f), colorU32);
            }
            else
            {
                drawList.AddRectFilled(position, maximum, colorU32);
            }

            var textSize = ImGui.CalcTextSize(labels[index]);
            drawList.AddText(
                position + new Vector2(
                    (width - textSize.X) / 2f,
                    (height - textSize.Y) / 2f
                ),
                ImGui.GetColorU32(
                    selected ?
                    BJBGui.OrangeHighlightTextColor :
                    BJBGui.ButtonTextColor
                ),
                labels[index]
            );
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Counts active, non-paused players only. The dealer is excluded.");
    }

    private void MultiplierInput(string label, ref float value, float defaultValue, string id)
    {
        ImGui.TextUnformatted(label);
        ImGui.SameLine(260f);
        ImGui.SetNextItemWidth(90f);
        if (BJBGui.InputFloat($"##{id}", ref value, 0.05f, 0.1f, "%.2fx", defaultValue: defaultValue))
        {
            value = (float)(Math.Round(value / 0.05f) * 0.05f);
            value = Math.Clamp(value, 1f, 3f);
            _save();
        }
    }

    private void IntInputSave(string label, ref int value, int min, int max, int step, int defaultValue)
    {
        var idSeparator = label.IndexOf("##", StringComparison.Ordinal);
        var visibleLabel = idSeparator >= 0 ? label[..idSeparator] : label;
        var inputId = idSeparator >= 0 ? $"##{label[(idSeparator + 2)..]}" : $"##{label}";
        ImGui.TextUnformatted(visibleLabel);
        ImGui.SameLine(260f);
        ImGui.SetNextItemWidth(120f);
        if (BJBGui.InputInt(inputId, ref value, step, defaultValue: defaultValue))
        {
            value = Math.Clamp(value, min, max);
            _save();
        }
    }

    private void FloatInputSave(string label, ref float value, float min, float max, float defaultValue)
    {
        ImGui.TextUnformatted(label.Split("##")[0]);
        ImGui.SameLine(260f);
        ImGui.SetNextItemWidth(120f);
        if (BJBGui.InputFloat($"##{label}", ref value, 1f, 10f, "%.2f", defaultValue: defaultValue))
        {
            value = Math.Clamp(value, min, max);
            _save();
        }
    }

    private void SliderSave(string label, ref float value, float min, float max, float step, float defaultValue, string suffix)
    {
        ImGui.TextUnformatted(label);
        ImGui.SameLine(260f);
        ImGui.SetNextItemWidth(220f);
        if (BJBGui.SliderFloat($"##{label}", ref value, min, max, suffix.Length > 0 ? $"%.2f{suffix}" : "%.2f"))
        {
            value = (float)(Math.Round(value / step) * step);
            value = Math.Clamp(value, min, max);
            _save();
        }
        ImGui.SameLine();
        if (BJBGui.SmallButton($"Reset##{label}_reset"))
        {
            value = defaultValue;
            _save();
        }
    }

    private void ColorRgbInputs(string label, ref Vector4 color, Vector4 defaultValue)
    {
        ImGui.TextUnformatted(label.Split("##")[0]);
        ImGui.SameLine(260f);
        bool changed = false;
        changed |= ColorComponent("R", ref color.X, defaultValue.X, label);
        ImGui.SameLine();
        changed |= ColorComponent("G", ref color.Y, defaultValue.Y, label);
        ImGui.SameLine();
        changed |= ColorComponent("B", ref color.Z, defaultValue.Z, label);
        color.W = 1f;
        if (changed) _save();
        ImGui.SameLine();
        if (BJBGui.SmallButton($"Reset##{label}_reset"))
        {
            color = defaultValue;
            _save();
        }
    }

    private bool ColorComponent(string component, ref float value, float defaultValue, string id)
    {
        ImGui.SetNextItemWidth(70f);
        if (BJBGui.InputFloat($"##{id}_{component}", ref value, 0.01f, 0.1f, "%.2f", defaultValue: defaultValue))
        {
            value = Math.Clamp(value, 0f, 1f);
            return true;
        }
        return false;
    }

    private void DrawButtonStyleEditor(string label, ButtonStyleConfig style, ButtonStyleConfig defaults, bool applyToGlobal = false, bool applyToCustom = false, bool applyToHighlight = false, string? styleId = null)
    {
        var id = styleId ?? label;
        Header(label);
        ColorRgbInputs($"Background##{id}", ref style.Background, defaults.Background);
        ColorRgbInputs($"Text##{id}", ref style.Text, defaults.Text);
        SliderSave($"Font-Size##{id}", ref style.FontSize, 0.25f, 3.5f, 0.25f, defaults.FontSize, "x");
        PaddingInput("Padding top", ref style.PaddingTop, defaults.PaddingTop, id);
        PaddingInput("Padding left", ref style.PaddingLeft, defaults.PaddingLeft, id);
        PaddingInput("Padding bottom", ref style.PaddingBottom, defaults.PaddingBottom, id);
        PaddingInput("Padding right", ref style.PaddingRight, defaults.PaddingRight, id);

        if (applyToGlobal)
        {
            _config.ButtonColor = style.Background;
            _config.ButtonTextColor = style.Text;
        }
        if (applyToCustom)
        {
            _config.CustomButtonFontScale = style.FontSize;
            _config.CustomButtonPaddingH = (style.PaddingLeft + style.PaddingRight) * 0.5f;
            _config.CustomButtonPaddingV = (style.PaddingTop + style.PaddingBottom) * 0.5f;
        }
        if (applyToHighlight)
        {
            _config.HighlightColor = style.Background;
            _config.HighlightTextColor = style.Text;
        }
    }

    private void PaddingInput(string text, ref int value, int defaultValue, string id)
    {
        ImGui.TextUnformatted(text);
        ImGui.SameLine(260f);
        ImGui.SetNextItemWidth(90f);
        if (BJBGui.InputInt($"##{id}_{text}", ref value, 1, defaultValue: defaultValue))
        {
            value = Math.Clamp(value, 0, 25);
            _save();
        }
    }

}
