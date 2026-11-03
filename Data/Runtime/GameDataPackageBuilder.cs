using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace OmniToolbox.Data;

internal static class GameDataPackageBuilder
{
    internal static readonly string[] ItemCatalogInputs = ["ItemSupplement.csv", "ItemPatch.csv"];
    internal static readonly string[] WikiInputs = ["WikiItemSources.json"];
    internal static readonly string[] DungeonInputs =
        ["DungeonBoss.csv", "DungeonChest.csv", "DungeonBossDrop.csv", "DungeonBossChest.csv", "DungeonChestItem.csv"];

    internal static ItemCatalogData BuildItemCatalog(IReadOnlyDictionary<string, string> files, CancellationToken cancellationToken = default) =>
        BuildItemCatalog(ItemCatalogInputs.ToDictionary(name => name, name => GameDataCSV.Parse(files[name], cancellationToken), StringComparer.Ordinal), cancellationToken);

    internal static ItemCatalogData BuildItemCatalog(IReadOnlyDictionary<string, GameDataCSV> files, CancellationToken cancellationToken = default) => new()
    {
        ItemSupplements = ReadCSV(files, "ItemSupplement.csv",
            ["ItemId", "SourceItemId", "ItemSupplementSource", "Min", "Max", "Probability"], csv => new ItemSupplementRow
            {
                ItemID = csv.GetField<uint>(0), SourceItemID = csv.GetField<uint>(1), Source = csv.GetField<uint>(2),
                Min = csv.GetField<uint?>(3), Max = csv.GetField<uint?>(4), Probability = csv.GetField<decimal?>(5)
            }, cancellationToken),
        PatchRanges = ReadCSV(files, "ItemPatch.csv", ["StartItemId", "EndItemId", "PatchNo"], csv => new ItemPatchRow
        {
            StartItemID = csv.GetField<uint>(0), EndItemID = csv.GetField<uint>(1), Patch = csv.GetField<decimal>(2)
        }, cancellationToken)
    };

    internal static DungeonSourceData BuildDungeonSources(IReadOnlyDictionary<string, string> files, CancellationToken cancellationToken = default) =>
        BuildDungeonSources(DungeonInputs.ToDictionary(name => name, name => GameDataCSV.Parse(files[name], cancellationToken), StringComparer.Ordinal), cancellationToken);

    internal static DungeonSourceData BuildDungeonSources(IReadOnlyDictionary<string, GameDataCSV> files, CancellationToken cancellationToken = default) => new()
    {
        Bosses = ReadCSV(files, "DungeonBoss.csv", ["RowId", "BNpcNameId", "ContentFinderConditionId", "FightNo"], csv => new DungeonBossRow
        {
            RowID = csv.GetField<uint>(0), BNPCNameID = csv.GetField<uint>(1),
            ContentFinderConditionID = csv.GetField<uint>(2), FightNo = csv.GetField<uint>(3)
        }, cancellationToken),
        Chests = ReadCSV(files, "DungeonChest.csv",
            ["RowId", "ChestNo", "ContentFinderConditionId", "MapId", "TerritoryTypeId", "TreasureId", "DungeonBossId", "Position"], csv => new DungeonChestRow
            {
                RowID = csv.GetField<uint>(0), ChestNo = csv.GetField<uint>(1), ContentFinderConditionID = csv.GetField<uint>(2),
                MapID = csv.GetField<uint>(3), TerritoryTypeID = csv.GetField<uint>(4), TreasureID = csv.GetField<uint>(5),
                DungeonBossID = csv.GetField<uint>(6), Position = csv.GetField(7) ?? string.Empty
            }, cancellationToken),
        BossDrops = ReadCSV(files, "DungeonBossDrop.csv", ["RowId", "ContentFinderConditionId", "FightNo", "ItemId", "Quantity"], csv => new DungeonBossDropRow
        {
            RowID = csv.GetField<uint>(0), ContentFinderConditionID = csv.GetField<uint>(1), FightNo = csv.GetField<uint>(2),
            ItemID = csv.GetField<uint>(3), Quantity = csv.GetField<uint>(4)
        }, cancellationToken),
        BossChests = ReadCSV(files, "DungeonBossChest.csv", ["RowId", "ItemId", "ContentFinderConditionId", "Quantity", "FightNo", "CofferNo"], csv => new DungeonBossChestRow
        {
            RowID = csv.GetField<uint>(0), ItemID = csv.GetField<uint>(1), ContentFinderConditionID = csv.GetField<uint>(2),
            Quantity = csv.GetField<uint>(3), FightNo = csv.GetField<uint>(4), CofferNo = csv.GetField<uint>(5)
        }, cancellationToken),
        ChestItems = ReadCSV(files, "DungeonChestItem.csv", ["RowId", "ItemId", "ChestId", "Min", "Max", "Probability"], csv => new DungeonChestItemRow
        {
            RowID = csv.GetField<uint>(0), ItemID = csv.GetField<uint>(1), ChestID = csv.GetField<uint>(2),
            Min = csv.GetField<uint>(3), Max = csv.GetField<uint>(4), Probability = csv.GetField<decimal>(5)
        }, cancellationToken)
    };

    internal static WikiItemData BuildWikiItems(string content, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var document = JsonDocument.Parse(content);
        return BuildWikiItems(document.RootElement, cancellationToken);
    }

    internal static WikiItemData BuildWikiItems(JsonElement root, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CheckObject(root, "Wiki root", ["schemaVersion", "source", "generatedAtUtc", "itemCount", "patches", "unobtainable", "sources", "items"]);
        if (root.GetProperty("schemaVersion").GetInt32() != 2)
            throw new InvalidDataException("Unsupported WikiItemSources schema version.");
        var result = new WikiItemData
        {
            SchemaVersion = root.GetProperty("schemaVersion").GetInt32(),
            Source = root.GetProperty("source").GetString() ?? throw new InvalidDataException("Wiki source is null."),
            GeneratedAtUTC = root.GetProperty("generatedAtUtc").GetString() ?? throw new InvalidDataException("Wiki timestamp is null."),
            ItemCount = root.GetProperty("itemCount").GetInt32(),
            Patches = ReadArray(root.GetProperty("patches"), row =>
            {
                CheckObject(row, "Wiki patch", ["i", "p"]);
                return new WikiPatchRow { ItemID = row.GetProperty("i").GetUInt32(), Patch = row.GetProperty("p").GetDecimal() };
            }, cancellationToken),
            UnobtainableItemIDs = ReadArray(root.GetProperty("unobtainable"), row => row.GetUInt32(), cancellationToken),
            Sources = ReadArray(root.GetProperty("sources"), ReadWikiSource, cancellationToken),
            Items = ReadArray(root.GetProperty("items"), row =>
            {
                CheckObject(row, "Wiki item", ["i", "s"]);
                return new WikiItemRow
                {
                    ItemID = row.GetProperty("i").GetUInt32(),
                    SourceIndices = ReadArray(row.GetProperty("s"), index => index.GetInt32(), cancellationToken)
                };
            }, cancellationToken)
        };
        if (result.ItemCount != result.Patches.Length)
            throw new InvalidDataException("Wiki itemCount does not match patches.");
        foreach (var row in result.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (row.SourceIndices.Any(index => index < 0 || index >= result.Sources.Length))
                throw new InvalidDataException($"Wiki source reference is out of range for item {row.ItemID}.");
        }
        return result;
    }

    private static WikiSourceRow ReadWikiSource(JsonElement row)
    {
        CheckObject(row, "Wiki source", ["c", "i", "n", "d"], ["m", "x", "y", "q", "r", "g"]);
        var fields = WikiSourceFields.None;
        if (row.TryGetProperty("m", out var map)) fields |= WikiSourceFields.MapID;
        if (row.TryGetProperty("x", out var x)) fields |= WikiSourceFields.X;
        if (row.TryGetProperty("y", out var y)) fields |= WikiSourceFields.Y;
        if (row.TryGetProperty("q", out var quests)) fields |= WikiSourceFields.RequiredQuests;
        if (row.TryGetProperty("r", out var costs)) fields |= WikiSourceFields.Costs;
        if (row.TryGetProperty("g", out var gil)) fields |= WikiSourceFields.GilShop;
        return new()
        {
            Category = row.GetProperty("c").GetInt32(), SourceID = row.GetProperty("i").GetUInt32(),
            Name = row.GetProperty("n").GetString(), Detail = row.GetProperty("d").GetString(), Fields = fields,
            MapID = map.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : map.GetUInt32(),
            X = x.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : x.GetDecimal(),
            Y = y.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : y.GetDecimal(),
            RequiredQuestIDs = quests.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : ReadArray(quests, id => id.GetUInt32()),
            CostItemIDs = costs.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : ReadArray(costs, id => id.GetUInt32()),
            IsGilShop = gil.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : gil.GetBoolean()
        };
    }

    private static T[] ReadArray<T>(JsonElement array, Func<JsonElement, T> read, CancellationToken cancellationToken = default)
    {
        var result = new T[array.GetArrayLength()];
        var index = 0;
        foreach (var row in array.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            result[index++] = read(row);
        }
        return result;
    }

    private static void CheckObject(JsonElement row, string label, string[] required, string[]? optional = null)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in row.EnumerateObject())
            if (!seen.Add(property.Name) || !required.Contains(property.Name, StringComparer.Ordinal) &&
                (optional is null || !optional.Contains(property.Name, StringComparer.Ordinal)))
                throw new InvalidDataException($"Unknown or duplicate field {label}.{property.Name}.");
        if (required.Any(name => !seen.Contains(name)))
            throw new InvalidDataException($"Missing required fields in {label}.");
    }

    private static T[] ReadCSV<T>(IReadOnlyDictionary<string, GameDataCSV> files, string name, string[] header,
        Func<GameDataCSV.Row, T> read, CancellationToken cancellationToken)
    {
        var table = files[name];
        if (!table.Header.SequenceEqual(header, StringComparer.Ordinal))
            throw new InvalidDataException($"Unexpected CSV fields: {name}.");
        var rows = new List<T>(table.Rows.Count);
        foreach (var row in table.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            rows.Add(read(row));
        }
        return rows.ToArray();
    }
}
