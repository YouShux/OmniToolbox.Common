using System.IO;

namespace OmniToolbox.Data;

internal static class EmbeddedData
{
    public static Stream Open(string fileName) =>
        typeof(EmbeddedData).Assembly.GetManifestResourceStream("OmniToolbox.Data." + fileName)
        ?? throw new InvalidDataException($"Embedded data resource is missing: {fileName}");
}
