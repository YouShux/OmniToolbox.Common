using FFXIVClientStructs.FFXIV.Client.Game;

namespace OmniToolbox.Items;

public readonly record struct HousingInventoryLocation
(
    ushort          TerritoryTypeID,
    byte            WardIndex,
    byte            PlotIndex,
    HousingPlotSize PlotSize
)
{
    public bool IsValid => TerritoryTypeID != 0 && PlotSize != HousingPlotSize.Unknown;
}

public enum HousingPlotSize
{
    Unknown = 0,
    Small = 1,
    Medium = 2,
    Large = 3
}

public interface IInventorySnapshotReader
{
    IReadOnlyDictionary<uint, uint> ArmoireRows { get; }

    long GetSnapshotRevision();

    IReadOnlyList<InventorySnapshotItem> GetItemsSnapshot();
}

public readonly record struct InventorySnapshotItem
(
    uint                     ItemID,
    int                      Quantity,
    bool                     IsHQ,
    byte                     Stain0,
    byte                     Stain1,
    InventoryType            ContainerType,
    ushort                   SlotIndex,
    ItemInventoryLocation    Location,
    ulong                    HolderID = 0,
    string                   HolderName = "",
    string                   OwnerName = "",
    HousingInventoryLocation HousingLocation = default,
    uint                     OwnerWorldID = 0
);

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
