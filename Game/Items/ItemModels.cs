namespace OmniToolbox.Items;

public enum ItemCategory
{
    All,
    Job,
    Weapon,
    Tool,
    Armor,
    Accessory,
    Consumable,
    Material,
    Other,
    Set
}

public readonly record struct ItemMainModelKey(ulong ModelMain, uint EquipSlotCategoryID)
{
    public bool IsValid => ModelMain != 0 && EquipSlotCategoryID != 0;
}

public readonly record struct ItemSearchResult(
    uint ID,
    string Name,
    uint IconID,
    uint Level,
    string Category,
    uint ItemUICategoryID,
    bool HasCategory,
    string ItemSearchCategory,
    uint ItemSearchCategoryID,
    uint ItemSearchCategoryIconID,
    ItemCategory MainCategory,
    bool IsTradable,
    bool IsCurrency,
    bool CanBeHQ,
    byte DyeCount,
    ItemMainModelKey MainModel,
    ulong JobMask,
    decimal Patch);

public readonly record struct ItemParameterBonus(
    uint BaseParamID,
    string Name,
    int Value,
    int Max,
    bool IsRelative);

public readonly record struct ItemRecoveryEffect(int Percent, int Max);

public readonly record struct FadedOrchestrionRoll(string Name, uint OrchestrionID);

public readonly record struct ItemSetInfo(
    ItemSearchResult SetItem,
    IReadOnlyList<ItemSearchResult> Items);

public readonly record struct ItemConsumableEffects(
    IReadOnlyList<ItemParameterBonus> NQBonuses,
    IReadOnlyList<ItemParameterBonus> HQBonuses,
    ItemRecoveryEffect? NQRecovery,
    ItemRecoveryEffect? HQRecovery)
{
    public static ItemConsumableEffects Empty { get; } = new(
        Array.Empty<ItemParameterBonus>(),
        Array.Empty<ItemParameterBonus>(),
        null,
        null);
}

public readonly record struct ItemDetailData(
    ItemSearchResult Item,
    bool IsFoodOrMedicine,
    ItemConsumableEffects Effects,
    IReadOnlyList<uint> SetItemIds,
    IReadOnlyList<ItemSetInfo> ContainingSets,
    IReadOnlyList<uint> BlindBoxDropPool,
    IReadOnlyList<FadedOrchestrionRoll> CraftableOrchestrionRolls);
