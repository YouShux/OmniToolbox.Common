using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using OmniToolbox.Host;
using TreeTools.IO;

namespace OmniToolbox.Data;

internal sealed partial class GameDataCache
{
    private readonly ConcurrentDictionary<string, RuntimeCacheBundle> runtimeBundles = new(StringComparer.Ordinal);
    private IReadOnlyDictionary<string, byte[]>? runtimeResources;
    private readonly ConcurrentDictionary<string, (GameDataSnapshot.Input[] Inputs, byte[] Bytes)> preparedRuntimePackages = new(StringComparer.Ordinal);

    private async Task<IReadOnlyDictionary<string, byte[]>> LoadRuntimeCachedAsync(
        string configDirectory, string gameVersion, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (runtimeResources is not null)
        {
            return runtimeResources;
        }
        var groups = await Task.WhenAll(
            LoadRuntimeGroupAsync("Supplemental", configDirectory, gameVersion, cancellationToken),
            LoadRuntimeGroupAsync("Community", configDirectory, gameVersion, cancellationToken)).ConfigureAwait(false);
        var resources = groups.SelectMany(group => group).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        Data.Select(resources);
        foreach (var bundle in runtimeBundles.Values)
        {
            if (!bundle.UsesCachedSources)
                continue;
            Data.InstallRuntimePackages(bundle.Packages);
            var path = GetSourceCachePath(configDirectory, bundle.Group);
            Dictionary<string, string>? fallbackFiles = null;
            foreach (var name in bundle.InputHashes.Keys)
            {
                var fileName = name;
                Data.RegisterSourceFallback(fileName, () =>
                {
                    if (fallbackFiles is null)
                    {
                        var file = new FileInfo(path);
                        if (!file.Exists || file.Length > 144 * 1024 * 1024)
                            throw new InvalidDataException("Selected source cache is unavailable.");
                        var snapshot = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(path), OmniToolbox.IO.OmniJson.SystemTextOptions)
                            ?? throw new InvalidDataException("Selected source cache is empty.");
                        if (snapshot.Revision != bundle.Revision || snapshot.GameVersion != gameVersion ||
                            snapshot.EmbeddedHash != bundle.EmbeddedHash)
                            throw new InvalidDataException("Selected source cache revision has changed.");
                        fallbackFiles = snapshot.Files;
                    }
                    if (!fallbackFiles.TryGetValue(fileName, out var content) || !MatchesHash(content, bundle.InputHashes[fileName]))
                        throw new InvalidDataException($"Selected source fallback checksum mismatch: {fileName}");
                    return content;
                });
            }
        }
        runtimeResources = resources.Where(pair => !IsPackedResource(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        return runtimeResources;
    }

    internal void CompleteStartup()
    {
        preparedRuntimePackages.Clear();
        Data.CompleteStartup();
        if (supplementalSnapshot is not null)
        {
            supplementalSnapshot = null;
            supplementalLoaded = false;
            supplementalResources = new Dictionary<string, byte[]>();
        }
        if (communitySnapshot is not null)
        {
            communitySnapshot = null;
            communityLoaded = false;
            communityResources = new Dictionary<string, byte[]>();
        }
    }

    private async Task<IReadOnlyDictionary<string, byte[]>> LoadRuntimeGroupAsync(
        string group, string configDirectory, string gameVersion, CancellationToken cancellationToken)
    {
        var names = group == "Supplemental" ? FileNames : CommunityFileNames;
        var sourcePath = GetSourceCachePath(configDirectory, group);
        var stamp = GetStamp(sourcePath);
        var bundlePath = GetRuntimeCachePath(configDirectory, group);
        if (stamp.HasValue && File.Exists(bundlePath))
        {
            try
            {
                var file = new FileInfo(bundlePath);
                if (file.Length > 64 * 1024 * 1024)
                    throw new InvalidDataException("Runtime data cache is too large.");
                var bytes = await File.ReadAllBytesAsync(bundlePath, cancellationToken).ConfigureAwait(false);
                var bundle = GameDataPackageCodec.Deserialize<RuntimeCacheBundle>(bytes, cancellationToken);
                if (bundle.SchemaVersion != 1 || bundle.GeneratorVersion != 1 || bundle.Group != group ||
                    bundle.GameVersion != gameVersion || bundle.EmbeddedHash != Data.RuntimeIndex.Groups[group] ||
                    bundle.CacheLength != stamp.Value.Length || bundle.CacheModifiedTicks != stamp.Value.Modified.Ticks)
                    throw new InvalidDataException("Runtime data cache does not match its selected source revision.");
                ValidateRuntimeBundle(bundle, names, cancellationToken);
                runtimeBundles[group] = bundle;
                return RestoreRuntimeResources(bundle);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or
                MessagePackSerializationException or InvalidOperationException or ArgumentException)
            {
                DalamudServices.PluginLog.Warning(exception, "Unable to reuse {Group} runtime cache; checking source data.", group);
            }
        }

        if (!stamp.HasValue)
        {
            return new Dictionary<string, byte[]>();
        }

        var resources = group == "Supplemental"
            ? await LoadSupplementalAsync(configDirectory, gameVersion, false, cancellationToken).ConfigureAwait(false)
            : await LoadCommunityAsync(configDirectory, gameVersion, false, cancellationToken).ConfigureAwait(false);
        var snapshot = group == "Supplemental" ? supplementalSnapshot : communitySnapshot;
        var generated = CreateRuntimeBundle(group, gameVersion, stamp.Value, snapshot, cancellationToken);
        runtimeBundles[group] = generated;
        SaveRuntimeBundle(bundlePath, generated, cancellationToken);
        return resources;
    }

    private async Task<IReadOnlyDictionary<string, byte[]>> RefreshRuntimeGroupAsync(
        string group, string configDirectory, string gameVersion, CancellationToken cancellationToken)
    {
        runtimeBundles.TryGetValue(group, out var previous);
        var now = DateTimeOffset.UtcNow;
        if (previous is { UsesCachedSources: true } && previous.CheckedAt <= now && now - previous.CheckedAt < TimeSpan.FromDays(3))
        {
            return RestoreRuntimeResources(previous);
        }
        var resources = group == "Supplemental"
            ? await LoadSupplementalAsync(configDirectory, gameVersion, true, cancellationToken).ConfigureAwait(false)
            : await LoadCommunityAsync(configDirectory, gameVersion, true, cancellationToken).ConfigureAwait(false);
        var snapshot = group == "Supplemental" ? supplementalSnapshot : communitySnapshot;
        var stamp = GetStamp(GetSourceCachePath(configDirectory, group));
        if (stamp.HasValue && snapshot is not null)
        {
            RuntimeCacheBundle bundle;
            if (previous is { UsesCachedSources: true } && previous.Revision == snapshot.Revision)
            {
                previous.CacheLength = stamp.Value.Length;
                previous.CacheModifiedTicks = stamp.Value.Modified.Ticks;
                previous.CheckedAt = snapshot.CheckedAt;
                bundle = previous;
            }
            else
                bundle = CreateRuntimeBundle(group, gameVersion, stamp.Value, snapshot, cancellationToken);
            runtimeBundles[group] = bundle;
            SaveRuntimeBundle(GetRuntimeCachePath(configDirectory, group), bundle, cancellationToken);
        }
        if (group == "Supplemental")
        {
            supplementalSnapshot = null;
            supplementalLoaded = false;
            supplementalResources = new Dictionary<string, byte[]>();
        }
        else
        {
            communitySnapshot = null;
            communityLoaded = false;
            communityResources = new Dictionary<string, byte[]>();
        }
        return resources;
    }

    private RuntimeCacheBundle CreateRuntimeBundle(string group, string gameVersion,
        (long Length, DateTime Modified) stamp, Snapshot? snapshot, CancellationToken cancellationToken)
    {
        var names = group == "Supplemental" ? FileNames : CommunityFileNames;
        var bundle = new RuntimeCacheBundle
        {
            SchemaVersion = 1, GeneratorVersion = 1, Group = group, GameVersion = gameVersion,
            EmbeddedHash = Data.RuntimeIndex.Groups[group], CacheLength = stamp.Length, CacheModifiedTicks = stamp.Modified.Ticks,
            UsesCachedSources = snapshot is not null, Revision = snapshot?.Revision ?? string.Empty,
            CheckedAt = snapshot?.CheckedAt ?? DateTimeOffset.MinValue
        };
        if (snapshot is null)
            return bundle;
        foreach (var name in names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var input = Data.GetInput(name, snapshot.Files[name]);
            bundle.InputHashes.Add(name, input.SHA256);
            bundle.RowCounts.Add(name, input.RowCount);
            if (!IsPackedResource(name))
                bundle.SmallFiles.Add(name, input.Content);
        }
        if (group == "Supplemental")
        {
            bundle.Packages.Add("item-catalog", PrepareRuntimePackage("item-catalog", snapshot.Files, cancellationToken));
            bundle.Packages.Add("dungeon-sources", PrepareRuntimePackage("dungeon-sources", snapshot.Files, cancellationToken));
        }
        else
            bundle.Packages.Add("item-sources", PrepareRuntimePackage("item-sources", snapshot.Files, cancellationToken));
        foreach (var (kind, bytes) in bundle.Packages)
        {
            bundle.PackageHashes.Add(kind, Convert.ToHexString(SHA256.HashData(bytes)));
        }
        return bundle;
    }

    private byte[] PrepareRuntimePackage(string kind, IReadOnlyDictionary<string, string> files, CancellationToken cancellationToken)
    {
        var names = Data.RuntimeIndex.Packages[kind].Inputs;
        var inputs = names.Select(name => Data.GetInput(name, files[name])).ToArray();
        if (preparedRuntimePackages.TryGetValue(kind, out var prepared) && prepared.Inputs.SequenceEqual(inputs))
            return prepared.Bytes;
        var previous = runtimeBundles.Values.FirstOrDefault(bundle => bundle.UsesCachedSources && bundle.Packages.ContainsKey(kind) &&
            inputs.Select((input, index) => bundle.InputHashes.TryGetValue(names[index], out var hash) && input.SHA256 == hash).All(matches => matches));
        byte[] bytes;
        if (previous is not null)
        {
            bytes = previous.Packages[kind];
        }
        else if (kind == "item-sources")
            bytes = GameDataPackageCodec.Serialize(GameDataPackageBuilder.BuildWikiItems(inputs[0].Document.RootElement, cancellationToken), cancellationToken);
        else
        {
            var tables = names.Select((name, index) => KeyValuePair.Create(name, inputs[index].GetCSV(cancellationToken)))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            bytes = kind == "item-catalog"
                ? GameDataPackageCodec.Serialize(GameDataPackageBuilder.BuildItemCatalog(tables, cancellationToken), cancellationToken)
                : GameDataPackageCodec.Serialize(GameDataPackageBuilder.BuildDungeonSources(tables, cancellationToken), cancellationToken);
        }
        preparedRuntimePackages[kind] = (inputs, bytes);
        return bytes;
    }

    private void ValidateRuntimeBundle(RuntimeCacheBundle bundle, string[] names, CancellationToken cancellationToken)
    {
        if (bundle.Packages is null || bundle.PackageHashes is null || bundle.InputHashes is null ||
            bundle.RowCounts is null || bundle.SmallFiles is null || bundle.Revision is null)
            throw new InvalidDataException("Runtime cache fields are missing.");
        if (!bundle.UsesCachedSources)
        {
            if (bundle.Packages.Count != 0 || bundle.SmallFiles.Count != 0 || bundle.InputHashes.Count != 0)
                throw new InvalidDataException("Invalid bundled data fallback marker.");
            return;
        }
        if ((bundle.Group == "Supplemental" ? !IsRevision(bundle.Revision) :
                bundle.Revision.Length != 64 || !bundle.Revision.All(Uri.IsHexDigit)) ||
            bundle.InputHashes.Count != names.Length || bundle.RowCounts.Count != names.Length ||
            names.Any(name => !bundle.InputHashes.TryGetValue(name, out var hash) || hash.Length != 64 || !hash.All(Uri.IsHexDigit) ||
                !bundle.RowCounts.TryGetValue(name, out var rows) || rows < 0))
            throw new InvalidDataException("Incomplete runtime cache source identity.");
        var kinds = bundle.Group == "Supplemental" ? new[] { "item-catalog", "dungeon-sources" } : ["item-sources"];
        if (bundle.Packages.Count != kinds.Length || bundle.PackageHashes.Count != kinds.Length ||
            bundle.SmallFiles.Count != names.Count(name => !IsPackedResource(name)))
            throw new InvalidDataException("Incomplete runtime data cache.");
        foreach (var kind in kinds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!bundle.Packages.TryGetValue(kind, out var bytes) || !bundle.PackageHashes.TryGetValue(kind, out var expectedHash) ||
                bytes.Length == 0 || !string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Runtime cache package checksum mismatch: {kind}");
        }
        foreach (var name in names.Where(name => !IsPackedResource(name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!bundle.SmallFiles.TryGetValue(name, out var content) || !MatchesHash(content, bundle.InputHashes[name]))
                throw new InvalidDataException($"Runtime cache source checksum mismatch: {name}");
        }
    }

    private IReadOnlyDictionary<string, byte[]> RestoreRuntimeResources(RuntimeCacheBundle bundle)
    {
        var resources = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var (name, content) in bundle.SmallFiles)
        {
            var input = Data.GetInput(name, content);
            input.RestoreValidationMetadata(bundle.RowCounts[name], Data.RuntimeIndex.Resources[name].Header);
            Data.MarkValidated(input);
            resources.Add(name, input.Bytes);
        }
        return resources;
    }

    private void ReuseRuntimeValidation(string name, GameDataSnapshot.Input input)
    {
        if (Data.IsValidated(input))
            return;
        foreach (var bundle in runtimeBundles.Values)
        {
            if (!bundle.UsesCachedSources || !bundle.InputHashes.TryGetValue(name, out var hash) || input.SHA256 != hash)
                continue;
            input.RestoreValidationMetadata(bundle.RowCounts[name], Data.RuntimeIndex.Resources[name].Header);
            Data.MarkValidated(input);
            return;
        }
    }

    private static bool MatchesHash(string content, string expectedHash) =>
        string.Equals(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))), expectedHash, StringComparison.OrdinalIgnoreCase);

    private static bool IsPackedResource(string name) => name is "WikiItemSources.json" or "ItemPatch.csv" or "ItemSupplement.csv" or
        "DungeonBoss.csv" or "DungeonChest.csv" or "DungeonBossDrop.csv" or "DungeonBossChest.csv" or "DungeonChestItem.csv";

    private static string GetSourceCachePath(string configDirectory, string group) => Path.Combine(configDirectory, "Data", group + "Data.json");

    private static string GetRuntimeCachePath(string configDirectory, string group) => Path.Combine(configDirectory, "Data", group + "Data.runtime.msgpack");

    private static void SaveRuntimeBundle(string path, RuntimeCacheBundle bundle, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = GameDataPackageCodec.Serialize(bundle, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            AtomicFile.WriteAllBytes(path, bytes);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DalamudServices.PluginLog.Warning(exception, "Unable to save derived runtime cache {Path}; source data is retained.", path);
        }
    }

    [MessagePackObject(AllowPrivate = true)]
    internal sealed partial class RuntimeCacheBundle
    {
        [Key(0)] public int SchemaVersion { get; set; }
        [Key(1)] public int GeneratorVersion { get; set; }
        [Key(2)] public string Group { get; set; } = string.Empty;
        [Key(3)] public string EmbeddedHash { get; set; } = string.Empty;
        [Key(4)] public string GameVersion { get; set; } = string.Empty;
        [Key(5)] public long CacheLength { get; set; }
        [Key(6)] public long CacheModifiedTicks { get; set; }
        [Key(7)] public bool UsesCachedSources { get; set; }
        [Key(8)] public string Revision { get; set; } = string.Empty;
        [Key(9)] public DateTimeOffset CheckedAt { get; set; }
        [Key(10)] public Dictionary<string, byte[]> Packages { get; set; } = new(StringComparer.Ordinal);
        [Key(11)] public Dictionary<string, string> PackageHashes { get; set; } = new(StringComparer.Ordinal);
        [Key(12)] public Dictionary<string, string> InputHashes { get; set; } = new(StringComparer.Ordinal);
        [Key(13)] public Dictionary<string, int> RowCounts { get; set; } = new(StringComparer.Ordinal);
        [Key(14)] public Dictionary<string, string> SmallFiles { get; set; } = new(StringComparer.Ordinal);
    }
}
