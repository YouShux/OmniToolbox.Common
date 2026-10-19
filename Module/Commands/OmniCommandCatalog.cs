using System.Collections.Frozen;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using OmniToolbox.Config;
using OmniToolbox.UI;

namespace OmniToolbox.Commands;

public static class OmniCommandCatalog
{
    private static readonly Lazy<FrozenDictionary<string, OmniCommandDefinition>> Aliases = new(CreateAliases);
    private static readonly Regex CommandTextPattern = new(@"(?<prefix>/omni[ \t]+)(?<name>\S+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static CommandDisplayMode DisplayMode;

    public static void SetDisplayMode(CommandDisplayMode value) => DisplayMode = value;

    private static FrozenDictionary<string, OmniCommandDefinition> CreateAliases()
    {
        return Definitions.SelectMany(definition => new[]
        {
            KeyValuePair.Create(definition.Name, definition),
            KeyValuePair.Create(definition.ChineseName, definition),
            KeyValuePair.Create(OmniLoc.ConvertToTraditional(definition.ChineseName), definition)
        }).DistinctBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
    }

    public static OmniCommandDefinition? Find(string token) => Aliases.Value.GetValueOrDefault(token);

    public static (string Name, string Arguments) Split(string arguments)
    {
        var text = arguments.TrimStart();
        var end = 0;
        while (end < text.Length && !char.IsWhiteSpace(text[end])) end++;
        return (text[..end], text[end..].TrimStart());
    }

    public static bool TryParseScenarioDestination(string arguments, out uint territoryID)
    {
        var text = arguments.Trim();
        territoryID = text.ToLowerInvariant() switch
        {
            "bozja" or "南方博兹雅战线" or "南方博茲雅戰線" => 920,
            "zadnor" or "扎杜诺尔高原" or "扎杜諾爾高原" => 975,
            _ => 0
        };
        return territoryID != 0 || uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out territoryID)
            && territoryID != 0;
    }

    public static string Format(string canonicalName, string arguments = "")
    {
        var definition = Find(canonicalName) ?? throw new ArgumentException("Unknown Omni command.", nameof(canonicalName));
        return arguments.Length == 0 ? $"/omni {definition.Name}" : $"/omni {definition.Name} {arguments}";
    }

    public static string FormatDisplayText(string text, UILanguage? language = null)
    {
        if (!text.Contains("/omni", StringComparison.OrdinalIgnoreCase))
            return text;

        return CommandTextPattern.Replace(text, match =>
        {
            var definition = Find(match.Groups["name"].Value);
            if (definition is null)
                return match.Value;

            var name = definition.Name;
            if (DisplayMode == CommandDisplayMode.Chinese)
                name = language is { } value ? OmniLoc.Get(definition.NameKey, value) : OmniLoc.Get(definition.NameKey);
            return $"{match.Groups["prefix"].Value}{name}";
        });
    }

    public static IReadOnlyDictionary<string, string> CreateTexts()
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var definition in Definitions)
        {
            texts.Add(definition.NameKey, definition.ChineseName);
            texts.Add(definition.DescriptionKey, definition.Description);
            texts.Add(definition.UsageKey, $"用法：{Format(definition.Name, definition.Parameters)}");
        }
        return texts;
    }

    public static string GetHelp() => GetHelp(false);

    public static string GetHelp(bool forCommandInfo)
    {
        var description = OmniLoc.Get("Command.Main.Description");
        var lines = new List<string> { forCommandInfo ? description : $"/omni → {description}" };
        foreach (var definition in Definitions)
        {
            if (forCommandInfo)
            {
                if (definition.Name is "off" or "toggle" or "chatlogreplay" or "foodcheck" or "armoirecheck" or
                    "relicreplicamap" or "battlehornpreset" or "bluemageset" or "itemmanagement")
                    continue;

                if (definition.Name == "on")
                {
                    var actions = DisplayMode == CommandDisplayMode.English
                        ? "on/off/toggle"
                        : $"{OmniLoc.Get("Command.on.Name")}/{OmniLoc.Get("Command.off.Name")}/{OmniLoc.Get("Command.toggle.Name")}";
                    var exampleModule = OmniLoc.Get("ChatFrameOptimizationTitle");
                    lines.Add($"/omni {actions} {definition.Parameters} → {string.Format(OmniLoc.Get("Command.Module.StateDescription"), Format("on", exampleModule))}");
                    continue;
                }
            }

            lines.Add($"{Format(definition.Name, definition.Parameters)} → {OmniLoc.Get(definition.DescriptionKey)}");
        }

        return FormatDisplayText(string.Join('\n', lines));
    }

    public static IReadOnlyList<OmniCommandDefinition> Definitions { get; } = Array.AsReadOnly<OmniCommandDefinition>(
    [
        new("help", "帮助", "", "显示命令用法", null, OmniCommandArguments.None),
        new("tree", "妙妙屋", "", "打开树树妙妙屋", null, OmniCommandArguments.None),
        new("search", "搜索", "<模块名称>", "搜索指定功能", null, OmniCommandArguments.Required),
        new("on", "开启", "<模块名称>", "开启指定模块并保存配置", null, OmniCommandArguments.Required),
        new("off", "关闭", "<模块名称>", "关闭指定模块并保存配置", null, OmniCommandArguments.Required),
        new("toggle", "切换", "<模块名称>", "切换指定模块的启用状态", null, OmniCommandArguments.Required),
        new("iconbrowser", "图标浏览器", "", "打开图标浏览器", null, OmniCommandArguments.None),
        new("marketboard", "虚空市场板", "", "打开市场板购买界面", "VoidPurchase", OmniCommandArguments.None),
        new("npcshop", "虚空商店", "", "打开 NPC 商店浏览器", "VoidNPCShop", OmniCommandArguments.None),
        new("marketbuy", "购买", "<物品ID> <hq|nq> × <数量> × <价格限制>", "按数量和最高单价从市场板购买", "VoidPurchase", OmniCommandArguments.Required),
        new("npcbuy", "商店购买", "<物品ID> <hq|nq> × <数量>", "在 NPC 商店批量购买", "VoidNPCShop", OmniCommandArguments.Required),
        new("retrieve", "取货", "<物品ID>", "取出指定物品", "AutoStore", OmniCommandArguments.Required),
        new("store", "存货", "<物品ID> <存货地>", "将指定物品存入指定位置", "AutoStore", OmniCommandArguments.Required),
        new("itemmanagement", "物品管理", "", "执行一次物品丢弃和售卖", "ItemManagement", OmniCommandArguments.None),
        new("itemlist", "物品列表", "", "打开自动存取的物品列表", "AutoStore", OmniCommandArguments.None),
        new("fcchest", "虚空部队箱", "", "打开部队储物柜", "VoidFreeCompanyChest", OmniCommandArguments.None),
        new("fcaction", "部队特效", "<编号>", "购买指定编号的部队特效", "FreeCompanyAction", OmniCommandArguments.Required),
        new("tp", "传送", "<地点名称>", "传送至指定地点", null, OmniCommandArguments.Required),
        new("bts", "区域传送", "<地图/区域ID/以太水晶>", "按区域或名称选择水晶并传送", null, OmniCommandArguments.Required),
        new("teleportpanel", "传送面板", "", "打开传送面板", "MultiToolbar", OmniCommandArguments.None),
        new("aetheryte", "更好的传送管理", "", "交互以太水晶传送管理", "BetterTeleportManagement", OmniCommandArguments.None),
        new("worldtravel", "快捷跨界传送", "[世界名]", "打开跨界传送窗口或前往指定世界", "FastWorldTravel", OmniCommandArguments.Optional),
        new("inn", "旅馆", "", "进入附近旅馆", "InnEntry", OmniCommandArguments.None),
        new("bis", "BIS配装", "", "打开全职业 BIS 配装", "BestInSlotGear", OmniCommandArguments.None),
        new("chatlogreplay", "日志回放", "", "打开聊天日志回放", "ChatFrameOptimization", OmniCommandArguments.None),
        new("glamourmanager", "幻化管理", "", "打开幻化管理", "BetterGlamourManagement", OmniCommandArguments.None),
        new("armoirecheck", "收藏柜记录", "", "检查收藏柜收录记录", "ArmoireRecord", OmniCommandArguments.None),
        new("glamourcabinet", "虚空收藏柜", "", "打开收藏柜", "VoidGlamourStation", OmniCommandArguments.None),
        new("macroicon", "宏图标", "<图标ID>", "设置当前选中宏的图标", "BetterUserMacro", OmniCommandArguments.Required),
        new("foodcheck", "食物检查", "", "立即检查食物状态", "FoodReminder", OmniCommandArguments.None),
        new("gcseal", "自动交军票", "", "打开军票上交窗口", "AutoGCSeal", OmniCommandArguments.None),
        new("gcsupply", "虚空筹备界面", "", "打开军队筹备界面", "AutoGCSeal", OmniCommandArguments.None),
        new("gcexchange", "虚空军票兑换", "", "打开军票兑换界面", "AutoGCSeal", OmniCommandArguments.None),
        new("sortitems", "自动整理", "", "整理物品", "AutoSortItems", OmniCommandArguments.None),
        new("specialscenario", "自动特殊场景探索", "[区域ID/名称]", "打开场景探索窗口或开始指定区域", "SpecialScenarioExplorer", OmniCommandArguments.Optional),
        new("leaveduty", "即刻退本", "", "离开当前副本", "InstantLeaveDuty", OmniCommandArguments.None),
        new("relicreplicamap", "肝武", "", "导出肝武复制品对应表", null, OmniCommandArguments.None),
        new("chatcn", "国服频道", "[消息]", "切换到国服频道或发送消息", "ChatFrameOptimization", OmniCommandArguments.Optional),
        new("mooglemail", "虚空莫古邮件", "", "打开莫古邮件", "VoidMoogleMail", OmniCommandArguments.None),
        new("housingsearch", "售房查询", "", "打开售房查询", "HousingSales", OmniCommandArguments.None),
        new("island", "自动无人岛", "[enter]", "打开无人岛管理；enter 自动前往无人岛", "AutoIslandSanctuary", OmniCommandArguments.Optional),
        new("ishgardrestoration", "自动重建伊修加德", "[open]", "打开伊修加德重建设置", "AutoIshgardRestoration", OmniCommandArguments.Optional),
        new("relogin", "重新登陆", "<角色名@世界名>", "重新登录指定角色", "AutoLogin", OmniCommandArguments.Required),
        new("battlehornpreset", "兽笛设置", "<方案名>", "应用指定兽笛方案", "BetterNotebooks", OmniCommandArguments.Required),
        new("bluemageset", "青魔技能预设", "<方案名>", "应用指定青魔技能预设", "BetterNotebooks", OmniCommandArguments.Required),
        new("retainerbell", "一键交互雇员铃", "", "交互雇员铃", "OneClickRetainerBell", OmniCommandArguments.None),
        new("treasuremap", "挖宝", "<宝图ID> <价格限制>", "开始指定宝图流程", "TreasureMap", OmniCommandArguments.Required),
        new("sendagentevent", "代理事件", "<AgentID> <事件类型> [参数...]", "发送指定 Agent 事件", null, OmniCommandArguments.Required)
    ]);
}
