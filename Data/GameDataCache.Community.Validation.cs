using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using CsvHelper;
using OmniToolbox.Collections;
using OmniToolbox.Items;

namespace OmniToolbox.Data;

internal static partial class GameDataCache
{
    private static IReadOnlyDictionary<string, byte[]> ValidateCommunityFiles
    (
        Dictionary<string, string>?         files,
        IReadOnlyDictionary<string, string> embedded,
        CancellationToken                  cancellationToken
    )
    {
        if (files is null || files.Count != CommunityFileNames.Length ||
            files.Keys.Any(name => !CommunityFileNames.Contains(name, StringComparer.Ordinal)))
            throw new InvalidDataException("Incomplete community data snapshot.");

        var resources = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var equipmentRows = 0;
        try
        {
            foreach (var fileName in CommunityFileNames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!files.TryGetValue(fileName, out var content) || string.IsNullOrWhiteSpace(content) ||
                    Encoding.UTF8.GetByteCount(content) > MAX_RESOURCE_BYTES)
                    throw new InvalidDataException($"Missing or oversized community data: {fileName}");

                if (fileName.EndsWith(".csv", StringComparison.Ordinal))
                {
                    var rows = ValidateCSV(fileName, content, embedded[fileName], cancellationToken);
                    if (fileName == "EquipmentSourceSupplement.csv") equipmentRows = rows;
                }
                else
                {
                    using var document = JsonDocument.Parse(content);
                    if (fileName == "WikiItemSources.json") ValidateWikiCommunityData(document.RootElement, cancellationToken);
                    else if (fileName == "FishingSources.json") ValidateFishingCommunityData(document.RootElement, cancellationToken);
                }

                resources.Add(fileName, Encoding.UTF8.GetBytes(content));
            }

            ValidateHistoricalEquipmentCommunityData(files["EquipmentSourceSupplement.csv"],
                files["EquipmentSourceSupplement.meta.json"], equipmentRows, cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            throw new InvalidDataException("Invalid community data snapshot.", exception);
        }

        return resources;
    }

    private static void ValidateWikiCommunityData(JsonElement root, CancellationToken cancellationToken)
    {
        if (root.GetProperty("schemaVersion").GetInt32() != 2)
            throw new InvalidDataException("Unsupported Wiki item source schema.");

        var patchRows = root.GetProperty("patches");
        var sourceRows = root.GetProperty("sources");
        var itemRows = root.GetProperty("items");
        if (patchRows.GetArrayLength() == 0 || sourceRows.GetArrayLength() == 0 || itemRows.GetArrayLength() == 0 ||
            root.TryGetProperty("itemCount", out var itemCount) && itemCount.GetInt32() != patchRows.GetArrayLength())
            throw new InvalidDataException("Empty or incomplete Wiki item source data.");

        var patchIDs = new HashSet<uint>();
        foreach (var row in patchRows.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var patch = row.GetProperty("p").GetDecimal();
            if (!patchIDs.Add(ReadPositiveCommunityID(row.GetProperty("i"))) || patch < 0 || patch > int.MaxValue)
                throw new InvalidDataException("Invalid Wiki item patch.");
        }

        var containerIDs = new uint[sourceRows.GetArrayLength()];
        var sourceIndex = 0;
        foreach (var source in sourceRows.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var category = source.GetProperty("c").GetInt32();
            if (!Enum.IsDefined(typeof(CollectionSourceCategory), category))
                throw new InvalidDataException("Unknown Wiki item source category.");
            var sourceID = source.GetProperty("i").GetUInt32();
            if (category == (int)CollectionSourceCategory.Container) containerIDs[sourceIndex] = sourceID;
            sourceIndex++;
            _ = source.GetProperty("n").GetString();
            _ = source.GetProperty("d").GetString();
            if (source.TryGetProperty("m", out var map) && map.GetUInt32() != 0)
            {
                if (!float.IsFinite(source.GetProperty("x").GetSingle()) || !float.IsFinite(source.GetProperty("y").GetSingle()))
                    throw new InvalidDataException("Invalid Wiki source coordinates.");
            }
            if (source.TryGetProperty("g", out var gilShop)) _ = gilShop.GetBoolean();
            foreach (var property in new[] { "q", "r" })
                if (source.TryGetProperty(property, out var values))
                    foreach (var value in values.EnumerateArray())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        _ = value.GetUInt32();
                    }
        }

        var itemIDs = new HashSet<uint>();
        var populatedContainers = new HashSet<uint>();
        foreach (var row in itemRows.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var itemID = ReadPositiveCommunityID(row.GetProperty("i"));
            if (!patchIDs.Contains(itemID) || !itemIDs.Add(itemID))
                throw new InvalidDataException("Duplicate Wiki item source ID.");
            foreach (var value in row.GetProperty("s").EnumerateArray())
            {
                var index = value.GetInt32();
                if (index < 0 || index >= containerIDs.Length)
                    throw new InvalidDataException("Invalid Wiki item source index.");
                if (containerIDs[index] != 0) populatedContainers.Add(containerIDs[index]);
            }
        }

        foreach (var value in root.GetProperty("unobtainable").EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!patchIDs.Contains(ReadPositiveCommunityID(value)))
                throw new InvalidDataException("Unobtainable Wiki item has no patch record.");
        }
        foreach (var itemID in BlindBoxCatalog.OrderedBlindBoxItemIds)
            if (!populatedContainers.Contains(itemID) &&
                (!BlindBoxCatalog.SupplementalDropPools.TryGetValue(itemID, out var rewards) || rewards.Count == 0))
                throw new InvalidDataException($"Wiki item sources do not contain blind box {itemID}.");
    }

    private static void ValidateFishingCommunityData(JsonElement root, CancellationToken cancellationToken)
    {
        if (root.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Unsupported fishing source schema.");

        var spotRows = root.GetProperty("spots");
        var itemRows = root.GetProperty("items");
        if (spotRows.GetArrayLength() == 0 || itemRows.GetArrayLength() == 0)
            throw new InvalidDataException("Empty fishing source data.");
        var spotIDs = new HashSet<uint>();
        foreach (var row in spotRows.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!spotIDs.Add(ReadPositiveCommunityID(row.GetProperty("i"))))
                throw new InvalidDataException("Duplicate fishing spot ID.");
            _ = ReadPositiveCommunityID(row.GetProperty("p"));
        }

        var itemIDs = new HashSet<uint>();
        foreach (var row in itemRows.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!itemIDs.Add(ReadPositiveCommunityID(row.GetProperty("i"))))
                throw new InvalidDataException("Duplicate fishing item ID.");
            var spots = row.GetProperty("s");
            if (spots.GetArrayLength() == 0) throw new InvalidDataException("Fishing item has no spot.");
            foreach (var spot in spots.EnumerateArray())
                if (!spotIDs.Contains(ReadPositiveCommunityID(spot)))
                    throw new InvalidDataException("Fishing item references an unknown spot.");
            if (row.TryGetProperty("f", out var folklore)) _ = ReadPositiveCommunityID(folklore);
            foreach (var property in new[] { "v", "w" })
                if (row.TryGetProperty(property, out var weather))
                    foreach (var value in weather.EnumerateArray()) _ = ReadPositiveCommunityID(value);
            foreach (var property in new[] { "a", "b" })
                if (row.TryGetProperty(property, out var value) &&
                    (!float.IsFinite(value.GetSingle()) || value.GetSingle() < 0 || value.GetSingle() > 24))
                    throw new InvalidDataException("Invalid fishing time.");
        }
    }

    private static void ValidateHistoricalEquipmentCommunityData
    (
        string            csvContent,
        string            metadata,
        int               rowCount,
        CancellationToken cancellationToken
    )
    {
        using var document = JsonDocument.Parse(metadata);
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1 || root.GetProperty("currentDropsVerified").GetBoolean() ||
            root.GetProperty("recordType").GetString() != "historical-duty-association")
            throw new InvalidDataException("Unsupported historical equipment source metadata.");
        var sources = root.GetProperty("sources");
        var sourceIDs = new HashSet<uint>();
        foreach (var source in sources.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var itemID = ReadPositiveCommunityID(source.GetProperty("itemID"));
            var sha256 = source.GetProperty("sha256").GetString();
            if (!sourceIDs.Add(itemID) || sha256 is not { Length: 64 } || !sha256.All(Uri.IsHexDigit) ||
                source.GetProperty("url").GetString() != $"https://garlandtools.org/db/doc/item/en/3/{itemID}.json")
                throw new InvalidDataException("Invalid historical equipment source provenance.");
        }

        var itemIDs = new HashSet<uint>();
        using var csv = new CsvReader(new StringReader(csvContent), CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();
        while (csv.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var itemID = csv.GetField<uint>("ItemId");
            if (!sourceIDs.Contains(itemID))
                throw new InvalidDataException("Historical equipment relation has no source provenance.");
            itemIDs.Add(itemID);
        }

        if (sourceIDs.Count == 0 || root.GetProperty("snapshotItemCount").GetInt32() != sourceIDs.Count ||
            root.GetProperty("itemCount").GetInt32() != itemIDs.Count || root.GetProperty("relationCount").GetInt32() != rowCount)
            throw new InvalidDataException("Historical equipment source counts do not match.");
    }

    private static uint ReadPositiveCommunityID(JsonElement value)
    {
        var id = value.GetUInt32();
        return id != 0 ? id : throw new InvalidDataException("Community data ID must be positive.");
    }
}
