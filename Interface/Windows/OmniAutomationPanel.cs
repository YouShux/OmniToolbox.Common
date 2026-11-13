using OmniToolbox.UI.Controls;
using OmniToolbox.UI.Theme;
using OmniToolbox.UI.Widgets;

namespace OmniToolbox.UI;

public static class OmniAutomationPanel
{
    public static Vector2 MeasureButtons(string runLabel, string actionLabel) =>
        Vector2.Max(OmniControls.CompactButtonSize(runLabel, 80f),
            OmniControls.CompactButtonSize(actionLabel, 80f));

    public static Vector2 MeasureCompactSize(string title, Vector2 buttonSize)
    {
        var inset = OmniTheme.WindowInset();
        var frameInset = OmniTheme.ChromeFrameInset();
        return OmniTheme.ClampWindowSize(new Vector2(
            MathF.Max(buttonSize.X * 2f + OmniTheme.Scale(8f) + (inset + frameInset) * 2f,
                OmniTextMeasurements.Measure(title).X + OmniTheme.TitleExpandIconRight() * 2f + inset * 2f),
            OmniTheme.TitleBarHeight() + buttonSize.Y + (inset + frameInset) * 2f));
    }

    public static OmniWindowChromeResult DrawChrome
    (
        Vector2 framePosition,
        Vector2 frameSize,
        string title,
        string collapseID,
        string closeID,
        string? icon = null
    )
    {
        var chrome = OmniWindowChrome.Draw(framePosition, frameSize, false, title, collapseID, closeID,
            drawTitle: false);
        var inset = OmniTheme.WindowInset();
        var titlePosition = framePosition + new Vector2(inset,
            (OmniTheme.TitleBarHeight() - ImGui.GetTextLineHeight()) * 0.5f);
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(framePosition,
            framePosition + new Vector2(frameSize.X - OmniTheme.TitleExpandIconRight() - inset,
                OmniTheme.TitleBarHeight()), true);
        if (!string.IsNullOrEmpty(icon))
        {
            drawList.AddText(titlePosition, OmniTheme.Color(OmniTheme.Tokens.Text), icon);
            titlePosition.X += OmniTextMeasurements.Measure(icon).X + ImGui.GetStyle().ItemSpacing.X;
        }
        drawList.AddText(titlePosition, OmniTheme.Color(OmniTheme.Tokens.Text), title);
        drawList.PopClipRect();
        var escape = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) &&
                     !OmniWindowChrome.IsTextInputActive && !ImGui.IsAnyItemActive() &&
                     ImGui.IsKeyPressed(ImGuiKey.Escape, false);
        return chrome with { CloseClicked = chrome.CloseClicked || escape };
    }

    public static Layout Arrange
    (
        Vector2 framePosition,
        Vector2 frameSize,
        bool collapsed,
        Vector2 buttonSize,
        float minimumSelectorWidth = 0f
    )
    {
        var inset = OmniTheme.WindowInset();
        var padding = collapsed ? Vector2.Zero : OmniTheme.Scale(OmniTheme.Tokens.WindowPadding);
        var spacing = OmniTheme.Scale(8f);
        var controlsLeft = inset + padding.X;
        var controlsTop = OmniTheme.TitleBarHeight() + inset + padding.Y;
        var controlsWidth = MathF.Max(1f, frameSize.X - controlsLeft * 2f);
        var actionWidth = MathF.Max(1f, MathF.Min(buttonSize.X, (controlsWidth - spacing) * 0.5f));
        var runWidth = collapsed ? MathF.Max(1f, controlsWidth - actionWidth - spacing) : actionWidth;
        var buttonsWidth = runWidth + actionWidth + spacing;
        var wrap = !collapsed && minimumSelectorWidth > 0f &&
                   controlsWidth < minimumSelectorWidth + buttonsWidth + spacing;
        var buttonsTop = controlsTop + (wrap ? buttonSize.Y + spacing : 0f);
        var buttonsLeft = collapsed ? controlsLeft : controlsLeft + controlsWidth - buttonsWidth;
        var contentTop = buttonsTop + buttonSize.Y + inset + padding.Y;
        if (!collapsed)
        {
            var (bodyPosition, bodySize) = OmniWindowChrome.GetContentBounds(framePosition, frameSize);
            OmniControls.DrawPanelBackground(bodyPosition, bodySize, OmniTheme.Tokens.Surface);
        }
        return new(
            framePosition + new Vector2(controlsLeft, controlsTop),
            MathF.Max(1f, controlsWidth - (wrap ? 0f : buttonsWidth + spacing)),
            framePosition + new Vector2(buttonsLeft, buttonsTop),
            new Vector2(runWidth, buttonSize.Y),
            framePosition + new Vector2(buttonsLeft + runWidth + spacing, buttonsTop),
            new Vector2(actionWidth, buttonSize.Y),
            framePosition + new Vector2(inset, contentTop),
            new Vector2(MathF.Max(1f, frameSize.X - inset * 2f),
                MathF.Max(1f, frameSize.Y - contentTop - inset)));
    }

    public static (bool RunClicked, bool CollapseClicked) DrawButtons
    (
        Layout layout,
        string runLabel,
        bool running,
        string actionLabel,
        bool runDisabled = false
    )
    {
        ImGui.SetCursorScreenPos(layout.RunPosition);
        bool runClicked;
        using (ImRaii.Disabled(runDisabled))
            runClicked = OmniControls.SmallButton(runLabel, running, layout.RunSize);
        ImGui.SetCursorScreenPos(layout.ActionPosition);
        var collapseClicked = OmniControls.SmallButton(actionLabel, false, layout.ActionSize);
        return (runClicked, collapseClicked);
    }

    public readonly record struct Layout
    (
        Vector2 SelectorPosition,
        float SelectorWidth,
        Vector2 RunPosition,
        Vector2 RunSize,
        Vector2 ActionPosition,
        Vector2 ActionSize,
        Vector2 ContentPosition,
        Vector2 ContentSize
    );
}
