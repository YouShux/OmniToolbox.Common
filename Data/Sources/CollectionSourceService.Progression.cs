using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.UI;
using Action = System.Action;

namespace OmniToolbox.Collections;

public sealed partial class CollectionSourceService
{
    private static void AddCosmicToolReplicaSources(IDictionary<uint, HashSet<CollectionSource>> itemRows)
    {
        if (!LuminaGetter.TryGetRow<SpecialShop>(1770975, out var shop) ||
            !LuminaGetter.TryGetRow<ENpcResident>(1052583, out var npc))
            return;

        var npcName = new CapturedSourceText(npc.Singular);
        var location = new CollectionSourceLocation(1237, 21.1f, 21.9f);
        for (var index = 0; index < shop.Item.Count; index++)
            AddCosmicToolReplicaSource(itemRows, shop, index, npc.RowId, npcName, location);
    }

    private static void AddCosmicToolReplicaCaptures
    (
        List<Action> captures,
        PreparedSourceText preparedText
    )
    {
        if (!LuminaGetter.TryGetRow<SpecialShop>(1770975, out var shop) ||
            !LuminaGetter.TryGetRow<ENpcResident>(1052583, out var npc))
            return;
        var location = new CollectionSourceLocation(1237, 21.1f, 21.9f);
        for (var index = 0; index < shop.Item.Count; index++)
        {
            var itemIndex = index;
            captures.Add(() => AddCosmicToolReplicaSource(preparedText.SupplementalSources, shop, itemIndex,
                npc.RowId, CaptureNPCName(npc, preparedText), location, preparedText));
        }
    }

    private static void AddCosmicToolReplicaSource
    (
        IDictionary<uint, HashSet<CollectionSource>> itemRows,
        SpecialShop shop,
        int index,
        uint npcID,
        CapturedSourceText npcName,
        CollectionSourceLocation location,
        PreparedSourceText? preparedText = null
    )
    {
        var entry = shop.Item[index];
        var reward = entry.ReceiveItems[0];
        if (reward.Item.RowId == 0 || !reward.Item.IsValid || reward.ReceiveCount == 0)
            return;

        var hasCost = false;
        foreach (var cost in entry.ItemCosts)
            hasCost |= cost.CurrencyCost != 0;
        if (hasCost)
            return;

        var description = entry.AchievementUnlock.ValueNullable is { RowId: > 0 } achievement
            ? new CapturedSourceText(achievement.Description)
            : null;
        var itemID = reward.Item.RowId;
        BuildSource(preparedText, () =>
        {
            var detail = OmniLoc.Get("Collection.Source.CosmicTool.Replica");
            if (description is not null)
                detail += "\n" + string.Format(
                    OmniLoc.Get("Collection.Source.CosmicTool.Unlock"),
                    description.Text);
            AddSource(itemRows, itemID, new(
                CollectionSourceCategory.Shop,
                npcID,
                npcName.Text,
                detail,
                location)
            {
                MapTerritoryID = location.TerritoryID,
                IsGilShop = true
            });
        });
    }

    private static void AddCosmicToolSources
    (
        IDictionary<uint, HashSet<CollectionSource>> itemRows,
        WKSCosmoToolClass                            toolClass,
        PreparedSourceText?                          preparedText = null
    )
    {
        if (toolClass.RowId == 0 || toolClass.DataAmount.ValueNullable is not { } dataAmounts)
            return;

        var dataNames = new CapturedSourceText[toolClass.Types.Count];
        for (var index = 0; index < dataNames.Length; index++)
            dataNames[index] = new(toolClass.Types[index].CosmicName.ValueNullable?.Name ?? default);

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

            var stageName = new CapturedSourceText(stage.Name.ValueNullable?.Name ?? default);
            var npcName = CaptureNPCName(npc, preparedText);
            var itemName = CaptureItemName(stage.Item.Value, preparedText);
            var itemID = stage.Item.RowId;
            var requirements = dataAmounts.Stages[stageIndex].RequiredAmount.ToArray();
            BuildSource(preparedText, () =>
            {
                var detail = stageName.Text.Length == 0
                    ? itemName.Text
                    : string.Format(OmniLoc.Get("Collection.Source.CosmicTool.Research"), stageName.Text);
                var dataRequirements = new List<string>();
                for (var index = 0; index < Math.Min(requirements.Length, dataNames.Length); index++)
                {
                    if (requirements[index] != 0 && dataNames[index].Text.Length != 0)
                        dataRequirements.Add(FormattableString.Invariant($"{dataNames[index].Text} × {requirements[index]}"));
                }
                if (dataRequirements.Count != 0)
                    detail += "\n" + string.Format(
                        OmniLoc.Get("Collection.Source.CosmicTool.Data"),
                        string.Join(" / ", dataRequirements));
                AddSource(itemRows, itemID, new(
                    CollectionSourceCategory.Other,
                    npcID,
                    npcName.Text,
                    detail,
                    location)
                {
                    MapTerritoryID = location.TerritoryID
                });
            });
        }
    }
}
