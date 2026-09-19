using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;

namespace OmniToolbox.Items;

internal static class HairstyleData
{
    private const uint UNLOCK_LINK_ITEM_ACTION_ID = 2633;
    private static Dictionary<uint, Item>? UnlockItemsByLink;

    public static IEnumerable<CharaMakeCustomize> GetPurchasableRows()
    {
        var hairstyleRowIDs = new HashSet<uint>();
        foreach (var hairMakeType in LuminaGetter.Get<HairMakeType>())
        {
            foreach (var rowID in hairMakeType.CharaMakeStruct[0].SubMenuParam)
            {
                if (rowID != 0)
                {
                    hairstyleRowIDs.Add(rowID);
                }
            }
        }

        foreach (var row in LuminaGetter.Get<CharaMakeCustomize>())
        {
            if (row.IsPurchasable &&
                row.UnlockLink != 0 &&
                row.FeatureID <= byte.MaxValue &&
                hairstyleRowIDs.Contains(row.RowId))
            {
                yield return row;
            }
        }
    }

    public static Item? GetUnlockItem(CharaMakeCustomize hairstyle)
    {
        if (hairstyle.HintItem.ValueNullable is { } hintItem)
        {
            return hintItem;
        }

        if (hairstyle.UnlockLink == 0)
        {
            return null;
        }

        UnlockItemsByLink ??= BuildUnlockItemsByLink();
        return UnlockItemsByLink.GetValueOrDefault(hairstyle.UnlockLink);
    }

    private static Dictionary<uint, Item> BuildUnlockItemsByLink()
    {
        var items = new Dictionary<uint, Item>();
        foreach (var item in LuminaGetter.Get<Item>())
        {
            if (!item.ItemAction.IsValid)
            {
                continue;
            }

            var itemAction = item.ItemAction.Value;
            if (itemAction.Action.RowId == UNLOCK_LINK_ITEM_ACTION_ID && itemAction.Data[0] > 0)
            {
                items.TryAdd((uint)itemAction.Data[0], item);
            }
        }

        return items;
    }
}
