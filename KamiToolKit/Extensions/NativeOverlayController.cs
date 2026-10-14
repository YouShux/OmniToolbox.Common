using KamiToolKit.UiOverlay;

namespace OmniToolbox.UI;

internal sealed class NativeOverlayController : IDisposable
{
    private static OverlayController? SharedController;
    private static int OwnerCount;

    private readonly OverlayController controller;
    private readonly List<OverlayNode> nodes = [];
    private bool disposed;

    public NativeOverlayController()
    {
        controller = SharedController ??= new();
        OwnerCount++;
    }

    public void AddNode(OverlayNode node)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (nodes.Contains(node))
            return;

        controller.AddNode(node);
        nodes.Add(node);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        try
        {
            foreach (var node in nodes)
                controller.RemoveNode(node);
            nodes.Clear();
        }
        finally
        {
            OwnerCount--;
            if (OwnerCount == 0)
            {
                SharedController = null;
                controller.Dispose();
            }
        }
    }
}
