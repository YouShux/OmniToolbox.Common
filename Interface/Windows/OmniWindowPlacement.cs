using OmniToolbox.Config;

namespace OmniToolbox.UI;

public static unsafe class OmniWindowPlacement
{
    private static UIConfig? Config;
    [ThreadStatic] private static string? ScopedWindowName;

    public static bool IsMultiMonitorActive => Config is { MultiMonitorEnabled: true } &&
        (ImGui.GetIO().ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0;

    public static void Configure(UIConfig? config) => Config = config;

    public static (Vector2 Position, Vector2 Size) GetWorkArea(string? windowName = null)
    {
        windowName ??= ScopedWindowName;
        var window = windowName is null ? ImGuiP.GetCurrentWindowRead() : ImGuiP.FindWindowByName(windowName);
        return GetWorkAreaAt(window.IsNull ? ImGui.GetMainViewport().GetCenter() : window.Pos + window.Size * 0.5f);
    }

    public static (Vector2 Position, Vector2 Size) GetWorkAreaAt(Vector2 point)
    {
        var main = ImGui.GetMainViewport();
        var result = (Position: main.WorkPos, Size: main.WorkSize);
        if (!IsMultiMonitorActive)
            return result;

        var monitors = ImGui.GetPlatformIO().Monitors;
        var nearestDistance = float.MaxValue;
        for (var index = 0; index < monitors.Size; index++)
        {
            var monitor = monitors[index];
            var distance = Vector2.DistanceSquared(point, Vector2.Clamp(point, monitor.WorkPos, monitor.WorkPos + monitor.WorkSize));
            if (distance >= nearestDistance)
                continue;
            nearestDistance = distance;
            result          = (monitor.WorkPos, monitor.WorkSize);
        }
        return result;
    }

    public static Vector2 ClampPosition
    (
        Vector2 position,
        Vector2 size,
        bool    allowHalfOutside
    )
    {
        var work = GetWorkAreaAt(position + size * 0.5f);
        return OmniWindowGeometry.ClampPosition(position, size, work.Position, work.Size, allowHalfOutside);
    }

    public static void SetNextWindowPosition
    (
        string   windowName,
        Vector2? position = null
    )
    {
        if (!IsMultiMonitorActive)
            ImGui.SetNextWindowViewport(ImGui.GetMainViewport().ID);

        var window = ImGuiP.FindWindowByName(windowName);
        if (window.IsNull && position is null)
            return;

        var size = window.IsNull ? Vector2.One : window.Size;
        if (!window.IsNull && (window.Flags & ImGuiWindowFlags.AlwaysAutoResize) != 0)
            size = ImGuiP.CalcWindowNextAutoFitSize(window);
        var nextWindow = new ImGuiContextPtr(ImGui.GetCurrentContext()).NextWindowData;
        if ((nextWindow.Flags & ImGuiNextWindowDataFlags.HasSize) != 0 &&
            (window.IsNull || nextWindow.SizeCond == ImGuiCond.Always ||
             (window.SetWindowSizeAllowFlags & nextWindow.SizeCond) != 0))
        {
            if (nextWindow.SizeVal.X > 0f)
                size.X = nextWindow.SizeVal.X;
            if (nextWindow.SizeVal.Y > 0f)
                size.Y = nextWindow.SizeVal.Y;
        }
        ImGui.SetNextWindowPos(ClampPosition(position ?? window.Pos, size, true));
    }

    public readonly struct WindowScope : IDisposable
    {
        private readonly string? previous;

        public WindowScope(string windowName)
        {
            previous         = ScopedWindowName;
            ScopedWindowName = windowName;
        }

        public void Dispose() => ScopedWindowName = previous;
    }
}
