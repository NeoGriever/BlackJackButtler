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
    private void DrawVisualSettings()
    {
        Header("Font");
        Indent(
            () =>
            {
            int fontIdx = _config.SelectedFontName == "Mono" ? 1 : 0;
            ImGui.SetNextItemWidth(200f);
            if (BJBGui.Combo("Selected Font##v2_font", ref fontIdx, "Default\0Mono\0"))
            {
                _config.SelectedFontName = fontIdx == 1 ? "Mono" : "Default";
                _config.CustomButtonUseMono = fontIdx == 1;
                _save();
            }
            }
        );

        Header("Poput-Bar");
        Indent(
            () =>
            {
            DrawSettingsToggle("Lock Position", "visual_button_bar_locked", ref _config.ButtonBarLocked);
            int layout = (int)_config.ButtonBarLayout;
            DrawEnumButtons(
                "bar_layout_v2",
                ref layout,
                new[] { "Horizontal", "Vertical" },
                idx =>
                {
                    _config.ButtonBarLayout = (ButtonBarLayout)idx;
                    _save();
                }
            );
            DrawSettingsToggle("Fixed width buttons", "visual_button_bar_fixed_width", ref _config.ButtonBarFixedWidth);
            FloatInputSave("Fixed width##v2_bar_width", ref _config.ButtonBarFixedWidthValue, 50f, 600f, 200f);
            ColorRgbInputs("Background##v2_bar_bg", ref _config.ButtonBarBackgroundColor, new Vector4(0.1f, 0.1f, 0.1f, 1f));
            }
        );

        Header("General Buttons");
        Indent(
            () =>
            {
            DrawButtonStyleEditor("Default", _config.GeneralButtonDefaultStyle, ButtonStyleConfig.Default(), applyToGlobal: true);
            DrawButtonStyleEditor("Active", _config.GeneralButtonActiveStyle, ButtonStyleConfig.Active(), applyToGlobal: false);
            DrawButtonStyleEditor("Highlight", _config.GeneralButtonHighlightStyle, ButtonStyleConfig.Highlight(), applyToHighlight: true);
            }
        );

        Header("Custom Buttons");
        Indent(
            () =>
            {
                DrawButtonStyleEditor("Default", _config.CustomButtonDefaultStyle, ButtonStyleConfig.Default(), applyToCustom: true, styleId: "custom");
            }
        );

        Header("Ingame Drawer");
        Indent(
            () =>
            {
            SliderSave("Scale", ref _config.DrawLogicScale, 0.05f, 3f, 0.05f, 1f, "x");
            FloatInputSave("Offset X", ref _config.DrawLogicOffsetX, -100f, 100f, 0f);
            FloatInputSave("Offset Y", ref _config.DrawLogicOffsetY, -100f, 100f, 0f);
            FloatInputSave("Offset Z", ref _config.DrawLogicOffsetZ, -100f, 100f, 0f);
            FloatInputSave("Offset Rotation", ref _config.DrawLogicOffsetR, 0f, 359.99f, 0f);
            }
        );

        Header("Suits");
        Indent(
            () =>
            {
            ColorRgbInputs("Spades", ref _config.DrawLogicColorSpades, new Vector4(0f, 0f, 0f, 1f));
            ColorRgbInputs("Clubs", ref _config.DrawLogicColorClubs, new Vector4(0f, 0f, 0f, 1f));
            ColorRgbInputs("Hearts", ref _config.DrawLogicColorHearts, new Vector4(1f, 0f, 0f, 1f));
            ColorRgbInputs("Diamonds", ref _config.DrawLogicColorDiamonds, new Vector4(1f, 0f, 0f, 1f));
            }
        );
    }
}
