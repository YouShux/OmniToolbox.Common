namespace OmniToolbox.Items;

public readonly record struct ItemDetailData
(
    ItemSearchResult                    Item,
    bool                                IsFoodOrMedicine,
    ItemConsumableEffects               Effects,
    IReadOnlyList<uint>                 SetItemIds,
    IReadOnlyList<ItemSetInfo>          ContainingSets,
    IReadOnlyList<uint>                 BlindBoxDropPool,
    IReadOnlyList<FadedOrchestrionRoll> CraftableOrchestrionRolls
);
