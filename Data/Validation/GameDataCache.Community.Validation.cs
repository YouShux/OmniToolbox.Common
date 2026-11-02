using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using OmniToolbox.Collections;
using OmniToolbox.Items;

namespace OmniToolbox.Data;

internal sealed partial class GameDataCache
{
    private IReadOnlyDictionary<string, byte[]> ValidateCommunityFiles
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

                var input = Data.GetInput(fileName, content);
                files[fileName] = input.Content;
                if (fileName.EndsWith(".csv", StringComparison.Ordinal))
                {
                    var rows = ValidateCSV(fileName, content, embedded[fileName], cancellationToken);
                    if (fileName == "EquipmentSourceSupplement.csv") equipmentRows = rows;
                }
                else
                {
                    if (Data.IsValidated(input) || input.IsEmbedded)
                    {
                        if (input.IsEmbedded)
                            StartupTiming.Count(fileName, "embedded-equality-reuse");
                        resources.Add(fileName, input.Bytes);
                        continue;
                    }
                    StartupTiming.Count(fileName, "validate");
                    var document = input.Document;
                    if (fileName is "WikiItemSources.json" or "FishingSources.json")
                    {
                        if (input.GeneratedAtUTC < Data.GetInput(fileName, embedded[fileName]).GeneratedAtUTC)
                            throw new InvalidDataException($"Community data snapshot predates bundled data: {fileName}");
                    }
                    if (fileName == "WikiItemSources.json") ValidateWikiCommunityData(document.RootElement, cancellationToken);
                    else if (fileName == "FishingSources.json") ValidateFishingCommunityData(document.RootElement, cancellationToken);
                    Data.MarkValidated(input);
                }

                resources.Add(fileName, Data.GetInput(fileName, content).Bytes);
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
            root.GetProperty("itemCount").GetInt32() != patchRows.GetArrayLength())
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
            if (category == (int)CollectionSourceCategory.Container)
            {
                if (!patchIDs.Contains(sourceID))
                    throw new InvalidDataException("Wiki container source references an unknown item.");
                containerIDs[sourceIndex] = sourceID;
            }
            sourceIndex++;
            if (source.GetProperty("n").ValueKind != JsonValueKind.String ||
                source.GetProperty("d").ValueKind != JsonValueKind.String)
                throw new InvalidDataException("Invalid Wiki source description.");

            var hasMap = source.TryGetProperty("m", out var map);
            var hasX = source.TryGetProperty("x", out var x);
            var hasY = source.TryGetProperty("y", out var y);
            if (hasMap != hasX || hasX != hasY)
                throw new InvalidDataException("Wiki source map and coordinates must be provided together.");
            if (hasMap)
            {
                _ = ReadPositiveCommunityID(map);
                if (!float.IsFinite(x.GetSingle()) || !float.IsFinite(y.GetSingle()))
                    throw new InvalidDataException("Invalid Wiki source coordinates.");
            }
            if (source.TryGetProperty("g", out var gilShop)) _ = gilShop.GetBoolean();
            foreach (var property in new[] { "q", "r" })
                if (source.TryGetProperty(property, out var values))
                {
                    var ids = new HashSet<uint>();
                    foreach (var value in values.EnumerateArray())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var id = ReadPositiveCommunityID(value);
                        if (!ids.Add(id) || property == "r" && !patchIDs.Contains(id))
                            throw new InvalidDataException("Invalid or duplicate Wiki source relation.");
                    }
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
            var sources = row.GetProperty("s");
            if (sources.GetArrayLength() == 0)
                throw new InvalidDataException("Wiki item has no source.");
            var sourceIndices = new HashSet<int>();
            foreach (var value in sources.EnumerateArray())
            {
                var index = value.GetInt32();
                if (index < 0 || index >= containerIDs.Length || !sourceIndices.Add(index))
                    throw new InvalidDataException("Invalid Wiki item source index.");
                if (containerIDs[index] != 0) populatedContainers.Add(containerIDs[index]);
            }
        }

        var unobtainableIDs = new HashSet<uint>();
        foreach (var value in root.GetProperty("unobtainable").EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = ReadPositiveCommunityID(value);
            if (!patchIDs.Contains(id) || !unobtainableIDs.Add(id))
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
            var referencedSpots = new HashSet<uint>();
            foreach (var spot in spots.EnumerateArray())
            {
                var id = ReadPositiveCommunityID(spot);
                if (!spotIDs.Contains(id) || !referencedSpots.Add(id))
                    throw new InvalidDataException("Fishing item references an unknown spot.");
            }
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

    private void ValidateHistoricalEquipmentCommunityData
    (
        string            csvContent,
        string            metadata,
        int               rowCount,
        CancellationToken cancellationToken
    )
    {
        var csvInput = Data.GetInput("EquipmentSourceSupplement.csv", csvContent);
        var metadataInput = Data.GetInput("EquipmentSourceSupplement.meta.json", metadata);
        if (Data.HasEquipmentValidation(csvInput, metadataInput))
            return;
        var document = metadataInput.Document;
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
        foreach (var csv in csvInput.GetCSV(cancellationToken).Rows)
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
        Data.MarkEquipmentValidated(csvInput, metadataInput);
    }

    private static uint ReadPositiveCommunityID(JsonElement value)
    {
        var id = value.GetUInt32();
        return id != 0 ? id : throw new InvalidDataException("Community data ID must be positive.");
    }
}
