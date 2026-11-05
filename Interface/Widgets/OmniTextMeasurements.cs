using Dalamud.Interface;
using TreeToolKit;

namespace OmniToolbox.UI.Widgets;

internal static class OmniTextMeasurements
{
    private static readonly Dictionary<Key, Vector2> Sizes = [];
    private static readonly Queue<Key> Order = new();
    private static long FontRevision = -1;
    private static long LocalizationRevision = -1;

    internal static unsafe Vector2 Measure(string text, bool hideTextAfterDoubleHash = false, float wrapWidth = -1f)
    {
        var key = new Key((nint)ImGui.GetFont().Handle, ImGui.GetFontSize(), text, wrapWidth, hideTextAfterDoubleHash, false);
        Refresh();
        if (Sizes.TryGetValue(key, out var size))
            return size;
        size = ImGui.CalcTextSize(text, hideTextAfterDoubleHash, wrapWidth);
        Add(key, size);
        return size;
    }

    internal static unsafe Vector2 MeasureAtSize(string text, ImFontPtr font, float fontSize, float wrapWidth = 0f)
    {
        var key = new Key((nint)font.Handle, fontSize, text, wrapWidth, false, true);
        Refresh();
        if (Sizes.TryGetValue(key, out var size))
            return size;
        size = ImGui.CalcTextSizeA(font, fontSize, float.MaxValue, wrapWidth, text, out _);
        Add(key, size);
        return size;
    }

    internal static void Clear()
    {
        Sizes.Clear();
        Order.Clear();
    }

    private static void Refresh()
    {
        if (FontRevision == Fonts.Revision && LocalizationRevision == OmniLoc.Revision)
            return;
        Clear();
        FontRevision = Fonts.Revision;
        LocalizationRevision = OmniLoc.Revision;
    }

    private static void Add(Key key, Vector2 size)
    {
        if (Sizes.Count >= MAX_ENTRIES)
            Sizes.Remove(Order.Dequeue());
        Sizes.Add(key, size);
        Order.Enqueue(key);
    }

    private readonly record struct Key(nint Font, float Size, string Text, float WrapWidth, bool HideID, bool ExplicitSize);

    #region 常量

    private const int MAX_ENTRIES = 2048;

    #endregion
}
