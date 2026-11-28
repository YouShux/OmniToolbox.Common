using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Utility;
using OmniToolbox.UI.Theme;
using TreeToolKit;

namespace OmniToolbox.UI.Widgets;

internal sealed class OmniTextDrawCache(string name) : IDisposable
{
    private readonly Dictionary<int, Slot> slots = [];
    private readonly List<int> expired = [];
    private long fontRevision = -1;
    private long localizationRevision = -1;
    private long retryAfter;
    private int prunedFrame = -1;

    internal unsafe void Draw(ImDrawListPtr drawList, int id, string text, Vector2 position, float fontSize,
        uint? color = null, float shadowOpacity = 1f)
    {
        if (text.Length == 0)
            return;
        var font = ImGui.GetFont();
        Draw(drawList, id, text, position, fontSize, color, shadowOpacity,
            ImGui.GetFrameCount(), OmniTextTextureBudget.GetWindowOwner(), (nint)font.Handle, ImGui.GetStyle().Alpha, font);
    }

    internal void Draw(ImDrawListPtr drawList, int id, string text, Vector2 position, float fontSize,
        uint? color, float shadowOpacity, int frame, uint windowOwner, nint fontHandle, float styleAlpha, ImFontPtr font)
    {
        if (text.Length == 0)
            return;
        if (shadowOpacity <= 0f)
        {
            if (color is { } plainColor)
                drawList.AddText(font, fontSize, position, plainColor, text);
            return;
        }
        if (fontRevision != Fonts.Revision || localizationRevision != OmniLoc.Revision)
        {
            Clear();
            fontRevision = Fonts.Revision;
            localizationRevision = OmniLoc.Revision;
        }
        if (prunedFrame != frame)
        {
            prunedFrame = frame;
            foreach (var (key, value) in slots)
                if (value.Frame < frame - 1)
                    expired.Add(key);
            foreach (var key in expired)
            {
                Release(slots[key]);
                slots.Remove(key);
            }
            expired.Clear();
        }
        var origin = new Vector2(MathF.Floor(position.X), MathF.Floor(position.Y));
        var content = new Content(windowOwner, fontHandle,
            fontSize, text, color, shadowOpacity, position - origin);
        if (!slots.TryGetValue(id, out var slot) && slots.Count < 256)
        {
            slot = new();
            slots.Add(id, slot);
        }
        if (slot is not null)
        {
            if (slot.Content != content)
            {
                Release(slot);
                slot.Content = content;
                slot.Frame = -1;
                slot.Failed = false;
            }
            var stable = slot.Frame == frame - 1 && styleAlpha == 1f;
            slot.Frame = frame;
            if (stable && !slot.Failed && (slot.Texture is not null || TryBuild(slot, frame, font)))
            {
                var texture = slot.Texture;
                if (texture is not null && frame >= slot.UseFromFrame)
                {
                    var min = origin - new Vector2(3f);
                    drawList.AddImage(texture.Handle, min, min + texture.Size);
                    return;
                }
            }
        }
        DrawImmediate(drawList, text, position, fontSize, color, shadowOpacity, font);
    }

    internal static void DrawImmediate(ImDrawListPtr drawList, string text, Vector2 position, float fontSize,
        uint? color, float shadowOpacity) =>
        DrawImmediate(drawList, text, position, fontSize, color, shadowOpacity, ImGui.GetFont());

    internal static void DrawImmediate(ImDrawListPtr drawList, string text, Vector2 position, float fontSize,
        uint? color, float shadowOpacity, ImFontPtr font)
    {
        if (color is { } textColor)
            OmniTextShadow.DrawWithText(drawList, font, fontSize, position, textColor, text, shadowOpacity);
        else
            OmniTextShadow.Draw(drawList, text, position, fontSize, shadowOpacity);
    }

    private bool TryBuild(Slot slot, int frame, ImFontPtr font)
    {
        if (Environment.TickCount64 < retryAfter)
            return false;
        var size = OmniTextMeasurements.MeasureAtSize(slot.Content.Text, font, slot.Content.Size);
        size = new(MathF.Ceiling(size.X + 6f), MathF.Ceiling(size.Y + 6f));
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X <= 0f || size.Y <= 0f ||
            size.X > 8192f || size.Y > 8192f)
            return false;
        var bytes = (long)size.X * (long)size.Y * 8;
        if (!OmniTextTextureBudget.TryReserve(slot.Content.Window, bytes, frame))
            return false;
        IDrawListTextureWrap? texture = null;
        try
        {
            texture = DalamudServices.TextureProvider.CreateDrawListTexture(name);
            texture.Size = size;
            using (var data = BufferBackedImDrawData.Create())
            {
                DrawImmediate(data.ListPtr, slot.Content.Text, new Vector2(3f) + slot.Content.Offset,
                    slot.Content.Size, slot.Content.Color, slot.Content.Opacity, font);
                texture.Draw(data.ListPtr, Vector2.Zero, Vector2.One);
            }
        }
        catch (Exception)
        {
            slot.Failed = true;
            retryAfter = Environment.TickCount64 + 30_000;
            try
            {
                texture?.Dispose();
            }
            catch (Exception)
            {
            }
            finally
            {
                OmniTextTextureBudget.Release(slot.Content.Window, bytes);
            }
            return false;
        }
        slot.Texture = texture;
        slot.Bytes = bytes;
        slot.UseFromFrame = frame + 1;
        return true;
    }

    private void Release(Slot slot)
    {
        if (slot.Texture is not { } texture)
            return;
        var bytes = slot.Bytes;
        slot.Texture = null;
        slot.Bytes = 0;
        try
        {
            texture.Dispose();
        }
        catch (Exception)
        {
        }
        finally
        {
            OmniTextTextureBudget.Release(slot.Content.Window, bytes);
        }
    }

    internal void Clear()
    {
        foreach (var slot in slots.Values)
            Release(slot);
        slots.Clear();
        expired.Clear();
        retryAfter = 0;
        prunedFrame = -1;
    }

    public void Dispose() => Clear();

    private readonly record struct Content(uint Window, nint Font, float Size, string Text, uint? Color,
        float Opacity, Vector2 Offset);

    private sealed class Slot
    {
        internal Content Content;
        internal int Frame = -1;
        internal IDrawListTextureWrap? Texture;
        internal long Bytes;
        internal int UseFromFrame;
        internal bool Failed;
    }
}
