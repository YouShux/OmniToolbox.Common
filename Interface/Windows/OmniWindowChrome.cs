using OmniToolbox.UI.Controls;
using OmniToolbox.UI.Theme;

namespace OmniToolbox.UI;

public readonly record struct OmniWindowChromeResult(
    bool CollapseClicked,
    bool CloseClicked,
    bool TitleDoubleClicked)
{
    public bool ToggleCollapse => CollapseClicked || TitleDoubleClicked;
}

public static class OmniWindowChrome
{
    public static OmniWindowChromeResult Draw(
        Vector2 framePosition,
        Vector2 frameSize,
        bool isCollapsed,
        string title,
        string collapseID,
        string closeID,
        bool drawTitle = true)
    {
        var titleBarSize = new Vector2(frameSize.X, OmniTheme.TitleBarHeight());
        OmniControls.DrawWindowBackground(framePosition, frameSize, isCollapsed);
        if (drawTitle)
        {
            OmniControls.DrawTextCentered(title, framePosition, titleBarSize, OmniTheme.Tokens.Text);
        }

        ImGui.SetCursorScreenPos(
            framePosition + new Vector2(
                titleBarSize.X - OmniTheme.TitleExpandIconRight(),
                OmniTheme.TitleIconTop()));
        var collapseClicked = OmniControls.TitleTriangleButton(isCollapsed, collapseID);

        ImGui.SetCursorScreenPos(
            framePosition + new Vector2(
                titleBarSize.X - OmniTheme.TitleCloseIconRight(),
                OmniTheme.TitleIconTop()));
        var closeClicked = OmniControls.CloseButton(closeID, OmniTheme.TitleIconSize());

        var titleEnd = framePosition.X + titleBarSize.X - OmniTheme.TitleExpandIconRight() -
                       ImGui.GetStyle().ItemSpacing.X;
        var titleDoubleClicked =
            ImGui.IsMouseHoveringRect(
                framePosition,
                new Vector2(titleEnd, framePosition.Y + titleBarSize.Y)) &&
            ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left);

        return new(collapseClicked, closeClicked, titleDoubleClicked);
    }
}
