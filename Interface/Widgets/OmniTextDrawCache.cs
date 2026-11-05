using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Utility;
using OmniToolbox.Host;
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
        if (fontRevision != Fonts.Revision || localizationRevision != OmniLoc.Revision)
        {
            Clear();
            fontRevision = Fonts.Revision;
            localizationRevision = OmniLoc.Revision;
        }
        var frame = ImGui.GetFrameCount();
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
        var content = new Content(OmniTextTextureBudget.GetWindowOwner(), (nint)ImGui.GetFont().Handle,
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
            var stable = slot.Frame == frame - 1 && ImGui.GetStyle().Alpha == 1f;
            slot.Frame = frame;
            if (stable && !slot.Failed && (slot.Texture is not null || TryBuild(slot, frame)))
            {
                var texture = slot.Texture;
                if (texture is not null)
                {
                    var min = origin - new Vector2(3f);
                    drawList.AddImage(texture.Handle, min, min + texture.Size);
                    return;
                }
            }
        }
        DrawImmediate(drawList, text, position, fontSize, color, shadowOpacity);
    }

    internal static void DrawImmediate(ImDrawListPtr drawList, string text, Vector2 position, float fontSize,
        uint? color, float shadowOpacity)
    {
        OmniTextShadow.Draw(drawList, text, position, fontSize, shadowOpacity);
        if (color is { } textColor)
            drawList.AddText(ImGui.GetFont(), fontSize, position, textColor, text);
    }

    private bool TryBuild(Slot slot, int frame)
    {
        if (Environment.TickCount64 < retryAfter)
            return false;
        var size = OmniTextMeasurements.MeasureAtSize(slot.Content.Text, ImGui.GetFont(), slot.Content.Size);
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
                    slot.Content.Size, slot.Content.Color, slot.Content.Opacity);
                texture.Draw(data.ListPtr, Vector2.Zero, Vector2.One);
            }
        }
        catch (Exception exception)
        {
            slot.Failed = true;
            retryAfter = Environment.TickCount64 + 30_000;
            try
            {
                texture?.Dispose();
            }
            catch (Exception releaseException)
            {
                DalamudServices.PluginLog.Warning(releaseException, "Omni text texture cleanup failed.");
            }
            finally
            {
                OmniTextTextureBudget.Release(slot.Content.Window, bytes);
            }
            if (exception is not ObjectDisposedException || !DalamudServices.IsUnloading)
                DalamudServices.PluginLog.Warning(exception, "Omni text texture creation failed; drawing text directly.");
            return false;
        }
        slot.Texture = texture;
        slot.Bytes = bytes;
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
        catch (Exception exception)
        {
            if (exception is not ObjectDisposedException || !DalamudServices.IsUnloading)
                DalamudServices.PluginLog.Warning(exception, "Omni text texture cleanup failed.");
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
        internal bool Failed;
    }
}
