using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.Host;

namespace OmniToolbox.Items;

public static unsafe class ItemLinkService
{
    public static bool TryInsert(uint itemID)
    {
        var agent = AgentChatLog.Instance();
        if (itemID == 0 ||
            agent == null ||
            !LuminaGetter.TryGetRow<Item>(itemID, out var item))
        {
            return false;
        }

        try
        {
            agent->LinkedItem.Clear();
            agent->LinkedItem.ItemId = itemID;
            agent->LinkedItem.Quantity = 1;
            agent->LinkedItem.Container = InventoryType.Invalid;
            agent->LinkedItem.Slot = -1;
            agent->LinkedItem.LinkedItemQuality = item.Rarity;
            agent->LinkedItemName.SetString(item.Name.ExtractText());
            agent->ContextItemId = itemID;
            return agent->InsertTextCommandParam(ITEM_TEXT_COMMAND_PARAM_ID, false);
        }
        catch (Exception ex)
        {
            DalamudServices.PluginLog.Warning(ex, $"Item link insertion failed for {itemID}.");
            return false;
        }
    }

    #region 常量

    private const uint ITEM_TEXT_COMMAND_PARAM_ID = 1096;

    #endregion
}
