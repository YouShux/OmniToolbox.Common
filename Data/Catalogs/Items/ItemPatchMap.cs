using System.Globalization;
using OmniToolbox.Data;

namespace OmniToolbox.Items;

public sealed class ItemPatchMap
{
    private readonly IReadOnlyDictionary<uint, decimal> patches;

    public ItemPatchMap() : this(null) { }

    internal ItemPatchMap(GameDataSnapshot? snapshot)
    {
        var catalog = snapshot?.GetItemCatalog() ?? EmbeddedData.GetItemCatalog();
        var loadedPatches = new Dictionary<uint, decimal>();
        foreach (var range in catalog.PatchRanges)
        {
            for (var itemID = range.StartItemID; itemID <= range.EndItemID; itemID++)
            {
                loadedPatches.Add(itemID, range.Patch);
            }
        }
        var wikiItems = snapshot?.GetWikiItems() ?? EmbeddedData.GetWikiItems();
        foreach (var row in wikiItems.Patches)
        {
            if (row.Patch > 0)
            {
                loadedPatches[row.ItemID] = row.Patch;
            }
        }

        patches = loadedPatches;
    }

    public static string Format(decimal patch) => patch.ToString("0.0#", CultureInfo.InvariantCulture);

    public decimal Get(uint itemID) => patches.GetValueOrDefault(itemID);
}
