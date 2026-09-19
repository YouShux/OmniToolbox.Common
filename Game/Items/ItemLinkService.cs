using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.Host;

namespace OmniToolbox.Items;

public static unsafe class ItemLinkService
{
    private static uint? ItemTextCommandParamID;

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
            var textCommandParamID = GetItemTextCommandParamID();
            if (textCommandParamID == 0)
            {
                return false;
            }

            agent->LinkedItem.Clear();
            agent->LinkedItem.ItemId = itemID;
            agent->LinkedItem.Quantity = 1;
            agent->LinkedItem.Container = InventoryType.Invalid;
            agent->LinkedItem.Slot = -1;
            agent->LinkedItem.LinkedItemQuality = item.Rarity;
            agent->LinkedItemName.SetString(item.Name.ExtractText());
            agent->ContextItemId = itemID;
            return agent->InsertTextCommandParam(textCommandParamID, false);
        }
        catch (Exception ex)
        {
            DalamudServices.PluginLog.Warning(ex, $"Item link insertion failed for {itemID}.");
            return false;
        }
    }

    private static uint GetItemTextCommandParamID()
    {
        if (ItemTextCommandParamID is { } cachedId)
        {
            return cachedId;
        }

        foreach (var row in LuminaGetter.Get<TextCommandParam>())
        {
            if (row.Param.ExtractText() != "<item>")
            {
                continue;
            }

            ItemTextCommandParamID = row.RowId;
            return row.RowId;
        }

        ItemTextCommandParamID = 0;
        return 0;
    }
}
