using System.Collections.Frozen;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using Lumina.Excel.Sheets;
using LuminaSupplemental.Excel.Model;
using LuminaSupplemental.Excel.Services;
using OmenTools;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.Data;
using OmniToolbox.Host;
using OmniToolbox.Items;

namespace OmniToolbox.Collections;

public sealed partial class CollectionSourceService
{

    private static readonly Regex BlueMageChineseNumberSpacingRegex = new(
        @"(?<=[\p{IsCJKUnifiedIdeographs}])\s+(?=\d)|(?<=\d)\s+(?=[\p{IsCJKUnifiedIdeographs}])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex BlueMageBossNumberRegex = new(
        @"BOSS\s*#\s*(\d+)(?=\p{IsCJKUnifiedIdeographs})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static (
        FrozenSet<uint> UnobtainableItemIds,
        Dictionary<uint, HashSet<CollectionSource>> ItemSources,
        Dictionary<uint, HashSet<uint>> BlindBoxDropPools) ReadWikiItemSources()
    {
        using var stream = EmbeddedData.Open("WikiItemSources.json");
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1)
        {
            throw new InvalidDataException("Unsupported WikiItemSources schema version.");
        }

        var territoryIdsByMapID = new Dictionary<uint, uint>();
        foreach (var territory in LuminaGetter.Get<TerritoryType>())
        {
            if (territory.Map.RowId != 0)
            {
                territoryIdsByMapID.TryAdd(territory.Map.RowId, territory.RowId);
            }
        }

        var sourceRows = root.GetProperty("sources");
        var sources = new CollectionSource[sourceRows.GetArrayLength()];
        var sourceIndex = 0;
        foreach (var source in sourceRows.EnumerateArray())
        {
            var category = source.GetProperty("c").GetInt32();
            if (!Enum.IsDefined(typeof(CollectionSourceCategory), category))
            {
                throw new InvalidDataException($"Unknown Wiki item source category: {category}.");
            }

            CollectionSourceLocation? location = null;
            if (source.TryGetProperty("m", out var mapIdElement) &&
                territoryIdsByMapID.TryGetValue(mapIdElement.GetUInt32(), out var territoryId))
            {
                location = new(
                    territoryId,
                    source.GetProperty("x").GetSingle(),
                    source.GetProperty("y").GetSingle());
            }

            var sourceID = source.GetProperty("i").GetUInt32();
            var description = source.GetProperty("n").GetString() ?? string.Empty;
            if ((CollectionSourceCategory)category == CollectionSourceCategory.Quest &&
                sourceID != 0 &&
                LuminaGetter.TryGetRow<Quest>(sourceID, out var quest))
            {
                description = quest.Name.ExtractText();
            }

            sources[sourceIndex++] = new(
                (CollectionSourceCategory)category,
                sourceID,
                description,
                source.GetProperty("d").GetString() ?? string.Empty,
                location);
        }

        var itemSources = new Dictionary<uint, HashSet<CollectionSource>>();
        var blindBoxDropPools = BlindBoxCatalog.OrderedBlindBoxItemIds.ToDictionary(
            itemID => itemID,
            static _ => new HashSet<uint>());
        foreach (var item in root.GetProperty("items").EnumerateArray())
        {
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

    private static void ReadCSV(string fileName, Action<CsvReader> readRow)
    {
        var failedRows = 0;
        Exception? firstException = null;
        using var reader = new StreamReader(EmbeddedData.Open(fileName));
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            AllowComments = true,
            Comment = '#',
            HasHeaderRecord = true,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim
        });
        if (!csv.Read())
        {
            return;
        }

        csv.ReadHeader();
        while (csv.Read())
        {
            try
            {
                readRow(csv);
            }
            catch (Exception ex)
            {
                failedRows++;
                firstException ??= ex;
            }
        }

        if (failedRows != 0)
        {
            DalamudServices.PluginLog.Warning(
                $"Collection source {fileName} had {failedRows} failed row(s): {firstException!.Message}");
        }
    }

    private static void AddCurrentMogStationSources(IDictionary<uint, HashSet<CollectionSource>> target)
    {
        try
        {
            var rows = CsvLoader.LoadResource<StoreItem>(
                CsvLoader.StoreItemResourceName,
                true,
                out var failedLines,
                out var exceptions);
            foreach (var row in rows)
            {
                AddMogStationSource(target, row.ItemId);
            }

            if (failedLines.Count != 0)
            {
                DalamudServices.PluginLog.Warning(
                    $"StoreItem 数据有 {failedLines.Count} 行加载失败，首个错误: {exceptions[0].Message}");
            }
        }
        catch (Exception ex)
        {
            DalamudServices.PluginLog.Warning(ex, "StoreItem source loading failed");
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

    private static void AddMogStationSource(
        IDictionary<uint, HashSet<CollectionSource>> target,
        uint itemID)
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

    private static string FormatBlueMageDetail(
        string location,
        float? x,
        float? y,
        uint? level,
        string note)
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
