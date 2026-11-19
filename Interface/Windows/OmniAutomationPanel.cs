using OmniToolbox.UI.Controls;
using OmniToolbox.UI.Theme;
using OmniToolbox.UI.Widgets;

namespace OmniToolbox.UI;

public static class OmniAutomationPanel
{
    private const float CONTROL_SPACING = 8f;

    public static Vector2 MeasureButtons(string runLabel, string collapseLabel) =>
        Vector2.Max(OmniControls.CompactButtonSize(runLabel, 80f),
            OmniControls.CompactButtonSize(collapseLabel, 80f));

    public static void ApplySize
    (
        OmniWindowSize windowSize,
        Vector2 defaultSize,
        Vector2 minimumSize,
        bool collapsed,
        string title,
        Vector2 buttonSize
    )
    {
        if (!windowSize.IsCompact)
        {
            windowSize.ApplyCollapsible(defaultSize, minimumSize, collapsed);
            return;
        }

        var inset = OmniTheme.WindowInset();
        var frameInset = OmniTheme.ChromeFrameInset();
        var buttonsWidth = buttonSize.X * 2f + OmniTheme.Scale(CONTROL_SPACING) + (inset + frameInset) * 2f;
        var titleWidth = OmniTextMeasurements.Measure(title).X + OmniTheme.TitleExpandIconRight() +
                         (inset + MathF.Max(frameInset, OmniTheme.CollapsedHeaderSafeInset())) * 2f;
        var width = MathF.Ceiling(MathF.Max(OmniTheme.Scale(232f), MathF.Max(buttonsWidth, titleWidth)));
        var compact = collapsed
            ? OmniTheme.CollapsedWindowSize(width)
            : OmniTheme.ClampWindowSize(new(width,
                MathF.Ceiling(OmniTheme.TitleBarHeight() + buttonSize.Y + (inset + frameInset) * 2f)));
        windowSize.Apply(defaultSize, minimumSize, compact);
    }

    public static OmniWindowChromeResult DrawChrome
    (
        Vector2 framePosition,
        Vector2 frameSize,
        bool collapsed,
        string title,
        string collapseID,
        string closeID,
        string? icon = null
    )
    {
        var chrome = OmniWindowChrome.Draw(framePosition, frameSize, collapsed, title, collapseID, closeID,
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
        return chrome;
    }

    public static Layout Arrange
    (
        Vector2 framePosition,
        Vector2 frameSize,
        Vector2 buttonSize,
        float minimumSelectorWidth = 0f
    )
        => Arrange(framePosition, frameSize, buttonSize, false, minimumSelectorWidth);

    public static Layout Arrange
    (
        Vector2 framePosition,
        Vector2 frameSize,
        Vector2 buttonSize,
        bool showCollapseButton,
        float minimumSelectorWidth = 0f
    ) => Arrange(framePosition, frameSize, buttonSize, showCollapseButton, minimumSelectorWidth, false);

    public static Layout Arrange
    (
        Vector2 framePosition,
        Vector2 frameSize,
        Vector2 buttonSize,
        bool showCollapseButton,
        float minimumSelectorWidth,
        bool compact
    )
    {
        var inset = OmniTheme.WindowInset();
        var padding = compact ? Vector2.Zero : OmniTheme.Scale(OmniTheme.Tokens.WindowPadding);
        var spacing = OmniTheme.Scale(CONTROL_SPACING);
        var controlsLeft = inset + padding.X;
        var controlsTop = OmniTheme.TitleBarHeight() + inset + padding.Y;
        var controlsWidth = MathF.Max(1f, frameSize.X - controlsLeft * 2f);
        var availableButtonWidth = showCollapseButton ? MathF.Max(1f, (controlsWidth - spacing) * 0.5f) : controlsWidth;
        var runWidth = compact ? MathF.Max(1f, MathF.Floor(availableButtonWidth)) : MathF.Min(buttonSize.X, availableButtonWidth);
        var buttonsWidth = showCollapseButton ? runWidth * 2f + spacing : runWidth;
        var wrap = !compact && minimumSelectorWidth > 0f && controlsWidth < minimumSelectorWidth + buttonsWidth + spacing;
        var buttonsTop = controlsTop + (wrap ? buttonSize.Y + spacing : 0f);
        var buttonsLeft = controlsLeft + (compact ? (controlsWidth - buttonsWidth) * 0.5f : controlsWidth - buttonsWidth);
        var contentTop = buttonsTop + buttonSize.Y + inset + padding.Y;
        if (!compact)
        {
            var (bodyPosition, bodySize) = OmniWindowChrome.GetContentBounds(framePosition, frameSize);
            OmniControls.DrawPanelBackground(bodyPosition, bodySize, OmniTheme.Tokens.Surface);
        }
        return new(
            framePosition + new Vector2(controlsLeft, controlsTop),
            MathF.Max(1f, controlsWidth - (wrap ? 0f : buttonsWidth + spacing)),
            framePosition + new Vector2(buttonsLeft, buttonsTop),
            new Vector2(runWidth, buttonSize.Y),
            framePosition + new Vector2(inset, contentTop),
            new Vector2(MathF.Max(1f, frameSize.X - inset * 2f),
                MathF.Max(1f, frameSize.Y - contentTop - inset)))
        {
            CollapsePosition = framePosition + new Vector2(buttonsLeft + runWidth + spacing, buttonsTop)
        };
    }

    public static bool DrawRunButton
    (
        Layout layout,
        string runLabel,
        bool running,
        bool runDisabled = false
    )
    {
        ImGui.SetCursorScreenPos(layout.RunPosition);
        using var disabled = ImRaii.Disabled(runDisabled);
        return OmniControls.SmallButton(runLabel, running, layout.RunSize);
    }

    public static void DrawStatusLine
    (
        string? status = null,
        string? notice = null,
        Vector4? noticeColor = null,
        bool showStatusLabel = false
    )
    {
        var detail = string.IsNullOrWhiteSpace(notice) ? string.Empty : notice.ReplaceLineEndings(" ");
        var color = noticeColor ?? OmniTheme.Tokens.Error;
        var message = string.IsNullOrWhiteSpace(status) || (detail.Length > 0 && color == OmniTheme.Tokens.Error)
            ? string.Empty
            : showStatusLabel ? string.Format(OmniLoc.Get("Common.StatusLine"), status) : status;
        var text = message.ReplaceLineEndings(" ");
        if (text.Length == 0 && detail.Length == 0) return;

        const string SEPARATOR = " · ";
        var prefix = text.Length > 0 && detail.Length > 0 ? text + SEPARATOR : text;
        var prefixWidth = ImGui.CalcTextSize(prefix).X;
        var detailWidth = ImGui.CalcTextSize(detail).X;
        var width = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var clipped = prefixWidth + detailWidth > width;
        if (clipped && detail.Length > 0)
        {
            prefix = string.Empty;
            prefixWidth = 0f;
        }

        var position = ImGui.GetCursorScreenPos();
        var size = new Vector2(MathF.Min(width, prefixWidth + detailWidth), ImGui.GetTextLineHeight());
        ImGui.Dummy(size);

        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(position, position + size, true);
        if (prefix.Length > 0)
            drawList.AddText(position, ImGui.GetColorU32(ImGuiCol.Text), prefix);
        if (detail.Length > 0)
            drawList.AddText(position + new Vector2(prefixWidth, 0f),
                ImGui.GetColorU32(color), detail);
        drawList.PopClipRect();

        if (clipped || text != message || (detail.Length > 0 && detail != notice))
            OmniControls.HelpTooltip(() => ImGui.TextWrapped(message.Length == 0 ? notice ?? string.Empty :
                detail.Length == 0 ? message : $"{message}{SEPARATOR}{notice}"));
    }

    public static (bool RunClicked, bool CompactClicked) DrawButtons
    (
        Layout layout,
        string runLabel,
        bool running,
        string collapseLabel,
        bool runDisabled = false
    )
    {
        var runClicked = DrawRunButton(layout, runLabel, running, runDisabled);
        ImGui.SetCursorScreenPos(layout.CollapsePosition);
        var compactClicked = OmniControls.SmallButton(collapseLabel, false, layout.RunSize);
        return (runClicked, compactClicked);
    }

    public readonly record struct Layout
    (
        Vector2 SelectorPosition,
        float SelectorWidth,
        Vector2 RunPosition,
        Vector2 RunSize,
        Vector2 ContentPosition,
        Vector2 ContentSize
    )
    {
        internal Vector2 CollapsePosition { get; init; }
    }
}
