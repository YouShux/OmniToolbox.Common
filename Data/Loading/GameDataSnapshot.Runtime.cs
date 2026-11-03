using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using MessagePack;
using OmniToolbox.Host;

namespace OmniToolbox.Data;

internal sealed partial class GameDataSnapshot
{
    private readonly Dictionary<string, byte[]> runtimePackages = new(StringComparer.Ordinal);
    private readonly object itemCatalogSync = new();
    private readonly object wikiItemsSync = new();
    private readonly object dungeonSourcesSync = new();
    private GameDataPackageIndex? runtimeIndex;
    private ItemCatalogData? itemCatalog;
    private WikiItemData? wikiItems;
    private DungeonSourceData? dungeonSources;

    internal GameDataPackageIndex RuntimeIndex
    {
        get
        {
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (runtimeIndex is not null)
                    return runtimeIndex;
                using var stream = typeof(EmbeddedData).Assembly.GetManifestResourceStream("OmniToolbox.Data.Runtime.embedded-index.json")
                    ?? throw new InvalidDataException("Game data runtime index is missing.");
                var index = JsonSerializer.Deserialize<GameDataPackageIndex>(stream, OmniToolbox.IO.OmniJson.SystemTextOptions)
                    ?? throw new InvalidDataException("Game data runtime index is empty.");
                if (index.SchemaVersion != 1 || index.GeneratorVersion != 1 ||
                    !index.Groups.ContainsKey("Supplemental") || !index.Groups.ContainsKey("Community") ||
                    !index.Packages.ContainsKey("item-catalog") || !index.Packages.ContainsKey("item-sources") ||
                    !index.Packages.ContainsKey("dungeon-sources"))
                    throw new InvalidDataException("Unsupported game data runtime index.");
                runtimeIndex = index;
                return index;
            }
        }
    }

    internal void InstallRuntimePackages(IReadOnlyDictionary<string, byte[]> packages)
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            foreach (var (kind, bytes) in packages)
            {
                if (!RuntimeIndex.Packages.ContainsKey(kind) || !runtimePackages.TryAdd(kind, bytes))
                    throw new InvalidDataException($"Duplicate or unknown runtime package: {kind}");
            }
        }
    }

    internal void RegisterSourceFallback(string name, Func<string> readSource)
    {
        lock (sync)
            sourceFallbacks.Add(name, readSource);
    }

    internal ItemCatalogData GetItemCatalog(CancellationToken cancellationToken = default)
    {
        lock (itemCatalogSync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (itemCatalog is not null)
                return itemCatalog;
            try
            {
                if (RequiresSourceConversion("item-catalog"))
                    throw new InvalidOperationException("Selected source data requires an item catalog conversion.");
                itemCatalog = DecodeRuntimePackage<ItemCatalogData>("item-catalog", cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or MessagePackSerializationException or InvalidOperationException)
            {
                DalamudServices.PluginLog.Warning(exception, "Unable to read item catalog runtime package; using source data.");
                itemCatalog = GameDataPackageBuilder.BuildItemCatalog(new Dictionary<string, string>
                {
                    ["ItemSupplement.csv"] = GetSelected("ItemSupplement.csv").Content,
                    ["ItemPatch.csv"] = GetSelected("ItemPatch.csv").Content
                }, cancellationToken);
            }
            return itemCatalog;
        }
    }

    internal WikiItemData GetWikiItems(CancellationToken cancellationToken = default)
    {
        lock (wikiItemsSync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (wikiItems is not null)
                return wikiItems;
            try
            {
                if (RequiresSourceConversion("item-sources"))
                    throw new InvalidOperationException("Selected source data requires an item source conversion.");
                wikiItems = DecodeRuntimePackage<WikiItemData>("item-sources", cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or MessagePackSerializationException or InvalidOperationException)
            {
                DalamudServices.PluginLog.Warning(exception, "Unable to read item source runtime package; using source data.");
                wikiItems = GameDataPackageBuilder.BuildWikiItems(GetSelected("WikiItemSources.json").Content, cancellationToken);
            }
            return wikiItems;
        }
    }

    internal DungeonSourceData GetDungeonSources(CancellationToken cancellationToken = default)
    {
        lock (dungeonSourcesSync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (dungeonSources is not null)
                return dungeonSources;
            try
            {
                if (RequiresSourceConversion("dungeon-sources"))
                    throw new InvalidOperationException("Selected source data requires a dungeon source conversion.");
                dungeonSources = DecodeRuntimePackage<DungeonSourceData>("dungeon-sources", cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or MessagePackSerializationException or InvalidOperationException)
            {
                DalamudServices.PluginLog.Warning(exception, "Unable to read dungeon runtime package; using source data.");
                var files = RuntimeIndex.Packages["dungeon-sources"].Inputs.ToDictionary(
                    name => name, name => GetSelected(name).Content, StringComparer.Ordinal);
                dungeonSources = GameDataPackageBuilder.BuildDungeonSources(files, cancellationToken);
            }
            return dungeonSources;
        }
    }

    private T DecodeRuntimePackage<T>(string kind, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] bytes;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (runtimePackages.TryGetValue(kind, out var existing))
                bytes = existing;
            else
                bytes = ReadEmbeddedRuntimePackage(kind);
        }
        return GameDataPackageCodec.Deserialize<T>(bytes, cancellationToken);
    }

    private bool RequiresSourceConversion(string kind)
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return !runtimePackages.ContainsKey(kind) && RuntimeIndex.Packages[kind].Inputs.Any(selected.ContainsKey);
        }
    }

    private byte[] ReadEmbeddedRuntimePackage(string kind)
    {
        var package = RuntimeIndex.Packages[kind];
        using var stream = typeof(EmbeddedData).Assembly.GetManifestResourceStream("OmniToolbox.Data.Runtime." + package.Name)
            ?? throw new InvalidDataException($"Game data runtime package is missing: {kind}");
        if (stream.Length > 64 * 1024 * 1024)
            throw new InvalidDataException($"Game data runtime package is too large: {kind}");
        using var destination = new MemoryStream((int)stream.Length);
        stream.CopyTo(destination);
        var bytes = destination.ToArray();
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), package.SHA256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Game data runtime package checksum mismatch: {kind}");
        runtimePackages.Add(kind, bytes);
        return bytes;
    }
}
