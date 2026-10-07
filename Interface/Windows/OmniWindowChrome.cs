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
    public static unsafe void SetNextWindowPosition(string windowName)
    {
        var window = ImGuiP.FindWindowByName(windowName);
        if (window.IsNull)
            return;

        var size = window.Size;
        var nextWindow = new ImGuiContextPtr(ImGui.GetCurrentContext()).NextWindowData;
        if ((nextWindow.Flags & ImGuiNextWindowDataFlags.HasSize) != 0 &&
            (nextWindow.SizeCond == ImGuiCond.Always ||
             (window.SetWindowSizeAllowFlags & nextWindow.SizeCond) != 0))
        {
            if (nextWindow.SizeVal.X > 0f)
                size.X = nextWindow.SizeVal.X;
            if (nextWindow.SizeVal.Y > 0f)
                size.Y = nextWindow.SizeVal.Y;
        }

        // 在 Begin 生成裁剪区域之前约束位置，使内容与窗口边界保持一致。
        ImGui.SetNextWindowPos(OmniTheme.ClampWindowPosition(window.Pos, size));
    }

    public static unsafe void DragBackground()
    {
        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left) ||
            !ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows))
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
