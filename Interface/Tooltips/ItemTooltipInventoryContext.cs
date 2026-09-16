using Dalamud.Hooking;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using OmenTools.Interop.Game.Models;
using OmniToolbox.Lifecycle;

namespace OmniToolbox.Tooltips;

public sealed unsafe class ItemTooltipInventoryContext(HookRegistry hookRegistry) : IDisposable
{
    private static readonly CompSig HoveredItemSignature = new(
        "E8 ?? ?? ?? ?? 84 C0 0F 84 ?? ?? ?? ?? 48 89 9C 24 ?? ?? ?? ?? 4C 89 A4 24");

    private Hook<AgentItemDetailOnItemHovered>? hoveredItemHook;
    private InventoryItem hoveredItem;
    private int leaseCount;
    private bool disposed;

    private delegate byte AgentItemDetailOnItemHovered(
        void* agent,
        void* source,
        void* context,
        void* owner,
        uint type,
        uint index,
        int* itemData);

    public IDisposable Acquire()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (leaseCount == 0)
        {
            hoveredItemHook = hookRegistry.Register(
                HoveredItemSignature,
                (AgentItemDetailOnItemHovered)OnItemHovered);
        }

        leaseCount++;
        return new Lease(this);
    }

    public bool TryGet(uint normalizedItemID, out InventoryItem item)
    {
        item = hoveredItem;
        return normalizedItemID != 0 && ItemUtil.GetBaseId(item.ItemId).ItemId == normalizedItemID;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        leaseCount = 0;
        ReleaseHook();
    }

    private byte OnItemHovered(
        void* agent,
        void* source,
        void* context,
        void* owner,
        uint type,
        uint index,
        int* itemData)
    {
        var result = hoveredItemHook!.Original(agent, source, context, owner, type, index, itemData);
        hoveredItem = itemData == null ? default : *(InventoryItem*)itemData;
        return result;
    }

    private void Release()
    {
        if (leaseCount == 0 || --leaseCount != 0)
        {
            return;
        }

        ReleaseHook();
    }

    private void ReleaseHook()
    {
        hookRegistry.Release(hoveredItemHook);
        hoveredItemHook = null;
        hoveredItem = default;
    }

    private sealed class Lease(ItemTooltipInventoryContext owner) : IDisposable
    {
        private ItemTooltipInventoryContext? owner = owner;

        public void Dispose()
        {
            owner?.Release();
            owner = null;
        }
    }
}
