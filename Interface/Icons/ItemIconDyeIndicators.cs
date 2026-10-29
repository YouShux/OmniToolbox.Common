using Dalamud.Interface.Textures;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using Lumina.Excel.Sheets;
using OmenTools.Extensions;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.Host;
using OmniToolbox.UI.Theme;

namespace OmniToolbox.UI;

internal static class ItemIconDyeIndicators
{
    private static ISharedImmediateTexture? IndicatorTexture;

    public static unsafe void Apply
    (
        IconNode icon,
        uint     itemID,
        byte     stain0 = 0,
        byte     stain1 = 0
    )
    {
        var dyeCount = GetDyeCount(itemID);
        var component = icon.Component;
        if (component->Flags.HasFlag(IconComponentFlags.HasFirstDyeChannel) != (dyeCount > 0))
            component->SetHasFirstDyeChannel(dyeCount > 0);
        if (component->Flags.HasFlag(IconComponentFlags.HasSecondDyeChannel) != (dyeCount > 1))
            component->SetHasSecondDyeChannel(dyeCount > 1);
        for (var channel = 0; channel < 2; channel++)
        {
            var color = channel < dyeCount ? GetColor(channel == 0 ? stain0 : stain1) : 0;
            if (component->DyeColors[channel].RGBA != color)
                component->SetDyeColor(new ByteColor { RGBA = color }, channel);
        }
    }

    public static void Draw
    (
        ImDrawListPtr drawList,
        uint          itemID,
        Vector2       position,
        Vector2       size,
        bool          drawFrame,
        Vector4       tint,
        byte          stain0 = 0,
        byte          stain1 = 0
    )
    {
        var dyeCount = GetDyeCount(itemID);
        if (dyeCount == 0)
            return;
        IndicatorTexture ??= DalamudServices.TextureProvider.GetFromGame("ui/uld/IconA_Frame.tex");
        if (IndicatorTexture.GetWrapOrDefault() is not { } texture)
            return;
        var textureSize = new Vector2(texture.Width, texture.Height);
        var scale = size / (drawFrame ? 48f : 40f);
        var iconOrigin = position + (drawFrame ? new Vector2(2f, 0f) : new Vector2(-2f, -3f)) * scale;
        for (var channel = 0; channel < dyeCount; channel++)
        {
            var color = GetColor(channel == 0 ? stain0 : stain1);
            var coordinates = new Vector2(color == 0 ? 210f : 192f, 114f);
            var offset = new Vector2(27f, -2f + channel * 13f);
            var indicatorPosition = iconOrigin + offset * scale;
            drawList.AddImage(
                texture.Handle,
                indicatorPosition,
                indicatorPosition + new Vector2(18f) * scale,
                coordinates / textureSize,
                (coordinates + new Vector2(18f)) / textureSize,
                OmniTheme.Color((color == 0 ? Vector4.One : color.ReverseToVector4()) * tint));
        }
    }

    private static int GetDyeCount(uint itemID) =>
        itemID != 0 && LuminaGetter.TryGetRow<Item>(ItemUtil.GetBaseId(itemID).ItemId, out var item)
            ? Math.Min((int)item.DyeCount, 2)
            : 0;

    private static uint GetColor(byte stainID) =>
        stainID != 0 && LuminaGetter.TryGetRow<Stain>(stainID, out var stain) ? stain.Color : 0;
}
