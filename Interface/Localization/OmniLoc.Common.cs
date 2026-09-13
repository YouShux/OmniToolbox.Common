namespace OmniToolbox.UI;

public static partial class OmniLoc
{
    private static Dictionary<string, string> CreateCommonTexts() => new(StringComparer.Ordinal)
    {
        ["Common.Status.AutoPathEnabled"] = "寻路生效",
        ["Common.Status.AutoPathDisabled"] = "寻路未开启",
        ["Common.ListSeparator"] = "、",
        ["Common.None"] = "无",
        ["Common.Required"] = "需要",
        ["Common.NotRequired"] = "不需要",
        ["Common.UiColorId"] = "颜色 ID: {0}",
        ["Common.UiColorCustom"] = "自定义颜色",
        ["Common.Item"] = "物品",
        ["Common.Action"] = "操作",
        ["Common.Delete"] = "删除",
        ["Common.ItemId"] = "物品 ID：{0}",
        ["Common.Cancel"] = "取消",
        ["Common.Close"] = "关闭",
        ["Common.TerritorySelector.Title"] = "地图选择 ({0})",
        ["Common.TerritorySelector.AddCurrent"] = "添加当前地图",
        ["Common.TerritorySelector.Current"] = "当前地图：{0} ({1})",
        ["Common.TerritorySelector.LoggedOut"] = "未登录",
        ["Common.TerritorySelector.SearchHint"] = "搜索地图名 / ID / 拼音首字母...",
        ["Common.TerritorySelector.NoMatches"] = "没有匹配的地图。",
        ["Common.TerritorySelector.Fallback"] = "地图 {0}",
        ["Common.TerritorySelector.Column.Map"] = "地图",
        ["Common.TerritorySelector.Column.Region"] = "地区",
        ["Common.TerritorySelector.Column.Id"] = "ID",
        ["Status.Obtainable"] = "可获得",
        ["Status.Purchasable"] = "可购买",
        ["Status.Unobtainable"] = "不可获取",
        ["Status.Tradable"] = "可交易",
        ["Status.Untradable"] = "不可交易",
    };
}
