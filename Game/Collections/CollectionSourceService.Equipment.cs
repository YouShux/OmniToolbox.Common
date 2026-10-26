using System.Threading;
using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.Data;
using OmniToolbox.UI;

namespace OmniToolbox.Collections;

public sealed partial class CollectionSourceService
{
    private static readonly uint[] UnfinishedRelicQuestIDs =
    [
        66655, 66656, 66657, 66658, 66659, 66660, 66661, 66662, 66663, 67115
    ];

    private static readonly uint[] DisguiseEquipmentQuestIDs = [66110, 67884];

    private static readonly uint[] RetiredArdentEquipmentIDs =
    [
        11745, 11746, 11747, 11748, 11749,
        11750, 11751, 11752, 11753, 11754,
        11755, 11756, 11757, 11758, 11759,
        11760, 11761, 11762, 11763, 11764
    ];

    private static readonly uint[] EternalBondWristletIDs = [8693, 8694, 8695];

    private static readonly uint[] LegacyGatheringToolIDs = [2533, 2559];

    private static void AddEternalBondWristletSource(
        IDictionary<uint, HashSet<CollectionSource>> itemRows,
        uint itemID)
    {
        var planItemID = itemID switch
        {
            8693 => 9545U,
            8694 => 9546U,
            8695 => 9547U,
            _ => 0U
        };
        if (planItemID == 0 || !LuminaGetter.TryGetRow<Item>(itemID, out _) ||
            !LuminaGetter.TryGetRow<Item>(planItemID, out var planItem) ||
            !LuminaGetter.TryGetRow<Quest>(67114, out var quest) ||
            !LuminaGetter.TryGetRow<ENpcResident>(1009761, out var npc))
            return;

        var questName = quest.Name.ExtractText();
        var location = new CollectionSourceLocation(152, 17.5f, 18.2f);
        AddSourceIfMissing(itemRows, itemID, new(
            CollectionSourceCategory.Quest,
            quest.RowId,
            questName,
            string.Format(
                OmniLoc.Get("Collection.Source.Equipment.EternalBondWristlet"),
                planItem.Name.ExtractText(),
                npc.Singular.ExtractText(),
                questName),
            location)
        {
            MapTerritoryID = location.TerritoryID
        });
    }

    private static void AddLegacyGatheringToolSource(
        IDictionary<uint, HashSet<CollectionSource>> itemRows,
        uint itemID)
    {
        if (itemID is not (2533 or 2559) || !LuminaGetter.TryGetRow<Item>(itemID, out _))
            return;

        AddSourceIfMissing(itemRows, itemID, new(
            CollectionSourceCategory.Other,
            0,
            OmniLoc.Get("Collection.Source.Equipment.LegacyGatheringShop"),
            OmniLoc.Get("Collection.Source.Equipment.LegacyGatheringShopDetail")));
    }

    private static void AddRetiredArdentEquipmentSource(
        IDictionary<uint, HashSet<CollectionSource>> itemRows,
        uint itemID)
    {
        if (!LuminaGetter.TryGetRow<Item>(itemID, out _) ||
            !LuminaGetter.TryGetRow<Item>(31, out var currency))
            return;

        AddSourceIfMissing(itemRows, itemID, new(
            CollectionSourceCategory.Tomestones,
            currency.RowId,
            currency.Name.ExtractText(),
            OmniLoc.Get("Collection.Source.Equipment.RetiredArdent")));
    }

    private static void AddDisguiseEquipmentSources(
        IDictionary<uint, HashSet<CollectionSource>> itemRows,
        uint questID)
    {
        if (!LuminaGetter.TryGetRow<Quest>(questID, out var quest))
            return;

        var questName = quest.Name.ExtractText();
        foreach (var parameter in quest.QuestParams)
        {
            if (parameter.ScriptArg is not (2995 or 3306) ||
                parameter.ScriptInstruction.ToString() is not ("RITEM0" or "RITEM1") ||
                !LuminaGetter.TryGetRow<Item>(parameter.ScriptArg, out _))
                continue;

            AddSourceIfMissing(itemRows, parameter.ScriptArg, new(
                CollectionSourceCategory.Quest,
                questID,
                questName,
                OmniLoc.Get("Collection.Source.Equipment.Disguise")));
        }
    }

    private static void AddUnfinishedRelicSources(
        IDictionary<uint, HashSet<CollectionSource>> itemRows,
        uint questID)
    {
        if (!LuminaGetter.TryGetRow<Quest>(questID, out var quest))
            return;

        var itemID = 0U;
        foreach (var parameter in quest.QuestParams)
        {
            if (parameter.ScriptInstruction.ToString() == "RITEM1")
            {
                itemID = parameter.ScriptArg;
                break;
            }
        }

        if (itemID == 0 || !LuminaGetter.TryGetRow<Item>(itemID, out _))
            return;

        var questName = quest.Name.ExtractText();
        AddSourceIfMissing(itemRows, itemID, new(
            CollectionSourceCategory.Quest,
            questID,
            questName,
            OmniLoc.Get("Collection.Source.Equipment.UnfinishedRelic")));

        if (!LuminaGetter.TryGetRow<ENpcResident>(1008161, out var npc))
            return;

        var location = new CollectionSourceLocation(154, 30.4f, 20.1f);
        AddSourceIfMissing(itemRows, itemID, new(
            CollectionSourceCategory.Other,
            npc.RowId,
            npc.Singular.ExtractText(),
            string.Format(OmniLoc.Get("Collection.Source.Equipment.UnfinishedRelicReclaim"), questName),
            location)
        {
            MapTerritoryID = location.TerritoryID
        });
    }

    private static (uint ItemID, uint ConditionID)[] ReadHistoricalEquipmentSourceRows(
        CancellationToken cancellationToken)
    {
        var rows = new List<(uint ItemID, uint ConditionID)>();
        EmbeddedData.ReadCSV("EquipmentSourceSupplement.csv", csv =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var itemID = csv.GetField<uint>("ItemId");
            var conditionID = csv.GetField<uint>("ContentFinderConditionId");
            if (itemID != 0 && conditionID != 0)
                rows.Add((itemID, conditionID));
        });
        return rows.ToArray();
    }

    private static void AddHistoricalEquipmentSource(
        IDictionary<uint, HashSet<CollectionSource>> itemRows,
        uint itemID,
        uint conditionID)
    {
        if (!LuminaGetter.TryGetRow<Item>(itemID, out var item) || item.Rarity != 7 ||
            !LuminaGetter.TryGetRow<ContentFinderCondition>(conditionID, out var condition) ||
            condition.ContentType.RowId != 2 || condition.Content.RowId == 0)
            return;

        AddSourceIfMissing(itemRows, itemID, new(
            CollectionSourceCategory.Duty,
            condition.Content.RowId,
            condition.Name.ExtractText(),
            OmniLoc.Get("Collection.Source.Equipment.HistoricalDuty")));
    }
}
