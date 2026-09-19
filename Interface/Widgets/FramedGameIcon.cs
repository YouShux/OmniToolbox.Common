using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using OmniToolbox.Host;
using OmniToolbox.Items;
using OmniToolbox.UI.Theme;
using LuminaAction = Lumina.Excel.Sheets.Action;
using LuminaItem = Lumina.Excel.Sheets.Item;

namespace OmniToolbox.UI.Controls;

internal static class FramedGameIcon
{
    private static readonly Vector4 DimmedTint = new(0.55f, 0.55f, 0.55f, 1f);
    private static ISharedImmediateTexture? ActiveTexture;
    private static ISharedImmediateTexture? CollectionStatusTexture;
    private static ISharedImmediateTexture? FrameTexture;
    private const string COLLECTION_STATUS_TEXTURE_PATH = "ui/uld/ReadyCheck_hr1.tex";

    public static void DrawItem(LuminaItem item, Vector2 size) =>
        DrawAtCursor(item.Icon, size, !ItemCategoryMap.IsCurrency(item));

    public static void DrawAction(LuminaAction action, Vector2 size) =>
        DrawAtCursor(action.Icon, size, !action.IsPvP);

    public static void Draw(
        uint iconID,
        Vector2 position,
        Vector2 size,
        bool itemHQ = false,
        bool dimmed = false,
        Vector4? tint = null,
        bool active = false,
        bool drawFrame = true,
        bool preserveAspectRatio = false) =>
        Draw(
            ImGui.GetWindowDrawList(),
            iconID,
            position,
            size,
            itemHQ,
            dimmed,
            tint,
            active,
            drawFrame,
            preserveAspectRatio);

    public static void Draw(
        ImDrawListPtr drawList,
        uint iconID,
        Vector2 position,
        Vector2 size,
        bool itemHQ = false,
        bool dimmed = false,
        Vector4? tint = null,
        bool active = false,
        bool drawFrame = true,
        bool preserveAspectRatio = false)
    {
        if (iconID == IconBrowser.LauncherIconID)
        {
            drawFrame = false;
        }
        var iconPosition = drawFrame
            ? position + size * new Vector2(0.042f, 0.042f)
            : position;
        var iconMaximum = drawFrame
            ? position + size - size * new Vector2(0.042f, 0.078f)
            : position + size;
        IDalamudTextureWrap? texture = null;
        if (iconID == IconBrowser.LauncherIconID)
        {
            texture = IconBrowser.GetIconTexture(iconID);
        }
        else if (iconID != 0 &&
            (DalamudServices.TextureProvider.TryGetFromGameIcon(new GameIconLookup(iconID, itemHQ), out var sharedTexture) ||
             DalamudServices.TextureProvider.TryGetFromGameIcon(new GameIconLookup(iconID, itemHQ, hiRes: false), out sharedTexture)))
        {
            texture = sharedTexture.GetWrapOrDefault();
        }
        if (texture is not null && texture.Handle != nint.Zero)
        {
            if (preserveAspectRatio)
            {
                var fittedSize = OmniTheme.FitImageSize(
                    new Vector2(texture.Width, texture.Height),
                    MathF.Min(iconMaximum.X - iconPosition.X, iconMaximum.Y - iconPosition.Y));
                iconPosition += (iconMaximum - iconPosition - fittedSize) * 0.5f;
                iconMaximum = iconPosition + fittedSize;
            }

            drawList.AddImage(
                texture.Handle,
                iconPosition,
                iconMaximum,
                Vector2.Zero,
                Vector2.One,
                OmniTheme.Color(tint ?? (dimmed ? DimmedTint : Vector4.One)));
        }
        else
        {
            var textSize = ImGui.CalcTextSize("?");
            drawList.AddText(
                iconPosition + (iconMaximum - iconPosition - textSize) * 0.5f,
                OmniTheme.Color(OmniTheme.Tokens.Text with { W = 0.55f }),
                "?");
        }

        if (drawFrame)
        {
            DrawFrame(drawList, position, size, active);
        }
    }

    public static void DrawFrame(ImDrawListPtr drawList, Vector2 position, Vector2 size, bool active = false)
    {
        if (TryGetFrame(out var frame))
        {
            drawList.AddImage(
                frame.Handle,
                position,
                position + size,
                Vector2.Zero,
                Vector2.One,
                OmniTheme.Color(Vector4.One));
        }

        if (!active || !TryGetActiveFrame(out var activeFrame))
        {
            return;
        }

        var activePadding = size * 0.05f;
        drawList.AddImage(
            activeFrame.Handle,
            position - activePadding,
            position + size + activePadding,
            Vector2.Zero,
            Vector2.One,
            OmniTheme.Color(Vector4.One));
    }

    public static void DrawCollectedCheck(
        ImDrawListPtr drawList,
        Vector2 position,
        Vector2 size,
        float maxSize)
    {
        var checkSize = Math.Clamp(
            MathF.Min(size.X, size.Y) * 0.5f,
            OmniTheme.Scale(10f),
            OmniTheme.Scale(maxSize));
        var checkPosition = position + size - new Vector2(
            checkSize - OmniTheme.Scale(2f),
            checkSize + OmniTheme.Scale(1f));
        DrawCollectionStatus(
            drawList,
            checkPosition,
            checkPosition + new Vector2(checkSize),
            true);
    }

    public static void DrawCollectionStatus(
        ImDrawListPtr drawList,
        Vector2 min,
        Vector2 max,
        bool collected)
    {
        if (GetCollectionStatusTexture() is not { } texture)
        {
            return;
        }

        DrawCollectionStatus(drawList, texture, min, max, collected);
    }

    public static bool DrawCollectionStatusAtCursor(Vector2 size, bool collected)
    {
        if (GetCollectionStatusTexture() is not { } texture)
        {
            return false;
        }

        var position = ImGui.GetCursorScreenPos();
        ImGui.Dummy(size);
        DrawCollectionStatus(ImGui.GetWindowDrawList(), texture, position, position + size, collected);
        return true;
    }

    public static bool IsCollectionStatusAvailable => GetCollectionStatusTexture() is not null;

    private static void DrawCollectionStatus(
        ImDrawListPtr drawList,
        IDalamudTextureWrap texture,
        Vector2 min,
        Vector2 max,
        bool collected)
    {
        drawList.AddImage(
            texture.Handle,
            min,
            max,
            collected ? Vector2.Zero : new Vector2(0.5f, 0f),
            collected ? new Vector2(0.5f, 1f) : Vector2.One);
    }

    private static IDalamudTextureWrap? GetCollectionStatusTexture()
    {
        CollectionStatusTexture ??= DalamudServices.TextureProvider.GetFromGame(COLLECTION_STATUS_TEXTURE_PATH);
        return CollectionStatusTexture.GetWrapOrDefault();
    }

    private static void DrawAtCursor(uint iconID, Vector2 size, bool drawFrame)
    {
        var position = ImGui.GetCursorScreenPos();
        ImGui.Dummy(size);
        Draw(iconID, position, size, drawFrame: drawFrame);
    }

    private static bool TryGetActiveFrame(out IDalamudTextureWrap frame)
    {
        ActiveTexture ??= DalamudServices.TextureProvider.GetFromFile(System.IO.Path.Combine(
            DalamudServices.PluginInterface.AssemblyLocation.DirectoryName!,
            @"Resources\activeaction.png"));
        if (ActiveTexture.TryGetWrap(out var frameWrap, out _))
        {
            frame = frameWrap;
            return true;
        }

        frame = null!;
        return false;
    }

    private static bool TryGetFrame(out IDalamudTextureWrap frame)
    {
        FrameTexture ??= DalamudServices.TextureProvider.GetFromManifestResource(
            typeof(FramedGameIcon).Assembly,
            "OmniToolbox.Resources.边框.png");
        if (FrameTexture.TryGetWrap(out var frameWrap, out _))
        {
            frame = frameWrap;
            return true;
        }

        frame = null!;
        return false;
    }
}
