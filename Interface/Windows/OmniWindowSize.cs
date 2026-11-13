using OmniToolbox.UI.Theme;

namespace OmniToolbox.UI;

public sealed class OmniWindowSize
{
    private readonly string windowID;
    private readonly Func<Vector2>? readSize;
    private readonly Action<Vector2>? writeSize;
    private float lastScale;
    private Vector2 lastWorkArea;
    private Vector2 preferredSize;
    private Vector2 appliedSize;
    private bool restoring;

    public OmniWindowSize(string windowID) => this.windowID = windowID;

    public OmniWindowSize(string windowID, Func<Vector2> readSize, Action<Vector2> writeSize) : this(windowID)
    {
        this.readSize = readSize;
        this.writeSize = writeSize;
    }

    public Vector2 GetExpandedSize(Vector2 defaultSize)
    {
        if (readSize is null)
            return OmniWindowPlacement.GetExpandedSize(windowID, defaultSize);

        var size = readSize();
        return float.IsFinite(size.X) && size.X > 0f && float.IsFinite(size.Y) && size.Y > 0f ? size : defaultSize;
    }

    public void Restore() => lastScale = 0f;

    public void Apply(Vector2 defaultSize, Vector2 minimumSize, Vector2? compactSize = null)
    {
        if (compactSize is { } compact)
        {
            Restore();
            restoring = true;
            ImGui.SetNextWindowSize(compact, ImGuiCond.Always);
            return;
        }

        var scale = OmniTheme.ScaleValue;
        var workArea = Vector2.Max(Vector2.One, OmniWindowPlacement.GetWorkArea(windowID).Size);
        restoring = scale != lastScale || workArea != lastWorkArea;
        lastScale = scale;
        lastWorkArea = workArea;

        var minimum = Vector2.Clamp(OmniTheme.Scale(minimumSize), Vector2.One, workArea);
        preferredSize = OmniTheme.Scale(GetExpandedSize(defaultSize));
        appliedSize = Vector2.Clamp(preferredSize, minimum, workArea);
        ImGui.SetNextWindowSizeConstraints(minimum, workArea);
        if (restoring && preferredSize != Vector2.Zero)
            ImGui.SetNextWindowSize(appliedSize, ImGuiCond.Always);
    }

    public void SaveAfterResize()
    {
        if (restoring || ImGui.IsWindowDocked() || ImGui.IsMouseDown(ImGuiMouseButton.Left)) return;

        var actual = ImGui.GetWindowSize();
        var size = preferredSize == Vector2.Zero ? actual : new Vector2(
            MathF.Abs(actual.X - appliedSize.X) < 1f ? preferredSize.X : actual.X,
            MathF.Abs(actual.Y - appliedSize.Y) < 1f ? preferredSize.Y : actual.Y);
        if (size == preferredSize) return;

        var unscaledSize = OmniTheme.Unscale(size);
        if (writeSize is { } write)
            write(unscaledSize);
        else
            OmniWindowPlacement.SaveExpandedSize(windowID, unscaledSize);
    }
}
