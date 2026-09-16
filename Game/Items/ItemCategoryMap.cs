using System.Collections.Frozen;
using System.Linq;
using Lumina.Excel.Sheets;
using OmniToolbox.Host;

namespace OmniToolbox.Items;

public sealed class ItemCategoryMap
{
    private static readonly List<byte> CurrencyFilterGroups =
    [
        29,
        38,
        40,
        47,
        52,
        53,
        54,
        55,
        56,
        57,
        58,
        59
    ];

    internal static bool IsCurrency(Item item) =>
        CurrencyFilterGroups.Contains(item.FilterGroup) ||
        item.ItemUICategory.RowId == 63 &&
        item.ItemSearchCategory.RowId == 0 &&
        item.ItemSortCategory.RowId == 3;

    public static readonly uint[] JobIds = DalamudServices.DataManager.GetExcelSheet<ClassJob>()
        .Where(job => job.UIPriority > 0 && (job.JobIndex > 0 || job.DohDolJobIndex >= 0))
        .OrderBy(job => job.UIPriority)
        .Select(job => job.RowId)
        .ToArray();

    private static readonly uint[] WeaponItemUICategoryIDs =
    [
        2, 3, 87, 106, 5, 108, 1, 96, 84, 110, 113, 4, 88, 107, 6, 7, 10, 97, 111, 105, 8, 9, 98, 89, 109
    ];

    private static readonly uint[] ToolItemUICategoryIDs =
    [
        12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 99
    ];

    private static readonly uint[] ArmorItemUICategoryIDs = [11, 34, 35, 37, 36, 38];
    private static readonly uint[] AccessoryItemUICategoryIDs = [41, 40, 42, 43, 62];
    private static readonly uint[] ConsumableItemUICategoryIDs = [44, 46];
    private static readonly uint[] MaterialItemUICategoryIDs =
    [
        45, 47, 48, 49, 50, 51, 52, 53, 54, 56, 55, 90, 91, 93, 92, 101, 102, 103, 104
    ];

    private static readonly uint[] OtherItemUICategoryIDs =
    [
        58, 59, 60, 61, 85, 81, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 57, 77, 78, 79,
        80, 82, 83, 86, 94, 33, 95, 112, 39, 100
    ];

    private static readonly FrozenDictionary<uint, ItemCategory> MainCategoryByItemUICategoryID =
        new (ItemCategory Category, uint[] IDs)[]
        {
            (ItemCategory.Weapon, WeaponItemUICategoryIDs),
            (ItemCategory.Tool, ToolItemUICategoryIDs),
            (ItemCategory.Armor, ArmorItemUICategoryIDs),
            (ItemCategory.Accessory, AccessoryItemUICategoryIDs),
            (ItemCategory.Consumable, ConsumableItemUICategoryIDs),
            (ItemCategory.Material, MaterialItemUICategoryIDs),
            (ItemCategory.Other, OtherItemUICategoryIDs)
        }
        .SelectMany(entry => entry.IDs.Select(id => KeyValuePair.Create(id, entry.Category)))
        .ToFrozenDictionary();

    private readonly IReadOnlyDictionary<ItemCategory, IReadOnlyList<ItemSubcategory>> subcategories;

    public ItemCategoryMap()
    {
        var jobs = DalamudServices.DataManager.GetExcelSheet<ClassJob>();
        var itemUICategories = DalamudServices.DataManager.GetExcelSheet<ItemUICategory>();
        var categories = new Dictionary<ItemCategory, IReadOnlyList<ItemSubcategory>>
        {
            [ItemCategory.Job] = PrependAll(JobIds.Select(id => new ItemSubcategory(
                jobs.GetRow(id).Name.ExtractText(), ItemCategory.Job, 0, id))),
            [ItemCategory.Weapon] = FromIDs(ItemCategory.Weapon, WeaponItemUICategoryIDs),
            [ItemCategory.Tool] = FromIDs(ItemCategory.Tool, ToolItemUICategoryIDs),
            [ItemCategory.Armor] = FromIDs(ItemCategory.Armor, ArmorItemUICategoryIDs),
            [ItemCategory.Accessory] = FromIDs(ItemCategory.Accessory, AccessoryItemUICategoryIDs),
            [ItemCategory.Consumable] = FromIDs(ItemCategory.Consumable, ConsumableItemUICategoryIDs),
            [ItemCategory.Material] = FromIDs(ItemCategory.Material, MaterialItemUICategoryIDs),
            [ItemCategory.Other] = FromIDs(ItemCategory.Other, OtherItemUICategoryIDs),
            [ItemCategory.Set] = PrependAll(Array.Empty<ItemSubcategory>())
        };
        categories[ItemCategory.All] = PrependAll(categories
            .SelectMany(entry => entry.Value.Skip(1).Select(value => value with
            {
                Label = $"{OmniToolbox.UI.OmniLoc.Get($"ItemSearch.Category.{entry.Key}")}/{value.Label}"
            })));
        subcategories = categories;

        IReadOnlyList<ItemSubcategory> FromIDs(ItemCategory category, IEnumerable<uint> ids) =>
            PrependAll(ids.Select(id => new ItemSubcategory(
                itemUICategories.GetRow(id).Name.ExtractText(), category, id, 0)));
    }

    public IReadOnlyList<ItemSubcategory> Get(ItemCategory category) => subcategories[category];

    public static ItemCategory Classify(uint itemUICategoryID, uint equipSlotCategoryID)
    {
        if (MainCategoryByItemUICategoryID.TryGetValue(itemUICategoryID, out var category))
        {
            return category;
        }

        if (equipSlotCategoryID is 1 or 2 or 13)
        {
            return ItemCategory.Weapon;
        }

        if (equipSlotCategoryID is >= 9 and <= 12)
        {
            return ItemCategory.Accessory;
        }

        return ItemCategory.Other;
    }

    private static IReadOnlyList<ItemSubcategory> PrependAll(IEnumerable<ItemSubcategory> values) =>
        [new(OmniToolbox.UI.OmniLoc.Get("ItemSearch.Category.All"), ItemCategory.All, 0, 0), .. values];
}

public readonly record struct ItemSubcategory(string Label, ItemCategory Category, uint ItemUICategoryID, uint JobID);
