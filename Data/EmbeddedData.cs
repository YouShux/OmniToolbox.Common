using System.Globalization;
using System.IO;
using CsvHelper;
using CsvHelper.Configuration;

namespace OmniToolbox.Data;

internal static class EmbeddedData
{
    public static Stream Open(string fileName) =>
        typeof(EmbeddedData).Assembly.GetManifestResourceStream("OmniToolbox.Data." + fileName)
        ?? throw new InvalidDataException($"Embedded data resource is missing: {fileName}");

    public static void ReadCSV(string fileName, Action<CsvReader> readRow)
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
