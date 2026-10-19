namespace OmniToolbox.Collections;

internal readonly record struct RelicMaterialRequirement(
    int StageIndex,
    uint ItemID,
    int Quantity,
    string QuantityText,
    string MaximumQuantityText,
    uint JobID,
    RelicMaterialScope Scope,
    string LabelKey,
    int FreeWeapons)
{
    public uint CostTargetItemID { get; init; }

    public int CostPerTargetItem { get; init; }
}
