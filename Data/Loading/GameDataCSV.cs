using System.Globalization;
using System.IO;
using System.Threading;
using CsvHelper;
using CsvHelper.Configuration;

namespace OmniToolbox.Data;

internal sealed class GameDataCSV(string[] header, IReadOnlyList<GameDataCSV.Row> rows)
{
    internal string[] Header { get; } = header;
    internal IReadOnlyList<Row> Rows { get; } = rows;

    internal static string[] ReadHeader(string content)
    {
        using var reader = new StringReader(content);
        using var csv = new CsvReader(reader, Configuration());
        if (!csv.Read())
            throw new InvalidDataException("Empty game data CSV.");
        csv.ReadHeader();
        return csv.HeaderRecord?.ToArray() ?? throw new InvalidDataException("Missing game data CSV header.");
    }

    internal static GameDataCSV Parse(string content, CancellationToken cancellationToken)
    {
        using var reader = new StringReader(content);
        using var csv = new CsvReader(reader, Configuration());
        if (!csv.Read())
            throw new InvalidDataException("Empty game data CSV.");
        csv.ReadHeader();
        var header = csv.HeaderRecord?.ToArray() ?? throw new InvalidDataException("Missing game data CSV header.");
        var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < header.Length; index++)
            if (!indexes.TryAdd(header[index], index))
                throw new InvalidDataException("Duplicate game data CSV column.");
        var rows = new List<Row>();
        while (csv.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (csv.Parser.Count != header.Length)
                throw new InvalidDataException("Inconsistent game data CSV column count.");
            rows.Add(new(indexes, csv.Parser.Record?.ToArray() ?? throw new InvalidDataException("Missing game data CSV record.")));
        }
        return new(header, rows.AsReadOnly());
    }

    private static CsvConfiguration Configuration() => new(CultureInfo.InvariantCulture)
    {
        AllowComments = true,
        Comment = '#',
        HasHeaderRecord = true,
        IgnoreBlankLines = true,
        TrimOptions = TrimOptions.Trim
    };

    internal readonly struct Row(IReadOnlyDictionary<string, int> indexes, string[] values)
    {
        internal string GetField(string name) => values[indexes[name]];
        internal string GetField(int index) => values[index];

        internal T? GetField<T>(string name) => ConvertField<T>(GetField(name), name);

        internal T? GetField<T>(int index) => ConvertField<T>(values[index], index.ToString(CultureInfo.InvariantCulture));

        private static T? ConvertField<T>(string text, string name)
        {
            var nullableType = Nullable.GetUnderlyingType(typeof(T));
            if (nullableType is not null && string.IsNullOrWhiteSpace(text))
                return default;
            var type = nullableType ?? typeof(T);
            try
            {
                return (T)(type.IsEnum
                    ? Enum.Parse(type, text)
                    : Convert.ChangeType(text, type, CultureInfo.InvariantCulture));
            }
            catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
            {
                throw new InvalidDataException($"Invalid game data field: {name}", exception);
            }
        }
    }
}
