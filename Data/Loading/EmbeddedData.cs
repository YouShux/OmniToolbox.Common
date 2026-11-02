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

    public static Stream Open(string fileName)
    {
        if (Snapshot is { } snapshot)
        {
            var input = snapshot.GetSelected(fileName);
            StartupTiming.Count(fileName, "view-open", bytes: input.Bytes.Length);
            return new MemoryStream(input.Bytes, writable: false);
        }
        if (UpdatedResources is { } resources && resources.TryGetValue(fileName, out var content))
        {
            StartupTiming.Count(fileName, "read", bytes: content.Length);
            return new MemoryStream(content, writable: false);
        }

        var stream = typeof(EmbeddedData).Assembly.GetManifestResourceStream("OmniToolbox.Data." + fileName)
               ?? throw new InvalidDataException($"Embedded data resource is missing: {fileName}");
        StartupTiming.Count(fileName, "read", bytes: stream.Length);
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
            StartupTiming.Count(fileName, "parse");
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
        StartupTiming.Count(fileName, "parse");
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
        var rows = 0L;
        while (csv.Read())
        {
            rows++;
            try
            {
                readRow(csv);
            }
            catch (Exception)
            {
            }
        }
        StartupTiming.Count(fileName, "rows", rows);
    }
}
