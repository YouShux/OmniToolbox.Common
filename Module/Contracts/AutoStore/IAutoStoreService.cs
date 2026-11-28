using OmniToolbox.Items;
using OmniToolbox.Operations;

namespace OmniToolbox.TreeHouse.InterfaceOperation;

public enum AutoStoreCacheCollection
{
    Retainer,
    GlamourDresserAndArmoire,
    FreeCompanyChest
}

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

public enum AutoStoreListTarget
{
    Saddlebag = 0,
    GlamourDresser = 1,
    Armoire = 2,
    FreeCompanyChest = 3,
    Armory = 4,
    Retainer1 = 101,
    Retainer2 = 102,
    Retainer3 = 103,
    Retainer4 = 104,
    Retainer5 = 105,
    Retainer6 = 106,
    Retainer7 = 107,
    Retainer8 = 108,
    Retainer9 = 109,
    Retainer10 = 110
}

public interface IAutoStoreService : IOperationService
{
    bool IsAvailable { get; }

    bool IsCacheCollectionAvailable { get; }

    bool IsBusy { get; }

    bool RequestRetrieve(uint itemID);

    bool RequestRetrieve(IReadOnlyList<uint> itemIds);

    bool RequestRetrieve(IReadOnlyList<AutoStoreItemRequest> items);

    bool RequestStore
    (
        uint   itemID,
        string destination
    );

    bool RequestStore
    (
        IReadOnlyList<AutoStoreItemRequest> items,
        AutoStoreListTarget                 destination
    );

    bool RequestCacheCollection(AutoStoreCacheCollection target);

    bool OpenList();

    bool ToggleList();

    void Cancel();
}
