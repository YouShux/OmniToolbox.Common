namespace OmniToolbox.Items;

[Flags]
public enum ItemInventoryLocation : ushort
{
    None = 0,
    Inventory = 1 << 0,
    Armory = 1 << 1,
    Saddlebag = 1 << 2,
    Retainer = 1 << 3,
    GlamourDresser = 1 << 4,
    Armoire = 1 << 5,
    Equipped = 1 << 6,
    FreeCompanyChest = 1 << 7,
    RetainerMarket = 1 << 8,
    HousingStoreroom = 1 << 9,
    Gearset = 1 << 10
}
