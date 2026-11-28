using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Game.Text.SeStringHandling;

namespace OmniToolbox.Teleport;

public readonly record struct AetheryteDestination
(
    uint   ID,
    string Name,
    string MapName,
    bool   CanTeleportDirectly
);

public readonly record struct AetheryteRouteSegment
(
    AetheryteRouteSegmentKind Kind,
    uint                      AetheryteID,
    string                    Name,
    bool                      CanTeleportDirectly
)
{
    public uint TerritoryID { get; init; }

    public uint NextTerritoryID { get; init; }

    public Vector2? BoundaryPosition { get; init; }
}

public enum AetheryteRouteSegmentKind
{
    Aetheryte,
    Boundary
}

public enum AutoPathStage
{
    Idle,
    WaitingForLanding,
    WaitingForNavigation,
    Moving,
    Arrived,
    Cancelled,
    Failed,
    Landing
}

public interface IAetheryteRouteResolver
{
    IReadOnlyList<AetheryteDestination> Destinations { get; }

    bool TryResolve
    (
        MapLinkPayload              mapLink,
        IReadOnlySet<uint>          ignoredAetheryteIds,
        out AetheryteRouteSegment[] route
    );

    void AppendRoutePayloads
    (
        List<Payload>                       payloads,
        IReadOnlyList<AetheryteRouteSegment> route,
        DalamudLinkPayload                  aetheryteLinkPayload
    );
}

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
