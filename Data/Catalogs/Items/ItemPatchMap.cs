using System.Globalization;
using System.Text.Json;
using OmniToolbox.Data;

namespace OmniToolbox.Items;

public sealed class ItemPatchMap
{
    private readonly IReadOnlyDictionary<uint, decimal> patches;

    public ItemPatchMap() : this(null) { }

    internal ItemPatchMap(GameDataSnapshot? snapshot)
    {
        var ranges = new List<(uint StartID, uint EndID, decimal Patch)>();
        EmbeddedData.ReadRows("ItemPatch.csv", csv => ranges.Add((
            csv.GetField<uint>("StartItemId"),
            csv.GetField<uint>("EndItemId"),
            csv.GetField<decimal>("PatchNo"))));
        var loadedPatches = new Dictionary<uint, decimal>();
        foreach (var (startID, endID, patch) in ranges)
        {
            for (var itemID = startID; itemID <= endID; itemID++)
            {
                loadedPatches.Add(itemID, patch);
            }
        }
        using var stream = snapshot is null ? EmbeddedData.Open("WikiItemSources.json") : null;
        using var ownedDocument = stream is null ? null : JsonDocument.Parse(stream);
        if (ownedDocument is not null)
            StartupTiming.Count("WikiItemSources.json", "parse");
        var document = snapshot?.GetSelected("WikiItemSources.json").Document ?? ownedDocument
            ?? throw new InvalidOperationException("Wiki item versions are unavailable.");
        foreach (var row in document.RootElement.GetProperty("patches").EnumerateArray())
        {
            var patch = row.GetProperty("p").GetDecimal();
            if (patch > 0)
            {
                loadedPatches[row.GetProperty("i").GetUInt32()] = patch;
            }
        }

        patches = loadedPatches;
    }

    public static string Format(decimal patch) => patch.ToString("0.0#", CultureInfo.InvariantCulture);

    public decimal Get(uint itemID) => patches.GetValueOrDefault(itemID);
}
