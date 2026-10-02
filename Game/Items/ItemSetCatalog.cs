using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;

namespace OmniToolbox.Items;

public static class ItemSetCatalog
{
    public static IReadOnlyList<uint> GetItemSlots(uint itemID)
    {
        if (!LuminaGetter.TryGetRow<MirageStoreSetItem>(itemID, out var set))
        {
            return Array.Empty<uint>();
        }

        return
        [
            set.MainHand.RowId,
            set.OffHand.RowId,
            set.Head.RowId,
            set.Body.RowId,
            set.Hands.RowId,
            set.Legs.RowId,
            set.Feet.RowId,
            set.Earrings.RowId,
            set.Necklace.RowId,
            set.Bracelets.RowId,
            set.Ring.RowId
        ];
    }
}
