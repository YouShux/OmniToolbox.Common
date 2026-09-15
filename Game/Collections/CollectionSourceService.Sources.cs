using System.Collections.Frozen;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game.Text.Evaluator;
using Lumina.Excel.Sheets;
using LuminaSupplemental.Excel.Model;
using LuminaSupplemental.Excel.Services;
using OmenTools;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.Host;

namespace OmniToolbox.Collections;

public sealed partial class CollectionSourceService
{

    private static readonly Regex CoordinateOnlyLineRegex = new(
        @"^\s*[（(]\s*[\d.]+\s*[,，]\s*[\d.]+\s*[）)]\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] TripleTriadNPCPrefixes =
    [
        "对局：",
        "对局:",
        "Match:",
        "対戦：",
        "対戦:",
        "Versus:"
    ];

    private static void AddContainerSources(
        IDictionary<uint, HashSet<CollectionSource>> itemRows,
        IReadOnlyDictionary<uint, Item> itemsByID)
    {
        try
        {
            var rows = CsvLoader.LoadResource<ItemSupplement>(
                CsvLoader.ItemSupplementResourceName,
                true,
                out var failedLines,
                out var exceptions);
            var containerNames = new Dictionary<uint, string>();
            foreach (var row in rows)
            {
                if (row.ItemId == 0 ||
                    row.SourceItemId == 0 ||
                    row.ItemSupplementSource == ItemSupplementSource.Desynth)
                {
                    continue;
                }

                if (!containerNames.TryGetValue(row.SourceItemId, out var containerName))
                {
                    containerName = itemsByID.TryGetValue(row.SourceItemId, out var container)
                        ? container.Name.ExtractText()
                        : FormattableString.Invariant($"#{row.SourceItemId}");
                    containerNames.Add(row.SourceItemId, containerName);
                }
                AddSourceIfMissing(itemRows, row.ItemId, new(
                    CollectionSourceCategory.Container,
                    row.SourceItemId,
                    containerName,
                    string.Empty));
            }

            if (failedLines.Count != 0)
            {
                DalamudServices.PluginLog.Warning(
                    $"ItemSupplement had {failedLines.Count} failed row(s): {exceptions[0].Message}");
            }
        }
        catch (Exception ex)
        {
            DalamudServices.PluginLog.Warning(ex, "ItemSupplement source loading failed.");
        }
    }

    private static void AddCraftingSources(IDictionary<uint, HashSet<CollectionSource>> itemRows)
    {
        var craftedItemIds = new HashSet<uint>();
        foreach (var recipe in LuminaGetter.Get<Recipe>())
        {
            if (recipe.RowId == 0 || !recipe.ItemResult.IsValid)
            {
                continue;
            }

            var itemID = recipe.ItemResult.RowId;
            if (craftedItemIds.Add(itemID) &&
                !HasSource(itemRows, itemID, CollectionSourceCategory.Crafting))
            {
                AddSource(itemRows, itemID, new(
                    CollectionSourceCategory.Crafting,
                    recipe.RowId,
                    FormattableString.Invariant($"#{recipe.RowId}"),
                    string.Empty));
            }
        }

        foreach (var (itemId, sources) in itemRows)
        {
            if (!craftedItemIds.Contains(itemId))
            {
                sources.RemoveWhere(source => source.Category == CollectionSourceCategory.Crafting);
            }
        }
    }

    private static void AddTripleTriadSources(
        IDictionary<uint, HashSet<CollectionSource>> itemRows,
        IReadOnlyDictionary<uint, Item> itemsByID)
    {
        foreach (var item in itemsByID.Values)
        {
            if (!item.ItemAction.IsValid || item.ItemAction.Value.Action.RowId != 3_357)
            {
                continue;
            }

            var cardID = (uint)item.ItemAction.Value.Data[0];
            if (cardID == 0 ||
                !LuminaGetter.TryGetRow<TripleTriadCardResident>(cardID, out var resident) ||
                !LuminaGetter.TryGetRow<TripleTriadCardObtain>(resident.AcquisitionType.RowId, out var obtain))
            {
                continue;
            }

            var npcName = string.Empty;
            if (resident.AcquisitionType.RowId is 6 or 10 &&
                LuminaGetter.TryGetRow<ENpcResident>(resident.Acquisition.RowId, out var npc))
            {
                npcName = npc.Singular.ExtractText();
            }

            var description = obtain.Text.RowId == 0
                ? npcName
                : FormatTripleTriadSource(
                    DService.Instance().SeStringEvaluator.EvaluateFromAddon(
                        obtain.Text.RowId,
                        new[]
                        {
                            new SeStringParameter(resident.Acquisition.RowId),
                            new SeStringParameter(resident.Location.RowId)
                        }).ToString(),
                    npcName);
            if (description.Length == 0)
            {
                continue;
            }

            var isDuty = resident.AcquisitionType.RowId is not (6 or 10) &&
                LuminaGetter.TryGetRow<ContentFinderCondition>(resident.Acquisition.RowId, out _);
            ReplaceSource(itemRows, item.RowId, new(
                isDuty ? CollectionSourceCategory.Duty : CollectionSourceCategory.Other,
                resident.Acquisition.RowId != 0 ? resident.Acquisition.RowId : cardID,
                description,
                string.Empty));
        }
    }

    private static string FormatTripleTriadSource(string text, string npcName)
    {
        var parts = new List<string>();
        var npc = npcName;
        foreach (var line in text
                     .Replace("\r\n", "\n", StringComparison.Ordinal)
                     .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (CoordinateOnlyLineRegex.IsMatch(line))
            {
                continue;
            }

            var isNPCLine = false;
            for (var index = 0; index < TripleTriadNPCPrefixes.Length; index++)
            {
                if (!line.StartsWith(TripleTriadNPCPrefixes[index], StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (npc.Length == 0)
                {
                    npc = line[TripleTriadNPCPrefixes[index].Length..].Trim();
                }

                isNPCLine = true;
                break;
            }

            if (!isNPCLine)
            {
                parts.Add(line);
            }
        }

        if (npc.Length != 0 && !parts.Any(part => part.Contains(npc, StringComparison.Ordinal)))
        {
            parts.Insert(0, npc);
        }

        return string.Join(' ', parts);
    }

    private static bool HasSource(
        IDictionary<uint, HashSet<CollectionSource>> target,
        uint itemID,
        CollectionSourceCategory category,
        uint? sourceID = null) =>
        target.TryGetValue(itemID, out var sources) &&
        sources.Any(source =>
            source.Category == category &&
            (!sourceID.HasValue || source.SourceID == sourceID.Value));

    private static void AddSourceIfMissing(
        IDictionary<uint, HashSet<CollectionSource>> target,
        uint itemID,
        CollectionSource source)
    {
        if (!HasSource(target, itemID, source.Category, source.SourceID))
        {
            AddSource(target, itemID, source);
        }
    }

    private static void ReplaceSource(
        IDictionary<uint, HashSet<CollectionSource>> target,
        uint itemID,
        CollectionSource source)
    {
        if (!target.TryGetValue(itemID, out var sources))
        {
            AddSource(target, itemID, source);
            return;
        }

        if (source.Category == CollectionSourceCategory.Duty &&
            sources.Any(existing =>
                existing.Category == CollectionSourceCategory.Duty &&
                existing.Detail.Length != 0 &&
                existing.Description.EndsWith(source.Description, StringComparison.Ordinal)))
        {
            return;
        }

        sources.RemoveWhere(existing =>
            existing.SourceID == source.SourceID &&
            existing.Category is CollectionSourceCategory.Other or CollectionSourceCategory.Duty);
        sources.Add(source);
    }

    private static void AddSource(
        IDictionary<uint, HashSet<CollectionSource>> target,
        uint id,
        CollectionSource source)
    {
        if (!target.TryGetValue(id, out var sources))
        {
            sources = [];
            target.Add(id, sources);
        }

        sources.Add(source);
    }

    private static FrozenDictionary<uint, IReadOnlyList<CollectionSource>> Freeze(
        IDictionary<uint, HashSet<CollectionSource>> source) =>
        source.ToFrozenDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<CollectionSource>)pair.Value
                .OrderBy(value => value.Category)
                .ThenBy(value => value.Description, StringComparer.Ordinal)
                .ThenBy(value => value.SourceID)
                .ThenBy(value => value.Detail, StringComparer.Ordinal)
                .ToArray());
}
