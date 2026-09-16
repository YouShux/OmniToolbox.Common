using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.Exd;
using Lumina.Excel.Sheets;
using OmenTools.Info.Game.ItemSource;
using OmenTools.Info.Game.ItemSource.Enums;
using OmenTools.Interop.Game.Lumina;
using Character = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
using OmniToolbox.Collections;
using OmniToolbox.Config;

namespace OmniToolbox.Items;

internal enum ItemPreviewImageLookup
{
    Found,
    Pending,
    NotFound
}

internal enum ItemPreviewImageKind
{
    Painting,
    Mount,
    Minion,
    Hairstyle,
    FashionAccessory
}

internal readonly record struct ItemPreviewImage(
    string TexturePath,
    ItemPreviewImageKind Kind,
    bool IsUnlocked);

public sealed unsafe partial class ItemPreviewService
{
    private readonly Dictionary<(uint HairstyleID, uint ItemID), uint> hairstyleIconCache = [];
    private (byte Tribe, byte Sex)? hairstyleIconCachePlayer;

    public uint ResolveCollectionIcon(CollectionEntry item)
    {
        if (item.Type != CollectionType.Hairstyle || item.ItemID is not { } itemID)
        {
            return item.IconID;
        }

        var character = GetLocalCharacter();
        if (character == null)
        {
            return item.IconID;
        }

        var player = (character->DrawData.CustomizeData.Tribe, character->DrawData.CustomizeData.Sex);
        if (hairstyleIconCachePlayer != player)
        {
            hairstyleIconCachePlayer = player;
            hairstyleIconCache.Clear();
        }

        var key = (item.ID, itemID);
        if (hairstyleIconCache.TryGetValue(key, out var iconID))
        {
            return iconID;
        }

        iconID = ResolveHairstyleForIcon(item.ID, itemID, character)?.Icon ?? item.IconID;
        hairstyleIconCache[key] = iconID;
        return iconID;
    }

    internal ItemPreviewImageLookup ResolveImagePreview(
        uint itemID,
        ItemImagePreviewConfig config,
        out ItemPreviewImage preview)
    {
        preview = default;
        itemID = ItemUtil.GetBaseId(itemID).ItemId;
        if (itemID == 0)
        {
            return ItemPreviewImageLookup.NotFound;
        }

        if (TryResolveDirectImage(itemID, config, out preview))
        {
            return ItemPreviewImageLookup.Found;
        }

        if (!config.ShowExchangeRewards)
        {
            return ItemPreviewImageLookup.NotFound;
        }

        var exchangeItems = ItemSourceInfo.QueryExchangeItems(itemID);
        if (exchangeItems.State == ItemSourceQueryState.Building)
        {
            return ItemPreviewImageLookup.Pending;
        }

        if (exchangeItems.State != ItemSourceQueryState.Ready || exchangeItems.Data is null)
        {
            return ItemPreviewImageLookup.NotFound;
        }

        foreach (var exchangeItem in exchangeItems.Data.Items)
        {
            if (TryResolveDirectImage(exchangeItem.ItemID, config, out preview))
            {
                return ItemPreviewImageLookup.Found;
            }
        }

        return ItemPreviewImageLookup.NotFound;
    }

    private bool TryResolveDirectImage(
        uint itemID,
        ItemImagePreviewConfig config,
        out ItemPreviewImage preview)
    {
        preview = default;
        if (config.ShowPaintings &&
            LuminaGetter.TryGetRow<Item>(itemID, out var item) &&
            item.ItemUICategory.RowId == 95 &&
            LuminaGetter.TryGetRow<Picture>(item.AdditionalData.RowId, out var picture) &&
            picture.Image > 0)
        {
            preview = new(
                BuildImageTexturePath((uint)picture.Image, false),
                ItemPreviewImageKind.Painting,
                false);
            return true;
        }

        if (!itemPreviewTargets.TryGetValue(itemID, out var target))
        {
            return false;
        }

        switch (target.Type)
        {
            case CollectionType.Mount when config.ShowMounts &&
                                                LuminaGetter.TryGetRow<Mount>(target.ID, out var mount) &&
                                                mount.Icon != 0:
                var playerState = PlayerState.Instance();
                preview = new(
                    BuildImageTexturePath(mount.Icon + 64_000U, true),
                    ItemPreviewImageKind.Mount,
                    playerState != null && playerState->IsMountUnlocked(target.ID));
                return true;
            case CollectionType.Minion when config.ShowMinions &&
                                                LuminaGetter.TryGetRow<Lumina.Excel.Sheets.Companion>(target.ID, out var minion) &&
                                                minion.Icon != 0:
                var uiState = UIState.Instance();
                preview = new(
                    BuildImageTexturePath(minion.Icon + 64_000U, true),
                    ItemPreviewImageKind.Minion,
                    uiState != null && uiState->IsCompanionUnlocked(target.ID));
                return true;
            case CollectionType.Hairstyle when config.ShowHairstyles:
                var character = GetLocalCharacter();
                if (character == null ||
                    ResolveHairstyleForIcon(target.ID, itemID, character) is not { } hairstyle)
                {
                    return false;
                }

                var exdItem = ExdModule.GetItemRowById(itemID);
                var hairstyleUIState = UIState.Instance();
                preview = new(
                    BuildImageTexturePath(hairstyle.Icon, true),
                    ItemPreviewImageKind.Hairstyle,
                    exdItem != null &&
                    hairstyleUIState != null &&
                    hairstyleUIState->IsItemActionUnlocked(exdItem) == 1);
                return true;
            case CollectionType.FashionAccessory when config.ShowFashionAccessories &&
                                                          LuminaGetter.TryGetRow<Ornament>(target.ID, out var ornament) &&
                                                          ornament.Icon != 0:
                var ornamentPlayerState = PlayerState.Instance();
                preview = new(
                    BuildImageTexturePath(ornament.Icon + 59_000U, true),
                    ItemPreviewImageKind.FashionAccessory,
                    ornamentPlayerState != null && ornamentPlayerState->IsOrnamentUnlocked(target.ID));
                return true;
            default:
                return false;
        }
    }

    private static CharaMakeCustomize? ResolveHairstyleForIcon(
        uint hairstyleID,
        uint itemID,
        Character* character)
    {
        if (ResolveHairstyle(hairstyleID, itemID, character) is not { } hairstyle)
        {
            return null;
        }

        return hairstyle.Icon is 0 or 131_094 ? null : hairstyle;
    }

    private static string BuildImageTexturePath(uint iconID, bool highResolution) =>
        $"ui/icon/{iconID / 1000 * 1000:000000}/{iconID:000000}{(highResolution ? "_hr1" : string.Empty)}.tex";
}
