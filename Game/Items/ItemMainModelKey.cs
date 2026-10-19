namespace OmniToolbox.Items;

public readonly record struct ItemMainModelKey(ulong ModelMain, uint EquipSlotCategoryID)
{
    public bool IsValid => ModelMain != 0 && EquipSlotCategoryID != 0;
}
