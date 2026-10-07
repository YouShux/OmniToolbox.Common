using System.Collections.Frozen;
using System.IO;

namespace OmniToolbox.Collections;

public sealed partial class CollectionSourceService
{
    private static FrozenDictionary<uint, IReadOnlyList<CollectionSource>> BuildBlueMageSources(
        Dictionary<uint, List<BlueSpellSource>> spellRows)
    {
        var sources = new Dictionary<uint, HashSet<CollectionSource>>();
        foreach (var (actionID, rows) in spellRows)
        {
            foreach (var row in rows)
            {
                var category = row.SourceType switch
                {
                    "carnivale" or "dungeon" or "guildhests" or "raid" or "trail" => CollectionSourceCategory.Duty,
                    "fate" => CollectionSourceCategory.Fate,
                    "hunt" => CollectionSourceCategory.TheHunt,
                    "jobquest" => CollectionSourceCategory.Quest,
                    "treasure" => CollectionSourceCategory.TreasureHunts,
                    "levequests" or "map" or "special" => CollectionSourceCategory.Other,
                    _ => throw new InvalidDataException($"未知的青魔获取类型：{row.SourceType}")
                };
                CollectionSourceLocation? location = row.TerritoryID != 0 && row.X is { } x && row.Y is { } y
                    ? new(row.TerritoryID, x, y)
                    : null;
                AddSource(sources, actionID, new(
                    category,
                    row.DutyID,
                    NormalizeBlueMageText(row.MobDescription),
                    FormatBlueMageDetail(row.LocationDescription, row.X, row.Y, row.Level, NormalizeBlueMageText(row.Note)),
                    location)
                {
                    MapTerritoryID = row.TerritoryID
                });
            }
        }

        return Freeze(sources);
    }
}
