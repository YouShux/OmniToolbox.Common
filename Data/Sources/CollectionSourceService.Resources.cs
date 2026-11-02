using System.Collections.Frozen;
using System.IO;
using System.Text.Json;
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
            JsonDocument?                      preparedDocument = null,
            IReadOnlyDictionary<uint, string>? preparedQuestNames = null,
            CancellationToken                  cancellationToken = default,
            IReadOnlyDictionary<uint, string>? preparedItemNames = null
        )
    {
        using var document = preparedDocument is null ? ReadDocument("WikiItemSources.json") : null;
        var root = (preparedDocument ?? document ?? throw new InvalidOperationException()).RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 2)
        {
            throw new InvalidDataException("Unsupported WikiItemSources schema version.");
        }

        var sourceRows = root.GetProperty("sources");
        var sources = new CollectionSource[sourceRows.GetArrayLength()];
        // 相同材料列表共享实例，保持来源记录的去重语义。
        var costsByIDs = new Dictionary<string, IReadOnlyList<uint>>(StringComparer.Ordinal);
        var sourceIndex = 0;
        foreach (var source in sourceRows.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var category = source.GetProperty("c").GetInt32();
            if (!Enum.IsDefined((CollectionSourceCategory)category))
            {
                throw new InvalidDataException($"Unknown Wiki item source category: {category}.");
            }

            CollectionSourceLocation? location = null;
            if (source.TryGetProperty("m", out var mapIdElement) &&
                LuminaGetter.TryGetRow<Map>(mapIdElement.GetUInt32(), out var map) &&
                map.TerritoryType.RowId != 0)
            {
                location = new(
                    map.TerritoryType.RowId,
                    source.GetProperty("x").GetSingle(),
                    source.GetProperty("y").GetSingle())
                {
                    MapID = map.RowId
                };
            }

            var sourceID = source.GetProperty("i").GetUInt32();
            if ((CollectionSourceCategory)category == CollectionSourceCategory.Duty && sourceID != 0)
            {
                sourceID = LuminaGetter.TryGetRow<InstanceContent>(sourceID, out var content)
                    ? content.ContentFinderCondition.RowId
                    : 0;
            }
            var description = source.GetProperty("n").GetString() ?? string.Empty;
            if ((CollectionSourceCategory)category == CollectionSourceCategory.Other && description == "幻卡对战")
                category = (int)CollectionSourceCategory.NPCBattle;
            if ((CollectionSourceCategory)category == CollectionSourceCategory.Quest &&
                sourceID != 0 &&
                TryGetQuestName(sourceID, preparedQuestNames, out var questName))
            {
                description = questName;
            }

            var detail = source.GetProperty("d").GetString() ?? string.Empty;
            var sourceDetail = detail;
            IReadOnlyList<uint> requiredQuests = [];
            var requiredQuestDetail = string.Empty;
            if (source.TryGetProperty("q", out var questIDs))
            {
                requiredQuests = questIDs.EnumerateArray().Select(questID => questID.GetUInt32()).ToArray();
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
            if (source.TryGetProperty("r", out var costItemIDs))
            {
                var key = costItemIDs.GetRawText();
                if (!costsByIDs.TryGetValue(key, out costs!))
                {
                    costsByIDs[key] = costs = costItemIDs.EnumerateArray().Select(itemID => itemID.GetUInt32()).ToArray();
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
                IsGilShop = source.TryGetProperty("g", out var gilShop) && gilShop.GetBoolean()
            };
        }

        var itemSources = new Dictionary<uint, HashSet<CollectionSource>>();
        var blindBoxDropPools = BlindBoxCatalog.OrderedBlindBoxItemIds.ToDictionary(
            itemID => itemID,
            static _ => new HashSet<uint>());
        foreach (var item in root.GetProperty("items").EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var itemID = item.GetProperty("i").GetUInt32();
            foreach (var source in item.GetProperty("s").EnumerateArray())
            {
                var index = source.GetInt32();
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
            root.GetProperty("unobtainable")
                .EnumerateArray()
                .Select(itemID => itemID.GetUInt32())
                .ToFrozenSet(),
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
        try
        {
            var itemIDs = new HashSet<uint>();
            EmbeddedData.ReadRows("StoreItem.csv", csv => itemIDs.Add(csv.GetField<uint>("ItemId")));
            foreach (var itemID in itemIDs)
            {
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
                    AddMogStationSource(target, rows[index].Item.RowId);
                }
            }
        }

        foreach (var itemSet in LuminaGetter.Get<FittingShopItemSet>())
        {
            for (var index = 0; index < itemSet.Item.Count; index++)
            {
                AddMogStationSource(target, itemSet.Item[index].RowId);
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
