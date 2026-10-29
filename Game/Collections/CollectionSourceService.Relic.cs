using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.UI;

namespace OmniToolbox.Collections;

public sealed partial class CollectionSourceService
{
    private static void AddRelicSources
    (
        IDictionary<uint, HashSet<CollectionSource>> itemRows,
        RelicItem                                    relic
    )
    {
        var (questID, detailKey) = relic.RowId switch
        {
            2 => (66971U, "Collection.Source.Relic.Atma"),
            3 => (66972U, "Collection.Source.Relic.Animus"),
            4 => (66998U, "Collection.Source.Relic.Novus"),
            5 => (65742U, "Collection.Source.Relic.Nexus"),
            6 => (65897U, "Collection.Source.Relic.Zodiac"),
            7 => (66096U, "Collection.Source.Relic.Zeta"),
            _ => (0U, string.Empty)
        };
        if (questID == 0 ||
            !LuminaGetter.TryGetRow<Quest>(questID, out var quest) ||
            !LuminaGetter.TryGetRow<RelicItem>(relic.RowId - 1, out var previous))
        {
            return;
        }

        var npcID = relic.RowId == 7 ? 1011791U : 1008948U;
        if (!LuminaGetter.TryGetRow<ENpcResident>(npcID, out var npc))
            return;

        var location = relic.RowId == 7
            ? new CollectionSourceLocation(138, 34.4f, 31.9f)
            : new CollectionSourceLocation(154, 29.5f, 19.6f);
        var questName = quest.Name.ExtractText();
        var npcName = npc.Singular.ExtractText();
        var itemIDs = GetRelicItemIDs(relic);
        var previousItemIDs = GetRelicItemIDs(previous);
        for (var index = 0; index < itemIDs.Length; index++)
        {
            if (itemIDs[index] == 0 ||
                !LuminaGetter.TryGetRow<Item>(previousItemIDs[index], out var previousItem))
            {
                continue;
            }

            AddSourceIfMissing(itemRows, itemIDs[index], new(
                CollectionSourceCategory.Quest,
                questID,
                questName,
                string.Format(OmniLoc.Get(detailKey), npcName, previousItem.Name.ExtractText()),
                location)
            {
                MapTerritoryID = location.TerritoryID
            });
        }
    }

    private static uint[] GetRelicItemIDs(RelicItem relic) =>
    [
        relic.GladiatorItem.RowId,
        relic.PugilistItem.RowId,
        relic.MarauderItem.RowId,
        relic.LancerItem.RowId,
        relic.ArcherItem.RowId,
        relic.ConjurerItem.RowId,
        relic.ThaumaturgeItem.RowId,
        relic.ArcanistSMNItem.RowId,
        relic.ArcanistSCHItem.RowId,
        relic.ShieldItem.RowId,
        relic.RogueItem.RowId
    ];
}
