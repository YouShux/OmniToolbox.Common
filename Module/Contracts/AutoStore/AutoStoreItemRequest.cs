using OmniToolbox.Items;
using OmniToolbox.Operations;

namespace OmniToolbox.TreeHouse.InterfaceOperation;

public readonly record struct AutoStoreItemRequest
(
    uint ItemID,
    int  Quantity = int.MaxValue
)
{
    public bool? IsHQ { get; init; }

    public ItemInventoryLocation Source { get; init; }

    public ulong HolderID { get; init; }
}
