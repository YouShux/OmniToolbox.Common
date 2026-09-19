using System.Collections.Frozen;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lumina.Excel.Sheets;
using OmenTools;
using OmenTools.Info.Game.ItemSource;
using OmenTools.Info.Game.ItemSource.Enums;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.Host;

namespace OmniToolbox.Collections;

public sealed partial class CollectionSourceService
{

    private static readonly (uint ItemId, CollectionSourceCategory Category)[] FixedCurrencyCategories =
    [
        (1, CollectionSourceCategory.Gil),
        (20, CollectionSourceCategory.CompanySeals),
        (21, CollectionSourceCategory.CompanySeals),
        (22, CollectionSourceCategory.CompanySeals),
        (25, CollectionSourceCategory.PvP),
        (21_067, CollectionSourceCategory.PvP),
        (36_656, CollectionSourceCategory.PvP),
        (40_479, CollectionSourceCategory.PvP),
        (30_341, CollectionSourceCategory.Duty),
        (27, CollectionSourceCategory.TheHunt),
        (10_307, CollectionSourceCategory.TheHunt),
        (26_533, CollectionSourceCategory.TheHunt),
        (26_807, CollectionSourceCategory.Fate),
        (29, CollectionSourceCategory.MGP),
        (41_629, CollectionSourceCategory.MGP),
        (10_308, CollectionSourceCategory.Scrips),
        (10_309, CollectionSourceCategory.Scrips),
        (10_310, CollectionSourceCategory.Scrips),
        (10_311, CollectionSourceCategory.Scrips),
        (17_833, CollectionSourceCategory.Scrips),
        (17_834, CollectionSourceCategory.Scrips),
        (25_199, CollectionSourceCategory.Scrips),
        (25_200, CollectionSourceCategory.Scrips),
        (33_913, CollectionSourceCategory.Scrips),
        (33_914, CollectionSourceCategory.Scrips),
        (41_784, CollectionSourceCategory.Scrips),
        (41_785, CollectionSourceCategory.Scrips),
        (28_063, CollectionSourceCategory.RestorationZone),
        (47_343, CollectionSourceCategory.RestorationZone),
        (47_594, CollectionSourceCategory.RestorationZone),
        (47_868, CollectionSourceCategory.FieldOperations),
        (21_172, CollectionSourceCategory.Achievement)
    ];

    private async Task BuildExchangeCategoriesAsync(CancellationToken cancellationToken)
    {
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(TimeSpan.FromMinutes(2));
        var buildCancellationToken = timeoutCancellation.Token;
        try
        {
            var sourceState = ItemSourceInfo.QueryExchangeItems(1).State;
            while (sourceState == ItemSourceQueryState.Building)
            {
                await Task.Delay(1_000, buildCancellationToken).ConfigureAwait(false);
                sourceState = ItemSourceInfo.QueryExchangeItems(1).State;
            }

            if (sourceState == ItemSourceQueryState.Failed)
            {
                DalamudServices.PluginLog.Warning("OmenTools item-source snapshot failed; exchange source categories were skipped.");
                return;
            }

            var categories = CreateCategoryMap();
            PropagateCategories(categories, wikiShopDependencies);
            var dependencies = new Dictionary<uint, HashSet<uint>>();
            var purchasableItems = new HashSet<uint>();
            for (var index = 0; index < itemIds.Length; index++)
            {
                buildCancellationToken.ThrowIfCancellationRequested();
                var costItemID = itemIds[index];
                var result = ItemSourceInfo.QueryExchangeItems(costItemID);
                if (result is { State: ItemSourceQueryState.Ready, Data: { } data })
                {
                    for (var itemIndex = 0; itemIndex < data.Items.Count; itemIndex++)
                    {
                        var targetItemID = data.Items[itemIndex].ItemID;
                        AddCategory(categories, targetItemID, CollectionSourceCategory.Shop);
                        var isPurchasable = costItemID == 1 && HasStandaloneGilPrice(targetItemID);
                        if (isPurchasable)
                        {
                            purchasableItems.Add(targetItemID);
                        }

                        if (costItemID != 1 || isPurchasable)
                        {
                            AddDependency(dependencies, targetItemID, costItemID);
                        }
                    }
                }

                if ((index & 255) == 255)
                {
                    await Task.Delay(1, buildCancellationToken).ConfigureAwait(false);
                }
            }

            Volatile.Write(ref purchasableItemIds, purchasableItems.ToFrozenSet());
            Volatile.Write(ref itemCategories, ResolveCategories(categories, dependencies));
            Volatile.Write(
                ref openingStageRestrictedItemIds,
                ExpandOpeningStageRestrictedItems(
                    Volatile.Read(ref openingStageRestrictedItemIds),
                    dependencies));
            Interlocked.Increment(ref revision);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (OperationCanceledException)
        {
            DalamudServices.PluginLog.Warning(
                $"Collection exchange category indexing exceeded {TimeSpan.FromMinutes(2).TotalSeconds:0} seconds and was stopped.");
        }
        catch (Exception ex)
        {
            DalamudServices.PluginLog.Warning(ex, "Collection exchange category indexing failed.");
        }
    }

    private static bool HasStandaloneGilPrice(uint itemID)
    {
        return ItemSourceInfo.Query(itemID) is { State: ItemSourceQueryState.Ready, Data: { } data } &&
               data.NPCInfos.Any(npc =>
                   npc.CostInfos.Any(cost => cost.ItemID == 1) &&
                   npc.CostInfos.All(cost => cost.ItemID == 1));
    }

    private Dictionary<uint, HashSet<CollectionSourceCategory>> CreateCategoryMap()
    {
        var categories = new Dictionary<uint, HashSet<CollectionSourceCategory>>(categorySeeds.Count);
        foreach (var (itemId, sourceCategories) in categorySeeds)
        {
            categories.Add(itemId, sourceCategories.ToHashSet());
        }

        return categories;
    }

    private static FrozenDictionary<uint, FrozenSet<CollectionSourceCategory>> ResolveCategories(
        Dictionary<uint, HashSet<CollectionSourceCategory>> categories,
        IReadOnlyDictionary<uint, HashSet<uint>> dependencies)
    {
        PropagateCategories(categories, dependencies);
        return categories
            .Where(pair => pair.Value.Count != 0)
            .ToFrozenDictionary(pair => pair.Key, pair => pair.Value.ToFrozenSet());
    }

    private static void PropagateCategories(
        Dictionary<uint, HashSet<CollectionSourceCategory>> categories,
        IReadOnlyDictionary<uint, HashSet<uint>> dependencies)
    {
        for (var depth = 0; depth < 10; depth++)
        {
            var changed = false;
            foreach (var (itemId, sourceItemIds) in dependencies)
            {
                if (!categories.TryGetValue(itemId, out var itemCategorySet))
                {
                    itemCategorySet = [];
                    categories.Add(itemId, itemCategorySet);
                }

                foreach (var sourceItemID in sourceItemIds)
                {
                    if (!categories.TryGetValue(sourceItemID, out var sourceCategorySet))
                    {
                        continue;
                    }

                    foreach (var category in sourceCategorySet)
                    {
                        if (category is CollectionSourceCategory.Shop or CollectionSourceCategory.Container)
                        {
                            continue;
                        }

                        changed |= itemCategorySet.Add(category);
                    }
                }
            }

            if (!changed)
            {
                break;
            }
        }
    }

    private static Dictionary<uint, HashSet<uint>> BuildWikiShopDependencies(
        IReadOnlyDictionary<uint, HashSet<CollectionSource>> itemRows,
        IReadOnlyDictionary<uint, Item> itemsByID)
    {
        var itemIdsByName = new Dictionary<string, uint>(itemsByID.Count, StringComparer.Ordinal);
        foreach (var item in itemsByID.Values)
        {
            if (!item.Name.IsEmpty)
            {
                itemIdsByName.TryAdd(item.Name.ExtractText(), item.RowId);
            }
        }

        var dependencies = new Dictionary<uint, HashSet<uint>>();
        foreach (var (itemId, sources) in itemRows)
        {
            foreach (var source in sources)
            {
                if (source.Category != CollectionSourceCategory.Shop ||
                    !TryGetWikiShopRequirements(source.Detail, out var requirements))
                {
                    continue;
                }

                foreach (var requirement in requirements.Split(
                             '、',
                             StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var separatorIndex = requirement.IndexOf(' ');
                    if (separatorIndex > 0 &&
                        itemIdsByName.TryGetValue(requirement[(separatorIndex + 1)..].Trim(), out var costItemID))
                    {
                        AddDependency(dependencies, itemId, costItemID);
                    }
                }
            }
        }

        return dependencies;
    }

    private static FrozenDictionary<uint, IReadOnlyList<uint>> BuildExchangeTargetsByCostItemID(
        IReadOnlyDictionary<uint, HashSet<uint>> dependencies)
    {
        var targetsByCostItemID = new Dictionary<uint, HashSet<uint>>();
        foreach (var (targetItemID, costItemIDs) in dependencies)
        {
            foreach (var costItemID in costItemIDs)
            {
                AddDependency(targetsByCostItemID, costItemID, targetItemID);
            }
        }

        return targetsByCostItemID.ToFrozenDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<uint>)Array.AsReadOnly(pair.Value.Order().ToArray()));
    }

    private static void AddWikiGilCategories(
        IDictionary<uint, HashSet<CollectionSourceCategory>> categories,
        IReadOnlyDictionary<uint, HashSet<CollectionSource>> itemRows,
        IReadOnlyDictionary<uint, Item> itemsByID)
    {
        foreach (var (itemId, sources) in itemRows)
        {
            if (!itemsByID.TryGetValue(itemId, out var item) || item.PriceMid == 0)
            {
                continue;
            }

            foreach (var source in sources)
            {
                if (source.Category == CollectionSourceCategory.Shop &&
                    source.Description.StartsWith("商店：", StringComparison.Ordinal) &&
                    !TryGetWikiShopRequirements(source.Detail, out _))
                {
                    AddCategory(categories, itemId, CollectionSourceCategory.Gil);
                    break;
                }
            }
        }
    }

    internal static bool TryGetWikiShopRequirements(string detail, out string requirements)
    {
        const string REQUIRED_PREFIX = "所需：";
        var startIndex = detail.IndexOf(REQUIRED_PREFIX, StringComparison.Ordinal);
        if (startIndex < 0)
        {
            requirements = string.Empty;
            return false;
        }

        startIndex += REQUIRED_PREFIX.Length;
        var endIndex = detail.IndexOf(" | ", startIndex, StringComparison.Ordinal);
        requirements = detail[
            startIndex..(endIndex < 0 ? detail.Length : endIndex)].Trim();
        return requirements.Length != 0;
    }

    private static Dictionary<uint, HashSet<CollectionSourceCategory>> BuildCategorySeeds(
        IReadOnlyDictionary<uint, HashSet<CollectionSource>> itemRows,
        IReadOnlyDictionary<uint, ContentFinderCondition> contentFinderConditions)
    {
        var categories = new Dictionary<uint, HashSet<CollectionSourceCategory>>(itemRows.Count);
        foreach (var (itemId, sources) in itemRows)
        {
            foreach (var source in sources)
            {
                AddCategory(
                    categories,
                    itemId,
                    source.Category == CollectionSourceCategory.Duty
                        ? GetDutyCategory(source.SourceID, contentFinderConditions)
                        : source.Category);
                if (source.Category == CollectionSourceCategory.Achievement &&
                    source.SourceID != 0 &&
                    LuminaGetter.TryGetRow<Achievement>(source.SourceID, out var achievement) &&
                    achievement.AchievementCategory.Value.AchievementKind.RowId == 2)
                {
                    AddCategory(categories, itemId, CollectionSourceCategory.PvP);
                }
            }
        }

        return categories;
    }

    private static CollectionSourceCategory GetDutyCategory(
        uint contentID,
        IReadOnlyDictionary<uint, ContentFinderCondition> contentFinderConditions)
    {
        if (!contentFinderConditions.TryGetValue(contentID, out var condition))
        {
            return CollectionSourceCategory.Duty;
        }

        return condition.ContentType.RowId switch
        {
            6 => CollectionSourceCategory.PvP,
            9 => CollectionSourceCategory.TreasureHunts,
            13 => CollectionSourceCategory.BeastTribes,
            21 => CollectionSourceCategory.DeepDungeon,
            _ => CollectionSourceCategory.Duty
        };
    }

    private static FrozenSet<uint> BuildOpeningStageRestrictedItems(
        IReadOnlyDictionary<uint, HashSet<CollectionSource>> itemRows,
        IReadOnlyDictionary<uint, HashSet<uint>> dependencies,
        IReadOnlyDictionary<uint, ContentFinderCondition> contentFinderConditions)
    {
        var restrictedItems = itemRows
            .Where(pair => pair.Value.Any(source =>
                source.Category == CollectionSourceCategory.Duty &&
                contentFinderConditions.TryGetValue(source.SourceID, out var condition) &&
                condition.HighEndDuty &&
                condition.ContentType.RowId == 5))
            .Select(static pair => pair.Key)
            .ToHashSet();

        return ExpandOpeningStageRestrictedItems(restrictedItems, dependencies);
    }

    private static FrozenSet<uint> ExpandOpeningStageRestrictedItems(
        IEnumerable<uint> seeds,
        IReadOnlyDictionary<uint, HashSet<uint>> dependencies)
    {
        var restrictedItems = seeds.ToHashSet();
        for (var depth = 0; depth < 10; depth++)
        {
            var changed = false;
            foreach (var (itemID, costItemIDs) in dependencies)
            {
                if (costItemIDs.Any(restrictedItems.Contains))
                {
                    changed |= restrictedItems.Add(itemID);
                }
            }

            if (!changed)
            {
                break;
            }
        }

        return restrictedItems.ToFrozenSet();
    }

    private static void AddCurrencyCategories(
        IDictionary<uint, HashSet<CollectionSourceCategory>> categories,
        IEnumerable<Item> items)
    {
        for (var index = 0; index < FixedCurrencyCategories.Length; index++)
        {
            AddCategory(
                categories,
                FixedCurrencyCategories[index].ItemId,
                FixedCurrencyCategories[index].Category);
        }

        foreach (var row in LuminaGetter.Get<TomestonesItem>())
        {
            if (row.Item.RowId != 0)
            {
                AddCategory(categories, row.Item.RowId, CollectionSourceCategory.Tomestones);
            }
        }

        foreach (var row in LuminaGetter.Get<BeastTribe>())
        {
            if (row.CurrencyItem.RowId != 0)
            {
                AddCategory(categories, row.CurrencyItem.RowId, CollectionSourceCategory.BeastTribes);
            }
        }

        foreach (var item in items)
        {
            switch (item.ItemSortCategory.RowId)
            {
                case 0:
                    if (item.FilterGroup is 55 or 56)
                    {
                        AddCategory(categories, item.RowId, CollectionSourceCategory.RestorationZone);
                    }
                    else if (item.FilterGroup == 47)
                    {
                        AddCategory(categories, item.RowId, CollectionSourceCategory.IslandSanctuary);
                    }

                    break;
                case 41:
                    AddCategory(categories, item.RowId, CollectionSourceCategory.DeepDungeon);
                    break;
                case 44:
                case 48:
                case 86:
                    AddCategory(categories, item.RowId, CollectionSourceCategory.FieldOperations);
                    break;
                case 55:
                    if (item.Unknown4 == 24_000)
                    {
                        AddCategory(categories, item.RowId, CollectionSourceCategory.Fate);
                    }
                    else if (item.Unknown4 is 10_000 or 3_000)
                    {
                        AddCategory(categories, item.RowId, CollectionSourceCategory.Duty);
                    }
                    else if (item.Unknown4 == 17_000)
                    {
                        AddCategory(categories, item.RowId, CollectionSourceCategory.Scrips);
                    }
                    else if ((item.Unknown4 is 0 or 1 or 5) && !item.Lot)
                    {
                        AddCategory(categories, item.RowId, CollectionSourceCategory.RestorationZone);
                    }

                    break;
            }
        }
    }

    private static void AddCategory(
        IDictionary<uint, HashSet<CollectionSourceCategory>> categories,
        uint itemID,
        CollectionSourceCategory category)
    {
        if (!categories.TryGetValue(itemID, out var values))
        {
            values = [];
            categories.Add(itemID, values);
        }

        values.Add(category);
    }

    private static void AddDependency(
        IDictionary<uint, HashSet<uint>> dependencies,
        uint itemID,
        uint sourceItemID)
    {
        if (itemID == 0 || sourceItemID == 0 || itemID == sourceItemID)
        {
            return;
        }

        if (!dependencies.TryGetValue(itemID, out var sourceItemIds))
        {
            sourceItemIds = [];
            dependencies.Add(itemID, sourceItemIds);
        }

        sourceItemIds.Add(sourceItemID);
    }
}
