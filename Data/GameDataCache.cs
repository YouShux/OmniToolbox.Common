using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using OmenTools.OmenService;
using OmniToolbox.Host;
using TreeTools.IO;

namespace OmniToolbox.Data;

internal static partial class GameDataCache
{
    private static readonly string[] FileNames =
    [
        "ItemPatch.csv", "ItemSupplement.csv", "StoreItem.csv", "BGMOrchestrion.csv",
        "DungeonBoss.csv", "DungeonChest.csv", "DungeonBossDrop.csv",
        "DungeonBossChest.csv", "DungeonChestItem.csv"
    ];

    internal static async Task<IReadOnlyDictionary<string, byte[]>> LoadAsync
    (
        string            configDirectory,
        string            gameVersion,
        CancellationToken cancellationToken
    )
    {
        var results = await Task.WhenAll(
            LoadSupplementalAsync(configDirectory, gameVersion, cancellationToken),
            LoadCommunityAsync(configDirectory, gameVersion, cancellationToken)).ConfigureAwait(false);
        return results.SelectMany(resources => resources).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    private static async Task<IReadOnlyDictionary<string, byte[]>> LoadSupplementalAsync
    (
        string            configDirectory,
        string            gameVersion,
        CancellationToken cancellationToken
    )
    {
        var (embedded, embeddedHash) = await ReadEmbeddedAsync(FileNames, cancellationToken).ConfigureAwait(false);
        var path = Path.Combine(configDirectory, "Data", "SupplementalData.json");
        Snapshot? snapshot = null;
        IReadOnlyDictionary<string, byte[]> resources = new Dictionary<string, byte[]>();
        try
        {
            if (File.Exists(path))
            {
                if (new FileInfo(path).Length > MAX_RESOURCE_BYTES * FileNames.Length)
                    throw new InvalidDataException("Supplemental data cache is too large.");

                var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                var cached = JsonSerializer.Deserialize<Snapshot>(json);
                if (cached is { SchemaVersion: 1 } && cached.EmbeddedHash == embeddedHash && IsRevision(cached.Revision))
                {
                    resources = ValidateSupplementalFiles(cached.Files, embedded, cancellationToken);
                    snapshot = cached;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or CsvHelperException)
        {
            DalamudServices.PluginLog.Warning(exception, "Unable to read supplemental data cache; using bundled data.");
        }

        var now = DateTimeOffset.UtcNow;
        if (snapshot is not null && snapshot.GameVersion == gameVersion &&
            snapshot.CheckedAt <= now && now - snapshot.CheckedAt < TimeSpan.FromDays(3))
            return resources;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            using var revisionDocument = JsonDocument.Parse(await DownloadAsync(
                "https://api.github.com/repos/Critical-Impact/LuminaSupplemental/commits?path=src%2FLuminaSupplemental.Excel%2FGenerated&per_page=1",
                timeout.Token).ConfigureAwait(false));
            if (revisionDocument.RootElement.GetArrayLength() != 1)
                throw new InvalidDataException("Supplemental data revision is unavailable.");
            var revision = revisionDocument.RootElement[0].GetProperty("sha").GetString() ?? string.Empty;
            if (!IsRevision(revision))
                throw new InvalidDataException("Invalid supplemental data revision.");

            if (snapshot is null || snapshot.Revision != revision)
            {
                var files = await Task.WhenAll(FileNames.Select(async fileName => KeyValuePair.Create(fileName,
                    await DownloadAsync(
                        $"https://raw.githubusercontent.com/Critical-Impact/LuminaSupplemental/{revision}/src/LuminaSupplemental.Excel/Generated/{fileName}",
                        timeout.Token).ConfigureAwait(false)))).ConfigureAwait(false);
                var contents = files.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                resources = ValidateSupplementalFiles(contents, embedded, cancellationToken);
                snapshot = new()
                {
                    SchemaVersion = 1,
                    EmbeddedHash = embeddedHash,
                    Revision = revision,
                    Files = contents
                };
            }

            snapshot.GameVersion = gameVersion;
            snapshot.CheckedAt = now;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException or DecoderFallbackException or
                                           CsvHelperException or KeyNotFoundException or InvalidOperationException ||
                                           exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            DalamudServices.PluginLog.Warning(exception, "Unable to update supplemental data; keeping cached or bundled data.");
            return resources;
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await AtomicFile.WriteAllTextAsync(path, JsonSerializer.Serialize(snapshot), new UTF8Encoding(false),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DalamudServices.PluginLog.Warning(exception, "Unable to save supplemental data cache.");
        }

        return resources;
    }

    private static async Task<(Dictionary<string, string> Files, string Hash)> ReadEmbeddedAsync
    (
        IEnumerable<string> fileNames,
        CancellationToken   cancellationToken
    )
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var fileName in fileNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var reader = new StreamReader(EmbeddedData.Open(fileName));
            var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            files.Add(fileName, content);
            hash.AppendData(Encoding.UTF8.GetBytes(fileName));
            hash.AppendData(Encoding.UTF8.GetBytes(content));
        }

        return (files, Convert.ToHexString(hash.GetHashAndReset()));
    }

    private static async Task<string> DownloadAsync
    (
        string            url,
        CancellationToken cancellationToken,
        int               maximumBytes = MAX_RESOURCE_BYTES
    )
    {
        using var response = await HTTPClientHelper.Instance().Get().GetAsync(
            url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadContentAsync(response, maximumBytes, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> ReadContentAsync
    (
        HttpResponseMessage response,
        int                 maximumBytes,
        CancellationToken   cancellationToken,
        bool                decompress = false,
        string?             expectedHash = null
    )
    {
        if (response.Content.Headers.ContentLength > maximumBytes)
            throw new InvalidDataException("Data response is too large.");

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var content = await ReadBytesAsync(source, maximumBytes, cancellationToken).ConfigureAwait(false);
        if (expectedHash is not null && !string.Equals(
                Convert.ToHexString(SHA256.HashData(content)), expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Downloaded data package does not match its published revision.");
        if (decompress)
        {
            using var compressed = new MemoryStream(content, writable: false);
            using var decompressor = new GZipStream(compressed, CompressionMode.Decompress);
            content = await ReadBytesAsync(decompressor, maximumBytes, cancellationToken).ConfigureAwait(false);
        }

        return new UTF8Encoding(false, true).GetString(content);
    }

    private static async Task<byte[]> ReadBytesAsync
    (
        Stream            source,
        int               maximumBytes,
        CancellationToken cancellationToken
    )
    {
        using var destination = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            if (destination.Length + count > maximumBytes)
                throw new InvalidDataException("Data response is too large.");
            destination.Write(buffer, 0, count);
        }

        return destination.ToArray();
    }

    private static IReadOnlyDictionary<string, byte[]> ValidateSupplementalFiles
    (
        Dictionary<string, string>?         files,
        IReadOnlyDictionary<string, string> embedded,
        CancellationToken                  cancellationToken
    )
    {
        if (files is null || files.Count != FileNames.Length)
            throw new InvalidDataException("Incomplete supplemental data snapshot.");

        var resources = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var fileName in FileNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!files.TryGetValue(fileName, out var content) || string.IsNullOrWhiteSpace(content) ||
                Encoding.UTF8.GetByteCount(content) > MAX_RESOURCE_BYTES)
                throw new InvalidDataException($"Missing or oversized supplemental data: {fileName}");

            ValidateCSV(fileName, content, embedded[fileName], cancellationToken);
            resources.Add(fileName, Encoding.UTF8.GetBytes(content));
        }

        return resources;
    }

    private static int ValidateCSV
    (
        string            fileName,
        string            content,
        string            embedded,
        CancellationToken cancellationToken
    )
    {
        using var expected = new CsvReader(new StringReader(embedded), CultureInfo.InvariantCulture);
        expected.Read();
        expected.ReadHeader();
        using var csv = new CsvReader(new StringReader(content), CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();
        if (expected.HeaderRecord is not { } header || csv.HeaderRecord is not { } actualHeader ||
            !header.SequenceEqual(actualHeader, StringComparer.Ordinal))
            throw new InvalidDataException($"Unsupported data columns: {fileName}");

        var idColumns = header.Where(column => column.EndsWith("Id", StringComparison.Ordinal) ||
                                               column.EndsWith("ID", StringComparison.Ordinal) ||
                                               column is "FightNo" or "ItemSupplementSource").ToArray();
        var rows = 0;
        var patchItems = new HashSet<uint>();
        var relicItems = new HashSet<uint>();
        var equipmentRelations = new HashSet<(uint ItemID, uint ConditionID)>();
        while (csv.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (csv.Parser.Count != header.Length)
                throw new InvalidDataException($"Invalid data row: {fileName}");
            foreach (var column in idColumns)
                _ = csv.GetField<uint>(column);

            if (fileName == "ItemPatch.csv")
            {
                var startID = csv.GetField<uint>("StartItemId");
                var endID = csv.GetField<uint>("EndItemId");
                var patch = csv.GetField<decimal>("PatchNo");
                if (startID > endID || endID == uint.MaxValue || (ulong)patchItems.Count + endID - startID + 1 > 1_000_000 ||
                    patch <= 0 || patch > int.MaxValue)
                    throw new InvalidDataException("Invalid supplemental item patch range.");
                for (var itemID = startID; itemID <= endID; itemID++)
                    if (!patchItems.Add(itemID))
                        throw new InvalidDataException("Overlapping supplemental item patch ranges.");
            }
            else if (fileName == "BlueSpells.csv")
            {
                if (csv.GetField<uint>("ActionId") == 0 || string.IsNullOrWhiteSpace(csv.GetField<string>("SourceType")))
                    throw new InvalidDataException("Invalid blue magic source.");
                var x = csv.GetField<float?>("X");
                var y = csv.GetField<float?>("Y");
                if (x.HasValue && !float.IsFinite(x.Value) || y.HasValue && !float.IsFinite(y.Value))
                    throw new InvalidDataException("Invalid blue magic coordinates.");
                _ = csv.GetField<uint?>("Level");
            }
            else if (fileName == "RelicItemIdToAchievement.csv")
            {
                var itemID = csv.GetField<uint>("ItemId");
                if (itemID == 0 || csv.GetField<uint>("AchievementId") == 0 || !relicItems.Add(itemID))
                    throw new InvalidDataException("Invalid relic achievement relation.");
            }
            else if (fileName == "EquipmentSourceSupplement.csv")
            {
                var itemID = csv.GetField<uint>("ItemId");
                var conditionID = csv.GetField<uint>("ContentFinderConditionId");
                if (itemID == 0 || conditionID == 0 || !equipmentRelations.Add((itemID, conditionID)))
                    throw new InvalidDataException("Invalid historical equipment relation.");
            }

            rows++;
        }

        if (rows == 0)
            throw new InvalidDataException($"Empty data: {fileName}");
        return rows;
    }

    private static bool IsRevision(string? revision) => revision is { Length: 40 } && revision.All(Uri.IsHexDigit);

    private sealed class Snapshot
    {
        public int                        SchemaVersion { get; set; }
        public string                     EmbeddedHash  { get; set; } = string.Empty;
        public string                     GameVersion   { get; set; } = string.Empty;
        public string                     Revision      { get; set; } = string.Empty;
        public DateTimeOffset             CheckedAt     { get; set; }
        public Dictionary<string, string> Files         { get; set; } = [];
    }

    #region 常量

    private const int MAX_RESOURCE_BYTES = 16 * 1024 * 1024;

    #endregion
}
