namespace OmniToolbox.UI.Theme;

internal static class MaterialPainter
{
    public static void Draw
    (
        ImDrawListPtr drawList,
        Vector2       pos,
        Vector2       size,
        Vector4       fill,
        float         radius,
        float         opacity,
        bool          sampleBackground,
        ImDrawFlags   corners,
        uint          interactionID = 0,
        bool          selected = false
    )
    {
        if (size.X <= 1f || size.Y <= 1f || opacity <= 0f)
        {
            return;
        }

        opacity = Math.Clamp(opacity, 0f, 1f);
        radius  = Math.Clamp(radius, 0f, MathF.Min(size.X, size.Y) * 0.5f);
        var extent = OmniTheme.Scale(sampleBackground ? 6f : 3f);
        if (pos.X - extent > drawList.GetClipRectMax().X || pos.Y - extent > drawList.GetClipRectMax().Y ||
            pos.X + size.X + extent < drawList.GetClipRectMin().X || pos.Y + size.Y + extent < drawList.GetClipRectMin().Y)
        {
            return;
        }
        if (OmniTheme.IsGlass && OmniTheme.GlassPainter?.Invoke(
                drawList, pos, size, fill, radius, opacity, sampleBackground, corners, interactionID, selected) == true)
        {
            return;
        }

        if (sampleBackground && !OmniTheme.HasCustomBackground)
        {
            fill.W = MathF.Max(fill.W, 0.96f);
        }
        drawList.AddRectFilled(pos, pos + size, OmniTheme.Color(fill with
        {
            W = fill.W * opacity
        }), radius, corners);
        drawList.AddRect(pos, pos + size,
            OmniTheme.Color(OmniTheme.Tokens.Border with
            {
                W = OmniTheme.Tokens.Border.W * opacity
            }),
            radius, corners, OmniTheme.BorderThickness());
    }
}
