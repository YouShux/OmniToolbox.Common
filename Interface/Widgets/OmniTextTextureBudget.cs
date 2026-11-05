namespace OmniToolbox.UI.Widgets;

internal static class OmniTextTextureBudget
{
    private static readonly Dictionary<uint, WindowBudget> Windows = [];
    private static readonly List<uint> ExpiredWindows = [];
    private static long TotalBytes;
    private static int TotalTextures;
    private static int BuildFrame = -1;
    private static int Builds;

    internal static unsafe uint GetWindowOwner() => ImGuiP.GetCurrentWindowRead().RootWindow.ID;

    internal static bool TryReserve(uint owner, long size, int frame)
    {
        if (BuildFrame != frame)
        {
            BuildFrame = frame;
            Builds = 0;
            foreach (var (windowID, budget) in Windows)
                if (budget.Textures == 0)
                    ExpiredWindows.Add(windowID);
            foreach (var windowID in ExpiredWindows)
                Windows.Remove(windowID);
            ExpiredWindows.Clear();
        }
        if (size <= 0 || size > MAX_WINDOW_BYTES || TotalBytes + size > MAX_BYTES ||
            TotalTextures >= MAX_TEXTURES || Builds >= MAX_BUILDS_PER_FRAME)
        {
            return false;
        }
        if (!Windows.TryGetValue(owner, out var window))
        {
            window = new();
            Windows.Add(owner, window);
        }
        if (window.BuildFrame != frame)
        {
            window.BuildFrame = frame;
            window.Builds = 0;
        }
        if (window.Bytes + size > MAX_WINDOW_BYTES || window.Textures >= MAX_WINDOW_TEXTURES ||
            window.Builds >= MAX_WINDOW_BUILDS_PER_FRAME)
        {
            return false;
        }
        window.Bytes += size;
        TotalBytes += size;
        window.Textures++;
        TotalTextures++;
        window.Builds++;
        Builds++;
        return true;
    }

    internal static void Release(uint owner, long size)
    {
        var window = Windows[owner];
        window.Bytes -= size;
        TotalBytes -= size;
        window.Textures--;
        TotalTextures--;
        if (window.Textures == 0 && window.BuildFrame != BuildFrame)
            Windows.Remove(owner);
    }

    private sealed class WindowBudget
    {
        internal long Bytes;
        internal int Textures;
        internal int BuildFrame = -1;
        internal int Builds;
    }

    #region 常量

    private const long MAX_BYTES = 32 * 1024 * 1024;
    private const int MAX_TEXTURES = 1024;
    private const int MAX_BUILDS_PER_FRAME = 4;
    private const long MAX_WINDOW_BYTES = 8 * 1024 * 1024;
    private const int MAX_WINDOW_TEXTURES = 256;
    private const int MAX_WINDOW_BUILDS_PER_FRAME = 2;

    #endregion
}
