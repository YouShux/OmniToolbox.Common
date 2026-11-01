namespace OmniToolbox.Items;

public interface IInventorySnapshotReader
{
    IReadOnlyDictionary<uint, uint> ArmoireRows { get; }

    long GetSnapshotRevision();

    IReadOnlyList<InventorySnapshotItem> GetItemsSnapshot();
}
