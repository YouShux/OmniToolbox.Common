namespace OmniToolbox.Teleport;

public interface ITeleportService
{
    bool IsHousingPlotTravelRunning { get; }

    bool TryTeleport(uint aetheryteID);

    bool TryTravelToHousingPlot
    (
        uint worldID,
        uint territoryID,
        int  ward,
        int  plot
    );

    void Cancel();
}
