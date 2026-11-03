using System.Collections.Frozen;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Lumina.Excel.Sheets;
using OmenTools;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.Data;
using OmniToolbox.Host;
using OmniToolbox.Items;
using OmniToolbox.UI;

namespace OmniToolbox.Collections;

public sealed partial class CollectionSourceService
{
    private static readonly Regex BlueMageChineseNumberSpacingRegex = new(
        @"(?<=[\p{IsCJKUnifiedIdeographs}])\s+(?=\d)|(?<=\d)\s+(?=[\p{IsCJKUnifiedIdeographs}])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex BlueMageBossNumberRegex           = new(
        @"BOSS\s*#\s*(\d+)(?=\p{IsCJKUnifiedIdeographs})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static (
        FrozenSet<uint> UnobtainableItemIds,
        Dictionary<uint, HashSet<CollectionSource>> ItemSources,
        Dictionary<uint, HashSet<uint>> BlindBoxDropPools) ReadWikiItemSources
        (
            WikiItemData?                     preparedItems = null,
            IReadOnlyDictionary<uint, string>? preparedQuestNames = null,
            CancellationToken                 cancellationToken = default,
            IReadOnlyDictionary<uint, string>? preparedItemNames = null
        )
    {
        var wikiItems = preparedItems ?? EmbeddedData.GetWikiItems(cancellationToken);
        if (wikiItems.SchemaVersion != 2)
        {
            throw new InvalidDataException("Unsupported WikiItemSources schema version.");
        }

        var sourceRows = wikiItems.Sources;
        var sources = new CollectionSource[sourceRows.Length];
        var costsByIDs = new Dictionary<string, IReadOnlyList<uint>>(StringComparer.Ordinal);
        var mapsByID = new Dictionary<uint, (uint TerritoryID, uint MapID)>();
        var dutiesByID = new Dictionary<uint, uint>();
        var sourceIndex = 0;
        foreach (var source in sourceRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var category = source.Category;
            if (!Enum.IsDefined((CollectionSourceCategory)category))
            {
                throw new InvalidDataException($"Unknown Wiki item source category: {category}.");
            }

            var description = source.Name ?? string.Empty;
            category = description switch
            {
                "其他：理符" or "其他：通过理符获得" => (int)CollectionSourceCategory.Other,
                "其他：蜃景幻界新月岛" => (int)CollectionSourceCategory.FieldOperations,
                "其他：女王古殿" or "其他：零式女王古殿" => (int)CollectionSourceCategory.Duty,
                "其他：群狼盛宴" => (int)CollectionSourceCategory.PvP,
                "其他：莫古莫古★大收集" => (int)CollectionSourceCategory.Event,
                _ when category == (int)CollectionSourceCategory.CompanySeals &&
                       (description == "部队战绩兑换" || description.StartsWith("部队战绩兑换：", StringComparison.Ordinal)) =>
                    (int)CollectionSourceCategory.Shop,
                _ => category
            };

            CollectionSourceLocation? location = null;
            if (source.MapID is { } mapID && source.X is { } x && source.Y is { } y)
            {
                if (!mapsByID.TryGetValue(mapID, out var mapIdentity))
                {
                    mapIdentity = LuminaGetter.TryGetRow<Map>(mapID, out var map)
                        ? (map.TerritoryType.RowId, map.RowId)
                        : default;
                    mapsByID.Add(mapID, mapIdentity);
                }
                if (mapIdentity.TerritoryID != 0)
                {
                    location = new(mapIdentity.TerritoryID, (float)x, (float)y)
                    {
                        MapID = mapIdentity.MapID
                    };
                }
            }

            var sourceID = source.SourceID;
            if ((CollectionSourceCategory)category == CollectionSourceCategory.Duty && sourceID != 0)
            {
                if (!dutiesByID.TryGetValue(sourceID, out var conditionID))
                {
                    conditionID = LuminaGetter.TryGetRow<InstanceContent>(sourceID, out var content)
                        ? content.ContentFinderCondition.RowId
                        : 0;
                    dutiesByID.Add(sourceID, conditionID);
                }
                sourceID = conditionID;
            }
            if ((CollectionSourceCategory)category == CollectionSourceCategory.Other && description == "幻卡对战")
                category = (int)CollectionSourceCategory.NPCBattle;
            if ((CollectionSourceCategory)category == CollectionSourceCategory.Quest &&
                sourceID != 0 &&
                TryGetQuestName(sourceID, preparedQuestNames, out var questName))
            {
                description = questName;
            }

            var detail = source.Detail ?? string.Empty;
            var sourceDetail = detail;
            IReadOnlyList<uint> requiredQuests = [];
            var requiredQuestDetail = string.Empty;
            if (source.RequiredQuestIDs is { } questIDs)
            {
                requiredQuests = questIDs;
                var questNames = new List<string>();
                foreach (var questID in requiredQuests)
                {
                    if (TryGetQuestName(questID, preparedQuestNames, out var requiredQuestName))
                    {
                        if (requiredQuestName.Length != 0)
                        {
                            questNames.Add(requiredQuestName);
                        }
                    }
                }

                if (questNames.Count != 0)
                {
                    requiredQuestDetail = string.Format(
                        OmniLoc.Get("Collection.Source.RequiredQuests"),
                        string.Join(OmniLoc.Get("Common.ListSeparator"), questNames));
                    detail += requiredQuestDetail;
                }
            }

            IReadOnlyList<uint> costs = [];
            if (source.CostItemIDs is { } costItemIDs)
            {
                var key = string.Join(',', costItemIDs);
                if (costsByIDs.TryGetValue(key, out var sharedCosts))
                    costs = sharedCosts;
                else
                {
                    costsByIDs[key] = costs = costItemIDs;
                }
            }

            sources[sourceIndex++] = new(
                (CollectionSourceCategory)category,
                sourceID,
                description,
                detail,
                location)
            {
                CostItemIDs = costs,
                RequiredQuestIDs = requiredQuests,
                RequiredQuestDetail = requiredQuestDetail,
                SourceDetail = sourceDetail,
                IsGilShop = source.IsGilShop == true
            };
        }

        var itemSources = new Dictionary<uint, HashSet<CollectionSource>>(wikiItems.Items.Length);
        var blindBoxDropPools = BlindBoxCatalog.OrderedBlindBoxItemIds.ToDictionary(
            itemID => itemID,
            static _ => new HashSet<uint>());
        foreach (var item in wikiItems.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var itemID = item.ItemID;
            foreach (var index in item.SourceIndices)
            {
                if (index < 0 || index >= sources.Length)
                {
                    throw new InvalidDataException($"Invalid Wiki item source index: {index}.");
                }

                var itemSource = sources[index];
                AddSource(itemSources, itemID, itemSource);
                if (itemSource.Category == CollectionSourceCategory.Container &&
                    blindBoxDropPools.TryGetValue(itemSource.SourceID, out var dropPool))
                {
                    dropPool.Add(itemID);
                }
            }
        }

        foreach (var (blindBoxItemID, rewardItemIDs) in BlindBoxCatalog.SupplementalDropPools)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dropPool = blindBoxDropPools[blindBoxItemID];
            var containerSource = new CollectionSource(
                CollectionSourceCategory.Container,
                blindBoxItemID,
                preparedItemNames is null
                    ? LuminaWrapper.GetItemName(blindBoxItemID)
                    : preparedItemNames.GetValueOrDefault(blindBoxItemID, string.Empty),
                string.Empty)
            {
                IsIdentityFallback = true
            };
            foreach (var rewardItemID in rewardItemIDs)
            {
                AddSource(itemSources, rewardItemID, containerSource);
                dropPool.Add(rewardItemID);
            }
        }

        foreach (var (blindBoxItemId, dropPool) in blindBoxDropPools)
        {
            if (dropPool.Count == 0)
            {
                throw new InvalidDataException($"Wiki item sources do not contain blind box {blindBoxItemId}.");
            }
        }

        return (
            wikiItems.UnobtainableItemIDs.ToFrozenSet(),
            itemSources,
            blindBoxDropPools);
    }

    private static bool TryGetQuestName
    (
        uint                               questID,
        IReadOnlyDictionary<uint, string>? preparedNames,
        out string                         name
    )
    {
        if (preparedNames is not null)
        {
            var found = preparedNames.TryGetValue(questID, out var preparedName);
            name = preparedName ?? string.Empty;
            return found;
        }
        if (LuminaGetter.TryGetRow<Quest>(questID, out var quest))
        {
            name = quest.Name.ExtractText();
            return true;
        }

        name = string.Empty;
        return false;
    }

    private static void AddCurrentMogStationSources(IDictionary<uint, HashSet<CollectionSource>> target)
    {
        var processedItemIDs = new HashSet<uint>();
        try
        {
            var itemIDs = new HashSet<uint>();
            EmbeddedData.ReadRows("StoreItem.csv", csv => itemIDs.Add(csv.GetField<uint>("ItemId")));
            foreach (var itemID in itemIDs)
            {
                if (processedItemIDs.Add(itemID))
                    AddMogStationSource(target, itemID);
            }
        }
        catch (Exception)
        {
        }

        if (DalamudServices.DataManager.GetSubrowExcelSheet<FittingShopCategoryItem>() is { } categoryItems)
        {
            foreach (var rows in categoryItems)
            {
                for (var index = 0; index < rows.Count; index++)
                {
                    var itemID = rows[index].Item.RowId;
                    if (processedItemIDs.Add(itemID))
                        AddMogStationSource(target, itemID);
                }
            }
        }

        foreach (var itemSet in LuminaGetter.Get<FittingShopItemSet>())
        {
            for (var index = 0; index < itemSet.Item.Count; index++)
            {
                var itemID = itemSet.Item[index].RowId;
                if (processedItemIDs.Add(itemID))
                    AddMogStationSource(target, itemID);
            }
        }
    }

    private static void AddMogStationSource
    (
        IDictionary<uint, HashSet<CollectionSource>> target,
        uint                                         itemID
    )
    {
        if (itemID == 0 || itemID is 12995 or 43587)
        {
            return;
        }

        if (target.TryGetValue(itemID, out var sources) &&
            sources.Any(source => source.Category == CollectionSourceCategory.MogStation))
        {
            return;
        }

        AddSource(target, itemID, new(
            CollectionSourceCategory.MogStation,
            0,
            string.Empty,
            string.Empty));
    }

    private static string FormatBlueMageDetail
    (
        string location,
        float? x,
        float? y,
        uint?  level,
        string note
    )
    {
        var parts = new List<string>(3);
        if (location.Length != 0)
        {
            parts.Add(location);
        }

        if (x.HasValue && y.HasValue)
        {
            parts.Add(FormattableString.Invariant($"({x:0.#}, {y:0.#})"));
        }

        if (level.HasValue)
        {
            parts.Add($"等级：{level}");
        }

        if (note.Length != 0)
        {
            parts.Add(note);
        }

        return string.Join(" | ", parts);
    }

    private static string NormalizeBlueMageText(string text)
    {
        var normalized = BlueMageChineseNumberSpacingRegex.Replace(text, string.Empty);
        return BlueMageBossNumberRegex.Replace(normalized, "BOSS $1 ")
            .Replace("BOSS #", "BOSS ", StringComparison.Ordinal);
    }
}
