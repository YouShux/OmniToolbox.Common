using OmniToolbox.Host;
using OmniToolbox.UI.Controls;
using OmniToolbox.UI.Theme;
using TreeToolKit;

namespace OmniToolbox.UI;

public static class OmniWindowChrome
{
    private static readonly Dictionary<string, string> DragIDs = new(StringComparer.Ordinal);
    private static EscapeCloseController? ImGuiEscapeCloseController;

    internal static void ClearDisplayCaches()
    {
        DragIDs.Clear();
        ImGuiEscapeCloseController = null;
    }

    public static bool IsTextInputActive => ImGui.GetIO().WantTextInput;

    private static bool IsEscapeReservedForInput =>
        IsTextInputActive || ImGui.IsAnyItemActive() ||
        ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel);

    public static bool Begin(string windowID, ImGuiWindowFlags flags, bool collapsed) =>
        Begin(windowID, flags, collapsed, false);

    public static bool Begin(string windowID, ImGuiWindowFlags flags, bool collapsed, bool compact)
    {
        ImGui.SetNextWindowCollapsed(false, ImGuiCond.Always);
        var fixedSize = collapsed || compact;
        using var minimum = OmniControls.ControlScope.PushStyle(ImGuiStyleVar.WindowMinSize,
            fixedSize ? Vector2.One : ImGui.GetStyle().WindowMinSize);
        return ImGui.Begin(windowID, fixedSize ? flags | ImGuiWindowFlags.NoResize : flags);
    }

    public static bool Begin(string windowID, ref bool open, ImGuiWindowFlags flags, bool collapsed)
    {
        ImGui.SetNextWindowCollapsed(false, ImGuiCond.Always);
        using var minimum = OmniControls.ControlScope.PushStyle(ImGuiStyleVar.WindowMinSize,
            collapsed ? Vector2.One : ImGui.GetStyle().WindowMinSize);
        return ImGui.Begin(windowID, ref open, collapsed ? flags | ImGuiWindowFlags.NoResize : flags);
    }

    public static void UpdateEscapeClose
    (
        EscapeCloseController  controller,
        IEscapeClosableWindow  first,
        IEscapeClosableWindow? second = null,
        IEscapeClosableWindow? third = null
    )
    {
        var sharedController = ImGuiEscapeCloseController ??= new(DalamudServices.KeyState);
        var sharedWasSuppressingEscape = sharedController.IsSuppressingEscape;
        sharedController.UpdateSuppression();
        if (sharedWasSuppressingEscape)
        {
            controller.TryConsumeEscape(false);
            return;
        }
        var wasSuppressingEscape = controller.IsSuppressingEscape;
        if (IsEscapeReservedForInput)
        {
            controller.TryConsumeEscape(false);
            sharedController.TryConsumeEscape(false);
        }
        else
            controller.Update(first, second, third);

        // 同步已消费的按键，避免同一次 Escape 继续关闭后方窗口。
        if (wasSuppressingEscape || controller.IsSuppressingEscape)
            sharedController.TryConsumeEscape(false);
    }

    public static bool ShouldCloseOnEscape()
    {
        var controller = ImGuiEscapeCloseController ??= new(DalamudServices.KeyState);
        if (IsEscapeReservedForInput)
        {
            controller.TryConsumeEscape(false);
            return false;
        }

        return ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows | ImGuiFocusedFlags.NoPopupHierarchy) &&
               controller.TryConsumeEscape(true);
    }

    public static (Vector2 Position, Vector2 Size) GetFrameBounds
    (
        Vector2 windowPosition,
        Vector2 windowSize,
        bool    isCollapsed
    )
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

    public static (Vector2 Position, Vector2 Size) GetContentBounds
    (
        Vector2 framePosition,
        Vector2 frameSize
    ) =>
        (framePosition + new Vector2(OmniTheme.WindowInset(), OmniTheme.TitleBarHeight() + OmniTheme.WindowInset()),
         new Vector2(
             MathF.Max(1f, frameSize.X - OmniTheme.WindowInset() * 2f),
             MathF.Max(1f, frameSize.Y - OmniTheme.TitleBarHeight() - OmniTheme.WindowInset() * 2f)));

    public static void SetNextWindowPosition(string windowName) => OmniWindowPlacement.SetNextWindowPosition(windowName);

    public static void SetNextWindowPosition
    (
        string  windowName,
        Vector2 position
    ) =>
        OmniWindowPlacement.SetNextWindowPosition(windowName, position);

    public static unsafe void DragBackground()
    {
        if (!ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows | ImGuiHoveredFlags.NoPopupHierarchy))
            return;

        var context = new ImGuiContextPtr(ImGui.GetCurrentContext());
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
            context.ActiveId == 0 && context.HoveredId == 0 && !context.HoveredIdDisabled)
        {
            var window = ImGuiP.GetCurrentWindow();
            var flags = window.Flags;
            window.Flags &= ~ImGuiWindowFlags.NoMove;
            ImGuiP.StartMouseMovingWindow(window);
            window.Flags = flags;
        }

        var position = ImGui.GetWindowPos();
        var clamped = OmniWindowPlacement.ClampPosition(position, ImGui.GetWindowSize(), true);
        if (position != clamped)
            ImGui.SetWindowPos(clamped);
    }

    public static void CenterNextWindow(string windowName, Vector2 defaultSize)
    {
        var work = OmniWindowPlacement.GetWorkArea();
        var window = ImGuiP.FindWindowByName(windowName);
        var size = window.IsNull ? defaultSize : window.Size;

        ImGui.SetNextWindowPos(
            OmniWindowPlacement.ClampPosition(work.Position + (work.Size - size) * 0.5f, size, false),
            ImGuiCond.Always);
    }

    public static Vector2 DragTitleBar
    (
        string  id,
        Vector2 position,
        Vector2 size
    )
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

    public static OmniWindowChromeResult Draw
    (
        Vector2 framePosition,
        Vector2 frameSize,
        bool    isCollapsed,
        string  title,
        string  collapseID,
        string  closeID,
        bool    drawTitle = true
    )
    {
        var titleBarSize = new Vector2(frameSize.X, OmniTheme.TitleBarHeight());
        if (!DragIDs.TryGetValue(collapseID, out var dragID))
        {
            if (DragIDs.Count >= 256)
                DragIDs.Clear();
            dragID = $"{collapseID}Drag";
            DragIDs.Add(collapseID, dragID);
        }
        framePosition = DragTitleBar(dragID, framePosition,
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

        return new(collapseClicked, closeClicked || ShouldCloseOnEscape(), titleDoubleClicked);
    }
}
