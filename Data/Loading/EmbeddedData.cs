using System.Globalization;
using System.IO;
using System.Threading;
using CsvHelper;
using CsvHelper.Configuration;

namespace OmniToolbox.Data;

internal static class EmbeddedData
{
    internal static IReadOnlyDictionary<string, byte[]>? UpdatedResources { private get; set; }
    internal static GameDataSnapshot? Snapshot { private get; set; }

    internal static ItemCatalogData GetItemCatalog(CancellationToken cancellationToken = default)
    {
        if (Snapshot is { } snapshot)
            return snapshot.GetItemCatalog(cancellationToken);
        using var owned = CreateRuntimeSnapshot();
        return owned.GetItemCatalog(cancellationToken);
    }

    internal static WikiItemData GetWikiItems(CancellationToken cancellationToken = default)
    {
        if (Snapshot is { } snapshot)
            return snapshot.GetWikiItems(cancellationToken);
        using var owned = CreateRuntimeSnapshot();
        return owned.GetWikiItems(cancellationToken);
    }

    internal static DungeonSourceData GetDungeonSources(CancellationToken cancellationToken = default)
    {
        if (Snapshot is { } snapshot)
            return snapshot.GetDungeonSources(cancellationToken);
        using var owned = CreateRuntimeSnapshot();
        return owned.GetDungeonSources(cancellationToken);
    }

    private static GameDataSnapshot CreateRuntimeSnapshot()
    {
        var snapshot = new GameDataSnapshot();
        if (UpdatedResources is { } resources)
        {
            var selected = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var (name, bytes) in resources)
                selected.Add(name, snapshot.GetInput(name, System.Text.Encoding.UTF8.GetString(bytes)).Bytes);
            snapshot.Select(selected);
        }
        return snapshot;
    }

    public static Stream Open(string fileName)
    {
        if (Snapshot is { } snapshot)
        {
            var input = snapshot.GetSelected(fileName);
            return new MemoryStream(input.Bytes, writable: false);
        }
        if (UpdatedResources is { } resources && resources.TryGetValue(fileName, out var content))
        {
            return new MemoryStream(content, writable: false);
        }

        var stream = typeof(EmbeddedData).Assembly.GetManifestResourceStream("OmniToolbox.Data." + fileName)
               ?? throw new InvalidDataException($"Embedded data resource is missing: {fileName}");
        return stream;
    }

    internal static void ReadRows(string fileName, Action<GameDataCSV.Row> readRow, CancellationToken cancellationToken = default)
    {
        GameDataCSV table;
        if (Snapshot is { } snapshot)
            table = snapshot.GetSelected(fileName).GetCSV(cancellationToken);
        else
        {
            using var stream = Open(fileName);
            using var reader = new StreamReader(stream);
            table = GameDataCSV.Parse(reader.ReadToEnd(), cancellationToken);
        }
        foreach (var row in table.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                readRow(row);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
            }
        }
    }

    public static void ReadCSV
    (
        string            fileName,
        Action<CsvReader> readRow
    )
    {
        using var reader = new StreamReader(Open(fileName));
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
            catch (Exception)
            {
            }
        }
    }
}
