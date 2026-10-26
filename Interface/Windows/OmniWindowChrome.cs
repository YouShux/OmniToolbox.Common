using TreeToolKit;
using OmniToolbox.UI.Controls;
using OmniToolbox.UI.Theme;

namespace OmniToolbox.UI;

public static class OmniWindowChrome
{
    public static bool IsTextInputActive => ImGui.GetIO().WantTextInput;

    public static void UpdateEscapeClose(
        EscapeCloseController controller,
        IEscapeClosableWindow first,
        IEscapeClosableWindow? second = null,
        IEscapeClosableWindow? third = null)
    {
        if (IsTextInputActive)
        {
            controller.TryConsumeEscape(false);
            return;
        }

        controller.Update(first, second, third);
    }

    public static (Vector2 Position, Vector2 Size) GetFrameBounds(
        Vector2 windowPosition, Vector2 windowSize, bool isCollapsed)
    {
        var position = isCollapsed
            ? windowPosition + new Vector2(OmniTheme.CollapsedHeaderSafeInset(), OmniTheme.CollapsedHeaderTop())
            : windowPosition + new Vector2(OmniTheme.ChromeFrameInset());
        var size = isCollapsed
            ? new Vector2(
                MathF.Max(1f, windowSize.X - OmniTheme.CollapsedHeaderSafeInset() * 2f),
                OmniTheme.TitleBarHeight())
            : windowSize - new Vector2(OmniTheme.ChromeFrameInset() * 2f);
        return (position, size);
    }

    public static (Vector2 Position, Vector2 Size) GetContentBounds(Vector2 framePosition, Vector2 frameSize) =>
        (framePosition + new Vector2(OmniTheme.WindowInset(), OmniTheme.TitleBarHeight() + OmniTheme.WindowInset()),
         new Vector2(
             MathF.Max(1f, frameSize.X - OmniTheme.WindowInset() * 2f),
             MathF.Max(1f, frameSize.Y - OmniTheme.TitleBarHeight() - OmniTheme.WindowInset() * 2f)));

    public static void SetNextWindowPosition(string windowName) => OmniWindowPlacement.SetNextWindowPosition(windowName);

    public static void SetNextWindowPosition(string windowName, Vector2 position) =>
        OmniWindowPlacement.SetNextWindowPosition(windowName, position);

    public static unsafe void DragBackground()
    {
        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left) ||
            !ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows | ImGuiHoveredFlags.NoPopupHierarchy))
            return;

        var context = new ImGuiContextPtr(ImGui.GetCurrentContext());
        if (context.ActiveId != 0 || context.HoveredId != 0 || context.HoveredIdDisabled)
            return;

        var window = ImGuiP.GetCurrentWindow();
        var flags = window.Flags;
        window.Flags &= ~ImGuiWindowFlags.NoMove;
        ImGuiP.StartMouseMovingWindow(window);
        window.Flags = flags;
    }

    public static Vector2 DragTitleBar(string id, Vector2 position, Vector2 size)
    {
        ImGui.SetCursorScreenPos(position);
        ImGui.InvisibleButton(id, Vector2.Max(Vector2.One, size));
        if (ImGui.IsItemActivated())
        {
            var window = ImGuiP.GetCurrentWindow();
            var flags = window.Flags;
            window.Flags &= ~ImGuiWindowFlags.NoMove;
            ImGuiP.StartMouseMovingWindow(window);
            window.Flags = flags;
        }
        return position;
    }

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
        framePosition = DragTitleBar($"{collapseID}Drag", framePosition,
            new Vector2(titleBarSize.X - OmniTheme.TitleExpandIconRight() - ImGui.GetStyle().ItemSpacing.X, titleBarSize.Y));
        var titleDoubleClicked = ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left);
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

        return new(collapseClicked, closeClicked, titleDoubleClicked);
    }
}
