using System.IO;
using System.Net;
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
    private static readonly string[] CommunityFileNames =
    [
        "WikiItemSources.json", "FishingSources.json", "BlueSpells.csv", "RelicItemIdToAchievement.csv",
        "EquipmentSourceSupplement.csv", "EquipmentSourceSupplement.meta.json"
    ];

    private static async Task<IReadOnlyDictionary<string, byte[]>> LoadCommunityAsync
    (
        string            configDirectory,
        string            gameVersion,
        CancellationToken cancellationToken
    )
    {
        var (embedded, embeddedHash) = await ReadEmbeddedAsync(CommunityFileNames, cancellationToken).ConfigureAwait(false);
        var path = Path.Combine(configDirectory, "Data", "CommunityData.json");
        Snapshot? snapshot = null;
        IReadOnlyDictionary<string, byte[]> resources = new Dictionary<string, byte[]>();
        try
        {
            if (File.Exists(path))
            {
                if (new FileInfo(path).Length > MAX_COMMUNITY_BYTES)
                    throw new InvalidDataException("Community data cache is too large.");
                var cached = JsonSerializer.Deserialize<Snapshot>(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false));
                if (cached is { SchemaVersion: 1 } && cached.EmbeddedHash == embeddedHash)
                {
                    resources = ValidateCommunityFiles(cached.Files, embedded, cancellationToken);
                    snapshot = cached;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or CsvHelperException or
                                           KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {
            DalamudServices.PluginLog.Warning(exception, "Unable to read community data cache; using bundled data.");
        }

        var now = DateTimeOffset.UtcNow;
        if (snapshot is not null && snapshot.Revision is { Length: 64 } && snapshot.Revision.All(Uri.IsHexDigit) &&
            snapshot.GameVersion == gameVersion && snapshot.CheckedAt <= now && now - snapshot.CheckedAt < TimeSpan.FromDays(3))
            return resources;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            string stateContent;
            try
            {
                stateContent = await DownloadAsync(
                    "https://github.com/YouShux/OmniToolbox.Common/releases/download/community-data/community-data-state.json",
                    timeout.Token, maximumBytes: 64 * 1024).ConfigureAwait(false);
            }
            catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                stateContent = await DownloadAsync(
                    "https://github.com/YouShux/OmniToolbox.Common/releases/download/community-data/community-data-state.previous.json",
                    timeout.Token, maximumBytes: 64 * 1024).ConfigureAwait(false);
            }
            using var stateDocument = JsonDocument.Parse(stateContent);
            var state = stateDocument.RootElement;
            var revision = state.GetProperty("BundleSHA256").GetString();
            if (state.GetProperty("SchemaVersion").GetInt32() != 1 ||
                revision is not { Length: 64 } || !revision.All(Uri.IsHexDigit))
                throw new InvalidDataException("Invalid community data state.");

            if (snapshot is null || !string.Equals(snapshot.Revision, revision, StringComparison.OrdinalIgnoreCase))
            {
                using var response = await HTTPClientHelper.Instance().Get().GetAsync(
                    $"https://github.com/YouShux/OmniToolbox.Common/releases/download/community-data/community-data-{revision.ToUpperInvariant()}.json.gz",
                    HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var content = await ReadContentAsync(response, MAX_COMMUNITY_BYTES, timeout.Token,
                    decompress: true, expectedHash: revision).ConfigureAwait(false);
                var files = ReadCommunityPackage(content, cancellationToken);
                resources = ValidateCommunityFiles(files, embedded, cancellationToken);
                snapshot = new()
                {
                    SchemaVersion = 1,
                    EmbeddedHash = embeddedHash,
                    Revision = revision,
                    Files = files
                };
            }

            snapshot.GameVersion = gameVersion;
            snapshot.CheckedAt = now;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException or CsvHelperException or
                                           KeyNotFoundException or InvalidOperationException or FormatException or OverflowException or DecoderFallbackException ||
                                           exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            DalamudServices.PluginLog.Warning(exception, "Unable to update community data; keeping cached or bundled data.");
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
            DalamudServices.PluginLog.Warning(exception, "Unable to save community data cache.");
        }

        return resources;
    }

    private static Dictionary<string, string> ReadCommunityPackage
    (
        string            content,
        CancellationToken cancellationToken
    )
    {
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        if (root.GetProperty("SchemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Unsupported community data package.");
        _ = root.GetProperty("UpdatedAtUtc").GetDateTimeOffset();
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in root.GetProperty("Files").EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CommunityFileNames.Contains(entry.Name, StringComparer.Ordinal))
                throw new InvalidDataException($"Unsupported community data resource: {entry.Name}");
            var text = entry.Value.GetProperty("Content").GetString();
            var expectedHash = entry.Value.GetProperty("SHA256").GetString();
            if (string.IsNullOrWhiteSpace(text) || Encoding.UTF8.GetByteCount(text) > MAX_RESOURCE_BYTES ||
                expectedHash is not { Length: 64 } || !expectedHash.All(Uri.IsHexDigit) ||
                !string.Equals(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))), expectedHash, StringComparison.OrdinalIgnoreCase) ||
                !files.TryAdd(entry.Name, text))
                throw new InvalidDataException($"Invalid community data resource: {entry.Name}");
        }

        if (files.Count != CommunityFileNames.Length)
            throw new InvalidDataException("Incomplete community data package.");
        return files;
    }

    #region 常量

    private const int MAX_COMMUNITY_BYTES = 64 * 1024 * 1024;

    #endregion
}
