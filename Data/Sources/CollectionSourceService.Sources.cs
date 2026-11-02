using System.Collections.Frozen;
using System.Text.RegularExpressions;
using Dalamud.Game.Text.Evaluator;
using Lumina.Excel.Sheets;
using OmenTools;
using OmenTools.Interop.Game.Helpers;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.Data;
using OmniToolbox.Host;

namespace OmniToolbox.Collections;

public sealed partial class CollectionSourceService
{
    private static readonly Regex CoordinateOnlyLineRegex = new(
        @"^\s*[（(]\s*[\d.]+\s*[,，]\s*[\d.]+\s*[）)]\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] TripleTriadNPCPrefixes =
    [
        "对局：",
        "对局:",
        "Match:",
        "対戦：",
        "対戦:",
        "Versus:"
    ];

    private static void AddContainerSources
    (
        IDictionary<uint, HashSet<CollectionSource>> itemRows,
        IReadOnlyDictionary<uint, Item>              itemsByID,
        IReadOnlyDictionary<uint, string>?           preparedNames = null,
        IReadOnlyList<(uint ItemID, uint SourceItemID, uint SourceType)>? preparedRows = null
    )
    {
        try
        {
            var rows = preparedRows ?? ReadContainerSourceRows();
            var containerNames = new Dictionary<uint, string>();
            foreach (var (itemID, sourceItemID, sourceType) in rows)
            {
                if (!containerNames.TryGetValue(sourceItemID, out var containerName))
                {
                    if (preparedNames is not null)
                        containerName = preparedNames.GetValueOrDefault(sourceItemID, FormattableString.Invariant($"#{sourceItemID}"));
                    else
                        containerName = itemsByID.TryGetValue(sourceItemID, out var container)
                            ? container.Name.ExtractText()
                            : FormattableString.Invariant($"#{sourceItemID}");
                    containerNames.Add(sourceItemID, containerName);
                }
                var category = sourceType switch
                {
                    2 or 4 => CollectionSourceCategory.Other,
                    5 => CollectionSourceCategory.RestorationZone,
                    3 when itemsByID.TryGetValue(sourceItemID, out var sourceItem) && sourceItem.FilterGroup == 18 =>
                        CollectionSourceCategory.TreasureHunts,
                    _ => CollectionSourceCategory.Container
                };
                AddSource(itemRows, itemID, new(
                    category,
                    sourceItemID,
                    containerName,
                    string.Empty)
                {
                    IsIdentityFallback = true
                });
            }
        }
        catch (Exception)
        {
        }
    }

    private static List<(uint ItemID, uint SourceItemID, uint SourceType)> ReadContainerSourceRows()
    {
        var rows = new List<(uint ItemID, uint SourceItemID, uint SourceType)>();
        EmbeddedData.ReadRows("ItemSupplement.csv", csv =>
        {
            var itemID = csv.GetField<uint>("ItemId");
            var sourceItemID = csv.GetField<uint>("SourceItemId");
            var sourceType = csv.GetField<uint>("ItemSupplementSource");
            if (itemID != 0 && sourceItemID != 0 &&
                sourceType is >= 2 and <= 19)
                rows.Add((itemID, sourceItemID, sourceType));
        });
        return rows;
    }

    private static void AddCraftingSources(IDictionary<uint, HashSet<CollectionSource>> itemRows)
    {
        var craftedItemIds = new HashSet<uint>();
        foreach (var recipe in LuminaGetter.Get<Recipe>())
        {
            if (recipe.RowId == 0 || !recipe.ItemResult.IsValid)
            {
                continue;
            }

            var itemID = recipe.ItemResult.RowId;
            if (craftedItemIds.Add(itemID) &&
                !HasSource(itemRows, itemID, CollectionSourceCategory.Crafting))
            {
                AddSource(itemRows, itemID, new(
                    CollectionSourceCategory.Crafting,
                    recipe.RowId,
                    FormattableString.Invariant($"#{recipe.RowId}"),
                    string.Empty));
            }
        }

        foreach (var (itemId, sources) in itemRows)
        {
            if (!craftedItemIds.Contains(itemId))
            {
                sources.RemoveWhere(source => source.Category == CollectionSourceCategory.Crafting);
            }
        }
    }

    private void AddItemSetSources
    (
        IDictionary<uint, HashSet<CollectionSource>> itemRows,
        IReadOnlyDictionary<uint, Item>              itemsByID,
        IReadOnlyDictionary<uint, string>?           preparedNames = null
    )
    {
        foreach (var (setItemID, memberItemIDs) in itemSetItemIDs)
        {
            foreach (var memberItemID in memberItemIDs)
            {
                if (!itemsByID.TryGetValue(memberItemID, out var item) ||
                    !itemRows.TryGetValue(memberItemID, out var sources))
                {
                    continue;
                }

                var name = preparedNames is null ? item.Name.ExtractText() : preparedNames[memberItemID];
                foreach (var source in sources)
                {
                    AddSource(itemRows, setItemID, source with
                    {
                        TargetItemID = memberItemID,
                        Detail = source.Detail.Length == 0 ? name : $"{name} | {source.Detail}"
                    });
                }
            }
        }
    }

    private static void AddTripleTriadSources
    (
        IDictionary<uint, HashSet<CollectionSource>> itemRows,
        IReadOnlyDictionary<uint, Item>              itemsByID
    )
    {
        var preparedText = new PreparedSourceText();
        foreach (var item in itemsByID.Values)
            AddTripleTriadSource(itemRows, item, preparedText);
        preparedText.BuildSources(default);
    }

    private static void AddTripleTriadSource
    (
        IDictionary<uint, HashSet<CollectionSource>> itemRows,
        Item                                         item,
        PreparedSourceText?                          preparedText = null
    )
    {
        if (!item.ItemAction.IsValid || item.ItemAction.Value.Action.RowId != 3_357)
        {
            return;
        }

        var cardID = (uint)item.ItemAction.Value.Data[0];
        if (cardID == 0 ||
            !LuminaGetter.TryGetRow<TripleTriadCardResident>(cardID, out var resident) ||
            !LuminaGetter.TryGetRow<TripleTriadCardObtain>(resident.AcquisitionType.RowId, out var obtain))
        {
            return;
        }

        var isNPC = resident.AcquisitionType.RowId is 6 or 10;
        CapturedSourceText? npcName = null;
        if (isNPC &&
            LuminaGetter.TryGetRow<ENpcResident>(resident.Acquisition.RowId, out var npc))
        {
            npcName = CaptureNPCName(npc, preparedText);
        }

        CollectionSourceLocation? location = null;
        if (isNPC &&
            LuminaGetter.TryGetRow<Level>(resident.Location.RowId, out var level) &&
            LuminaGetter.TryGetRow<TerritoryType>(level.Territory.RowId, out var territory) &&
            LuminaGetter.TryGetRow<Map>(level.Map.RowId != 0 ? level.Map.RowId : territory.Map.RowId, out var map))
        {
            var mapPosition = PositionHelper.WorldToMap(new Vector2(level.X, level.Z), map);
            location = new(territory.RowId, mapPosition.X, mapPosition.Y)
            {
                MapID = map.RowId
            };
        }

        CapturedSourceText? addonText = null;
        if (obtain.Text.RowId != 0)
        {
            var key = (AddonTextID: obtain.Text.RowId, AcquisitionID: resident.Acquisition.RowId, LocationID: resident.Location.RowId);
            if (preparedText is null || !preparedText.TripleTriadText.TryGetValue(key, out addonText))
            {
                addonText = new(DService.Instance().SeStringEvaluator.EvaluateFromAddon(
                    obtain.Text.RowId,
                    new[]
                    {
                        new SeStringParameter(resident.Acquisition.RowId),
                        new SeStringParameter(resident.Location.RowId)
                    }));
                preparedText?.TripleTriadText.Add(key, addonText);
            }
        }

        var isDuty = !isNPC &&
            LuminaGetter.TryGetRow<ContentFinderCondition>(resident.Acquisition.RowId, out _);
        var category = isNPC ? CollectionSourceCategory.NPCBattle : CollectionSourceCategory.Other;
        if (isDuty)
            category = CollectionSourceCategory.Duty;
        BuildSource(preparedText, () =>
        {
            var name = npcName?.Text ?? string.Empty;
            var description = addonText is null ? name : FormatTripleTriadSource(addonText.Text, name);
            if (description.Length == 0)
                return;
            AddSource(itemRows, item.RowId, new(
                category,
                resident.Acquisition.RowId != 0 ? resident.Acquisition.RowId : cardID,
                description,
                string.Empty,
                location)
            {
                RequiredQuestIDs = resident.Quest.RowId == 0 ? [] : [resident.Quest.RowId],
                IsIdentityFallback = isNPC || isDuty
            });
        });
    }

    private static string FormatTripleTriadSource
    (
        string text,
        string npcName
    )
    {
        var parts = new List<string>();
        var npc = npcName;
        foreach (var line in text
                     .Replace("\r\n", "\n", StringComparison.Ordinal)
                     .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (CoordinateOnlyLineRegex.IsMatch(line))
            {
                continue;
            }

            var isNPCLine = false;
            for (var index = 0; index < TripleTriadNPCPrefixes.Length; index++)
            {
                if (!line.StartsWith(TripleTriadNPCPrefixes[index], StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (npc.Length == 0)
                {
                    npc = line[TripleTriadNPCPrefixes[index].Length..].Trim();
                }

                isNPCLine = true;
                break;
            }

            if (!isNPCLine)
            {
                parts.Add(line);
            }
        }

        if (npc.Length != 0 && !parts.Any(part => part.Contains(npc, StringComparison.Ordinal)))
        {
            parts.Insert(0, npc);
        }

        return string.Join(' ', parts);
    }

    private static void AddSupplementalSourceLocations(IDictionary<uint, HashSet<CollectionSource>> itemRows)
    {
        ReadOnlySpan<(uint ItemID, uint MapID, float X, float Y)> locations =
        [
            (21801, 414, 17.9f, 32.2f),
            (23024, 371, 21.6f, 9.9f),
            (23309, 467, 6.1f, 21.5f),
            (24122, 484, 22.3f, 6.1f),
            (39486, 25, 34.7f, 19.3f),
            (46832, 494, 29.8f, 5.8f)
        ];
        foreach (var (itemID, mapID, x, y) in locations)
        {
            if (!itemRows.TryGetValue(itemID, out var sources) ||
                !LuminaGetter.TryGetRow<Map>(mapID, out var map) || map.TerritoryType.RowId == 0)
                continue;

            foreach (var source in sources.Where(source =>
                         source.Category is CollectionSourceCategory.Other or CollectionSourceCategory.FieldOperations or CollectionSourceCategory.DeepDungeon &&
                         source.SourceID == 0 && !source.Location.HasValue).ToArray())
            {
                sources.Remove(source);
                sources.Add(source with
                {
                    Location = new(map.TerritoryType.RowId, x, y) { MapID = map.RowId }
                });
            }
        }
    }

    private static bool HasSource
    (
        IDictionary<uint, HashSet<CollectionSource>> target,
        uint                                         itemID,
        CollectionSourceCategory                     category,
        uint?                                        sourceID = null
    ) =>
        target.TryGetValue(itemID, out var sources) &&
        sources.Any(source =>
            source.Category == category &&
            (!sourceID.HasValue || source.SourceID == sourceID.Value));

    private static void AddSource
    (
        IDictionary<uint, HashSet<CollectionSource>> target,
        uint                                         id,
        CollectionSource                             source
    )
    {
        if (source.SourceDetail.Length == 0 && source.Detail.Length != 0)
            source = source with { SourceDetail = source.Detail };
        if (!target.TryGetValue(id, out var sources))
        {
            sources = new(SourceRelationshipComparer.Instance);
            target.Add(id, sources);
        }

        if (source.IsIdentityFallback)
        {
            foreach (var existing in sources)
            {
                if (existing.IsIdentityFallback || !CoversIdentityFallback(existing, source))
                    continue;

                if (existing.Category == CollectionSourceCategory.NPCBattle && existing.Description == "幻卡对战")
                {
                    sources.Remove(existing);
                    sources.Add(existing with { Description = source.Description });
                }
                StartupTiming.Count("collection-source-relations", "covered-fallback");
                return;
            }
        }
        else
        {
            List<CollectionSource>? covered = null;
            foreach (var existing in sources)
            {
                if (existing.IsIdentityFallback && CoversIdentityFallback(source, existing))
                    (covered ??= []).Add(existing);
            }
            if (covered is not null)
            {
                foreach (var existing in covered)
                {
                    sources.Remove(existing);
                    if (source.Category == CollectionSourceCategory.NPCBattle && source.Description == "幻卡对战")
                        source = source with { Description = existing.Description };
                    StartupTiming.Count("collection-source-relations", "covered-fallback");
                }
            }
        }

        if (!sources.Add(source))
            StartupTiming.Count("collection-source-relations", "exact-duplicate");
    }

    private static bool CoversIdentityFallback(CollectionSource source, CollectionSource fallback) =>
        source.Category == fallback.Category && source.SourceID != 0 && source.SourceID == fallback.SourceID &&
        source.Category is CollectionSourceCategory.Container or CollectionSourceCategory.Duty or CollectionSourceCategory.Quest or CollectionSourceCategory.NPCBattle &&
        source.TargetItemID == fallback.TargetItemID && fallback.Detail.Length == 0 &&
        fallback.CostItemIDs.Count == 0 && (!fallback.IsGilShop || source.IsGilShop) &&
        fallback.RequiredQuestIDs.All(source.RequiredQuestIDs.Contains) &&
        (fallback.MapTerritoryID == 0 || source.MapTerritoryID == fallback.MapTerritoryID) &&
        (!fallback.Location.HasValue || source.Location == fallback.Location);

    private sealed class SourceRelationshipComparer : IEqualityComparer<CollectionSource>
    {
        internal static readonly SourceRelationshipComparer Instance = new();

        public bool Equals(CollectionSource left, CollectionSource right) =>
            left.Category == right.Category && left.SourceID == right.SourceID &&
            left.TargetItemID == right.TargetItemID && left.MapTerritoryID == right.MapTerritoryID &&
            left.Location == right.Location && left.IsGilShop == right.IsGilShop &&
            left.IsIdentityFallback == right.IsIdentityFallback &&
            left.Description == right.Description && left.Detail == right.Detail &&
            left.SourceDetail == right.SourceDetail &&
            IDSetEquals(left.CostItemIDs, right.CostItemIDs) && IDSetEquals(left.RequiredQuestIDs, right.RequiredQuestIDs);

        public int GetHashCode(CollectionSource source)
        {
            var hash = new HashCode();
            hash.Add(source.Category);
            hash.Add(source.SourceID);
            hash.Add(source.TargetItemID);
            hash.Add(source.MapTerritoryID);
            hash.Add(source.Location);
            hash.Add(source.IsGilShop);
            hash.Add(source.IsIdentityFallback);
            hash.Add(source.Description, StringComparer.Ordinal);
            hash.Add(source.Detail, StringComparer.Ordinal);
            hash.Add(source.SourceDetail, StringComparer.Ordinal);
            hash.Add(IDSetHash(source.CostItemIDs));
            hash.Add(IDSetHash(source.RequiredQuestIDs));
            return hash.ToHashCode();
        }

        private static bool IDSetEquals(IReadOnlyList<uint> left, IReadOnlyList<uint> right) =>
            ReferenceEquals(left, right) || left.All(right.Contains) && right.All(left.Contains);

        private static int IDSetHash(IReadOnlyList<uint> ids)
        {
            var hash = 0;
            for (var index = 0; index < ids.Count; index++)
            {
                var id = ids[index];
                var duplicate = false;
                for (var previous = 0; previous < index; previous++)
                    duplicate |= ids[previous] == id;
                if (!duplicate)
                    hash ^= id.GetHashCode();
            }
            return hash;
        }
    }

    private static FrozenDictionary<uint, IReadOnlyList<CollectionSource>> Freeze
    (
        IDictionary<uint, HashSet<CollectionSource>> source
    ) =>
        source.ToFrozenDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<CollectionSource>)(pair.Value.Count <= 1
                ? pair.Value.ToArray()
                : pair.Value
                    .OrderBy(value => value.Category)
                    .ThenBy(value => value.Description, StringComparer.Ordinal)
                    .ThenBy(value => value.SourceID)
                    .ThenBy(value => value.Detail, StringComparer.Ordinal)
                    .ToArray()));
}
