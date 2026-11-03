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

internal sealed partial class GameDataCache : IDisposable
{
    private static readonly string[] FileNames = GameDataManifest.GetNames("Supplemental");
    private readonly SemaphoreSlim loadingLock = new(1, 1);
    private Snapshot? supplementalSnapshot;
    private Snapshot? communitySnapshot;
    private IReadOnlyDictionary<string, byte[]> supplementalResources = new Dictionary<string, byte[]>();
    private IReadOnlyDictionary<string, byte[]> communityResources = new Dictionary<string, byte[]>();
    private (long Length, DateTime Modified)? supplementalStamp;
    private (long Length, DateTime Modified)? communityStamp;
    private bool supplementalLoaded;
    private bool communityLoaded;

    internal GameDataSnapshot Data { get; } = new();

    internal Task<IReadOnlyDictionary<string, byte[]>> LoadCachedAsync
    (
        string            configDirectory,
        string            gameVersion,
        CancellationToken cancellationToken
    ) => LoadAsync(configDirectory, gameVersion, false, cancellationToken);

    internal Task<IReadOnlyDictionary<string, byte[]>> LoadAsync
    (
        string            configDirectory,
        string            gameVersion,
        CancellationToken cancellationToken
    ) => LoadAsync(configDirectory, gameVersion, true, cancellationToken);

    private async Task<IReadOnlyDictionary<string, byte[]>> LoadAsync
    (
        string            configDirectory,
        string            gameVersion,
        bool              updateOnline,
        CancellationToken cancellationToken
    )
    {
        await loadingLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!updateOnline)
                return await LoadRuntimeCachedAsync(configDirectory, gameVersion, cancellationToken).ConfigureAwait(false);
            var results = await Task.WhenAll(
                RefreshRuntimeGroupAsync("Supplemental", configDirectory, gameVersion, cancellationToken),
                RefreshRuntimeGroupAsync("Community", configDirectory, gameVersion, cancellationToken)).ConfigureAwait(false);
            var resources = results.SelectMany(group => group).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            return resources;
        }
        finally
        {
            if (updateOnline)
            {
                Data.ReleaseDocuments();
                Data.ReleaseUnusedInputs();
                preparedRuntimePackages.Clear();
            }
            loadingLock.Release();
        }
    }

    private async Task<IReadOnlyDictionary<string, byte[]>> LoadSupplementalAsync
    (
        string            configDirectory,
        string            gameVersion,
        bool              updateOnline,
        CancellationToken cancellationToken
    )
    {
        var (embedded, embeddedHash) = await ReadEmbeddedAsync(FileNames, cancellationToken).ConfigureAwait(false);
        var path = Path.Combine(configDirectory, "Data", "SupplementalData.json");
        var snapshot = supplementalSnapshot;
        var resources = supplementalResources;
        try
        {
            var stamp = GetStamp(path);
            if (!supplementalLoaded || stamp != supplementalStamp)
            {
                snapshot = null;
                resources = new Dictionary<string, byte[]>();
                supplementalLoaded = true;
                supplementalStamp = stamp;
                if (stamp.HasValue)
                {
                    var cacheLength = new FileInfo(path).Length;
                    if (cacheLength > MAX_RESOURCE_BYTES * FileNames.Length)
                        throw new InvalidDataException("Supplemental data cache is too large.");

                    var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                    var cached = JsonSerializer.Deserialize<Snapshot>(json, OmniToolbox.IO.OmniJson.SystemTextOptions);
                    if (cached is { SchemaVersion: 1 } && cached.EmbeddedHash == embeddedHash &&
                        cached.GameVersion == gameVersion && IsRevision(cached.Revision))
                    {
                        resources = ValidateSupplementalFiles(cached.Files, embedded, cancellationToken);
                        snapshot = cached;
                    }
                }
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or CsvHelperException)
        {
            DalamudServices.PluginLog.Warning(exception, "Unable to read supplemental data cache; using bundled data.");
        }

        supplementalSnapshot = snapshot;
        supplementalResources = resources;

        if (!updateOnline)
            return resources;

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
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or JsonException or DecoderFallbackException or
                                           CsvHelperException or KeyNotFoundException or InvalidOperationException ||
                                           exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            DalamudServices.PluginLog.Warning(exception, "Unable to update supplemental data; keeping cached or bundled data.");
            return resources;
        }

        cancellationToken.ThrowIfCancellationRequested();
        supplementalSnapshot = snapshot;
        supplementalResources = resources;
        try
        {
            await AtomicFile.WriteAllTextAsync(path, JsonSerializer.Serialize(snapshot, OmniToolbox.IO.OmniJson.SystemTextOptions), new UTF8Encoding(false),
                cancellationToken).ConfigureAwait(false);
            supplementalStamp = GetStamp(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DalamudServices.PluginLog.Warning(exception, "Unable to save supplemental data cache.");
        }

        return resources;
    }

    private Task<(Dictionary<string, string> Files, string Hash)> ReadEmbeddedAsync
    (
        IEnumerable<string> fileNames,
        CancellationToken   cancellationToken
    )
    {
        var names = fileNames.ToArray();
        var group = string.Join('|', names);
        if (Data.TryGetBundle(group, out var bundle))
            return Task.FromResult(bundle);
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var fileName in names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsPackedResource(fileName))
            {
                files.Add(fileName, string.Empty);
                continue;
            }
            var input = Data.ReadEmbedded(fileName);
            files.Add(fileName, input.Content);
        }

        var resourceGroup = names.SequenceEqual(FileNames, StringComparer.Ordinal) ? "Supplemental" : "Community";
        var result = (files, Data.RuntimeIndex.Groups[resourceGroup]);
        Data.SetBundle(group, files, result.Item2);
        return Task.FromResult(result);
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

    private IReadOnlyDictionary<string, byte[]> ValidateSupplementalFiles
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
            var input = Data.GetInput(fileName, content);
            files[fileName] = input.Content;
            resources.Add(fileName, input.Bytes);
        }

        _ = PrepareRuntimePackage("item-catalog", files, cancellationToken);
        _ = PrepareRuntimePackage("dungeon-sources", files, cancellationToken);
        return resources;
    }

    private int ValidateCSV
    (
        string            fileName,
        string            content,
        string            embedded,
        CancellationToken cancellationToken
    )
    {
        var input = Data.GetInput(fileName, content);
        ReuseRuntimeValidation(fileName, input);
        if (Data.IsValidated(input))
            return input.RowCount;
        var table = input.GetCSV(cancellationToken);
        if (input.IsEmbedded)
        {
            Data.MarkValidated(input);
            return table.Rows.Count;
        }
        if (!(Data.RuntimeIndex.Resources[fileName].Header ?? []).SequenceEqual(table.Header, StringComparer.Ordinal))
            throw new InvalidDataException($"Unsupported data columns: {fileName}");

        var idColumns = table.Header.Where(column => column.EndsWith("Id", StringComparison.Ordinal) ||
                                               column.EndsWith("ID", StringComparison.Ordinal) ||
                                               column is "FightNo" or "ItemSupplementSource").ToArray();
        var rows = 0;
        var patchItems = new HashSet<uint>();
        var relicItems = new HashSet<uint>();
        var equipmentRelations = new HashSet<(uint ItemID, uint ConditionID)>();
        foreach (var csv in table.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
                if (csv.GetField<uint>("ActionId") == 0 || csv.GetField<string>("SourceType") is not
                    ("carnivale" or "dungeon" or "fate" or "guildhests" or "hunt" or "jobquest" or
                     "levequests" or "map" or "raid" or "special" or "trail" or "treasure"))
                    throw new InvalidDataException("Invalid blue magic source.");
                var x = csv.GetField<float?>("X");
                var y = csv.GetField<float?>("Y");
                if (x.HasValue != y.HasValue ||
                    x.HasValue && !float.IsFinite(x.Value) || y.HasValue && !float.IsFinite(y.Value))
                    throw new InvalidDataException("Invalid blue magic coordinates.");
                if (x.HasValue && csv.GetField<uint>("TerritoryID") == 0 && csv.GetField<uint>("DutyID") == 0)
                    throw new InvalidDataException("Blue magic coordinates require a territory or duty ID.");
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
        Data.MarkValidated(input);
        return rows;
    }

    public void Dispose()
    {
        Data.Dispose();
        loadingLock.Dispose();
    }

    private static (long Length, DateTime Modified)? GetStamp(string path)
    {
        var file = new FileInfo(path);
        return file.Exists ? (file.Length, file.LastWriteTimeUtc) : null;
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
