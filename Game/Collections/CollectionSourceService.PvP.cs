using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.UI;

namespace OmniToolbox.Collections;

public sealed partial class CollectionSourceService
{
    private static readonly uint[] PVPSeason21PortraitItemIDs =
    [
        51723, 51724, 51725, 51726, 51727, 51728, 51729, 51730, 51731, 51732, 51733
    ];

    private static void AddPVPSeasonPortraitSources(IDictionary<uint, HashSet<CollectionSource>> itemRows)
    {
        if (!LuminaGetter.TryGetRow<ENpcResident>(1005417, out var npc))
            return;

        var npcName = npc.Singular.ExtractText();
        var location = new CollectionSourceLocation(250, 4.9f, 5.7f);
        foreach (var itemID in PVPSeason21PortraitItemIDs)
        {
            if (!LuminaGetter.TryGetRow<Item>(itemID, out var item) ||
                item.ItemAction.ValueNullable?.Action.RowId != 29459)
            {
                continue;
            }

            var detailKey = itemID switch
            {
                51723 or 51724 or 51725 => "Collection.Source.PvP.Season21Rank",
                51726 => "Collection.Source.PvP.Season21Bronze",
                _ => "Collection.Source.PvP.Season21Tier"
            };
            AddSourceIfMissing(itemRows, itemID, new(
                CollectionSourceCategory.PvP,
                0,
                item.Name.ExtractText(),
                string.Format(OmniLoc.Get(detailKey), npcName),
                location)
            {
                MapTerritoryID = location.TerritoryID
            });
        }
    }

    private static void AddPVPSeriesSources(
        IDictionary<uint, HashSet<CollectionSource>> itemRows,
        PvPSeries series)
    {
        if (series.RowId == 0)
            return;

        for (var level = 0; level < series.LevelRewards.Count; level++)
        {
            var rewards = series.LevelRewards[level];
            for (var slot = 0; slot < rewards.LevelRewardItem.Count; slot++)
            {
                var itemID = rewards.LevelRewardItem[slot].RowId;
                if (itemID == 0 || rewards.LevelRewardCount[slot] == 0 ||
                    !LuminaGetter.TryGetRow<Item>(itemID, out var item) || item.Name.IsEmpty)
                {
                    continue;
                }

                AddSourceIfMissing(itemRows, itemID, new(
                    CollectionSourceCategory.PvP,
                    0,
                    item.Name.ExtractText(),
                    string.Format(OmniLoc.Get("Collection.Source.PvP.SeriesReward"), series.RowId, level)));
            }
        }
    }
}
