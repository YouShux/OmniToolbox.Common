using System.Collections.Frozen;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.Data;
using OmniToolbox.Host;
using OmniToolbox.Items;
using TreeTools.Threading;

namespace OmniToolbox.Collections;

public sealed partial class CollectionSourceService : IAsyncDisposable
{
    private static readonly IReadOnlyList<CollectionSource> EmptySources = Array.Empty<CollectionSource>();
    private static readonly FrozenSet<CollectionSourceCategory> EmptyCategories =
        Array.Empty<CollectionSourceCategory>().ToFrozenSet();
    private static readonly FrozenSet<uint> EmptyItemIDs = Array.Empty<uint>().ToFrozenSet();

    private readonly FrozenSet<uint> unobtainableItemIds;
    private readonly FrozenDictionary<uint, IReadOnlyList<CollectionSource>> itemSources;
    private readonly FrozenDictionary<uint, uint[]> itemSetItemIDs;
    private readonly FrozenDictionary<uint, IReadOnlyList<uint>> blindBoxDropPools;
    private readonly FrozenDictionary<uint, IReadOnlyList<CollectionSource>> blueMageSources;
    private readonly FrozenDictionary<uint, IReadOnlyList<BlueSpellSource>> blueSpellRows;
    private readonly FrozenDictionary<uint, uint> relicAchievementIds;
    private readonly FrozenDictionary<uint, CollectionSourceCategory[]> categorySeeds;
    private readonly Dictionary<uint, HashSet<uint>> wikiShopDependencies;
    private readonly FrozenDictionary<uint, IReadOnlyList<uint>> exchangeTargetsByCostItemID;
    private FrozenSet<uint> openingStageRestrictedItemIds;
    private readonly uint[] itemIds;
    private readonly CancellationTokenSource categoryBuildCancellation;
    private readonly Task exchangeCategoryTask;
    private FrozenDictionary<uint, FrozenSet<CollectionSourceCategory>> itemCategories;
    private FrozenSet<uint> purchasableItemIds = EmptyItemIDs;
    private long revision;
    private readonly object disposalLock = new();
    private Task? disposal;

    public CollectionSourceService()
        : this(null, null, null, null, default)
    {
    }

    private CollectionSourceService(
        Dictionary<uint, Item>? preparedItems,
        JsonDocument? wikiDocument,
        JsonDocument? fishingDocument,
        PreparedSourceText? preparedText,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var itemsByID = preparedItems ?? LuminaGetter.Get<Item>()
            .Where(item => item.RowId != 0)
            .ToDictionary(item => item.RowId);
        itemSetItemIDs = LuminaGetter.Get<MirageStoreSetItem>()
            .Where(set => itemsByID.ContainsKey(set.RowId))
            .ToFrozenDictionary(
                set => set.RowId,
                set => ItemSetCatalog.GetItemSlots(set.RowId).Where(itemID => itemID != 0).ToArray());
        var wikiSources = ReadWikiItemSources(wikiDocument, preparedText?.QuestNames, cancellationToken);
        unobtainableItemIds = wikiSources.UnobtainableItemIds;
        blindBoxDropPools = wikiSources.BlindBoxDropPools.ToFrozenDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<uint>)Array.AsReadOnly(pair.Value.Order().ToArray()));
        var itemRows = wikiSources.ItemSources;
        AddFishingSources(itemRows, fishingDocument, preparedText, cancellationToken);

        var relicAchievements = new Dictionary<uint, uint>();
        EmbeddedData.ReadCSV("RelicItemIdToAchievement.csv", csv =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var itemID = csv.GetField<uint>("ItemId");
            var achievementID = csv.GetField<uint>("AchievementId");
            if (itemID != 0 && achievementID != 0)
            {
                relicAchievements.TryAdd(itemID, achievementID);
            }
        });

        cancellationToken.ThrowIfCancellationRequested();
        AddCurrentMogStationSources(itemRows);
        AddPromotionalUnlockSources(itemRows);
        AddContainerSources(itemRows, itemsByID, preparedText?.ItemNames);
        cancellationToken.ThrowIfCancellationRequested();
        AddCraftingSources(itemRows);
        cancellationToken.ThrowIfCancellationRequested();
        if (preparedText is null)
        {
            AddPVPSeasonPortraitSources(itemRows);
            foreach (var series in LuminaGetter.Get<PvPSeries>())
                AddPVPSeriesSources(itemRows, series);
            foreach (var toolClass in LuminaGetter.Get<WKSCosmoToolClass>())
                AddCosmicToolSources(itemRows, toolClass);
            AddCosmicToolReplicaSources(itemRows);
            foreach (var stage in LuminaGetter.Get<RelicItem>())
                AddRelicSources(itemRows, stage);
            foreach (var questID in UnfinishedRelicQuestIDs)
                AddUnfinishedRelicSources(itemRows, questID);
            foreach (var questID in DisguiseEquipmentQuestIDs)
                AddDisguiseEquipmentSources(itemRows, questID);
            foreach (var itemID in RetiredArdentEquipmentIDs)
                AddRetiredArdentEquipmentSource(itemRows, itemID);
            foreach (var itemID in EternalBondWristletIDs)
                AddEternalBondWristletSource(itemRows, itemID);
            foreach (var itemID in LegacyGatheringToolIDs)
                AddLegacyGatheringToolSource(itemRows, itemID);
            foreach (var row in ReadHistoricalEquipmentSourceRows(cancellationToken))
                AddHistoricalEquipmentSource(itemRows, row.ItemID, row.ConditionID);
        }
        else
        {
            foreach (var (itemID, sources) in preparedText.SupplementalSources)
                foreach (var source in sources)
                    AddSourceIfMissing(itemRows, itemID, source);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (preparedText is null)
            AddTripleTriadSources(itemRows, itemsByID);
        else
            foreach (var (itemID, sources) in preparedText.TripleTriadSources)
                foreach (var source in sources)
                    ReplaceSource(itemRows, itemID, source);
        AddItemSetSources(itemRows, itemsByID, preparedText?.ItemNames);

        var contentFinderConditions = new Dictionary<uint, ContentFinderCondition>();
        foreach (var condition in LuminaGetter.Get<ContentFinderCondition>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (condition.Content.RowId != 0)
            {
                contentFinderConditions.TryAdd(condition.Content.RowId, condition);
            }
        }
        var categories = BuildCategorySeeds(itemRows, contentFinderConditions);
        AddCurrencyCategories(categories, itemsByID.Values);
        AddWikiGilCategories(categories, itemRows, itemsByID);
        wikiShopDependencies = BuildWikiShopDependencies(itemRows);
        exchangeTargetsByCostItemID = BuildExchangeTargetsByCostItemID(wikiShopDependencies);
        openingStageRestrictedItemIds = BuildOpeningStageRestrictedItems(
            itemRows,
            wikiShopDependencies,
            contentFinderConditions);
        categorySeeds = categories.ToFrozenDictionary(
            pair => pair.Key,
            pair => pair.Value.Order().ToArray());
        itemCategories = ResolveCategories(categories, wikiShopDependencies, cancellationToken);
        itemIds = itemsByID.Keys.Order().ToArray();

        var blueSpellDetails = new Dictionary<uint, List<BlueSpellSource>>();
        EmbeddedData.ReadCSV("BlueSpells.csv", csv =>
        {
            cancellationToken.ThrowIfCancellationRequested();
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

            details.Add(new(sourceType, mobDescription, locationDescription, x, y, level, note)
            {
                DutyID = csv.GetField<uint>("DutyID"),
                TerritoryID = csv.GetField<uint>("TerritoryID")
            });
        });

        itemSources = Freeze(itemRows);
        blueMageSources = BuildBlueMageSources(blueSpellDetails);
        blueSpellRows = blueSpellDetails.ToFrozenDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<BlueSpellSource>)pair.Value.ToArray());
        relicAchievementIds = relicAchievements.ToFrozenDictionary();
        cancellationToken.ThrowIfCancellationRequested();
        categoryBuildCancellation = new();
        exchangeCategoryTask = Task.Run(() => BuildExchangeCategoriesAsync(categoryBuildCancellation.Token));
    }

    public long Revision => Interlocked.Read(ref revision);

    /// <summary>在后台解析来源资源和构建索引，并在框架线程分帧取得游戏文本。</summary>
    public static async Task<CollectionSourceService> CreateAsync(CancellationToken cancellationToken = default)
    {
        using var wikiDocument = await Task.Run(() => ReadDocument("WikiItemSources.json"), cancellationToken).ConfigureAwait(false);
        using var fishingDocument = await Task.Run(() => ReadDocument("FishingSources.json"), cancellationToken).ConfigureAwait(false);
        var itemsByID = await Task.Run(() => LuminaGetter.Get<Item>()
            .Where(item => item.RowId != 0).ToDictionary(item => item.RowId), cancellationToken).ConfigureAwait(false);
        var text = new PreparedSourceText();
        var textIDs = await Task.Run(() => GetSourceTextIDs(wikiDocument, fishingDocument, itemsByID, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        await FrameworkWorkScheduler.RunAsync(textIDs.ItemIDs, itemID =>
        {
            if (itemsByID.TryGetValue(itemID, out var item))
                text.ItemNames[itemID] = item.Name.ExtractText();
        }, cancellationToken).ConfigureAwait(false);
        await FrameworkWorkScheduler.RunAsync(textIDs.QuestIDs, questID =>
        {
            if (LuminaGetter.TryGetRow<Quest>(questID, out var quest))
                text.QuestNames[questID] = quest.Name.ExtractText();
        }, cancellationToken).ConfigureAwait(false);
        await FrameworkWorkScheduler.RunAsync(textIDs.PlaceNameIDs,
            placeNameID => text.PlaceNames[placeNameID] = LuminaWrapper.GetPlaceName(placeNameID),
            cancellationToken).ConfigureAwait(false);
        await FrameworkWorkScheduler.RunAsync(textIDs.WeatherIDs,
            weatherID => text.WeatherNames[weatherID] = LuminaWrapper.GetWeatherName(weatherID),
            cancellationToken).ConfigureAwait(false);
        var cards = await Task.Run(() => itemsByID.Values
            .Where(item => item.ItemAction.IsValid && item.ItemAction.Value.Action.RowId == 3_357)
            .ToArray(), cancellationToken).ConfigureAwait(false);
        await FrameworkWorkScheduler.RunAsync(cards,
            item => AddTripleTriadSource(text.TripleTriadSources, item), cancellationToken).ConfigureAwait(false);
        var toolClasses = await Task.Run(() => LuminaGetter.Get<WKSCosmoToolClass>().ToArray(),
            cancellationToken).ConfigureAwait(false);
        await FrameworkWorkScheduler.RunAsync(toolClasses,
            toolClass => AddCosmicToolSources(text.SupplementalSources, toolClass), cancellationToken).ConfigureAwait(false);
        await DalamudServices.Framework.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddCosmicToolReplicaSources(text.SupplementalSources);
            AddPVPSeasonPortraitSources(text.SupplementalSources);
        }, cancellationToken).ConfigureAwait(false);
        var pvpSeries = await Task.Run(() => LuminaGetter.Get<PvPSeries>().ToArray(),
            cancellationToken).ConfigureAwait(false);
        await FrameworkWorkScheduler.RunAsync(pvpSeries,
            series => AddPVPSeriesSources(text.SupplementalSources, series), cancellationToken).ConfigureAwait(false);
        var relicStages = await Task.Run(() => LuminaGetter.Get<RelicItem>().ToArray(),
            cancellationToken).ConfigureAwait(false);
        await FrameworkWorkScheduler.RunAsync(relicStages,
            stage => AddRelicSources(text.SupplementalSources, stage), cancellationToken).ConfigureAwait(false);
        await FrameworkWorkScheduler.RunAsync(UnfinishedRelicQuestIDs,
            questID => AddUnfinishedRelicSources(text.SupplementalSources, questID), cancellationToken).ConfigureAwait(false);
        await FrameworkWorkScheduler.RunAsync(DisguiseEquipmentQuestIDs,
            questID => AddDisguiseEquipmentSources(text.SupplementalSources, questID), cancellationToken).ConfigureAwait(false);
        await FrameworkWorkScheduler.RunAsync(RetiredArdentEquipmentIDs,
            itemID => AddRetiredArdentEquipmentSource(text.SupplementalSources, itemID), cancellationToken).ConfigureAwait(false);
        await FrameworkWorkScheduler.RunAsync(EternalBondWristletIDs,
            itemID => AddEternalBondWristletSource(text.SupplementalSources, itemID), cancellationToken).ConfigureAwait(false);
        await FrameworkWorkScheduler.RunAsync(LegacyGatheringToolIDs,
            itemID => AddLegacyGatheringToolSource(text.SupplementalSources, itemID), cancellationToken).ConfigureAwait(false);
        var historicalEquipment = await Task.Run(() => ReadHistoricalEquipmentSourceRows(cancellationToken),
            cancellationToken).ConfigureAwait(false);
        await FrameworkWorkScheduler.RunAsync(historicalEquipment,
            row => AddHistoricalEquipmentSource(text.SupplementalSources, row.ItemID, row.ConditionID), cancellationToken).ConfigureAwait(false);
        return await Task.Run(() => new CollectionSourceService(
            itemsByID, wikiDocument, fishingDocument, text, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

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
        unobtainableItemIds.Contains(itemID) || RetiredArdentEquipmentIDs.Contains(itemID)
            ? ItemAvailability.Unobtainable
            : Volatile.Read(ref purchasableItemIds).Contains(itemID)
                ? ItemAvailability.Purchasable
                : ItemAvailability.Obtainable;

    public bool TryGetRelicAchievementID(uint itemID, out uint achievementID) =>
        relicAchievementIds.TryGetValue(itemID, out achievementID);

    /// <summary>取消来源分类构建，异步等待实际退出；重复调用等待同一个释放任务。</summary>
    public ValueTask DisposeAsync()
    {
        lock (disposalLock)
            return new(disposal ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        try
        {
            await categoryBuildCancellation.CancelAsync().ConfigureAwait(false);
        }
        finally
        {
            try
            {
                await exchangeCategoryTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (categoryBuildCancellation.IsCancellationRequested)
            {
            }
            finally
            {
                categoryBuildCancellation.Dispose();
            }
        }
    }

    private static JsonDocument ReadDocument(string resourceName)
    {
        using var stream = EmbeddedData.Open(resourceName);
        return JsonDocument.Parse(stream);
    }

    private static (uint[] ItemIDs, uint[] QuestIDs, uint[] PlaceNameIDs, uint[] WeatherIDs) GetSourceTextIDs(
        JsonDocument wikiDocument,
        JsonDocument fishingDocument,
        IReadOnlyDictionary<uint, Item> itemsByID,
        CancellationToken cancellationToken)
    {
        var itemIDs = new HashSet<uint>();
        try
        {
            EmbeddedData.ReadCSV("ItemSupplement.csv", csv =>
            {
                var itemID = csv.GetField<uint>("ItemId");
                var sourceItemID = csv.GetField<uint>("SourceItemId");
                if (itemID != 0 && sourceItemID != 0 && csv.GetField<uint>("ItemSupplementSource") != 1)
                    itemIDs.Add(sourceItemID);
            });
        }
        catch (Exception)
        {
        }

        foreach (var set in LuminaGetter.Get<MirageStoreSetItem>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (itemsByID.ContainsKey(set.RowId))
                foreach (var itemID in ItemSetCatalog.GetItemSlots(set.RowId))
                    if (itemID != 0)
                        itemIDs.Add(itemID);
        }

        var questIDs = new HashSet<uint>();
        foreach (var source in wikiDocument.RootElement.GetProperty("sources").EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var questID = source.GetProperty("i").GetUInt32();
            if (source.GetProperty("c").GetInt32() == (int)CollectionSourceCategory.Quest && questID != 0)
                questIDs.Add(questID);
            if (source.TryGetProperty("q", out var requiredQuests))
                foreach (var quest in requiredQuests.EnumerateArray())
                    questIDs.Add(quest.GetUInt32());
        }

        var placeNameIDs = fishingDocument.RootElement.GetProperty("spots").EnumerateArray()
            .Select(spot => spot.GetProperty("p").GetUInt32()).Distinct().ToArray();
        var weatherIDs = new HashSet<uint>();
        foreach (var item in fishingDocument.RootElement.GetProperty("items").EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.TryGetProperty("w", out var weather))
                foreach (var row in weather.EnumerateArray())
                    weatherIDs.Add(row.GetUInt32());
            if (item.TryGetProperty("v", out var previousWeather))
                foreach (var row in previousWeather.EnumerateArray())
                    weatherIDs.Add(row.GetUInt32());
        }

        return (itemIDs.ToArray(), questIDs.ToArray(), placeNameIDs, weatherIDs.ToArray());
    }

    private sealed class PreparedSourceText
    {
        public Dictionary<uint, string> ItemNames { get; } = [];

        public Dictionary<uint, string> QuestNames { get; } = [];

        public Dictionary<uint, string> PlaceNames { get; } = [];

        public Dictionary<uint, string> WeatherNames { get; } = [];

        public Dictionary<uint, HashSet<CollectionSource>> TripleTriadSources { get; } = [];

        public Dictionary<uint, HashSet<CollectionSource>> SupplementalSources { get; } = [];
    }
}
