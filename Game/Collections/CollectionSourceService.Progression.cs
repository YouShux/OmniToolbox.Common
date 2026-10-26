using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.UI;

namespace OmniToolbox.Collections;

public sealed partial class CollectionSourceService
{
    private static void AddCosmicToolReplicaSources(IDictionary<uint, HashSet<CollectionSource>> itemRows)
    {
        if (!LuminaGetter.TryGetRow<SpecialShop>(1770975, out var shop) ||
            !LuminaGetter.TryGetRow<ENpcResident>(1052583, out var npc))
            return;

        var npcName = npc.Singular.ExtractText();
        var location = new CollectionSourceLocation(1237, 21.1f, 21.9f);
        foreach (var entry in shop.Item)
        {
            var reward = entry.ReceiveItems[0];
            if (reward.Item.RowId == 0 || !reward.Item.IsValid || reward.ReceiveCount == 0)
                continue;

            var hasCost = false;
            foreach (var cost in entry.ItemCosts)
                hasCost |= cost.CurrencyCost != 0;
            if (hasCost)
                continue;

            var detail = OmniLoc.Get("Collection.Source.CosmicTool.Replica");
            if (entry.AchievementUnlock.ValueNullable is { RowId: > 0 } achievement)
                detail += "\n" + string.Format(
                    OmniLoc.Get("Collection.Source.CosmicTool.Unlock"),
                    achievement.Description.ExtractText());

            AddSourceIfMissing(itemRows, reward.Item.RowId, new(
                CollectionSourceCategory.Shop,
                npc.RowId,
                npcName,
                detail,
                location)
            {
                MapTerritoryID = location.TerritoryID,
                IsGilShop = true
            });
        }
    }

    private static void AddCosmicToolSources(
        IDictionary<uint, HashSet<CollectionSource>> itemRows,
        WKSCosmoToolClass toolClass)
    {
        if (toolClass.RowId == 0 || toolClass.DataAmount.ValueNullable is not { } dataAmounts)
            return;

        var dataNames = new string[toolClass.Types.Count];
        for (var index = 0; index < dataNames.Length; index++)
            dataNames[index] = toolClass.Types[index].CosmicName.ValueNullable?.Name.ExtractText() ?? string.Empty;

        var stageCount = Math.Min(toolClass.Stages.Count, dataAmounts.Stages.Count);
        for (var stageIndex = 0; stageIndex < stageCount; stageIndex++)
        {
            var stage = toolClass.Stages[stageIndex];
            if (stage.Item.RowId == 0 || !stage.Item.IsValid)
                continue;

            var (npcID, location) = stageIndex switch
            {
                <= 8 => (1052583U, new CollectionSourceLocation(1237, 21.1f, 21.9f)),
                <= 13 => (1052629U, new CollectionSourceLocation(1291, 27.9f, 13.4f)),
                <= 16 => (1052647U, new CollectionSourceLocation(1310, 17.4f, 23.9f)),
                <= 19 => (1056821U, new CollectionSourceLocation(1319, 27.2f, 29.4f)),
                _ => (0U, default(CollectionSourceLocation))
            };
            if (npcID == 0 || !LuminaGetter.TryGetRow<ENpcResident>(npcID, out var npc))
                continue;

            var stageName = stage.Name.ValueNullable?.Name.ExtractText() ?? string.Empty;
            var detail = stageName.Length == 0
                ? stage.Item.Value.Name.ExtractText()
                : string.Format(OmniLoc.Get("Collection.Source.CosmicTool.Research"), stageName);
            var requirements = dataAmounts.Stages[stageIndex].RequiredAmount;
            var dataRequirements = new List<string>();
            for (var index = 0; index < Math.Min(requirements.Count, dataNames.Length); index++)
            {
                if (requirements[index] != 0 && dataNames[index].Length != 0)
                    dataRequirements.Add(FormattableString.Invariant($"{dataNames[index]} × {requirements[index]}"));
            }

            if (dataRequirements.Count != 0)
                detail += "\n" + string.Format(
                    OmniLoc.Get("Collection.Source.CosmicTool.Data"),
                    string.Join(" / ", dataRequirements));

            AddSourceIfMissing(itemRows, stage.Item.RowId, new(
                CollectionSourceCategory.Other,
                npcID,
                npc.Singular.ExtractText(),
                detail,
                location)
            {
                MapTerritoryID = location.TerritoryID
            });
        }
    }
}
