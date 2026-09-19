using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using OmniToolbox.Config;
using OmniToolbox.Host;

namespace OmniToolbox.UI.Theme;

public static class GlassBackdrop
{
    private static UIConfig? CurrentConfig;
    private static CancellationTokenSource? Cancellation;
    private static Task<IDalamudTextureWrap>? Pending;
    private static IDalamudTextureWrap? Capture;
    private static readonly IDrawListTextureWrap?[] BlurLevels = new IDrawListTextureWrap?[4];
    private static BufferBackedImDrawData Commands;
    private static bool HasCommands;
    private static bool Failed;
    private static int RequestedFrame = -10;
    private static int RenderedFrame = -10;
    private static uint ViewportID;
    private static GlassQuality LastQuality;
    private static UITheme LastTheme;

    public static void Initialize(UIConfig ui)
    {
        CurrentConfig = ui;
        LastQuality = ui.GlassQuality;
        LastTheme = ui.Theme;
        var builder = DalamudServices.PluginInterface.UiBuilder;
        builder.Draw += BeginFrame;
        builder.HideUi += Release;
    }

    public static void Dispose()
    {
        var builder = DalamudServices.PluginInterface.UiBuilder;
        builder.Draw -= BeginFrame;
        builder.HideUi -= Release;
        Release();
        GlassMotion.Clear();
        MaterialPainter.Clear();
        CurrentConfig = null;
    }

    private static void BeginFrame()
    {
        if (CurrentConfig is null)
        {
            return;
        }

        GlassMotion.Mode = CurrentConfig.MotionMode == UIMotionMode.Full &&
                           DalamudServices.PluginInterface.UiBuilder.ShouldUseReducedMotion
            ? UIMotionMode.Reduced
            : CurrentConfig.MotionMode;
        GlassMotion.Prune();
        if (LastTheme != CurrentConfig.Theme || LastQuality != CurrentConfig.GlassQuality)
        {
            if (LastTheme is not (UITheme.GlassLight or UITheme.GlassDark) ||
                !OmniTheme.IsGlass || LastQuality != CurrentConfig.GlassQuality || Failed)
            {
                Release();
            }
            MaterialPainter.Clear();
            Failed = false;
            LastTheme = CurrentConfig.Theme;
            LastQuality = CurrentConfig.GlassQuality;
        }
        if (!OmniTheme.IsGlass || CurrentConfig.GlassQuality == GlassQuality.Compatible ||
            ImGui.GetFrameCount() - RequestedFrame > 1)
        {
            Release();
        }
    }

    public static bool Draw(
        ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius,
        float opacity = 1f, ImDrawFlags corners = ImDrawFlags.RoundCornersAll)
    {
        if (!OmniTheme.IsGlass || CurrentConfig is null || Failed ||
            CurrentConfig.GlassQuality == GlassQuality.Compatible || opacity <= 0f ||
            max.X <= min.X || max.Y <= min.Y)
        {
            return false;
        }
        var viewport = ImGui.GetMainViewport();
        var end = viewport.Pos + viewport.Size;
        if (min.X < viewport.Pos.X || min.Y < viewport.Pos.Y || max.X > end.X || max.Y > end.Y)
        {
            return false;
        }

        RequestedFrame = ImGui.GetFrameCount();
        try
        {
            if (ViewportID != viewport.ID)
            {
                Release();
                ViewportID = viewport.ID;
            }
            if (Capture is null)
            {
                Cancellation ??= new CancellationTokenSource();
                Pending ??= DalamudServices.TextureProvider.CreateFromImGuiViewportAsync(
                    new ImGuiViewportTextureArgs
                    {
                        ViewportId = viewport.ID,
                        AutoUpdate = true,
                        TakeBeforeImGuiRender = true,
                        KeepTransparency = false
                    },
                    "Omni Glass game background", Cancellation.Token);
                if (!Pending.IsCompleted)
                {
                    return false;
                }
                Capture = Pending.GetAwaiter().GetResult();
                Pending = null;
            }

            var levelCount = CurrentConfig.GlassQuality == GlassQuality.Light ? 2 : 4;
            if (RenderedFrame != RequestedFrame)
            {
                RenderBlur(levelCount);
                RenderedFrame = RequestedFrame;
            }
            var texture = BlurLevels[levelCount - 1]!;
            drawList.AddImageRounded(
                texture.Handle, min, max,
                (min - viewport.Pos) / viewport.Size,
                (max - viewport.Pos) / viewport.Size,
                OmniTheme.Color(Vector4.One with { W = Math.Clamp(opacity, 0f, 1f) }),
                radius, corners);
            return true;
        }
        catch (Exception exception)
        {
            Failed = true;
            Release();
            DalamudServices.PluginLog.Warning(exception, "Omni glass background is unavailable; using compatible material.");
            return false;
        }
    }

    internal static bool TryGetSurfaceTexture(Vector2 min, Vector2 max, out SurfaceTexture surface)
    {
        surface = default;
        if (!OmniTheme.IsGlass || CurrentConfig is null || Failed ||
            CurrentConfig.GlassQuality == GlassQuality.Compatible || RenderedFrame != ImGui.GetFrameCount())
        {
            return false;
        }
        var viewport = ImGui.GetMainViewport();
        var end = viewport.Pos + viewport.Size;
        var texture = BlurLevels[CurrentConfig.GlassQuality == GlassQuality.Light ? 1 : 3];
        if (texture is null || ViewportID != viewport.ID ||
            min.X < viewport.Pos.X || min.Y < viewport.Pos.Y || max.X > end.X || max.Y > end.Y)
        {
            return false;
        }
        surface = new SurfaceTexture(texture.Handle, viewport.Pos, viewport.Size, new Vector2(0.5f) / texture.Size);
        return true;
    }

    private static void RenderBlur(int levelCount)
    {
        if (!HasCommands)
        {
            Commands = BufferBackedImDrawData.Create();
            HasCommands = true;
        }

        ReadOnlySpan<int> divisors = levelCount == 2 ? [8, 4] : [4, 8, 16, 4];
        IDalamudTextureWrap source = Capture!;
        for (var level = 0; level < levelCount; level++)
        {
            var target = BlurLevels[level] ??=
                DalamudServices.TextureProvider.CreateDrawListTexture($"Omni Glass blur {level}");
            var divisor = divisors[level];
            target.Size = Vector2.Max(Vector2.One, Capture!.Size / divisor);
            var list = Commands.ListPtr;
            list._ResetForNewFrame();
            list.PushClipRect(Vector2.Zero, target.Size, false);
            list.PushTextureID(source.Handle);
            // 首层扩大采样间距补偿直接降采样，后续保持四点均值。
            var sampleRadius = level == 0 ? divisor * 0.5f : 1f;
            for (var tap = 0; tap < 4; tap++)
            {
                var offset = new Vector2((tap & 1) == 0 ? -1f : 1f, (tap & 2) == 0 ? -1f : 1f) /
                             source.Size * sampleRadius;
                list.AddImage(source.Handle, Vector2.Zero, target.Size, offset, Vector2.One + offset,
                    ImGui.ColorConvertFloat4ToU32(Vector4.One with { W = 1f / (tap + 1) }));
            }
            list.PopTextureID();
            list.PopClipRect();
            target.Draw(list, Vector2.Zero, Vector2.One);
            source = target;
        }
    }

    private static void Release()
    {
        Cancellation?.Cancel();
        Cancellation?.Dispose();
        Cancellation = null;
        if (Pending is { } task)
        {
            // 取消与首次捕获完成可能同时发生，仍需接管并释放成功返回的纹理。
            _ = task.ContinueWith(completed =>
            {
                if (completed.IsCompletedSuccessfully)
                {
                    completed.Result.Dispose();
                }
                else
                {
                    _ = completed.Exception;
                }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            Pending = null;
        }
        Capture?.Dispose();
        Capture = null;
        for (var index = 0; index < BlurLevels.Length; index++)
        {
            BlurLevels[index]?.Dispose();
            BlurLevels[index] = null;
        }
        if (HasCommands)
        {
            Commands.Dispose();
            HasCommands = false;
        }
        RenderedFrame = -10;
    }

    internal readonly record struct SurfaceTexture(ImTextureID Handle, Vector2 Origin, Vector2 Size, Vector2 HalfTexel);
}
