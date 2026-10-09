using FFXIVClientStructs.FFXIV.Client.Game;

namespace OmniToolbox.Items;

internal static unsafe class InventorySlotAccess
{
    // 返回的原生槽位仅供当前游戏线程调用使用，物品身份和操作条件由调用方校验。
    public static bool TryGet(InventoryType inventoryType, ushort slot, out InventoryItem* item)
    {
        item = null;
        var manager = InventoryManager.Instance();
        if (manager == null)
            return false;

        var container = manager->GetInventoryContainer(inventoryType);
        if (container == null || !container->IsLoaded || slot >= container->Size)
            return false;

        item = container->GetInventorySlot(slot);
        return item != null;
    }
}
