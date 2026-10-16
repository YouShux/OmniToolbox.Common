using Dalamud.Game.Text;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Text.ReadOnly;
using OmenTools.OmenService;

namespace OmniToolbox.UI;

internal static class OmniContextMenu
{
    public static readonly ReadOnlySeString Prefix = CreatePrefix();

    public static unsafe uint GetTargetItemID(ContextMenuOpenedArgs args)
    {
        if (args.TargetItemID != 0 || args.AddonName != "DailyQuestSupply")
            return args.TargetItemID;

        var agent = AgentDailyQuestSupply.Instance();
        return agent == null ? 0 : ItemUtil.GetBaseId(agent->ContextMenuItemId).ItemId;
    }

    private static ReadOnlySeString CreatePrefix()
    {
        using var prefix = new RentedSeStringBuilder();
        return prefix.Builder.PushColorType(10)
            .Append($"{(char)SeIconChar.BoxedLetterO}{(char)SeIconChar.BoxedLetterT}")
            .PopColorType().ToReadOnlySeString();
    }
}
