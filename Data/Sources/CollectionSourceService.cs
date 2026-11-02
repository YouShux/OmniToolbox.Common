using System.Collections.Frozen;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.Data;
using OmniToolbox.Items;
using TreeTools.Threading;
using Action = System.Action;

namespace OmniToolbox.Collections;

public sealed partial class CollectionSourceService : IAsyncDisposable
{
    private static readonly IReadOnlyList<CollectionSource>     EmptySources    = Array.Empty<CollectionSource>();
    private static readonly FrozenSet<CollectionSourceCategory> EmptyCategories =
        Array.Empty<CollectionSourceCategory>().ToFrozenSet();
    private static readonly FrozenSet<uint>                     EmptyItemIDs    = Array.Empty<uint>().ToFrozenSet();

    private readonly FrozenSet<uint>                                         unobtainableItemIds;
    private readonly FrozenDictionary<uint, IReadOnlyList<CollectionSource>> itemSources;
    private readonly FrozenDictionary<uint, uint[]>                          itemSetItemIDs;
    private readonly FrozenDictionary<uint, IReadOnlyList<uint>>             blindBoxDropPools;
    private readonly FrozenDictionary<uint, IReadOnlyList<CollectionSource>> blueMageSources;
    private readonly FrozenDictionary<uint, IReadOnlyList<BlueSpellSource>>  blueSpellRows;
    private readonly FrozenDictionary<uint, uint>                            relicAchievementIds;
    private readonly FrozenDictionary<uint, CollectionSourceCategory[]>      categorySeeds;
    private readonly Dictionary<uint, HashSet<uint>>                         wikiShopDependencies;
    private readonly FrozenDictionary<uint, IReadOnlyList<uint>>             exchangeTargetsByCostItemID;
    private FrozenSet<uint>                                                  openingStageRestrictedItemIds;
    private readonly uint[]                                                  itemIds;
    private readonly CancellationTokenSource                                 categoryBuildCancellation;
    private readonly Task                                                    exchangeCategoryTask;
    private FrozenDictionary<uint, FrozenSet<CollectionSourceCategory>>      itemCategories;
    private FrozenSet<uint>                                                  purchasableItemIds            = EmptyItemIDs;
    private long                                                             revision;
    private readonly object                                                  disposalLock                  = new();
    private Task?                                                            disposal;

    public CollectionSourceService()
        : this(null, null, null, null, default)
    {
    }

    private CollectionSourceService
    (
        Dictionary<uint, Item>? preparedItems,
        JsonDocument?           wikiDocument,
        JsonDocument?           fishingDocument,
        PreparedSourceText?     preparedText,
        CancellationToken       cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var itemsByID = preparedItems ?? LuminaGetter.Get<Item>()
            .Where(item => item.RowId != 0)
            .ToDictionary(item => item.RowId);
        itemSetItemIDs = preparedText?.ItemSetItemIDs ?? LuminaGetter.Get<MirageStoreSetItem>()
            .Where(set => itemsByID.ContainsKey(set.RowId))
            .ToFrozenDictionary(
                set => set.RowId,
                set => ItemSetCatalog.GetItemSlots(set.RowId).Where(itemID => itemID != 0).ToArray());
        var wikiSources = ReadWikiItemSources(wikiDocument, preparedText?.QuestNames, cancellationToken, preparedText?.ItemNames);
        unobtainableItemIds = wikiSources.UnobtainableItemIds;
        blindBoxDropPools   = wikiSources.BlindBoxDropPools.ToFrozenDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<uint>)Array.AsReadOnly(pair.Value.Order().ToArray()));
        var itemRows = wikiSources.ItemSources;
        AddFishingSources(itemRows, fishingDocument, preparedText, cancellationToken);
        AddRelicMaterialSources(itemRows);

        var relicAchievements = new Dictionary<uint, uint>();
        EmbeddedData.ReadRows("RelicItemIdToAchievement.csv", csv =>
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
        AddContainerSources(itemRows, itemsByID, preparedText?.ItemNames, preparedText?.ContainerSourceRows);
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
                    AddSource(itemRows, itemID, source);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (preparedText is null)
            AddTripleTriadSources(itemRows, itemsByID);
        else
            foreach (var (itemID, sources) in preparedText.TripleTriadSources)
                foreach (var source in sources)
                    AddSource(itemRows, itemID, source);
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
        var (categories, shopDependencies, restrictedItems, gilShopItemIDs) = BuildCategorySeeds(
            itemRows, itemsByID, contentFinderConditions, cancellationToken);
        AddCurrencyCategories(categories, itemsByID.Values);
        foreach (var itemID in gilShopItemIDs)
            AddCategory(categories, itemID, CollectionSourceCategory.Gil);
        wikiShopDependencies          = shopDependencies;
        exchangeTargetsByCostItemID   = BuildExchangeTargetsByCostItemID(wikiShopDependencies);
        openingStageRestrictedItemIds = ExpandOpeningStageRestrictedItems(restrictedItems, wikiShopDependencies);
        categorySeeds                 = categories.ToFrozenDictionary(
            pair => pair.Key,
            pair => pair.Value.Order().ToArray());
        itemCategories                = ResolveCategories(categories, wikiShopDependencies, cancellationToken);
        itemIds                       = itemsByID.Keys.Order().ToArray();

        var blueSpellDetails = new Dictionary<uint, List<BlueSpellSource>>();
        EmbeddedData.ReadRows("BlueSpells.csv", csv =>
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

        AddSupplementalSourceLocations(itemRows);
        itemSources         = Freeze(itemRows);
        StartupTiming.Count("collection-sources", "records", itemSources.Sum(pair => pair.Value.Count));
        blueMageSources     = BuildBlueMageSources(blueSpellDetails);
        blueSpellRows       = blueSpellDetails.ToFrozenDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<BlueSpellSource>)pair.Value.ToArray());
        relicAchievementIds = relicAchievements.ToFrozenDictionary();
        cancellationToken.ThrowIfCancellationRequested();
        categoryBuildCancellation = new();
        exchangeCategoryTask      = Task.Run(() => BuildExchangeCategoriesAsync(categoryBuildCancellation.Token));
    }

    public long Revision => Interlocked.Read(ref revision);

    /// <summary>在后台解析来源资源和构建索引，并在框架线程分帧取得游戏文本。</summary>
    public static Task<CollectionSourceService> CreateAsync(CancellationToken cancellationToken = default) =>
        CreateAsync(null, cancellationToken);

    internal static Task<CollectionSourceService> CreateAsync
    (
        GameItemSnapshot? itemSnapshot,
        CancellationToken cancellationToken
    ) => CreateAsync(itemSnapshot, null, cancellationToken);

    internal static async Task<CollectionSourceService> CreateAsync
    (
        GameItemSnapshot? itemSnapshot,
        GameDataSnapshot? dataSnapshot,
        CancellationToken cancellationToken
    )
    {
        using var ownedWikiDocument = dataSnapshot is null
            ? await Task.Run(() => ReadDocument("WikiItemSources.json"), cancellationToken).ConfigureAwait(false) : null;
        using var ownedFishingDocument = dataSnapshot is null
            ? await Task.Run(() => ReadDocument("FishingSources.json"), cancellationToken).ConfigureAwait(false) : null;
        var wikiDocument = dataSnapshot?.GetSelected("WikiItemSources.json").Document ?? ownedWikiDocument
            ?? throw new InvalidOperationException("Wiki item sources are unavailable.");
        var fishingDocument = dataSnapshot?.GetSelected("FishingSources.json").Document ?? ownedFishingDocument
            ?? throw new InvalidOperationException("Fishing sources are unavailable.");
        itemSnapshot ??= await GameItemSnapshot.CreateAsync(cancellationToken).ConfigureAwait(false);
        var itemsByID = itemSnapshot.ItemsByID;
        var text = new PreparedSourceText { ItemSnapshot = itemSnapshot };
        var prepared = await Task.Run(() =>
        {
            var cards = itemsByID.Values
                .Where(item => item.ItemAction.IsValid && item.ItemAction.Value.Action.RowId == 3_357)
                .ToArray();
            cancellationToken.ThrowIfCancellationRequested();
            var toolClasses = LuminaGetter.Get<WKSCosmoToolClass>().ToArray();
            var pvpSeries = LuminaGetter.Get<PvPSeries>().ToArray();
            var relicStages = LuminaGetter.Get<RelicItem>().ToArray();
            var historicalEquipment = ReadHistoricalEquipmentSourceRows(cancellationToken);
            var sourceTextIDs = GetSourceTextIDs(wikiDocument, fishingDocument, itemsByID, text,
                toolClasses, pvpSeries, relicStages, cancellationToken);
            var captures = new List<Action>();
            AddCaptures(captures, sourceTextIDs.QuestIDs, questID =>
            {
                if (LuminaGetter.TryGetRow<Quest>(questID, out var quest))
                    CaptureQuestName(quest, text);
            });
            AddCaptures(captures, sourceTextIDs.PlaceNameIDs, placeNameID =>
                text.PlaceText[placeNameID] = new(LuminaGetter.TryGetRow<PlaceName>(placeNameID, out var place) ? place.Name : default));
            AddCaptures(captures, sourceTextIDs.WeatherIDs, weatherID =>
                text.WeatherText[weatherID] = new(LuminaGetter.TryGetRow<Weather>(weatherID, out var weather) ? weather.Name : default));
            AddCaptures(captures, cards, item => AddTripleTriadSource(text.TripleTriadSources, item, text));
            AddCaptures(captures, toolClasses, toolClass => AddCosmicToolSources(text.SupplementalSources, toolClass, text));
            AddCosmicToolReplicaCaptures(captures, text);
            AddPVPSeasonPortraitCaptures(captures, text);
            AddCaptures(captures, pvpSeries, series => AddPVPSeriesSources(text.SupplementalSources, series, text));
            AddCaptures(captures, relicStages, stage => AddRelicSources(text.SupplementalSources, stage, text));
            AddCaptures(captures, UnfinishedRelicQuestIDs, questID => AddUnfinishedRelicSources(text.SupplementalSources, questID, text));
            AddCaptures(captures, DisguiseEquipmentQuestIDs, questID => AddDisguiseEquipmentSources(text.SupplementalSources, questID, text));
            AddCaptures(captures, RetiredArdentEquipmentIDs, itemID => AddRetiredArdentEquipmentSource(text.SupplementalSources, itemID, text));
            AddCaptures(captures, EternalBondWristletIDs, itemID => AddEternalBondWristletSource(text.SupplementalSources, itemID, text));
            AddCaptures(captures, LegacyGatheringToolIDs, itemID => AddLegacyGatheringToolSource(text.SupplementalSources, itemID, text));
            AddCaptures(captures, historicalEquipment, row => AddHistoricalEquipmentSource(text.SupplementalSources, row.ItemID, row.ConditionID, text));
            return (TextIDs: sourceTextIDs, Captures: captures);
        }, cancellationToken).ConfigureAwait(false);
        var textIDs = prepared.TextIDs;
        await itemSnapshot.PrepareNamesAsync(textIDs.ItemIDs, cancellationToken).ConfigureAwait(false);
        var captureStarted = Stopwatch.GetTimestamp();
        var captureWork = 0L;
        var captureAttempts = 0L;
        try
        {
            var captureTask = () => FrameworkWorkScheduler.RunAsync(prepared.Captures, capture =>
            {
                var started = Stopwatch.GetTimestamp();
                Interlocked.Increment(ref captureAttempts);
                try
                {
                    capture();
                }
                finally
                {
                    Interlocked.Add(ref captureWork, Stopwatch.GetTimestamp() - started);
                }
            }, cancellationToken);
            if (StartupTiming.Current is { } timing)
                await timing.RunAsync("source-framework-capture", captureTask).ConfigureAwait(false);
            else
                await captureTask().ConfigureAwait(false);
        }
        finally
        {
            var elapsed = Stopwatch.GetTimestamp() - captureStarted;
            StartupTiming.Count("source-text", "processing-us", (long)(captureWork * 1_000_000d / Stopwatch.Frequency));
            StartupTiming.Count("source-text", "waiting-us", (long)(Math.Max(0, elapsed - captureWork) * 1_000_000d / Stopwatch.Frequency));
            StartupTiming.Count("source-text", "captures", captureAttempts);
        }
        prepared.Captures.Clear();
        return await Task.Run(() =>
        {
            foreach (var itemID in textIDs.ItemIDs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (itemsByID.TryGetValue(itemID, out var item))
                    text.ItemNames[itemID] = itemSnapshot.GetName(item);
            }
            text.BuildSources(cancellationToken);
            return new CollectionSourceService(itemsByID, wikiDocument, fishingDocument, text, cancellationToken);
        }, cancellationToken).ConfigureAwait(false);
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

    public bool TryGetRelicAchievementID
    (
        uint     itemID,
        out uint achievementID
    ) =>
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
        StartupTiming.Count(resourceName, "parse");
        return JsonDocument.Parse(stream);
    }

    private static (uint[] ItemIDs, uint[] QuestIDs, uint[] PlaceNameIDs, uint[] WeatherIDs) GetSourceTextIDs
    (
        JsonDocument                    wikiDocument,
        JsonDocument                    fishingDocument,
        IReadOnlyDictionary<uint, Item> itemsByID,
        PreparedSourceText              preparedText,
        IReadOnlyList<WKSCosmoToolClass> toolClasses,
        IReadOnlyList<PvPSeries>         pvpSeries,
        IReadOnlyList<RelicItem>         relicStages,
        CancellationToken               cancellationToken
    )
    {
        var itemIDs = new HashSet<uint>();
        try
        {
            var containerRows = ReadContainerSourceRows();
            preparedText.ContainerSourceRows = containerRows;
            foreach (var row in containerRows)
                itemIDs.Add(row.SourceItemID);
        }
        catch (Exception)
        {
        }

        preparedText.ItemSetItemIDs = LuminaGetter.Get<MirageStoreSetItem>()
            .Where(set =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return itemsByID.ContainsKey(set.RowId);
            })
            .ToFrozenDictionary(
                set => set.RowId,
                set => ItemSetCatalog.GetItemSlots(set.RowId).Where(itemID => itemID != 0).ToArray());
        foreach (var memberItemIDs in preparedText.ItemSetItemIDs.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var itemID in memberItemIDs)
                itemIDs.Add(itemID);
        }
        itemIDs.UnionWith(BlindBoxCatalog.SupplementalDropPools.Keys);
        itemIDs.UnionWith(PVPSeason21PortraitItemIDs);
        itemIDs.UnionWith(EternalBondWristletIDs.Select(GetEternalBondPlanItemID));
        itemIDs.Add(31);
        foreach (var toolClass in toolClasses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var stage in toolClass.Stages)
                itemIDs.Add(stage.Item.RowId);
        }
        foreach (var series in pvpSeries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var rewards in series.LevelRewards)
                foreach (var item in rewards.LevelRewardItem)
                    itemIDs.Add(item.RowId);
        }
        foreach (var stage in relicStages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (stage.RowId is >= 2 and <= 7 && LuminaGetter.TryGetRow<RelicItem>(stage.RowId - 1, out var previous))
                itemIDs.UnionWith(GetRelicItemIDs(previous));
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
        public GameItemSnapshot? ItemSnapshot { get; init; }

        public List<Action> SourceBuilds { get; } = [];

        public Dictionary<(uint AddonTextID, uint AcquisitionID, uint LocationID), CapturedSourceText> TripleTriadText { get; } = [];

        public Dictionary<uint, CapturedSourceText> QuestText { get; } = [];

        public Dictionary<uint, CapturedSourceText> NPCText { get; } = [];

        public Dictionary<uint, CapturedSourceText> DutyText { get; } = [];

        public Dictionary<uint, CapturedSourceText> PlaceText { get; } = [];

        public Dictionary<uint, CapturedSourceText> WeatherText { get; } = [];

        public IReadOnlyList<(uint ItemID, uint SourceItemID, uint SourceType)>? ContainerSourceRows { get; set; }

        public FrozenDictionary<uint, uint[]> ItemSetItemIDs { get; set; } = FrozenDictionary<uint, uint[]>.Empty;

        public Dictionary<uint, string> ItemNames { get; } = [];

        public Dictionary<uint, string> QuestNames { get; } = [];

        public Dictionary<uint, string> PlaceNames { get; } = [];

        public Dictionary<uint, string> WeatherNames { get; } = [];

        public Dictionary<uint, HashSet<CollectionSource>> TripleTriadSources { get; } = [];

        public Dictionary<uint, HashSet<CollectionSource>> SupplementalSources { get; } = [];

        public void BuildSources(CancellationToken cancellationToken)
        {
            foreach (var (id, value) in QuestText)
            {
                cancellationToken.ThrowIfCancellationRequested();
                QuestNames[id] = value.Text;
            }
            foreach (var (id, value) in PlaceText)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PlaceNames[id] = value.Text;
            }
            foreach (var (id, value) in WeatherText)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WeatherNames[id] = value.Text;
            }
            foreach (var build in SourceBuilds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                build();
            }
            SourceBuilds.Clear();
        }
    }

    private sealed class CapturedSourceText(ReadOnlySeString value)
    {
        private string? text;

        public CapturedSourceText(string text) : this(default(ReadOnlySeString)) => this.text = text;

        public string Text => text ??= value.ExtractText();
    }

    private static void BuildSource(PreparedSourceText? preparedText, Action build)
    {
        if (preparedText is null)
            build();
        else
            preparedText.SourceBuilds.Add(build);
    }

    private static void AddCaptures<T>(List<Action> captures, IReadOnlyList<T> rows, Action<T> capture)
    {
        foreach (var row in rows)
            captures.Add(() => capture(row));
    }

    private static CapturedSourceText CaptureQuestName(Quest quest, PreparedSourceText? preparedText)
    {
        if (preparedText is not null && preparedText.QuestText.TryGetValue(quest.RowId, out var name))
            return name;
        name = new(quest.Name);
        preparedText?.QuestText.Add(quest.RowId, name);
        return name;
    }

    private static CapturedSourceText CaptureNPCName(ENpcResident npc, PreparedSourceText? preparedText)
    {
        if (preparedText is not null && preparedText.NPCText.TryGetValue(npc.RowId, out var name))
            return name;
        name = new(npc.Singular);
        preparedText?.NPCText.Add(npc.RowId, name);
        return name;
    }

    private static CapturedSourceText CaptureItemName(Item item, PreparedSourceText? preparedText) =>
        preparedText?.ItemSnapshot is { } snapshot && item.RowId != 0
            ? new(snapshot.GetName(item))
            : new(item.Name);
}
