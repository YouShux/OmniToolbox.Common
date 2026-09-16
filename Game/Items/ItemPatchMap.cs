using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using LuminaSupplemental.Excel.Model;
using LuminaSupplemental.Excel.Services;
using OmniToolbox.Data;
using OmniToolbox.Host;

namespace OmniToolbox.Items;

public sealed class ItemPatchMap
{
    private readonly IReadOnlyDictionary<uint, decimal> patches;

    public ItemPatchMap()
    {
        var rows = CsvLoader.LoadResource<ItemPatch>(
            CsvLoader.ItemPatchResourceName,
            true,
            out var failedLines,
            out var exceptions);
        var loadedPatches = ItemPatch.ToItemLookup(rows).ToDictionary();
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

        if (failedLines.Count != 0)
        {
            DalamudServices.PluginLog.Warning(
                $"ItemPatch 数据有 {failedLines.Count} 行加载失败，首个错误: {exceptions[0].Message}");
        }
    }

    public static string Format(decimal patch) => patch.ToString("0.0#", CultureInfo.InvariantCulture);

    public decimal Get(uint itemID) => patches.GetValueOrDefault(itemID);
}
