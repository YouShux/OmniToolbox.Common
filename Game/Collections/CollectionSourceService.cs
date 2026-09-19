using System.Collections.Frozen;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;

namespace OmniToolbox.Collections;

public sealed partial class CollectionSourceService : IDisposable
{

    private static readonly IReadOnlyList<CollectionSource> EmptySources = Array.Empty<CollectionSource>();
    private static readonly FrozenSet<CollectionSourceCategory> EmptyCategories =
        Array.Empty<CollectionSourceCategory>().ToFrozenSet();
    private static readonly FrozenSet<uint> EmptyItemIDs = Array.Empty<uint>().ToFrozenSet();

    private readonly FrozenSet<uint> unobtainableItemIds;
    private readonly FrozenDictionary<uint, IReadOnlyList<CollectionSource>> itemSources;
    private readonly FrozenDictionary<uint, IReadOnlyList<uint>> blindBoxDropPools;
    private readonly FrozenDictionary<uint, IReadOnlyList<CollectionSource>> blueMageSources;
    private readonly FrozenDictionary<uint, IReadOnlyList<BlueSpellSource>> blueSpellRows;
    private readonly FrozenDictionary<uint, uint> relicAchievementIds;
    private readonly FrozenDictionary<uint, CollectionSourceCategory[]> categorySeeds;
    private readonly Dictionary<uint, HashSet<uint>> wikiShopDependencies;
    private readonly FrozenDictionary<uint, IReadOnlyList<uint>> exchangeTargetsByCostItemID;
    private FrozenSet<uint> openingStageRestrictedItemIds;
    private readonly uint[] itemIds;
    private readonly CancellationTokenSource categoryBuildCancellation = new();
    private readonly Task exchangeCategoryTask;
    private FrozenDictionary<uint, FrozenSet<CollectionSourceCategory>> itemCategories;
    private FrozenSet<uint> purchasableItemIds = EmptyItemIDs;
    private long revision;
    private bool disposed;

    public CollectionSourceService()
    {
        var itemsByID = LuminaGetter.Get<Item>()
            .Where(item => item.RowId != 0)
            .ToDictionary(item => item.RowId);
        var wikiSources = ReadWikiItemSources();
        unobtainableItemIds = wikiSources.UnobtainableItemIds;
        blindBoxDropPools = wikiSources.BlindBoxDropPools.ToFrozenDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<uint>)Array.AsReadOnly(pair.Value.Order().ToArray()));
        var itemRows = wikiSources.ItemSources;
        AddFishingSources(itemRows);

        var relicAchievements = new Dictionary<uint, uint>();
        ReadCSV("RelicItemIdToAchievement.csv", csv =>
        {
            var itemID = csv.GetField<uint>("ItemId");
            var achievementID = csv.GetField<uint>("AchievementId");
            if (itemID != 0 && achievementID != 0)
            {
                relicAchievements.TryAdd(itemID, achievementID);
            }
        });

        AddCurrentMogStationSources(itemRows);
        AddContainerSources(itemRows, itemsByID);
        AddCraftingSources(itemRows);
        AddTripleTriadSources(itemRows, itemsByID);

        var contentFinderConditions = new Dictionary<uint, ContentFinderCondition>();
        foreach (var condition in LuminaGetter.Get<ContentFinderCondition>())
        {
            if (condition.Content.RowId != 0)
            {
                contentFinderConditions.TryAdd(condition.Content.RowId, condition);
            }
        }
        var categories = BuildCategorySeeds(itemRows, contentFinderConditions);
        AddCurrencyCategories(categories, itemsByID.Values);
        AddWikiGilCategories(categories, itemRows, itemsByID);
        wikiShopDependencies = BuildWikiShopDependencies(itemRows, itemsByID);
        exchangeTargetsByCostItemID = BuildExchangeTargetsByCostItemID(wikiShopDependencies);
        openingStageRestrictedItemIds = BuildOpeningStageRestrictedItems(
            itemRows,
            wikiShopDependencies,
            contentFinderConditions);
        categorySeeds = categories.ToFrozenDictionary(
            pair => pair.Key,
            pair => pair.Value.Order().ToArray());
        itemCategories = ResolveCategories(categories, wikiShopDependencies);
        itemIds = itemsByID.Keys.Order().ToArray();

        var blueRows = new Dictionary<uint, HashSet<CollectionSource>>();
        var blueSpellDetails = new Dictionary<uint, List<BlueSpellSource>>();
        ReadCSV("BlueSpells.csv", csv =>
        {
            var actionID = csv.GetField<uint>("ActionId");
            if (actionID == 0)
            {
                return;
            }

            var sourceType = csv.GetField<string>("SourceType") ?? string.Empty;
            var mobDescription = csv.GetField<string>("MobDescription") ?? string.Empty;
            var locationDescription = csv.GetField<string>("LocationDescription") ?? string.Empty;
            var x = csv.GetField<float?>("X");
            var y = csv.GetField<float?>("Y");
            var level = csv.GetField<uint?>("Level");
            var note = csv.GetField<string>("Note") ?? string.Empty;
            if (!blueSpellDetails.TryGetValue(actionID, out var details))
            {
                blueSpellDetails[actionID] = details = [];
            }

            details.Add(new(sourceType, mobDescription, locationDescription, x, y, level, note));
            AddSource(blueRows, actionID, new(
                sourceType switch
                {
                    "carnivale" or "dungeon" or "guildhests" or "raid" or "trail" =>
                        CollectionSourceCategory.Duty,
                    "fate" => CollectionSourceCategory.Fate,
                    "hunt" => CollectionSourceCategory.TheHunt,
                    "jobquest" => CollectionSourceCategory.Quest,
                    "treasure" => CollectionSourceCategory.TreasureHunts,
                    "levequests" or "map" or "special" => CollectionSourceCategory.Other,
                    _ => throw new InvalidDataException($"未知的青魔获取类型：{sourceType}")
                },
                0,
                NormalizeBlueMageText(mobDescription),
                FormatBlueMageDetail(locationDescription, x, y, level, NormalizeBlueMageText(note))));
        });

        itemSources = Freeze(itemRows);
        blueMageSources = Freeze(blueRows);
        blueSpellRows = blueSpellDetails.ToFrozenDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<BlueSpellSource>)pair.Value.ToArray());
        relicAchievementIds = relicAchievements.ToFrozenDictionary();
        exchangeCategoryTask = Task.Run(() => BuildExchangeCategoriesAsync(categoryBuildCancellation.Token));
    }

    public long Revision => Interlocked.Read(ref revision);

    public bool IsExchangeIndexBuilding => !exchangeCategoryTask.IsCompleted;

    public IReadOnlyList<CollectionSource> GetItemSources(uint itemID) =>
        itemSources.GetValueOrDefault(itemID, EmptySources);

    public IReadOnlyDictionary<uint, IReadOnlyList<uint>> BlindBoxDropPools => blindBoxDropPools;

    public IReadOnlyList<uint> GetBlindBoxDropPool(uint blindBoxItemID) =>
        blindBoxDropPools.TryGetValue(blindBoxItemID, out var itemIds)
            ? itemIds
            : Array.Empty<uint>();

    public IReadOnlyList<uint> GetExchangeTargets(uint costItemID) =>
        exchangeTargetsByCostItemID.TryGetValue(costItemID, out var itemIds)
            ? itemIds
            : Array.Empty<uint>();

    public IReadOnlyList<CollectionSource> GetBlueMageSources(uint actionID) =>
        blueMageSources.GetValueOrDefault(actionID, EmptySources);

    public IReadOnlyList<BlueSpellSource> GetBlueSpellRows(uint actionID) =>
        blueSpellRows.GetValueOrDefault(actionID, []);

    public IReadOnlySet<CollectionSourceCategory> GetItemCategories(uint itemID) =>
        Volatile.Read(ref itemCategories).GetValueOrDefault(itemID, EmptyCategories);

    public bool IsOpeningStageRestricted(uint itemID) =>
        Volatile.Read(ref openingStageRestrictedItemIds).Contains(itemID);

    public ItemAvailability GetItemAvailability(uint itemID) =>
        unobtainableItemIds.Contains(itemID)
            ? ItemAvailability.Unobtainable
            : Volatile.Read(ref purchasableItemIds).Contains(itemID)
                ? ItemAvailability.Purchasable
                : ItemAvailability.Obtainable;

    public bool TryGetRelicAchievementID(uint itemID, out uint achievementID) =>
        relicAchievementIds.TryGetValue(itemID, out achievementID);

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        categoryBuildCancellation.Cancel();
        try
        {
            exchangeCategoryTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            categoryBuildCancellation.Dispose();
        }
    }
}
