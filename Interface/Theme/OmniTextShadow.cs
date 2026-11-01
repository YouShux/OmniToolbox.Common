using System.Text;

namespace OmniToolbox.UI.Theme;

internal static class OmniTextShadow
{
    internal static readonly Vector4 Color = new(0.04f, 0.05f, 0.06f, 0.9f);

    private static readonly Vector2[] EdgeOffsets =
    [
        new(-1f, -1f),
        new(0f, -1f),
        new(1f, -1f),
        new(-1f, 0f),
        new(1f, 0f),
        new(-1f, 1f),
        new(0f, 1f),
        new(1f, 1f)
    ];

    internal static void Draw
    (
        ImDrawListPtr drawList,
        string        text,
        Vector2       position,
        float         fontSize,
        float         opacity = 1f
    )
    {
        if (text.Length == 0 || opacity <= 0f)
            return;

        var color = Color;
        color.W *= opacity;
        var capacity = Encoding.UTF8.GetMaxByteCount(text.Length);
        var buffer = capacity <= 1024 ? stackalloc byte[capacity] : new byte[capacity];
        ReadOnlySpan<byte> encoded = buffer[..Encoding.UTF8.GetBytes(text, buffer)];
        drawList.AddText(ImGui.GetFont(), fontSize, position + Vector2.UnitY, ImGui.GetColorU32(color), encoded);
        color.W *= EDGE_STRENGTH;
        var edgeColor = ImGui.GetColorU32(color);
        foreach (var offset in EdgeOffsets)
        {
            drawList.AddText(ImGui.GetFont(), fontSize, position + offset, edgeColor, encoded);
        }
    }

    #region 常量

    internal const float EDGE_STRENGTH = 0.28f;

    #endregion
}
