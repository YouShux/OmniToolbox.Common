using OmniToolbox.Items;
using OmniToolbox.Operations;

namespace OmniToolbox.TreeHouse.InterfaceOperation;

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
