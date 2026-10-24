using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace BlackJackButtler.Windows;

public partial class BlackJackButtlerWindow
{
    private void DrawAutoContinueControls()
    {
        if (!_config.AutoContinue || !Plugin.AutoContinueActive) return;

        float barHeight = _config.AutoContinueBarHeight;
        if (_config.AutoContinueBarShowText && barHeight < 20f) barHeight = 20f;

        float elapsed = (float)Plugin.AutoContinueElapsedSeconds;
        float progress = Math.Clamp(elapsed / _config.AutoContinueDelay, 0f, 1f);

        var cursorPos = ImGui.GetCursorScreenPos();
        float availWidth = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();

        drawList.AddRectFilled(cursorPos, new Vector2(cursorPos.X + availWidth, cursorPos.Y + barHeight), ImGui.GetColorU32(new Vector4(0.15f, 0.15f, 0.15f, 1f)));
        drawList.AddRectFilled(cursorPos, new Vector2(cursorPos.X + availWidth * progress, cursorPos.Y + barHeight), ImGui.GetColorU32(_config.AutoContinueBarColor));

        if (_config.AutoContinueBarShowText)
        {
            float remaining = Math.Max(0f, _config.AutoContinueDelay - elapsed);
            string text = $"{remaining:F0}s";
            var textSize = ImGui.CalcTextSize(text);
            drawList.AddText(
                new Vector2(
                    cursorPos.X + (availWidth - textSize.X) * 0.5f,
                    cursorPos.Y + (barHeight - textSize.Y) * 0.5f
                ),
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)),
                text
            );
        }

        ImGui.Dummy(new Vector2(availWidth, barHeight));
    }
}
