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

    private static uint GetEternalBondPlanItemID(uint itemID) => itemID switch
    {
        8693 => 9545U,
        8694 => 9546U,
        8695 => 9547U,
        _ => 0U
    };

    private static void AddEternalBondWristletSource
    (
        IDictionary<uint, HashSet<CollectionSource>> itemRows,
        uint                                         itemID,
        PreparedSourceText?                          preparedText = null
    )
    {
        var planItemID = GetEternalBondPlanItemID(itemID);
        if (planItemID == 0 || !LuminaGetter.TryGetRow<Item>(itemID, out _) ||
            !LuminaGetter.TryGetRow<Item>(planItemID, out var planItem) ||
            !LuminaGetter.TryGetRow<Quest>(67114, out var quest) ||
            !LuminaGetter.TryGetRow<ENpcResident>(1009761, out var npc))
            return;

        var questName = CaptureQuestName(quest, preparedText);
        var npcName = CaptureNPCName(npc, preparedText);
        var planItemName = CaptureItemName(planItem, preparedText);
        var location = new CollectionSourceLocation(152, 17.5f, 18.2f);
        BuildSource(preparedText, () => AddSource(itemRows, itemID, new(
            CollectionSourceCategory.Quest,
            quest.RowId,
            questName.Text,
            string.Format(
                OmniLoc.Get("Collection.Source.Equipment.EternalBondWristlet"),
                planItemName.Text,
                npcName.Text,
                questName.Text),
            location)
        {
            MapTerritoryID = location.TerritoryID
        }));
    }

    private static void AddLegacyGatheringToolSource
    (
        IDictionary<uint, HashSet<CollectionSource>> itemRows,
        uint                                         itemID,
        PreparedSourceText?                          preparedText = null
    )
    {
        if (itemID is not (2533 or 2559) || !LuminaGetter.TryGetRow<Item>(itemID, out _))
            return;

        BuildSource(preparedText, () => AddSource(itemRows, itemID, new(
            CollectionSourceCategory.Other,
            0,
            OmniLoc.Get("Collection.Source.Equipment.LegacyGatheringShop"),
            OmniLoc.Get("Collection.Source.Equipment.LegacyGatheringShopDetail"))));
    }

    private static void AddRetiredArdentEquipmentSource
    (
        IDictionary<uint, HashSet<CollectionSource>> itemRows,
        uint                                         itemID,
        PreparedSourceText?                          preparedText = null
    )
    {
        if (!LuminaGetter.TryGetRow<Item>(itemID, out _) ||
            !LuminaGetter.TryGetRow<Item>(31, out var currency))
            return;

        var currencyName = CaptureItemName(currency, preparedText);
        BuildSource(preparedText, () => AddSource(itemRows, itemID, new(
            CollectionSourceCategory.Tomestones,
            currency.RowId,
            currencyName.Text,
            OmniLoc.Get("Collection.Source.Equipment.RetiredArdent"))));
    }

    private static void AddDisguiseEquipmentSources
    (
        IDictionary<uint, HashSet<CollectionSource>> itemRows,
        uint                                         questID,
        PreparedSourceText?                          preparedText = null
    )
    {
        if (!LuminaGetter.TryGetRow<Quest>(questID, out var quest))
            return;

        var questName = CaptureQuestName(quest, preparedText);
        var parameters = quest.QuestParams
            .Where(parameter => parameter.ScriptArg is 2995 or 3306)
            .Select(parameter => (ItemID: parameter.ScriptArg, Instruction: parameter.ScriptInstruction,
                HasItem: LuminaGetter.TryGetRow<Item>(parameter.ScriptArg, out _))).ToArray();
        BuildSource(preparedText, () =>
        {
            foreach (var parameter in parameters)
            {
                if (parameter.Instruction.ToString() is not ("RITEM0" or "RITEM1") || !parameter.HasItem)
                    continue;
                AddSource(itemRows, parameter.ItemID, new(
                    CollectionSourceCategory.Quest,
                    questID,
                    questName.Text,
                    OmniLoc.Get("Collection.Source.Equipment.Disguise")));
            }
        });
    }

    private static void AddUnfinishedRelicSources
    (
        IDictionary<uint, HashSet<CollectionSource>> itemRows,
        uint                                         questID,
        PreparedSourceText?                          preparedText = null
    )
    {
        if (!LuminaGetter.TryGetRow<Quest>(questID, out var quest))
            return;

        var parameters = quest.QuestParams.Select(parameter =>
            (ItemID: parameter.ScriptArg, Instruction: parameter.ScriptInstruction,
                HasItem: LuminaGetter.TryGetRow<Item>(parameter.ScriptArg, out _))).ToArray();
        var questName = CaptureQuestName(quest, preparedText);
        var npcName = LuminaGetter.TryGetRow<ENpcResident>(1008161, out var npc)
            ? CaptureNPCName(npc, preparedText)
            : null;
        var location = new CollectionSourceLocation(154, 30.4f, 20.1f);
        BuildSource(preparedText, () =>
        {
            var itemID = 0U;
            foreach (var parameter in parameters)
            {
                if (parameter.Instruction.ToString() != "RITEM1")
                    continue;
                if (parameter.HasItem)
                    itemID = parameter.ItemID;
                break;
            }
            if (itemID == 0)
                return;
            AddSource(itemRows, itemID, new(
                CollectionSourceCategory.Quest,
                questID,
                questName.Text,
                OmniLoc.Get("Collection.Source.Equipment.UnfinishedRelic")));
            if (npcName is null)
                return;
            AddSource(itemRows, itemID, new(
                CollectionSourceCategory.Other,
                npc.RowId,
                npcName.Text,
                string.Format(OmniLoc.Get("Collection.Source.Equipment.UnfinishedRelicReclaim"), questName.Text),
                location)
            {
                MapTerritoryID = location.TerritoryID
            });
        });
    }

    private static (uint ItemID, uint ConditionID)[] ReadHistoricalEquipmentSourceRows
    (
        CancellationToken cancellationToken
    )
    {
        var rows = new List<(uint ItemID, uint ConditionID)>();
        EmbeddedData.ReadRows("EquipmentSourceSupplement.csv", csv =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var itemID = csv.GetField<uint>("ItemId");
            var conditionID = csv.GetField<uint>("ContentFinderConditionId");
            if (itemID != 0 && conditionID != 0)
                rows.Add((itemID, conditionID));
        });
        return rows.ToArray();
    }

    private static void AddHistoricalEquipmentSource
    (
        IDictionary<uint, HashSet<CollectionSource>> itemRows,
        uint                                         itemID,
        uint                                         conditionID,
        PreparedSourceText?                          preparedText = null
    )
    {
        if (!LuminaGetter.TryGetRow<Item>(itemID, out var item) || item.Rarity != 7 ||
            !LuminaGetter.TryGetRow<ContentFinderCondition>(conditionID, out var condition) ||
            condition.ContentType.RowId != 2 || condition.Content.RowId == 0)
            return;

        if (preparedText is null || !preparedText.DutyText.TryGetValue(conditionID, out var name))
        {
            name = new(condition.Name);
            preparedText?.DutyText.Add(conditionID, name);
        }
        BuildSource(preparedText, () => AddSource(itemRows, itemID, new(
            CollectionSourceCategory.Duty,
            condition.RowId,
            name.Text,
            OmniLoc.Get("Collection.Source.Equipment.HistoricalDuty"))));
    }
}
