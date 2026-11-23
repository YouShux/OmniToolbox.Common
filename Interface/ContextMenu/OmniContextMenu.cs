using Dalamud.Game.Text;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Text.ReadOnly;
using OmenTools.OmenService;
using OmniToolbox.Config;

namespace OmniToolbox.UI;

internal static class OmniContextMenu
{
    private static readonly ReadOnlySeString DefaultPrefix = CreatePrefix();
    private static UIConfig? Config;

    public static ReadOnlySeString? Prefix => Config?.ShowOTPrefix == false ? (ReadOnlySeString?)null : DefaultPrefix;

    internal static void Configure(UIConfig? config) => Config = config;

    public static unsafe uint GetTargetItemID(ContextMenuOpenedArgs args)
    {
        if (args.TargetItemID != 0 || args.Addon is null || args.Addon->NameString != "DailyQuestSupply")
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
