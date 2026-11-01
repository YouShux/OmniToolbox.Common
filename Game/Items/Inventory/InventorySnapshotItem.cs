using FFXIVClientStructs.FFXIV.Client.Game;

namespace OmniToolbox.Items;

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
