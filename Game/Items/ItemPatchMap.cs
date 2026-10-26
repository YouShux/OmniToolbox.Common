using System.Globalization;
using System.Text.Json;
using OmniToolbox.Data;

namespace OmniToolbox.Items;

public sealed class ItemPatchMap
{
    private readonly IReadOnlyDictionary<uint, decimal> patches;

    public ItemPatchMap()
    {
        var ranges = new List<(uint StartID, uint EndID, decimal Patch)>();
        EmbeddedData.ReadCSV("ItemPatch.csv", csv => ranges.Add((
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
        using var stream = EmbeddedData.Open("WikiItemSources.json");
        using var document = JsonDocument.Parse(stream);
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
