using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Utility;
using OmniToolbox.Host;
using OmniToolbox.UI.Widgets;

namespace OmniToolbox.UI.Controls;

public static partial class OmniControls
{
    private static readonly Dictionary<(uint Window, Vector2 Position), HighlightSlot> Highlights = [];
    private static readonly List<(uint Window, Vector2 Position)> ExpiredHighlights = [];
    private static int HighlightPrunedFrame = -1;
    private static long HighlightRetryAfter;

    internal static void PruneControlHighlights()
    {
        var frame = ImGui.GetFrameCount();
        if (HighlightPrunedFrame == frame)
            return;
        HighlightPrunedFrame = frame;
        foreach (var (key, slot) in Highlights)
            if (slot.Frame < frame - 1)
                ExpiredHighlights.Add(key);
        foreach (var key in ExpiredHighlights)
        {
            ReleaseHighlight(Highlights[key]);
            Highlights.Remove(key);
        }
        ExpiredHighlights.Clear();
    }

    private static unsafe bool TryDrawControlHighlight(ImDrawListPtr drawList, Vector2 position,
        Vector2 size, float radius, float height, float alpha)
    {
        PruneControlHighlights();
        var frame = ImGui.GetFrameCount();
        var window = ImGuiP.GetCurrentWindowRead();
        var key = (window.ID, position - window.Pos);
        if (!Highlights.TryGetValue(key, out var slot))
        {
            if (Highlights.Count >= 256)
                return false;
            slot = new();
            Highlights.Add(key, slot);
        }
        var origin = new Vector2(MathF.Floor(position.X), MathF.Floor(position.Y));
        var style = ImGui.GetStyle();
        var content = new HighlightContent(OmniTextTextureBudget.GetWindowOwner(), size, radius, height, alpha, position - origin,
            drawList.Flags, style.CurveTessellationTol, style.CircleTessellationMaxError);
        if (slot.Content != content)
        {
            ReleaseHighlight(slot);
            slot.Content = content;
            slot.Frame = -1;
            slot.Failed = false;
        }
        var stable = slot.Frame == frame - 1 && style.Alpha == 1f;
        slot.Frame = frame;
        if (!stable || slot.Failed || (slot.Texture is null && !TryBuildHighlight(slot, frame)))
            return false;
        if (slot.Texture is not { } texture)
            return false;
        var min = origin - Vector2.One;
        drawList.AddImage(texture.Handle, min, min + texture.Size);
        return true;
    }

    private static bool TryBuildHighlight(HighlightSlot slot, int frame)
    {
        if (Environment.TickCount64 < HighlightRetryAfter)
            return false;
        var content = slot.Content;
        var size = new Vector2(MathF.Ceiling(content.Size.X + content.Offset.X + 2f),
            MathF.Ceiling(content.Height + content.Offset.Y + 2f));
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X <= 0f || size.Y <= 0f ||
            size.X > 8192f || size.Y > 8192f)
            return false;
        var bytes = (long)size.X * (long)size.Y * 8;
        if (!OmniTextTextureBudget.TryReserve(content.Window, bytes, frame))
            return false;
        IDrawListTextureWrap? texture = null;
        try
        {
            texture = DalamudServices.TextureProvider.CreateDrawListTexture("Omni control highlight");
            texture.Size = size;
            using (var data = BufferBackedImDrawData.Create())
            {
                data.ListPtr.Flags = content.Flags;
                DrawControlHighlightImmediate(data.ListPtr, Vector2.One + content.Offset, content.Size,
                    content.Radius, content.Height, content.Alpha);
                texture.Draw(data.ListPtr, Vector2.Zero, Vector2.One);
            }
        }
        catch (Exception exception)
        {
            slot.Failed = true;
            HighlightRetryAfter = Environment.TickCount64 + 30_000;
            try
            {
                texture?.Dispose();
            }
            catch (Exception releaseException)
            {
                DalamudServices.PluginLog.Warning(releaseException, "Omni control highlight texture cleanup failed.");
            }
            finally
            {
                OmniTextTextureBudget.Release(content.Window, bytes);
            }
            if (exception is not ObjectDisposedException || !DalamudServices.IsUnloading)
                DalamudServices.PluginLog.Warning(exception, "Omni control highlight texture creation failed; drawing directly.");
            return false;
        }
        slot.Texture = texture;
        slot.Bytes = bytes;
        return true;
    }

    private static void ReleaseHighlight(HighlightSlot slot)
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
                DalamudServices.PluginLog.Warning(exception, "Omni control highlight texture cleanup failed.");
        }
        finally
        {
            OmniTextTextureBudget.Release(slot.Content.Window, bytes);
        }
    }

    private static void ClearControlHighlights()
    {
        foreach (var slot in Highlights.Values)
            ReleaseHighlight(slot);
        Highlights.Clear();
        ExpiredHighlights.Clear();
        HighlightPrunedFrame = -1;
        HighlightRetryAfter = 0;
    }

    private readonly record struct HighlightContent(uint Window, Vector2 Size, float Radius, float Height, float Alpha,
        Vector2 Offset, ImDrawListFlags Flags, float CurveTolerance, float CircleError);

    private sealed class HighlightSlot
    {
        internal HighlightContent Content;
        internal int Frame = -1;
        internal IDrawListTextureWrap? Texture;
        internal long Bytes;
        internal bool Failed;
    }
}
