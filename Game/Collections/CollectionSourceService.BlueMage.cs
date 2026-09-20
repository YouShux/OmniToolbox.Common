using System.Collections.Frozen;
using System.IO;
using System.Linq;
using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;

namespace OmniToolbox.Collections;

public sealed partial class CollectionSourceService
{
    private static FrozenDictionary<uint, IReadOnlyList<CollectionSource>> BuildBlueMageSources(
        Dictionary<uint, List<BlueSpellSource>> spellRows)
    {
        var coordinates = spellRows.Values.SelectMany(rows => rows)
            .Where(row => row.X.HasValue && row.Y.HasValue)
            .GroupBy(row => (row.SourceType, row.MobDescription.Trim(), BlueSpellZone(row.LocationDescription)))
            .Select(group => new { group.Key, Positions = group.Select(row => (row.X, row.Y)).Distinct().ToArray() })
            .Where(group => group.Positions.Length == 1)
            .ToDictionary(group => group.Key, group => group.Positions[0]);
        var duties = LuminaGetter.Get<ContentFinderCondition>()
            .Where(row => row.RowId != 0 && !row.Name.IsEmpty)
            .GroupBy(row => row.Name.ExtractText())
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single().RowId);
        var territories = LuminaGetter.Get<TerritoryType>()
            .Where(row => row.RowId != 0 && row.PlaceName.RowId != 0 &&
                          row.ContentFinderCondition.RowId == 0 &&
                          row.Map.ValueNullable is { } map && map.TerritoryType.RowId == row.RowId)
            .GroupBy(row => row.PlaceName.Value.Name.ExtractText())
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single().RowId);
        var sources = new Dictionary<uint, HashSet<CollectionSource>>();
        foreach (var (actionID, rows) in spellRows)
        {
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var zone = BlueSpellZone(row.LocationDescription);
                if ((!row.X.HasValue || !row.Y.HasValue) &&
                    coordinates.TryGetValue((row.SourceType, row.MobDescription.Trim(), zone), out var position))
                {
                    row = row with { X = position.X, Y = position.Y };
                    rows[index] = row;
                }

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
                var dutyName = row.LocationDescription.Trim();
                if (row.SourceType == "guildhests" && dutyName.StartsWith("行会令：", StringComparison.Ordinal))
                {
                    dutyName = dutyName[4..];
                }

                var dutyID = category == CollectionSourceCategory.Duty ? duties.GetValueOrDefault(dutyName) : 0;
                var territoryID = category != CollectionSourceCategory.Duty ? territories.GetValueOrDefault(zone) : 0;
                CollectionSourceLocation? location = territoryID != 0 && row.X is { } x && row.Y is { } y
                    ? new(territoryID, x, y)
                    : null;
                AddSource(sources, actionID, new(
                    category,
                    dutyID,
                    NormalizeBlueMageText(row.MobDescription),
                    FormatBlueMageDetail(row.LocationDescription, row.X, row.Y, row.Level, NormalizeBlueMageText(row.Note)),
                    location)
                {
                    MapTerritoryID = territoryID
                });
            }
        }

        return Freeze(sources);
    }

    private static string BlueSpellZone(string locationDescription)
    {
        var separator = locationDescription.IndexOfAny(['：', ':']);
        return (separator > 0 ? locationDescription[..separator] : locationDescription).Trim();
    }
}
