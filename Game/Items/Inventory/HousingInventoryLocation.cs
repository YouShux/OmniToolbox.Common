using FFXIVClientStructs.FFXIV.Client.Game;

namespace OmniToolbox.Items;

public readonly record struct HousingInventoryLocation
(
    ushort          TerritoryTypeID,
    byte            WardIndex,
    byte            PlotIndex,
    HousingPlotSize PlotSize
)
{
    public bool IsValid => TerritoryTypeID != 0 && PlotSize != HousingPlotSize.Unknown;
}
