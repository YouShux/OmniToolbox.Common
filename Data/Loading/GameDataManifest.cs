using System.IO;
using System.Text.Json;

namespace OmniToolbox.Data;

internal static class GameDataManifest
{
    private static readonly IReadOnlyList<(string Name, string Group)> Resources = Read();

    internal static string[] GetNames(string group) => Resources.Where(resource => resource.Group == group)
        .Select(resource => resource.Name).ToArray();

    private static IReadOnlyList<(string Name, string Group)> Read()
    {
        using var stream = typeof(GameDataManifest).Assembly.GetManifestResourceStream("OmniToolbox.Data.resource-manifest.json")
            ?? throw new InvalidDataException("Game data resource manifest is missing.");
        using var document = JsonDocument.Parse(stream);
        if (document.RootElement.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Unsupported game data resource manifest.");
        var resources = new List<(string Name, string Group)>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in document.RootElement.GetProperty("resources").EnumerateArray())
        {
            var name = row.GetProperty("name").GetString();
            var group = row.GetProperty("group").GetString();
            if (string.IsNullOrEmpty(name) || Path.GetFileName(name) != name || !names.Add(name) ||
                group is not ("Supplemental" or "Community" or "Collection"))
                throw new InvalidDataException("Invalid game data resource definition.");
            resources.Add((name, group));
        }
        return resources.AsReadOnly();
    }
}
