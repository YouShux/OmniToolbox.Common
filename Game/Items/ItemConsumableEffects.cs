namespace OmniToolbox.Items;

public readonly record struct ItemConsumableEffects
(
    IReadOnlyList<ItemParameterBonus> NQBonuses,
    IReadOnlyList<ItemParameterBonus> HQBonuses,
    ItemRecoveryEffect?               NQRecovery,
    ItemRecoveryEffect?               HQRecovery
)
{
    public static ItemConsumableEffects Empty { get; } = new(
        Array.Empty<ItemParameterBonus>(),
        Array.Empty<ItemParameterBonus>(),
        null,
        null);
}
