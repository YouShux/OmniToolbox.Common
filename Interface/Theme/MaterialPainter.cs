namespace OmniToolbox.UI.Theme;

internal static class MaterialPainter
{
    private static readonly Vector3 OfficeBlue = new(56f / 255f, 189f / 255f, 248f / 255f);
    private static readonly Vector3 OfficePink = new(244f / 255f, 114f / 255f, 182f / 255f);
    private static readonly Vector3 OfficeViolet = new(192f / 255f, 132f / 255f, 252f / 255f);
    private static readonly Vector3 OfficeLight = new(0.96f, 0.853333f, 0.64f);
    private static readonly (Vector2 Center, Vector3 Color)[] OfficeFields =
    [
        (new(0.80f, 0.55f), OfficeViolet),
        (new(0.69f, 0.34f), OfficePink),
        (new(0.08f, 0.06f), OfficeBlue),
        (new(0.41f, 0.38f), OfficeViolet),
        (new(0.86f, 0.85f), OfficePink),
        (new(0.82f, 0.18f), OfficeBlue),
        (new(0.51f, 0.04f), OfficePink)
    ];
    private const int CONTOUR_CAPACITY = 240;
    private const int GEOMETRY_CACHE_CAPACITY = 256;
    private static readonly Dictionary<GeometryKey, Geometry> GeometryCache = [];

    internal static void Clear() => GeometryCache.Clear();

    public static void Draw(
        ImDrawListPtr drawList, Vector2 pos, Vector2 size, Vector4 fill, float radius,
        float opacity, bool sampleBackground, ImDrawFlags corners, uint interactionID = 0, bool selected = false)
    {
        if (size.X <= 1f || size.Y <= 1f || opacity <= 0f)
        {
            return;
        }

        opacity = Math.Clamp(opacity, 0f, 1f);
        radius = Math.Clamp(radius, 0f, MathF.Min(size.X, size.Y) * 0.5f);
        var extent = OmniTheme.Scale(OmniTheme.IsGlass ? sampleBackground ? 6f : 3f : 14f);
        if (pos.X - extent > drawList.GetClipRectMax().X || pos.Y - extent > drawList.GetClipRectMax().Y ||
            pos.X + size.X + extent < drawList.GetClipRectMin().X || pos.Y + size.Y + extent < drawList.GetClipRectMin().Y)
        {
            return;
        }
        if (OmniTheme.IsGlass)
        {
            if (OmniTheme.GlassPainter?.Invoke(
                    drawList, pos, size, fill, radius, opacity, sampleBackground, corners, interactionID, selected) == true)
            {
                return;
            }

            if (sampleBackground && !OmniTheme.HasCustomBackground)
            {
                fill.W = MathF.Max(fill.W, 0.96f);
            }
            drawList.AddRectFilled(pos, pos + size, OmniTheme.Color(fill with { W = fill.W * opacity }), radius, corners);
            drawList.AddRect(pos, pos + size,
                OmniTheme.Color(OmniTheme.Tokens.Border with { W = OmniTheme.Tokens.Border.W * opacity }),
                radius, corners, OmniTheme.BorderThickness());
            return;
        }
        if (sampleBackground)
        {
            var offset = OmniTheme.Scale(new Vector2(0f, 2f));
            drawList.AddRectFilled(pos + offset, pos + size + offset,
                OmniTheme.Color(OmniTheme.Tokens.Shadow with { W = OmniTheme.Tokens.Shadow.W * opacity }), radius, corners);
        }
        DrawGradient(drawList, pos, size, radius, fill with { W = fill.W * opacity },
            new Vector4(fill.X * 0.96f, fill.Y * 0.96f, fill.Z * 0.96f, fill.W * opacity), corners);
        DrawOfficeRim(drawList, pos, size, radius, opacity, interactionID, corners);
    }

    private static Geometry GetGeometry(Vector2 size, float radius, ImDrawFlags corners)
    {
        var key = new GeometryKey(size, radius, OmniTheme.ScaleValue, corners,
            OmniTheme.Tokens.Border, OmniTheme.HasCustomBorder);
        if (GeometryCache.TryGetValue(key, out var geometry))
        {
            return geometry;
        }
        if (GeometryCache.Count >= GEOMETRY_CACHE_CAPACITY)
        {
            GeometryCache.Clear();
        }
        Span<Vector2> points = stackalloc Vector2[CONTOUR_CAPACITY];
        var count = BuildContour(points, Vector2.Zero, size, radius, corners);
        geometry = new Geometry(points[..count].ToArray(), new Vector2[count],
            new Vector4[count], new Vector2[count]);
        for (var index = 0; index < count; index++)
        {
            geometry.Normals[index] = GetNormal(points[..count], index);
            geometry.Colors[index] = new Vector4(GetOfficeColor(points[index] / size), 1f);
            geometry.Directions[index] = Vector2.Normalize(points[index] - size * 0.5f);
        }
        // 缓存只保存局部坐标与静态光照，移动、滚动和透明度不改变几何数据。
        GeometryCache.Add(key, geometry);
        return geometry;
    }

    private static void DrawGradient(
        ImDrawListPtr drawList, Vector2 pos, Vector2 size, float radius,
        Vector4 top, Vector4 bottom, ImDrawFlags corners)
    {
        var firstVertex = drawList.VtxBuffer.Size;
        drawList.AddRectFilled(pos, pos + size, uint.MaxValue, radius, corners);
        var vertices = drawList.VtxBuffer;
        var styleAlpha = ImGui.GetStyle().Alpha;
        // 只改变本次填充顶点的颜色，保留圆角抗锯齿的 alpha 覆盖率。
        for (var index = firstVertex; index < vertices.Size; index++)
        {
            var vertex = vertices[index];
            var progress = Math.Clamp((vertex.Pos.Y - pos.Y) / size.Y, 0f, 1f);
            var color = Vector4.Lerp(top, bottom, progress);
            color.W *= (vertex.Col >> 24) / 255f * styleAlpha;
            vertex.Col = ImGui.ColorConvertFloat4ToU32(color);
            vertices[index] = vertex;
        }
    }

    private static unsafe void DrawBand(
        ImDrawListPtr drawList, ReadOnlySpan<Vector2> contour, ReadOnlySpan<Vector2> normals,
        ReadOnlySpan<Vector4> colors, ReadOnlySpan<float> offsets, ReadOnlySpan<float> alpha,
        Vector2 translation = default, float opacity = 1f)
    {
        var rows = offsets.Length;
        drawList.PrimReserve(contour.Length * (rows - 1) * 6, contour.Length * rows);
        var first = drawList.VtxCurrentIdx;
        var vertex = drawList.Handle->VtxWritePtr;
        var indices = drawList.Handle->IdxWritePtr;
        var uv = ImGui.GetFontTexUvWhitePixel();
        var styleAlpha = ImGui.GetStyle().Alpha * opacity;
        // 闭合轮廓共享顶点，使颜色沿边和径向连续插值。
        for (var point = 0; point < contour.Length; point++)
        {
            var color = ImGui.ColorConvertFloat4ToU32(colors[point] with { W = colors[point].W * styleAlpha });
            for (var row = 0; row < rows; row++)
            {
                var position = contour[point] + normals[point] * offsets[row] + translation;
                *vertex++ = new ImDrawVert
                {
                    Pos = position,
                    Uv = uv,
                    Col = (color & 0x00FFFFFFu) | ((uint)MathF.Round((color >> 24) * alpha[row]) << 24)
                };
            }
            var current = first + (uint)(point * rows);
            var next = first + (uint)(((point + 1) % contour.Length) * rows);
            for (var row = 0; row < rows - 1; row++)
            {
                var a = (ushort)(current + row);
                var b = (ushort)(next + row);
                *indices++ = a;
                *indices++ = b;
                *indices++ = (ushort)(b + 1);
                *indices++ = a;
                *indices++ = (ushort)(b + 1);
                *indices++ = (ushort)(a + 1);
            }
        }
        drawList.Handle->VtxWritePtr = vertex;
        drawList.Handle->IdxWritePtr = indices;
        drawList.VtxCurrentIdx += (uint)(contour.Length * rows);
    }

    private static void DrawOfficeRim(
        ImDrawListPtr drawList, Vector2 pos, Vector2 size, float radius, float opacity, uint id, ImDrawFlags corners)
    {
        var hovered = ImGui.IsWindowHovered() &&
                      ImGui.IsMouseHoveringRect(pos, pos + size, true) && !GlassMotion.IsDisabled;
        if (id == 0)
        {
            var relative = OmniTheme.Unscale(pos - ImGui.GetWindowPos() +
                new Vector2(ImGui.GetScrollX(), ImGui.GetScrollY()));
            id = unchecked((uint)HashCode.Combine(ImGui.GetID("##officeEdge"),
                (int)MathF.Round(relative.X), (int)MathF.Round(relative.Y)));
        }
        var pointer = Vector2.Clamp((ImGui.GetMousePos() - pos) / size, Vector2.Zero, Vector2.One);
        var delta = (pointer - new Vector2(0.5f)) * 2f;
        var proximity = MathF.Max(MathF.Abs(delta.X), MathF.Abs(delta.Y));
        var glow = GlassMotion.Mode == Config.UIMotionMode.Off ? 0f :
            GlassMotion.Value(id, 10, hovered ? Math.Clamp((proximity - 0.34f) / 0.66f, 0f, 1f) : 0f,
                hovered ? 0.25f : 0.75f);
        var spectrum = GlassMotion.Mode == Config.UIMotionMode.Off ? 0f :
            GlassMotion.Value(id, 11, hovered ? Math.Clamp((proximity - 0.52f) / 0.48f, 0f, 1f) : 0f,
                hovered ? 0.25f : 0.75f);
        var border = OmniTheme.Tokens.Border;
        var baseColor = new Vector3(border.X, border.Y, border.Z);
        var baseAlpha = border.W * opacity;
        if (glow < 0.001f && spectrum < 0.001f)
        {
            drawList.AddRect(pos, pos + size, OmniTheme.Color(border with { W = baseAlpha }),
                radius, corners, OmniTheme.Scale(1f));
            return;
        }
        pointer = GlassMotion.Pointer(id, pointer, hovered);
        var direction = (pointer - new Vector2(0.5f)) * size;
        direction = direction.LengthSquared() > 0.01f ? Vector2.Normalize(direction) : Vector2.UnitX;
        var geometry = GetGeometry(size, radius, corners);
        var contour = geometry.Points.AsSpan();
        Span<Vector4> colors = stackalloc Vector4[contour.Length];
        Span<Vector4> light = stackalloc Vector4[contour.Length];
        for (var index = 0; index < contour.Length; index++)
        {
            var angle = GlassMotion.Mode == Config.UIMotionMode.Full
                ? MathF.Acos(Math.Clamp(Vector2.Dot(geometry.Directions[index], direction), -1f, 1f))
                : 0f;
            var colorAlpha = spectrum * OfficeCone(angle, MathF.PI * 0.5f, MathF.PI * 0.8f) * opacity;
            var lightAlpha = glow * OfficeCone(angle, MathF.PI * 0.05f, MathF.PI * 0.2f) * opacity;
            if (OmniTheme.HasCustomBorder)
            {
                colorAlpha *= border.W;
                lightAlpha *= border.W;
            }
            var alpha = colorAlpha + baseAlpha * (1f - colorAlpha);
            var cached = geometry.Colors[index];
            var color = alpha > 0f
                ? (new Vector3(cached.X, cached.Y, cached.Z) * colorAlpha + baseColor * baseAlpha * (1f - colorAlpha)) / alpha
                : Vector3.Zero;
            colors[index] = new Vector4(color, alpha);
            light[index] = new Vector4(OmniTheme.HasCustomBorder ? baseColor : OfficeLight, lightAlpha);
        }
        var halfWidth = OmniTheme.Scale(0.5f);
        DrawBand(drawList, contour, geometry.Normals, colors,
            [-halfWidth - 1f, -halfWidth, halfWidth, halfWidth + 1f], [0f, 1f, 1f, 0f], translation: pos);
        if (glow < 0.001f)
        {
            return;
        }
        var inner = MathF.Min(OmniTheme.Scale(7f),
            MathF.Min(MathF.Min(size.X, size.Y) * 0.20f, radius > 0f ? radius * 0.8f : float.MaxValue));
        var lightWidth = MathF.Min(halfWidth, inner * 0.30f);
        DrawBand(drawList, contour, geometry.Normals, light,
            [-inner, -inner * 0.4f, -lightWidth, lightWidth, OmniTheme.Scale(2f), OmniTheme.Scale(7f), OmniTheme.Scale(14f)],
            [0f, 0.07f, 0.9f, 0.9f, 0.14f, 0.025f, 0f], translation: pos);
    }

    private static float OfficeCone(float angle, float start, float end) =>
        1f - Math.Clamp((angle - start) / (end - start), 0f, 1f);

    private static Vector3 GetOfficeColor(Vector2 uv)
    {
        if (OmniTheme.HasCustomBorder)
        {
            var border = OmniTheme.Tokens.Border;
            return new Vector3(border.X, border.Y, border.Z);
        }
        uv = Vector2.Clamp(uv, Vector2.Zero, Vector2.One);
        var color = OfficeViolet;
        for (var index = OfficeFields.Length - 1; index >= 0; index--)
        {
            var field = OfficeFields[index];
            var ellipse = Vector2.Max(field.Center, Vector2.One - field.Center) * MathF.Sqrt(2f);
            var alpha = Math.Clamp(1f - ((uv - field.Center) / ellipse).Length() * 2f, 0f, 1f);
            color = Vector3.Lerp(color, field.Color, alpha);
        }
        return color;
    }

    private static int BuildContour(Span<Vector2> points, Vector2 pos, Vector2 size, float radius, ImDrawFlags corners)
    {
        var topLeft = Round(ImDrawFlags.RoundCornersTopLeft) ? radius : 0f;
        var topRight = Round(ImDrawFlags.RoundCornersTopRight) ? radius : 0f;
        var bottomRight = Round(ImDrawFlags.RoundCornersBottomRight) ? radius : 0f;
        var bottomLeft = Round(ImDrawFlags.RoundCornersBottomLeft) ? radius : 0f;
        var max = pos + size;
        var count = 1;
        points[0] = pos + new Vector2(topLeft, 0f);
        AddLine(points, ref count, new Vector2(max.X - topRight, pos.Y));
        AddCorner(points, ref count, new Vector2(max.X - topRight, pos.Y + topRight), topRight, -MathF.PI * 0.5f);
        AddLine(points, ref count, new Vector2(max.X, max.Y - bottomRight));
        AddCorner(points, ref count, max - new Vector2(bottomRight), bottomRight, 0f);
        AddLine(points, ref count, new Vector2(pos.X + bottomLeft, max.Y));
        AddCorner(points, ref count, new Vector2(pos.X + bottomLeft, max.Y - bottomLeft), bottomLeft, MathF.PI * 0.5f);
        AddLine(points, ref count, new Vector2(pos.X, pos.Y + topLeft));
        AddCorner(points, ref count, pos + new Vector2(topLeft), topLeft, MathF.PI);
        if (Vector2.DistanceSquared(points[count - 1], points[0]) < 0.01f)
        {
            count--;
        }
        return count;

        bool Round(ImDrawFlags corner) => corners == ImDrawFlags.None || (corners & corner) != 0;
    }

    private static void AddLine(Span<Vector2> points, ref int count, Vector2 end)
    {
        var start = points[count - 1];
        var length = Vector2.Distance(start, end);
        if (length < 0.01f)
        {
            return;
        }
        var steps = Math.Clamp((int)MathF.Ceiling(length / OmniTheme.Scale(24f)), 1, 48);
        for (var step = 1; step <= steps; step++)
        {
            points[count++] = Vector2.Lerp(start, end, step / (float)steps);
        }
    }

    private static void AddCorner(Span<Vector2> points, ref int count, Vector2 center, float radius, float angle)
    {
        if (radius < 0.01f)
        {
            return;
        }
        var steps = Math.Clamp((int)MathF.Ceiling(radius * MathF.PI * 0.5f / OmniTheme.Scale(2f)), 4, 10);
        for (var step = 1; step <= steps; step++)
        {
            var a = angle + MathF.PI * 0.5f * step / steps;
            points[count++] = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
        }
    }

    private static Vector2 GetNormal(ReadOnlySpan<Vector2> contour, int index)
    {
        var before = Vector2.Normalize(contour[index] - contour[(index + contour.Length - 1) % contour.Length]);
        var after = Vector2.Normalize(contour[(index + 1) % contour.Length] - contour[index]);
        var normal = new Vector2(before.Y + after.Y, -before.X - after.X) * 0.5f;
        return normal / MathF.Max(0.5f, normal.LengthSquared());
    }

    private readonly record struct GeometryKey(
        Vector2 Size, float Radius, float Scale, ImDrawFlags Corners, Vector4 Border, bool CustomBorder);

    private sealed record Geometry(Vector2[] Points, Vector2[] Normals, Vector4[] Colors, Vector2[] Directions);
}
