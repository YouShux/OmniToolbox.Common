using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using OmenTools;
using OmenTools.Extensions;
using OmenTools.Interop.Game.AddonEvent;
using OmenTools.Interop.Game.Helpers;
using OmenTools.Interop.Game.Lumina;

namespace OmniToolbox.TreeHouse;

public static unsafe class TreasureMapNativeUI
{
    public static bool TryResolveMap
    (
        uint                      itemID,
        out TreasureMapDefinition map
    )
    {
        foreach (var row in LuminaGetter.Get<TreasureHuntRank>())
        {
            if (row.RowId == 0 || row.ItemName.RowId != itemID || row.KeyItemName.RowId == 0)
            {
                continue;
            }

            map = new(itemID, row.RowId, LuminaWrapper.GetItemName(itemID));
            return map.Name.Length != 0;
        }

        map = default;
        return false;
    }

    public static bool TryGetCurrentMap
    (
        out uint rankID,
        out uint subRowID
    )
    {
        var manager = EventItemManager.Instance();
        if (manager == null)
        {
            rankID   = 0;
            subRowID = 0;
            return false;
        }

        rankID   = manager->GetTreasureHuntRank();
        subRowID = manager->GetTreasureSpotSubKey();
        return rankID != 0;
    }

    public static bool TryResolveLocation
    (
        uint                    rankID,
        uint                    subRowID,
        out TreasureMapLocation location
    )
    {
        if (!LuminaGetter.TryGetSubRow<TreasureSpot>(rankID, out var spot, (int)subRowID) ||
            spot.Location.RowId == 0)
        {
            location = default;
            return false;
        }

        var map = spot.Location.Value.Map.Value;
        if (map.RowId == 0 || map.TerritoryType.RowId == 0)
        {
            location = default;
            return false;
        }

        location = new(
            map.TerritoryType.RowId,
            map.RowId,
            new Vector3(spot.Location.Value.X, spot.Location.Value.Y, spot.Location.Value.Z));
        return true;
    }

    public static bool TryCreateMapLink
    (
        TreasureMapLocation location,
        out MapLinkPayload  mapLink
    )
    {
        if (!LuminaGetter.TryGetRow<Map>(location.MapID, out var map))
        {
            mapLink = null!;
            return false;
        }

        var position = PositionHelper.WorldToMap(
            new Vector2(location.Position.X, location.Position.Z),
            map);
        mapLink = new(location.TerritoryID, location.MapID, position.X, position.Y);
        return true;
    }

    public static bool TryUseDecipher()
    {
        var manager = ActionManager.Instance();
        return manager != null &&
            manager->GetActionStatus(ActionType.GeneralAction, DECIPHER_GENERAL_ACTION_ID) == 0 &&
            manager->UseAction(ActionType.GeneralAction, DECIPHER_GENERAL_ACTION_ID);
    }

    public static bool TrySelectMap(string name) =>
        AddonSelectIconStringEvent.Select(name) || AddonSelectStringEvent.Select(name);

    public static bool TryConfirmDecipher(string name) => AddonSelectYesnoEvent.ClickYes(name);

    public static bool IsDecipherConfirmationVisible() =>
        AddonHelper.TryGetByName("SelectYesno", out var addon) &&
        addon->IsAddonAndNodesReady();

    public static void OpenMap(TreasureMapLocation location) =>
        DalamudServices.GameGUI.OpenMapWithMapLink(
            location.TerritoryID,
            location.MapID,
            location.Position);

    public readonly record struct TreasureMapDefinition
    (
        uint   ItemID,
        uint   RankID,
        string Name
    );

    public readonly record struct TreasureMapLocation
    (
        uint    TerritoryID,
        uint    MapID,
        Vector3 Position
    );

    #region 常量

    internal const uint DECIPHER_GENERAL_ACTION_ID = 19;

    #endregion
}
